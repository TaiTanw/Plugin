using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 本文件是 RetinarBatchModelBuilder 的主体（选择模型、生成预制体、构建 AssetBundle、导出交付物）。
//
// 这个类被拆成三个 partial 分文件，各自职责如下：
//   RetinarBatchModelBuilder.cs                  主流程：选模型 -> 规范化 -> 出包 -> 拷交付物
//   RetinarBatchModelBuilder.AssetResolution.cs  源资产发现（贴图/材质在哪）+ 打包前校验与自愈
//   RetinarBatchModelBuilder.AssetInfoWorkbook.cs 交付文档 asset_info.xlsx 的生成（手写 OOXML）
//
// 拆分原因：AssetResolution 那两块正是"移动文件位置导致打包终止"问题的根源所在；
// AssetInfoWorkbook 则完全是另一件事（把统计数据渲染成 Excel），和打包流程零耦合。
// 分开之后，排查问题时能直接定位到文件，不用在 2700 行里翻找。
public static partial class RetinarBatchModelBuilder
{
    private const string ArtRoot = "Assets/Art";
    private const string DeliverableRoot = "Deliverables";
    private const string AssetBundleRoot = "AssetBundles";
    private const string AssetBundleVariant = "assetbundle";
    private const string AssetInfoTemplatePath = "Assets/Retinar/Templates/asset_info_template.xlsx";
    private const string RequiredRuntimeVersion = "RetinarRuntime_v1.0.0";
    private const float SafeZonePadding = 0.8f;
    private const float EmissionIntensity = 0.3f;
    private const float MetallicValue = 0.4f;
    private const float SmoothnessValue = 0.4f;
    private const long MaxTextureSourceBytes = 5L * 1024L * 1024L;

    private static readonly Vector3 SafeZoneCenter = new Vector3(0f, 0.15f, 0f);
    private static readonly Vector3 SafeZoneSize = Vector3.one;
    private static readonly Color EmissionColor = Color.white;

    // ---------------------------------------------------------------------------
    // 延迟弹窗：修复"打包结果正确，但控制台报
    // InvalidOperationException: Failed to restore override lighting settings /
    // Previous PreviewRenderUtility.BeginPreview() was not closed with EndPreview()"
    // 的问题。
    //
    // 根因：批处理开始前用户选中了若干 FBX/Prefab（Selection.objects），Inspector
    // 窗口因此正显示这些资产的 3D 预览（内部用 PreviewRenderUtility.BeginPreview /
    // EndPreview 包一次 GUI 帧渲染）。如果我们在同一次 GUI 事件循环里直接调用
    // EditorUtility.DisplayDialog 弹出模态对话框，会打断 Inspector 正在进行到一半的
    // 预览渲染（BeginPreview 还没来得及配对 EndPreview 就被模态对话框抢占），
    // Unity 收尾时就会抛这个异常。这是 Unity 编辑器本身的时序问题，不是我们逻辑
    // 出错——所以打包产物依然是对的，只是控制台多了一条噪音报错。
    //
    // 修复方式：不在当前这次 GUI 事件里直接弹窗，而是用 EditorApplication.delayCall
    // 把弹窗推迟到下一次编辑器 tick 再显示，这时候当前这次 Inspector 预览渲染已经
    // 完整走完了 BeginPreview/EndPreview 配对，不会再互相打断。
    // 本文件里所有 EditorUtility.DisplayDialog 调用都只是单按钮"OK"提示，不依赖
    // 返回值，所以可以安全地全部换成这个延迟版本。
    // ---------------------------------------------------------------------------
    private static void ShowDialogDeferred(string title, string message, string ok)
    {
        EditorApplication.delayCall += () => EditorUtility.DisplayDialog(title, message, ok);
    }

    [MenuItem("Tools/Retinar/Batch Build Selected Models")]
    public static void BatchBuildSelectedModels()
    {
        if (StopIfEditorIsPlaying())
        {
            return;
        }

        List<string> sourcePaths = GetSelectedModelPaths();
        if (sourcePaths.Count == 0)
        {
            ShowDialogDeferred("Retinar Batch Builder", "Select one or more FBX or Prefab assets.", "OK");
            return;
        }

        EnsureAssetFolder(ArtRoot);
        EnsureDiskDirectory(Path.Combine(Directory.GetCurrentDirectory(), DeliverableRoot));
        EnsureDiskDirectory(Path.Combine(Directory.GetCurrentDirectory(), AssetBundleRoot));

        var generated = new List<GeneratedAsset>();
        try
        {
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                string sourcePath = sourcePaths[i];
                EditorUtility.DisplayProgressBar(
                    "Retinar Batch Builder",
                    "Building prefab: " + sourcePath,
                    (float)i / sourcePaths.Count);

                GeneratedAsset asset = CreateNormalizedPrefab(sourcePath);
                if (asset.IsValid)
                {
                    generated.Add(asset);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (generated.Count == 0)
        {
            ShowDialogDeferred("Retinar Batch Builder", "No valid prefabs were generated. AssetBundles were not built.", "OK");
            return;
        }

        // 逐个资产做校验，只把没通过的那几个排除掉，其余照常出包。
        // 原来的做法是"三道校验对整批一起做，任何一条不通过就 return"，
        // 于是一个模型有问题会把已经生成好的其它模型全部拖下水——这正是
        // "打包出现终止，还要再去生成目录里选中预设体重新打一次"这个手动补救流程的来源。
        var excludedReports = new List<string>();
        List<GeneratedAsset> buildable = PartitionAssetsThatPassValidation(generated, excludedReports);
        string excludedReportPath = excludedReports.Count > 0 ? WriteValidationFailureReport(excludedReports) : null;

        if (buildable.Count == 0)
        {
            ShowDialogDeferred(
                "Retinar Batch Builder",
                "Packaging stopped: 选中的 " + generated.Count + " 个资产全部没通过校验。\n\n" +
                BuildDialogPreview(string.Join("\n", excludedReports.ToArray()), 10) +
                "\n\nFull report:\n" + excludedReportPath,
                "OK");
            return;
        }

        BuildAssetBundles(BuildTarget.Android);
        BuildAssetBundles(BuildTarget.iOS);
        int textureWarningCount = CopySourceFilesToDeliverables(buildable);
        CopyBuiltBundlesToDeliverables(buildable);
        ExportUnityPackages(buildable);
        WriteDocsFiles(buildable);

        string warningText = textureWarningCount > 0
            ? "\n\nTexture check: " + textureWarningCount + " texture issue(s). See 01_source/texture_size_report.txt."
            : "\n\nTexture check: all copied textures are power-of-two and <= 5 MB.";

        string excludedText = excludedReports.Count == 0
            ? string.Empty
            : "\n\n已排除 " + excludedReports.Count + " 个未通过校验的资产（其余照常出包）：\n" +
              BuildDialogPreview(string.Join("\n", excludedReports.ToArray()), 6) +
              "\n完整清单:\n" + excludedReportPath;

        ShowDialogDeferred(
            "Retinar Batch Builder",
            "Done. Processed " + buildable.Count + " / " + generated.Count + " asset(s).\n\nUnity prefabs: " + ArtRoot +
            "\nAssetBundles: " + AssetBundleRoot +
            "\nDeliverables: " + GetDeliverablesAbsolutePath() +
            warningText +
            excludedText,
            "OK");
    }

    /// <summary>
    /// 弹窗里塞不下太长的文本，超过 maxLines 行就截断并说明还有多少条。
    /// 完整内容一律写进报告文件，弹窗只负责让人第一眼知道"大概是什么问题"。
    /// </summary>
    private static string BuildDialogPreview(string text, int maxLines)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string[] lines = text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        string preview = string.Join("\n", lines.Take(maxLines).ToArray());
        if (lines.Length > maxLines)
        {
            preview += "\n... 还有 " + (lines.Length - maxLines) + " 行，见完整报告。";
        }

        return preview;
    }

    [MenuItem("Tools/Retinar/Normalize Selected Models Only")]
    public static void NormalizeSelectedModelsOnly()
    {
        if (StopIfEditorIsPlaying())
        {
            return;
        }

        List<string> sourcePaths = GetSelectedModelPaths();
        if (sourcePaths.Count == 0)
        {
            ShowDialogDeferred("Retinar Batch Builder", "Select one or more FBX or Prefab assets.", "OK");
            return;
        }

        EnsureAssetFolder(ArtRoot);

        int generatedCount = 0;
        try
        {
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                string sourcePath = sourcePaths[i];
                EditorUtility.DisplayProgressBar(
                    "Retinar Batch Builder",
                    "Normalizing prefab: " + sourcePath,
                    (float)i / sourcePaths.Count);

                GeneratedAsset asset = CreateNormalizedPrefab(sourcePath);
                if (asset.IsValid)
                {
                    generatedCount++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ShowDialogDeferred(
            "Retinar Batch Builder",
            "Done. Normalized " + generatedCount + " asset(s).\n\nUnity prefabs: " + ArtRoot,
            "OK");
    }

    [MenuItem("Tools/Retinar/Batch Build Selected Models", true)]
    private static bool ValidateBatchBuildSelectedModels()
    {
        return !EditorApplication.isCompiling;
    }

    [MenuItem("Tools/Retinar/Normalize Selected Models Only", true)]
    private static bool ValidateNormalizeSelectedModelsOnly()
    {
        return !EditorApplication.isCompiling;
    }

    [MenuItem("Tools/Retinar/Open Deliverables Folder")]
    public static void OpenDeliverablesFolder()
    {
        string path = GetDeliverablesAbsolutePath();
        EnsureDiskDirectory(path);
        EditorUtility.RevealInFinder(path);
    }

    private static string GetDeliverablesAbsolutePath()
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), DeliverableRoot));
    }

    private static bool StopIfEditorIsPlaying()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return false;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        ShowDialogDeferred(
            "Retinar Batch Builder",
            "Unity is in Play Mode. The builder stopped Play Mode first. Please run the tool again after Unity returns to Edit Mode.",
            "OK");
        return true;
    }

    private static List<string> GetSelectedModelPaths()
    {
        var paths = new List<string>();
        foreach (Object selected in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".fbx" || extension == ".prefab")
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static void ApplyModelImportSettings(string sourcePath)
    {
        var importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        importer.globalScale = 1f;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        TrySetModelImporterMaterialImportMode(importer, "ImportViaMaterialDescription");

        // InPrefab 是硬性防回归基线，不要改成 External（PACKAGING_RULES.md 规则 20/21，
        // 已于 2026-07-20 由用户完成导入回归验收）。
        // 理由：External 会让目标工程首次导入 UnityPackage 时，Unity 自动在
        // Assets/Art/<名字>/Model/ 下面生成 Materials/ 和 <FBX名>.fbm 两个目录，
        // 而 ValidateModelFoldersAreClean 规定 Model 目录只允许放模型文件、
        // 不允许有子文件夹——于是打包终止。
        //
        // 注意这里曾经和导入插件（TOol/ModelImportSettingsProcessor，把 FBX 设成 External）
        // 互相覆盖：本方法设 InPrefab 后调 SaveAndReimport()，这次 reimport 又会触发
        // 那个插件的 OnPreprocessModel 把它改回 External，两边来回打架，
        // 最终生效的是哪个取决于时序，非常难查。
        // 解决办法不在这里，而是让导入插件跳过 Assets/Art 这个产物目录——
        // 艺术家新导入的 FBX 用 External（编辑器生成外部 .mat，符合上游流程），
        // 打包工具的交付工作副本用 InPrefab（符合交付规范）。两者不再有交集。
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialSearch = ModelImporterMaterialSearch.Everywhere;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.addCollider = false;
        importer.SaveAndReimport();
    }

    private static GeneratedAsset CreateNormalizedPrefab(string sourcePath)
    {
        if (Path.GetExtension(sourcePath).ToLowerInvariant() == ".prefab")
        {
            return CreatePackagedAdjustedPrefab(sourcePath);
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogWarning("Could not load model asset: " + sourcePath);
            return GeneratedAsset.Invalid;
        }

        string assetName = MakeSafeName(Path.GetFileNameWithoutExtension(sourcePath));
        string assetFolder = ArtRoot + "/" + assetName;
        string modelFolder = assetFolder + "/Model";
        string textureFolder = assetFolder + "/Texture";
        string prefabFolder = assetFolder + "/Prefab";
        string materialFolder = assetFolder + "/Material";
        string animationFolder = assetFolder + "/Animation";
        EnsureStandardAssetFolders(assetFolder);

        CopySourceTexturesToUnityArtFolder(sourcePath, textureFolder);
        string unityModelPath = CopyModelToUnityArtFolder(sourcePath, modelFolder, assetName);
        if (unityModelPath != sourcePath)
        {
            ApplyModelImportSettings(unityModelPath);
        }
        FlattenModelCompanionFolders(assetFolder);

        string prefabPath = prefabFolder + "/" + assetName + "_prefab.prefab";

        source = AssetDatabase.LoadAssetAtPath<GameObject>(unityModelPath);
        if (source == null)
        {
            Debug.LogWarning("Could not load copied model asset: " + unityModelPath);
            return GeneratedAsset.Invalid;
        }

        GameObject root = new GameObject(assetName + "_prefab");
        GameObject model = Object.Instantiate(source);
        model.name = assetName + "_Model";
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        if (!TryGetRendererBounds(root, out Bounds bounds))
        {
            Object.DestroyImmediate(root);
            Debug.LogWarning("No Renderer bounds found for: " + sourcePath);
            return GeneratedAsset.Invalid;
        }

        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxSize > 0f)
        {
            float targetMaxSize = Mathf.Min(SafeZoneSize.x, SafeZoneSize.y, SafeZoneSize.z) * SafeZonePadding;
            float scale = targetMaxSize / maxSize;
            model.transform.localScale *= scale;
        }

        if (TryGetRendererBounds(root, out bounds))
        {
            Vector3 offset = SafeZoneCenter - bounds.center;
            model.transform.position += offset;
        }

        AddOrUpdateBoxCollider(root);
        SetupAnimationController(model, unityModelPath, animationFolder, assetName);
        ApplyMaterialCopies(root, materialFolder, textureFolder, assetName);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            Debug.LogWarning("Failed to create prefab: " + prefabPath);
            return GeneratedAsset.Invalid;
        }

        AssetDatabase.ImportAsset(prefabPath);
        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (savedPrefab == null || savedPrefab.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            ClearBundleName(prefabPath);
            Debug.LogError("Generated prefab has no renderers and will not be bundled: " + prefabPath);
            return GeneratedAsset.Invalid;
        }

        string bundleName = assetName.ToLowerInvariant();
        AssetImporter prefabImporter = AssetImporter.GetAtPath(prefabPath);
        prefabImporter.assetBundleName = bundleName;
        prefabImporter.assetBundleVariant = AssetBundleVariant;
        prefabImporter.SaveAndReimport();
        ClearDuplicateBundleNames(prefabFolder, prefabPath, bundleName);

        AssetStats stats = CollectAssetStats(savedPrefab);
        return new GeneratedAsset(assetName, assetFolder, sourcePath, unityModelPath, prefabPath, bundleName + "." + AssetBundleVariant, stats);
    }

    private static GeneratedAsset CreatePackagedAdjustedPrefab(string sourcePath)
    {
        string sourceModelPath = FindMainModelDependency(sourcePath);
        if (string.IsNullOrEmpty(sourceModelPath))
        {
            Debug.LogWarning("Selected prefab has no FBX/OBJ dependency: " + sourcePath);
            return GeneratedAsset.Invalid;
        }

        string assetName;
        string assetFolder;
        ResolvePackagedAssetIdentity(sourcePath, out assetName, out assetFolder);
        string prefabFolder = assetFolder + "/Prefab";
        string animationFolder = assetFolder + "/Animation";
        EnsureStandardAssetFolders(assetFolder);

        string prefabPath = PreparePackagePrefab(sourcePath, prefabFolder, assetName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Dictionary<string, string> copiedDependencies = CopyAdjustedPrefabDependencies(prefabPath, assetFolder);
        FlattenModelCompanionFolders(assetFolder);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ApplyImportSettingsToPackagedModels(assetFolder);
        RemapPackagedModelImporterMaterials(assetFolder, copiedDependencies);
        RemapCopiedAssetReferences(copiedDependencies, assetFolder);
        RemapCopiedPrefabModelReferences(prefabPath, copiedDependencies);
        CopyPrefabRendererMaterials(prefabPath, assetFolder, assetName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        NormalizePreparedPrefabBounds(prefabPath);
        AddOrUpdateBoxColliderInPrefab(prefabPath);
        NormalizePreparedPrefabAnimations(prefabPath, animationFolder, assetName);

        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (savedPrefab == null || savedPrefab.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            ClearBundleName(prefabPath);
            Debug.LogError("Prepared prefab has no renderers and will not be bundled: " + prefabPath);
            return GeneratedAsset.Invalid;
        }

        string finalModelPath = FindMainModelDependency(prefabPath);
        if (string.IsNullOrEmpty(finalModelPath))
        {
            finalModelPath = assetFolder + "/Model/" + Path.GetFileName(sourceModelPath);
        }
        string bundleName = assetName.ToLowerInvariant();
        AssetImporter prefabImporter = AssetImporter.GetAtPath(prefabPath);
        prefabImporter.assetBundleName = bundleName;
        prefabImporter.assetBundleVariant = AssetBundleVariant;
        prefabImporter.SaveAndReimport();
        ClearDuplicateBundleNames(prefabFolder, prefabPath, bundleName);

        AssetStats stats = CollectAssetStats(savedPrefab);
        return new GeneratedAsset(assetName, assetFolder, finalModelPath, finalModelPath, prefabPath, bundleName + "." + AssetBundleVariant, stats);
    }

    /// <summary>
    /// 决定这个预设体应该归到 Assets/Art 下面哪个资产名和哪个目录。
    ///
    /// 修复的问题：原来无条件用 Path.GetFileNameWithoutExtension(sourcePath) 当资产名。
    /// 于是"重新选中上一轮生成的预设体再打包一次"这个补救操作会这样走：
    ///   第一轮：Chair.fbx  ->  Assets/Art/Chair/Prefab/Chair_prefab.prefab
    ///   第二轮：选中 Chair_prefab.prefab  ->  资产名变成 "Chair_prefab"
    ///           ->  新开一个 Assets/Art/Chair_prefab/ 目录，和 Art/Chair/ 并存
    ///   第三轮：又变成 "Chair_prefab_prefab"…
    /// 每补救一次就多一份重复资产，AssetBundle 名字也跟着变，交付目录里会出现
    /// 两套同一个模型的产物，很容易交错。
    ///
    /// 现在的规则：如果选中的预设体已经在 Assets/Art/&lt;名字&gt;/ 下面（也就是本工具
    /// 上一轮的产物），就直接复用那个 &lt;名字&gt; 和那个目录，重跑多少次结果都一样。
    /// </summary>
    private static void ResolvePackagedAssetIdentity(string sourcePath, out string assetName, out string assetFolder)
    {
        string normalized = sourcePath.Replace("\\", "/");
        if (normalized.StartsWith(ArtRoot + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            string relative = normalized.Substring(ArtRoot.Length + 1);
            int separatorIndex = relative.IndexOf('/');
            if (separatorIndex > 0)
            {
                assetName = relative.Substring(0, separatorIndex);
                assetFolder = ArtRoot + "/" + assetName;
                return;
            }
        }

        assetName = MakeSafeName(Path.GetFileNameWithoutExtension(sourcePath));
        assetFolder = ArtRoot + "/" + assetName;
    }

    /// <summary>
    /// 拿到本次要处理的预设体路径。选中的预设体已经在目标 Prefab 目录里时原地处理，
    /// 不再复制一份——否则重跑一次就会在同一个 Prefab 目录里多出一个只是改了名的副本，
    /// 还要靠 ClearDuplicateBundleNames 去善后。
    /// </summary>
    private static string PreparePackagePrefab(string sourcePath, string prefabFolder, string assetName)
    {
        string normalized = sourcePath.Replace("\\", "/");
        if (normalized.StartsWith(prefabFolder + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            UnpackNestedPrefabInstancesInPlace(normalized);
            return normalized;
        }

        return CreatePackagePrefabCopy(sourcePath, prefabFolder + "/" + assetName + ".prefab");
    }

    private static void UnpackNestedPrefabInstancesInPlace(string prefabPath)
    {
        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            UnpackNestedPrefabInstances(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static string CreatePackagePrefabCopy(string sourcePath, string requestedDestinationPath)
    {
        requestedDestinationPath = requestedDestinationPath.Replace("\\", "/");
        GameObject instance = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            UnpackNestedPrefabInstances(instance);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, requestedDestinationPath);
            if (saved == null)
            {
                Debug.LogWarning("Failed to create package prefab copy: " + requestedDestinationPath);
                return sourcePath;
            }

            return requestedDestinationPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static void UnpackNestedPrefabInstances(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform == root.transform)
            {
                continue;
            }

            if (PrefabUtility.GetPrefabInstanceStatus(transform.gameObject) == PrefabInstanceStatus.Connected &&
                PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject) == transform.gameObject)
            {
                PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }
    }

    private static string FindMainModelDependency(string assetPath)
    {
        foreach (string dependency in AssetDatabase.GetDependencies(assetPath, true))
        {
            string normalized = dependency.Replace("\\", "/");
            if (IsModelAsset(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static Dictionary<string, string> CopyAdjustedPrefabDependencies(string prefabPath, string assetFolder)
    {
        var copied = new Dictionary<string, string>();
        foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
        {
            string path = dependency.Replace("\\", "/");
            if (path == prefabPath || path.StartsWith(assetFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string targetFolder = GetPreparedPrefabDependencyFolder(path);
            if (string.IsNullOrEmpty(targetFolder))
            {
                continue;
            }

            string requestedTargetPath = assetFolder + "/" + targetFolder + "/" + Path.GetFileName(path);
            if (IsTextureAsset(path))
            {
                SyncNewerSourceTextureToWorkingCopy(path, requestedTargetPath);
            }

            string copiedPath = CopyAssetToExactPath(path, requestedTargetPath);
            if (copiedPath != path)
            {
                copied[path] = copiedPath;
            }
        }

        return copied;
    }

    private static string GetPreparedPrefabDependencyFolder(string assetPath)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        System.Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

        if (IsModelAsset(assetPath))
        {
            return "Model";
        }

        if (extension == ".controller" || extension == ".anim" || type == typeof(AnimatorController) || type == typeof(AnimationClip))
        {
            return "Animation";
        }

        if (IsMaterialAsset(assetPath))
        {
            return "Material";
        }

        if (IsTextureAsset(assetPath) || typeof(Texture).IsAssignableFrom(type))
        {
            return "Texture";
        }

        if (IsTextAsset(assetPath) || type == typeof(TextAsset))
        {
            return "Text";
        }

        return null;
    }

    // MoveAssetToExactPath 已迁移到 RetinarBatchModelBuilder.AssetResolution.cs，
    // 并修复了失败时静默放弃（只 LogWarning，不重试、不上报）的问题。

    private static string CopyAssetToExactPath(string sourcePath, string requestedDestinationPath)
    {
        sourcePath = sourcePath.Replace("\\", "/");
        requestedDestinationPath = requestedDestinationPath.Replace("\\", "/");

        if (sourcePath.Equals(requestedDestinationPath, System.StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(requestedDestinationPath) != null)
        {
            return requestedDestinationPath;
        }

        if (!AssetDatabase.CopyAsset(sourcePath, requestedDestinationPath))
        {
            Debug.LogWarning("Failed to copy asset: " + sourcePath + " -> " + requestedDestinationPath);
            return sourcePath;
        }

        return requestedDestinationPath;
    }

    private static void SyncNewerSourceTextureToWorkingCopy(string sourcePath, string targetPath)
    {
        sourcePath = sourcePath.Replace("\\", "/");
        targetPath = targetPath.Replace("\\", "/");
        if (sourcePath.Equals(targetPath, System.StringComparison.OrdinalIgnoreCase) ||
            AssetDatabase.LoadAssetAtPath<Texture>(targetPath) == null)
        {
            return;
        }

        string sourceFullPath = AssetPathToFullPath(sourcePath);
        string targetFullPath = AssetPathToFullPath(targetPath);
        if (!File.Exists(sourceFullPath) || !File.Exists(targetFullPath) ||
            File.GetLastWriteTimeUtc(sourceFullPath) <= File.GetLastWriteTimeUtc(targetFullPath))
        {
            return;
        }

        var sourceInfo = new FileInfo(sourceFullPath);
        var targetInfo = new FileInfo(targetFullPath);
        if (sourceInfo.Length == targetInfo.Length && File.ReadAllBytes(sourceFullPath).SequenceEqual(File.ReadAllBytes(targetFullPath)))
        {
            return;
        }

        File.Copy(sourceFullPath, targetFullPath, true);
        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
    }

    private static void RemapCopiedPrefabModelReferences(string prefabPath, Dictionary<string, string> copiedDependencies)
    {
        if (copiedDependencies == null || copiedDependencies.Count == 0)
        {
            return;
        }

        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap = BuildCopiedObjectMap(copiedDependencies);

        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (MeshFilter meshFilter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh != null && objectMap.TryGetValue(meshFilter.sharedMesh, out UnityEngine.Object copiedMesh))
                {
                    meshFilter.sharedMesh = copiedMesh as Mesh;
                    EditorUtility.SetDirty(meshFilter);
                }
            }

            foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh != null && objectMap.TryGetValue(skinned.sharedMesh, out UnityEngine.Object copiedMesh))
                {
                    skinned.sharedMesh = copiedMesh as Mesh;
                    EditorUtility.SetDirty(skinned);
                }
            }

            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController != null &&
                    objectMap.TryGetValue(animator.runtimeAnimatorController, out UnityEngine.Object copiedController))
                {
                    animator.runtimeAnimatorController = copiedController as RuntimeAnimatorController;
                }

                if (animator.avatar != null && objectMap.TryGetValue(animator.avatar, out UnityEngine.Object copiedAvatar))
                {
                    animator.avatar = copiedAvatar as Avatar;
                }

                EditorUtility.SetDirty(animator);
            }

            foreach (Component component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                {
                    continue;
                }

                RemapSerializedObjectReferences(component, objectMap);
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static void RemapCopiedAssetReferences(
        Dictionary<string, string> copiedDependencies,
        string assetFolder)
    {
        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap =
            copiedDependencies != null && copiedDependencies.Count > 0
                ? BuildCopiedObjectMap(copiedDependencies)
                : new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        RemapCopiedMaterials(assetFolder, objectMap);

        if (copiedDependencies == null || copiedDependencies.Count == 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return;
        }

        foreach (string copiedPath in copiedDependencies.Values.Distinct(System.StringComparer.OrdinalIgnoreCase))
        {
            string extension = Path.GetExtension(copiedPath).ToLowerInvariant();
            if (IsModelAsset(copiedPath) || IsTextureAsset(copiedPath) || IsTextAsset(copiedPath) || extension == ".anim")
            {
                continue;
            }

            foreach (UnityEngine.Object target in AssetDatabase.LoadAllAssetsAtPath(copiedPath))
            {
                if (target != null)
                {
                    RemapSerializedObjectReferences(target, objectMap);
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CopyPrefabRendererMaterials(string prefabPath, string assetFolder, string assetName)
    {
        string materialFolder = assetFolder + "/Material";
        string textureFolder = assetFolder + "/Texture";
        EnsureAssetFolder(materialFolder);
        EnsureAssetFolder(textureFolder);

        var materialMap = new Dictionary<Material, Material>();
        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material sourceMaterial = materials[i];
                    if (sourceMaterial == null)
                    {
                        continue;
                    }

                    string sourceMaterialPath = AssetDatabase.GetAssetPath(sourceMaterial).Replace("\\", "/");
                    bool alreadyOrganized = sourceMaterialPath.StartsWith(
                        materialFolder + "/",
                        System.StringComparison.OrdinalIgnoreCase);

                    if (alreadyOrganized)
                    {
                        materials[i] = sourceMaterial;
                        continue;
                    }

                    if (!materialMap.TryGetValue(sourceMaterial, out Material copiedMaterial))
                    {
                        copiedMaterial = CreateMaterialCopyPreserveSettings(sourceMaterial, materialFolder, textureFolder, assetName, materialMap.Count + 1);
                        materialMap.Add(sourceMaterial, copiedMaterial);
                    }

                    materials[i] = copiedMaterial;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static Material CreateMaterialCopyPreserveSettings(Material source, string materialFolder, string textureFolder, string assetName, int index)
    {
        string materialName = "Mat_" + assetName + "_ID" + index.ToString("00");
        string materialPath = materialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
        }

        material.name = materialName;

        foreach (string propertyName in material.GetTexturePropertyNames())
        {
            Texture texture = material.GetTexture(propertyName);
            if (texture == null)
            {
                continue;
            }

            string texturePath = AssetDatabase.GetAssetPath(texture).Replace("\\", "/");
            if (string.IsNullOrEmpty(texturePath) || !IsTextureAsset(texturePath))
            {
                continue;
            }

            string copiedTexturePath = CopyAssetToExactPath(texturePath, textureFolder + "/" + Path.GetFileName(texturePath));
            Texture copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(copiedTexturePath);
            if (copiedTexture != null)
            {
                material.SetTexture(propertyName, copiedTexture);
            }
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Dictionary<UnityEngine.Object, UnityEngine.Object> BuildCopiedObjectMap(Dictionary<string, string> copiedDependencies)
    {
        var objectMap = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        foreach (KeyValuePair<string, string> pair in copiedDependencies)
        {
            UnityEngine.Object[] originals = AssetDatabase.LoadAllAssetsAtPath(pair.Key);
            UnityEngine.Object[] copies = AssetDatabase.LoadAllAssetsAtPath(pair.Value);

            foreach (UnityEngine.Object original in originals)
            {
                if (original == null)
                {
                    continue;
                }

                UnityEngine.Object copy = copies.FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.GetType() == original.GetType() &&
                    candidate.name == original.name);

                if (copy != null && !objectMap.ContainsKey(original))
                {
                    objectMap.Add(original, copy);
                }
            }
        }

        return objectMap;
    }

    private static void RemapCopiedMaterials(string assetFolder, Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        string materialFolder = assetFolder + "/Material";
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        foreach (string materialGuid in materialGuids)
        {
            string copiedPath = AssetDatabase.GUIDToAssetPath(materialGuid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(copiedPath);
            if (material == null)
            {
                continue;
            }

            foreach (string propertyName in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(propertyName);
                if (texture == null)
                {
                    continue;
                }

                Texture packagedTexture = null;
                if (objectMap.TryGetValue(texture, out UnityEngine.Object copiedTexture))
                {
                    packagedTexture = copiedTexture as Texture;
                }

                if (packagedTexture == null)
                {
                    string sourceTexturePath = AssetDatabase.GetAssetPath(texture).Replace("\\", "/");
                    string expectedTexturePath = assetFolder + "/Texture/" + Path.GetFileName(sourceTexturePath);
                    if (AssetDatabase.LoadAssetAtPath<Texture>(expectedTexturePath) == null &&
                        IsTextureAsset(sourceTexturePath))
                    {
                        CopyAssetToExactPath(sourceTexturePath, expectedTexturePath);
                    }
                    packagedTexture = AssetDatabase.LoadAssetAtPath<Texture>(expectedTexturePath);
                }

                if (packagedTexture != null && packagedTexture != texture)
                {
                    material.SetTexture(propertyName, packagedTexture);
                }
            }

            EditorUtility.SetDirty(material);
        }
    }

    private static void RemapCopiedAssets(Dictionary<string, string> copiedDependencies, Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        foreach (string copiedPath in copiedDependencies.Values)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(copiedPath))
            {
                RemapSerializedObjectReferences(asset, objectMap);
            }
        }
    }

    private static void RemapSerializedObjectReferences(UnityEngine.Object target, Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        if (target == null || objectMap.Count == 0)
        {
            return;
        }

        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(target);
        }
        catch (System.Exception)
        {
            return;
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool changed = false;
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            UnityEngine.Object originalReference = iterator.objectReferenceValue;
            if (originalReference != null && objectMap.TryGetValue(originalReference, out UnityEngine.Object copiedReference))
            {
                iterator.objectReferenceValue = copiedReference;
                changed = true;
            }
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    private static void FlattenModelCompanionFolders(string assetFolder)
    {
        string modelFolder = assetFolder + "/Model";
        if (!AssetDatabase.IsValidFolder(modelFolder))
        {
            return;
        }

        string modelFullPath = AssetPathToFullPath(modelFolder);
        if (!Directory.Exists(modelFullPath))
        {
            return;
        }

        // 先刷一次再按磁盘枚举。上一步的 SaveAndReimport() 会让 Unity 把 FBX 内嵌贴图
        // 抽取到 Model/<FBX名>.fbm/ 下面，这些文件立刻落盘、但还没进 AssetDatabase，
        // 直接搬移会失败（详见 MoveAssetToExactPath 里的说明）。
        AssetDatabase.Refresh();

        foreach (string filePath in Directory.GetFiles(modelFullPath, "*.*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(filePath).ToLowerInvariant() == ".meta")
            {
                continue;
            }

            string assetPath = FullPathToAssetPath(filePath);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(modelFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string targetFolder = GetModelCompanionTargetFolder(assetPath);
            if (string.IsNullOrEmpty(targetFolder))
            {
                continue;
            }

            string targetPath = assetFolder + "/" + targetFolder + "/" + Path.GetFileName(assetPath);
            MoveAssetToExactPath(assetPath, targetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        DeleteEmptySubfolders(modelFolder);
    }

    private static string GetModelCompanionTargetFolder(string assetPath)
    {
        if (IsModelAsset(assetPath))
        {
            return "Model";
        }

        if (IsMaterialAsset(assetPath))
        {
            return "Material";
        }

        if (IsTextureAsset(assetPath))
        {
            return "Texture";
        }

        if (IsTextAsset(assetPath))
        {
            return "Text";
        }

        return null;
    }

    // IsTextAsset 已迁移到 RetinarBatchModelBuilder.AssetResolution.cs

    private static void DeleteEmptySubfolders(string rootAssetFolder)
    {
        string rootFullPath = AssetPathToFullPath(rootAssetFolder);
        if (!Directory.Exists(rootFullPath))
        {
            return;
        }

        foreach (string directory in Directory.GetDirectories(rootFullPath, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            bool hasNonMetaFile = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Any(path => Path.GetExtension(path).ToLowerInvariant() != ".meta");
            bool hasSubfolder = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly).Length > 0;

            if (hasNonMetaFile || hasSubfolder)
            {
                continue;
            }

            string assetPath = FullPathToAssetPath(directory);
            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }

    private static void AddOrUpdateBoxColliderInPrefab(string prefabPath)
    {
        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            AddOrUpdateBoxCollider(instance);
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static void NormalizePreparedPrefabBounds(string prefabPath)
    {
        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            RebasePrefabRootToOrigin(instance);
            Transform[] movableRoots = GetMovablePrefabRoots(instance);
            if (movableRoots.Length == 0 || !TryGetRendererBounds(instance, out Bounds bounds))
            {
                return;
            }

            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0f)
            {
                float targetMaxSize = Mathf.Min(SafeZoneSize.x, SafeZoneSize.y, SafeZoneSize.z) * SafeZonePadding;
                float scale = targetMaxSize / maxSize;
                foreach (Transform movable in movableRoots)
                {
                    movable.localScale *= scale;
                    movable.localPosition *= scale;
                }
            }

            if (TryGetRendererBounds(instance, out bounds))
            {
                Vector3 offset = SafeZoneCenter - bounds.center;
                foreach (Transform movable in movableRoots)
                {
                    movable.position += offset;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static void RebasePrefabRootToOrigin(GameObject instance)
    {
        Transform root = instance.transform;
        if (root.localPosition.sqrMagnitude < 0.00000001f &&
            Quaternion.Angle(root.localRotation, Quaternion.identity) < 0.001f &&
            (root.localScale - Vector3.one).sqrMagnitude < 0.00000001f)
        {
            return;
        }

        Transform[] directChildren = new Transform[root.childCount];
        Vector3[] worldPositions = new Vector3[root.childCount];
        Quaternion[] worldRotations = new Quaternion[root.childCount];
        Vector3[] worldScales = new Vector3[root.childCount];
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            directChildren[i] = child;
            worldPositions[i] = child.position;
            worldRotations[i] = child.rotation;
            worldScales[i] = child.lossyScale;
        }

        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        for (int i = 0; i < directChildren.Length; i++)
        {
            directChildren[i].SetPositionAndRotation(worldPositions[i], worldRotations[i]);
            directChildren[i].localScale = worldScales[i];
        }
    }

    private static Transform[] GetMovablePrefabRoots(GameObject instance)
    {
        var movableRoots = new List<Transform>();
        foreach (Transform child in instance.transform)
        {
            movableRoots.Add(child);
        }

        if (movableRoots.Count == 0 && instance.GetComponentsInChildren<Renderer>(true).Length > 0)
        {
            movableRoots.Add(instance.transform);
        }

        return movableRoots.ToArray();
    }

    private static void NormalizePreparedPrefabAnimations(string prefabPath, string animationFolder, string assetName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            bool loop = ShouldLoopAnimation(clip.name);
            SetClipLoopFlag(clip, loop);

            string clipPath = AssetDatabase.GetAssetPath(clip);
            if (!string.IsNullOrEmpty(clipPath) && clipPath.StartsWith(animationFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                string clipName = "Anim_" + assetName + "_" + CleanAnimationName(clip.name) + "_" + (loop ? "loop" : "once");
                AssetDatabase.RenameAsset(clipPath, clipName);
            }
        }

        AnimationClip primaryClip = clips.FirstOrDefault();
        string suffix = primaryClip == null || ShouldLoopAnimation(primaryClip.name) ? "loop" : "once";
        string controllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
        if (!string.IsNullOrEmpty(controllerPath) && controllerPath.StartsWith(animationFolder + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            AssetDatabase.RenameAsset(controllerPath, "Anim_" + assetName + "_" + CleanAnimationName(primaryClip != null ? primaryClip.name : "default") + "_" + suffix);
        }
    }

    private static bool ShouldLoopAnimation(string animationName)
    {
        return animationName.IndexOf("once", System.StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static string CleanAnimationName(string animationName)
    {
        string name = animationName.Replace("Anim_", "");
        name = name.Replace("_loop", "");
        name = name.Replace("_once", "");
        return MakeSafeName(name);
    }

    private static void SetClipLoopFlag(AnimationClip clip, bool loop)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty animationSettings = serializedClip.FindProperty("m_AnimationClipSettings");
        if (animationSettings == null)
        {
            return;
        }

        SerializedProperty loopTime = animationSettings.FindPropertyRelative("m_LoopTime");
        if (loopTime != null)
        {
            loopTime.boolValue = loop;
        }

        serializedClip.ApplyModifiedProperties();
        EditorUtility.SetDirty(clip);
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds();
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void BuildAssetBundles(BuildTarget target)
    {
        string outputPath = Path.Combine(AssetBundleRoot, ToPlatformFolder(target));
        EnsureDiskDirectory(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
        BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, target);
    }

    private static int CopySourceFilesToDeliverables(List<GeneratedAsset> assets)
    {
        int textureWarningCount = 0;
        foreach (GeneratedAsset asset in assets)
        {
            string root = Path.Combine(Directory.GetCurrentDirectory(), DeliverableRoot, asset.AssetName, "01_source");
            string modelDir = Path.Combine(root, "Model");
            string textureDir = Path.Combine(root, "Textures");

            EnsureDiskDirectory(modelDir);
            EnsureDiskDirectory(textureDir);

            var modelPaths = new HashSet<string>();
            var ignoredSourceMaterials = new HashSet<string>();
            var ignoredSourceTextures = new HashSet<string>();
            var ignoredPackagedModels = new HashSet<string>();
            var packagedMaterialPaths = new HashSet<string>();
            var texturePaths = new HashSet<string>();

            CollectSourceAssets(asset.SourcePath, modelPaths, ignoredSourceMaterials, ignoredSourceTextures);
            CollectSourceAssets(asset.PrefabPath, ignoredPackagedModels, packagedMaterialPaths, texturePaths);

            foreach (string modelPath in modelPaths)
            {
                CopyAssetFile(modelPath, modelDir);
            }

            List<string> textureReport = new List<string>
            {
                "Texture report notes:",
                "- Unity Imported Size is the effective Texture2D size after TextureImporter Max Size/settings.",
                "- Source File Size is the original PNG/JPG file size on disk; Unity import settings do not rewrite source files.",
                "- Source file warning threshold: 5 MB.",
                "Path\tUnity Imported Size\tSource File Size\tStatus"
            };
            foreach (string texturePath in texturePaths)
            {
                CopyAssetFile(texturePath, textureDir);
                int issueCount = GetTextureIssueCount(texturePath);
                if (issueCount > 0)
                {
                    textureWarningCount += issueCount;
                }

                textureReport.Add(BuildTextureReportLine(texturePath));
            }

            string reportPath = Path.Combine(root, "texture_size_report.txt");
            File.WriteAllLines(reportPath, textureReport.ToArray(), Encoding.UTF8);

            string dccReportPath = Path.Combine(root, "dcc_model_report.txt");
            File.WriteAllLines(dccReportPath, BuildDccModelReport(asset).ToArray(), Encoding.UTF8);
        }

        return textureWarningCount;
    }

    private static List<string> BuildDccModelReport(GeneratedAsset asset)
    {
        var lines = new List<string>();
        lines.Add("DCC / model report");
        lines.Add("Asset: " + asset.AssetName);
        lines.Add("Original source: " + asset.SourcePath);
        lines.Add("Unity model path: " + asset.UnityModelPath);
        lines.Add("Unity version: " + Application.unityVersion);
        lines.Add("Mesh count: " + asset.Stats.MeshCount);
        lines.Add("Vertices: " + asset.Stats.VertexCount);
        lines.Add("Faces: " + asset.Stats.TriangleCount);
        lines.Add("Renderers: " + asset.Stats.RendererCount);
        lines.Add("");
        lines.Add("DCC software/version: Unity cannot reliably detect the original DCC software version from every FBX/MAX/MA/BLEND file.");
        lines.Add("Manual check required: open the editable DCC source file in its authoring software and record software name/version in asset_info.xlsx.");
        lines.Add("If only FBX is provided, treat DCC source/version as pending unless the producer supplies export metadata or source screenshots.");
        return lines;
    }

    private static void CopySourceTexturesToUnityArtFolder(string sourcePath, string textureFolder)
    {
        var modelPaths = new HashSet<string>();
        var materialPaths = new HashSet<string>();
        var texturePaths = new HashSet<string>();
        CollectSourceAssets(sourcePath, modelPaths, materialPaths, texturePaths);

        foreach (string texturePath in texturePaths)
        {
            string targetPath = textureFolder + "/" + Path.GetFileName(texturePath);
            CopyProjectAssetIfNeeded(texturePath, targetPath);
        }

        AssetDatabase.Refresh();
    }

    private static string CopyModelToUnityArtFolder(string sourcePath, string modelFolder, string assetName)
    {
        if (!IsModelAsset(sourcePath))
        {
            return sourcePath;
        }

        string extension = Path.GetExtension(sourcePath);
        string targetPath = modelFolder + "/" + assetName + extension.ToLowerInvariant();
        CopyProjectAssetIfNeeded(sourcePath, targetPath);
        AssetDatabase.Refresh();
        return targetPath;
    }

    private static void TrySetModelImporterMaterialImportMode(ModelImporter importer, string enumName)
    {
        System.Reflection.PropertyInfo property = typeof(ModelImporter).GetProperty("materialImportMode");
        if (property == null || !property.CanWrite)
        {
            return;
        }

        try
        {
            object value = System.Enum.Parse(property.PropertyType, enumName);
            property.SetValue(importer, value, null);
        }
        catch (System.Exception)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    // TrySetModelImporterMaterialLocation 已删除：它是用反射设置 materialLocation 的
    // 兜底实现，但 ApplyModelImportSettings 一直是直接用强类型属性赋值的，
    // 这个方法在工程里从来没有任何调用方，留着只会让人以为材质来源有两条设置路径。

    private static void ApplyImportSettingsToPackagedModels(string assetFolder)
    {
        string modelFolder = assetFolder + "/Model";
        if (!AssetDatabase.IsValidFolder(modelFolder))
        {
            return;
        }

        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelFolder });
        foreach (string guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            if (IsModelAsset(modelPath))
            {
                ApplyModelImportSettings(modelPath);
            }
        }
    }

    private static void RemapPackagedModelImporterMaterials(
        string assetFolder,
        Dictionary<string, string> copiedDependencies)
    {
        if (copiedDependencies == null || copiedDependencies.Count == 0)
        {
            return;
        }

        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap = BuildCopiedObjectMap(copiedDependencies);
        string modelFolder = assetFolder + "/Model";
        string materialFolder = assetFolder + "/Material";
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelFolder });

        foreach (string guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsModelAsset(modelPath))
            {
                continue;
            }

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            bool changed = false;
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> pair in importer.GetExternalObjectMap())
            {
                UnityEngine.Object sourceObject = pair.Value;
                if (sourceObject == null)
                {
                    continue;
                }

                UnityEngine.Object packagedObject = null;
                objectMap.TryGetValue(sourceObject, out packagedObject);

                if (packagedObject == null && sourceObject is Material)
                {
                    string sourcePath = AssetDatabase.GetAssetPath(sourceObject);
                    string expectedPath = materialFolder + "/" + Path.GetFileName(sourcePath);
                    packagedObject = AssetDatabase.LoadAssetAtPath<Material>(expectedPath);
                }

                string packagedPath = AssetDatabase.GetAssetPath(packagedObject);
                if (packagedObject != null &&
                    packagedPath.StartsWith(assetFolder + "/", System.StringComparison.OrdinalIgnoreCase) &&
                    packagedObject != sourceObject)
                {
                    importer.AddRemap(pair.Key, packagedObject);
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static void CopyProjectAssetIfNeeded(string sourcePath, string targetPath)
    {
        if (sourcePath == targetPath)
        {
            return;
        }

        string sourceFullPath = AssetPathToFullPath(sourcePath);
        string targetFullPath = AssetPathToFullPath(targetPath);
        try
        {
            EnsureDiskDirectory(Path.GetDirectoryName(targetFullPath));
            File.Copy(sourceFullPath, targetFullPath, true);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Failed to refresh generated asset copy: " + sourcePath + " -> " + targetPath + "\n" + exception.Message);
        }
    }

    // CollectSourceAssets / AddTypedAssetPath / AddAssetsFromFolder /
    // IsModelAsset / IsMaterialAsset / IsTextureAsset
    // 已迁移到 RetinarBatchModelBuilder.AssetResolution.cs 并修复为递归查找、
    // 支持更多伴生文件夹命名、失败时输出详细诊断，而不是静默找不到贴图。

    private static void CopyAssetFile(string assetPath, string targetDirectory)
    {
        string sourceFullPath = AssetPathToFullPath(assetPath);
        if (!File.Exists(sourceFullPath))
        {
            Debug.LogWarning("Source file not found for 01_source copy: " + assetPath);
            return;
        }

        string targetPath = Path.Combine(targetDirectory, Path.GetFileName(sourceFullPath));
        File.Copy(sourceFullPath, targetPath, true);
    }

    private static string BuildTextureReportLine(string texturePath)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            return texturePath + "\tUnknown size\tWARN: texture could not be loaded";
        }

        bool widthOk = IsPowerOfTwo(texture.width);
        bool heightOk = IsPowerOfTwo(texture.height);
        long fileSize = GetAssetFileSize(texturePath);
        bool fileSizeOk = fileSize <= MaxTextureSourceBytes;
        string status = widthOk && heightOk && fileSizeOk ? "OK" : BuildTextureWarningText(widthOk, heightOk, fileSizeOk);
        string line = texturePath + "\t" + texture.width + "x" + texture.height + "\t" + FormatBytes(fileSize) + "\t" + status;
        if (!widthOk || !heightOk)
        {
            Debug.LogWarning("Texture size is not power of two: " + line);
        }

        if (!fileSizeOk)
        {
            // 本工具不改写贴图像素（规则 28/34），所以这里只能告警并指出该去哪里处理。
            // 最常见的成因是 FBX 内嵌贴图：Unity 每次导入模型工作副本都会从 FBX 二进制里
            // 重新抽取一份原始大图，艺术家在导入区压过的那一份帮不上忙。
            Debug.LogWarning("Texture source file is larger than 5 MB and should be optimized: " + line +
                "\n处理方式：打开 Tools > 贴图处理工具，选中该贴图执行\"压缩超标的贴图源文件\"，然后重新打包一次。" +
                "\n重新打包时会保留已压缩的这一份，不会被 FBX 里重新抽取的大图覆盖。");
        }

        return line;
    }

    private static int GetTextureIssueCount(string texturePath)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        int issueCount = 0;
        if (texture == null || !IsPowerOfTwo(texture.width) || !IsPowerOfTwo(texture.height))
        {
            issueCount++;
        }

        if (GetAssetFileSize(texturePath) > MaxTextureSourceBytes)
        {
            issueCount++;
        }

        return issueCount;
    }

    private static string BuildTextureWarningText(bool widthOk, bool heightOk, bool fileSizeOk)
    {
        var warnings = new List<string>();
        if (!widthOk || !heightOk)
        {
            warnings.Add("WARN: size is not power of two");
        }

        if (!fileSizeOk)
        {
            warnings.Add("WARN: source file > 5 MB");
        }

        return string.Join("; ", warnings.ToArray());
    }

    private static long GetAssetFileSize(string assetPath)
    {
        string fullPath = AssetPathToFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            return 0;
        }

        return new FileInfo(fullPath).Length;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "Unknown size";
        }

        return (bytes / 1024f / 1024f).ToString("0.00") + " MB";
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        if (assetPath == "Assets")
        {
            return Application.dataPath;
        }

        if (assetPath.StartsWith("Assets/"))
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath).Replace("/", Path.DirectorySeparatorChar.ToString());
        }

        return assetPath;
    }

    private static string FullPathToAssetPath(string fullPath)
    {
        string normalizedFullPath = fullPath.Replace("\\", "/");
        string normalizedProjectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
        if (!normalizedFullPath.StartsWith(normalizedProjectRoot))
        {
            return null;
        }

        return normalizedFullPath.Substring(normalizedProjectRoot.Length + 1);
    }

    private static void CopyBuiltBundlesToDeliverables(List<GeneratedAsset> assets)
    {
        foreach (GeneratedAsset asset in assets)
        {
            CopyBuiltBundle(asset, "Android");
            CopyBuiltBundle(asset, "iOS");
        }
    }

    private static void CopyBuiltBundle(GeneratedAsset asset, string platformFolder)
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string sourceDir = Path.Combine(projectRoot, AssetBundleRoot, platformFolder);
        string bundleSource = Path.Combine(sourceDir, asset.BundleFileName);
        string manifestSource = bundleSource + ".manifest";
        string targetDir = Path.Combine(projectRoot, DeliverableRoot, asset.AssetName, "03_assetbundles", platformFolder);

        EnsureDiskDirectory(targetDir);
        CopyFileIfExists(bundleSource, Path.Combine(targetDir, asset.BundleFileName));
        CopyFileIfExists(manifestSource, Path.Combine(targetDir, asset.BundleFileName + ".manifest"));
    }

    private static void ExportUnityPackages(List<GeneratedAsset> assets)
    {
        foreach (GeneratedAsset asset in assets)
        {
            string outputDir = Path.Combine(DeliverableRoot, asset.AssetName, "02_unity");
            EnsureDiskDirectory(Path.Combine(Directory.GetCurrentDirectory(), outputDir));
            string outputPath = Path.Combine(outputDir, asset.AssetName + ".unitypackage");
            string[] modelPackageAssets = AssetDatabase.GetDependencies(asset.PrefabPath, true)
                .Select(path => path.Replace("\\", "/"))
                .Where(path => path.Equals(asset.AssetFolder, System.StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith(asset.AssetFolder + "/", System.StringComparison.OrdinalIgnoreCase))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToArray();

            AssetDatabase.ExportPackage(modelPackageAssets, outputPath, ExportPackageOptions.Default);
        }
    }

    // 三道校验（ValidateModelFoldersAreClean / ValidateExternalDependencies）以及
    // 逐资产判定的调度（PartitionAssetsThatPassValidation）都在
    // RetinarBatchModelBuilder.AssetResolution.cs 里。
    // 修复点一：校验从"整批一起判、任一条不过就全批终止"改成"逐个资产判、只排除没过的那个"。
    // 修复点二：ValidateExternalDependencies 会先尝试自愈（把贴图/材质复制并重定向进该模型
    // 自己的 Art 目录），只有自愈也失败时才报错，
    // 并且报错信息里会带上磁盘绝对路径、最后修改时间等线索。

    private static bool ValidatePrefabSpatialPlacement(List<GeneratedAsset> assets, out string errorText)
    {
        var errors = new List<string>();
        foreach (GeneratedAsset asset in assets)
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(asset.PrefabPath);
            try
            {
                Transform root = instance.transform;
                if (root.localPosition.sqrMagnitude > 0.000001f)
                {
                    errors.Add(asset.AssetName + ": root position is not zero: " + root.localPosition);
                }

                if (Quaternion.Angle(root.localRotation, Quaternion.identity) > 0.1f)
                {
                    errors.Add(asset.AssetName + ": root rotation is not identity: " + root.localEulerAngles);
                }

                if ((root.localScale - Vector3.one).sqrMagnitude > 0.000001f)
                {
                    errors.Add(asset.AssetName + ": root scale is not one: " + root.localScale);
                }

                if (!TryGetRendererBounds(instance, out Bounds bounds))
                {
                    errors.Add(asset.AssetName + ": no renderer bounds were found.");
                    continue;
                }

                float centerDistance = Vector3.Distance(bounds.center, SafeZoneCenter);
                float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float targetMaxSize = Mathf.Min(SafeZoneSize.x, SafeZoneSize.y, SafeZoneSize.z) * SafeZonePadding;
                if (centerDistance > 0.02f)
                {
                    errors.Add(asset.AssetName + ": renderer center is outside SafeZone center by " + centerDistance.ToString("F4") + " m.");
                }

                if (maxSize < 0.01f || maxSize > targetMaxSize + 0.02f)
                {
                    errors.Add(asset.AssetName + ": renderer max size is invalid: " + maxSize.ToString("F4") + " m.");
                }

                BoxCollider collider = instance.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    errors.Add(asset.AssetName + ": root BoxCollider is missing.");
                }
                else
                {
                    Vector3 colliderWorldCenter = root.TransformPoint(collider.center);
                    if (Vector3.Distance(colliderWorldCenter, bounds.center) > 0.02f)
                    {
                        errors.Add(asset.AssetName + ": BoxCollider center does not match renderer center.");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        errorText = string.Join("\n", errors.Distinct().ToArray());
        return errors.Count == 0;
    }

    // WriteSpatialPlacementFailureReport 已删除，报告统一由
    // RetinarBatchModelBuilder.AssetResolution.cs 里的 WriteValidationFailureReport 产出。
    // IsApprovedRuntimeDependency 也在那个分文件里。

    private static void WriteDocsFiles(List<GeneratedAsset> assets)
    {
        foreach (GeneratedAsset asset in assets)
        {
            WriteRuntimeRequirements(asset);
            string docsDir = Path.Combine(Directory.GetCurrentDirectory(), DeliverableRoot, asset.AssetName, "06_docs");
            EnsureDiskDirectory(docsDir);
            string legacyReadme = Path.Combine(docsDir, "README.txt");
            if (File.Exists(legacyReadme))
            {
                File.Delete(legacyReadme);
            }

            WriteAssetInfoWorkbook(asset, Path.Combine(docsDir, "asset_info.xlsx"));
        }
    }

    private static void WriteRuntimeRequirements(GeneratedAsset asset)
    {
        string outputDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            DeliverableRoot,
            asset.AssetName,
            "00_runtime_requirements");
        EnsureDiskDirectory(outputDir);

        string[] runtimeDependencies = AssetDatabase.GetDependencies(asset.PrefabPath, true)
            .Select(path => path.Replace("\\", "/"))
            .Where(IsApprovedRuntimeDependency)
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path)
            .ToArray();

        bool requiresRuntime = runtimeDependencies.Length > 0;
        var lines = new List<string>
        {
            "Model: " + asset.AssetName,
            "Unity: 2020.3.49f1c1",
            "Required Runtime: " + (requiresRuntime ? RequiredRuntimeVersion : "None detected"),
            "Import order: Runtime first, model UnityPackage second.",
            "AssetBundle note: Runtime C# code must already be compiled into the validation app/player.",
            "",
            "Detected Runtime Dependencies:"
        };

        if (runtimeDependencies.Length == 0)
        {
            lines.Add("- None");
        }
        else
        {
            lines.AddRange(runtimeDependencies.Select(path => "- " + path));
        }

        File.WriteAllLines(
            Path.Combine(outputDir, "runtime_requirements.txt"),
            lines.ToArray(),
            new UTF8Encoding(false));
    }

    private static AssetStats CollectAssetStats(GameObject prefab)
    {
        var stats = new AssetStats();
        var materialNames = new HashSet<string>();
        var textureNames = new HashSet<string>();

        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            stats.MeshCount++;
            stats.VertexCount += mesh.vertexCount;
            stats.TriangleCount += mesh.triangles.Length / 3;
        }

        foreach (SkinnedMeshRenderer skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = skinned.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            stats.MeshCount++;
            stats.VertexCount += mesh.vertexCount;
            stats.TriangleCount += mesh.triangles.Length / 3;
        }

        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            stats.RendererCount++;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                materialNames.Add(material.name);
                foreach (string textureProperty in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(textureProperty);
                    if (texture == null)
                    {
                        continue;
                    }

                    textureNames.Add(texture.name);
                    stats.MaxTextureWidth = Mathf.Max(stats.MaxTextureWidth, texture.width);
                    stats.MaxTextureHeight = Mathf.Max(stats.MaxTextureHeight, texture.height);
                }
            }
        }

        stats.MaterialCount = materialNames.Count;
        stats.TextureCount = textureNames.Count;
        return stats;
    }

    // asset_info.xlsx 的全部生成代码（WriteAssetInfoWorkbook 一直到 XmlEscape，约 500 行
    // 手写 OOXML / zip 处理）已迁移到同名 partial 分文件：
    //   RetinarBatchModelBuilder.AssetInfoWorkbook.cs
    // 迁移原因：那部分只是"把统计数据渲染成一个 Excel 文件"，和模型规范化、打包流程
    // 没有任何耦合，却占了主文件近五分之一的体积。分开之后改表格内容和改打包流程
    // 互不干扰。调用入口仍然是 WriteDocsFiles 里的 WriteAssetInfoWorkbook。
    private static void AddOrUpdateBoxCollider(GameObject root)
    {
        if (!TryGetRendererBounds(root, out Bounds bounds))
        {
            return;
        }

        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider>();
        }

        collider.center = root.transform.InverseTransformPoint(bounds.center);
        Vector3 min = root.transform.InverseTransformPoint(bounds.min);
        Vector3 max = root.transform.InverseTransformPoint(bounds.max);
        collider.size = new Vector3(
            Mathf.Abs(max.x - min.x),
            Mathf.Abs(max.y - min.y),
            Mathf.Abs(max.z - min.z));
    }

    private static void SetupAnimationController(GameObject model, string modelPath, string animationFolder, string assetName)
    {
        AnimationClip[] clips = GetUsableAnimationClips(modelPath);
        if (clips.Length == 0)
        {
            return;
        }

        string controllerPath = animationFolder + "/" + assetName + "_controller.controller";
        if (AssetDatabase.LoadAssetAtPath<Object>(controllerPath) != null)
        {
            AssetDatabase.DeleteAsset(controllerPath);
        }

        UnityEditor.Animations.AnimatorController controller =
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        UnityEditor.Animations.AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            UnityEditor.Animations.AnimatorState state = stateMachine.AddState(MakeSafeName(clip.name));
            state.motion = clip;
            if (i == 0)
            {
                stateMachine.defaultState = state;
            }
        }

        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(animator);
    }

    private static AnimationClip[] GetUsableAnimationClips(string modelPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(modelPath)
            .OfType<AnimationClip>()
            .Where(clip => clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static void ApplyMaterialCopies(GameObject root, string materialFolder, string textureFolder, string assetName)
    {
        var materialMap = new Dictionary<Material, Material>();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null)
                {
                    continue;
                }

                if (!materialMap.TryGetValue(source, out Material copied))
                {
                    copied = CreateOrUpdateMaterialCopy(source, materialFolder, textureFolder, assetName, materialMap.Count + 1);
                    materialMap.Add(source, copied);
                }

                materials[i] = copied;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static Material CreateOrUpdateMaterialCopy(Material source, string materialFolder, string textureFolder, string assetName, int index)
    {
        string materialName = "Mat_" + assetName + "_ID" + index.ToString("00");
        string materialPath = materialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
        }

        material.name = materialName;
        RemapMaterialTexturesToArtFolder(material, textureFolder);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RemapMaterialTexturesToArtFolder(Material material, string textureFolder)
    {
        foreach (string propertyName in material.GetTexturePropertyNames())
        {
            Texture texture = material.GetTexture(propertyName);
            if (texture == null)
            {
                continue;
            }

            string sourceTexturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(sourceTexturePath) || sourceTexturePath.Replace("\\", "/").StartsWith(textureFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                // 内嵌贴图（无独立资产路径）或已经在目标文件夹里，跳过。
                continue;
            }

            string targetTexturePath = textureFolder + "/" + Path.GetFileName(sourceTexturePath);
            Texture copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(targetTexturePath);
            if (copiedTexture == null)
            {
                // 目标文件夹里没有副本——很可能是因为源贴图/伴生文件夹被移动过，
                // CollectSourceAssets 没找到它。这里做兜底：现在就地补一份副本，
                // 而不是让材质继续指向工程里的外部路径（那样后面校验会把整批打包判为失败）。
                string copiedPath = CopyAssetToExactPath(sourceTexturePath, targetTexturePath);
                copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(copiedPath);
                if (copiedTexture == null)
                {
                    Debug.LogWarning("Texture could not be relocated into " + textureFolder + ": " + sourceTexturePath +
                        "\n如果该贴图最近被人为移动过位置，请确认它仍然存在于工程内，或手动把它放回模型旁边的 Texture/Materials 文件夹后重新执行打包。");
                    continue;
                }
            }

            material.SetTexture(propertyName, copiedTexture);
        }
    }

    private static void ClearDuplicateBundleNames(string prefabFolder, string currentPrefabPath, string bundleName)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == currentPrefabPath)
            {
                continue;
            }

            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null && importer.assetBundleName == bundleName)
            {
                importer.assetBundleVariant = null;
                importer.assetBundleName = null;
                importer.SaveAndReimport();
            }
        }
    }

    private static void ClearBundleName(string assetPath)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            return;
        }

        importer.assetBundleVariant = null;
        importer.assetBundleName = null;
        importer.SaveAndReimport();
    }

    private static void EnsureStandardAssetFolders(string assetFolder)
    {
        EnsureAssetFolder(assetFolder);
        EnsureAssetFolder(assetFolder + "/Model");
        EnsureAssetFolder(assetFolder + "/Texture");
        EnsureAssetFolder(assetFolder + "/Material");
        EnsureAssetFolder(assetFolder + "/Animation");
        EnsureAssetFolder(assetFolder + "/Prefab");
        EnsureAssetFolder(assetFolder + "/UI");
        EnsureAssetFolder(assetFolder + "/Text");
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void EnsureDiskDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static void CopyFileIfExists(string source, string target)
    {
        if (File.Exists(source))
        {
            File.Copy(source, target, true);
        }
        else
        {
            Debug.LogWarning("Expected bundle file was not found: " + source);
        }
    }

    private static string ToPlatformFolder(BuildTarget target)
    {
        if (target == BuildTarget.iOS)
        {
            return "iOS";
        }

        return target.ToString();
    }

    private static string MakeSafeName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Replace(' ', '_');
    }

    private struct GeneratedAsset
    {
        public static readonly GeneratedAsset Invalid = new GeneratedAsset(null, null, null, null, null, null, new AssetStats());

        public readonly string AssetName;
        public readonly string AssetFolder;
        public readonly string SourcePath;
        public readonly string UnityModelPath;
        public readonly string PrefabPath;
        public readonly string BundleFileName;
        public readonly AssetStats Stats;

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(PrefabPath); }
        }

        public GeneratedAsset(string assetName, string assetFolder, string sourcePath, string unityModelPath, string prefabPath, string bundleFileName, AssetStats stats)
        {
            AssetName = assetName;
            AssetFolder = assetFolder;
            SourcePath = sourcePath;
            UnityModelPath = unityModelPath;
            PrefabPath = prefabPath;
            BundleFileName = bundleFileName;
            Stats = stats;
        }
    }

    private struct AssetStats
    {
        public int MeshCount;
        public long VertexCount;
        public long TriangleCount;
        public int RendererCount;
        public int MaterialCount;
        public int TextureCount;
        public int MaxTextureWidth;
        public int MaxTextureHeight;

        public string TextureSummary
        {
            get
            {
                if (TextureCount <= 0)
                {
                    return "待复核 / 未从材质引用中统计到贴图";
                }

                return TextureCount + " 张贴图；最大 " + MaxTextureWidth + " x " + MaxTextureHeight;
            }
        }
    }
}
