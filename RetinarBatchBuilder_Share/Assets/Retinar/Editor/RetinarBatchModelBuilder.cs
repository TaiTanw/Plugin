using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 本文件是 RetinarBatchModelBuilder 的主体（选择模型、生成预制体、构建 AssetBundle、导出交付物）。
// 资产发现（贴图/材质在哪）与打包前校验的逻辑已拆分到同名 partial 文件：
//   RetinarBatchModelBuilder.AssetResolution.cs
// 拆分原因：这两块正是“移动文件位置导致打包终止”问题的根源所在，
// 单独放一个文件便于以后单独排查、单独测试，不用在 2000+ 行主文件里翻找。
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

        if (generated.Count > 0)
        {
            if (!ValidateModelFoldersAreClean(generated, out string modelFolderError))
            {
                string reportPath = WriteModelFolderFailureReport(modelFolderError);
                ShowDialogDeferred(
                    "Retinar Batch Builder",
                    "Packaging stopped: Model must contain model files only.\n\n" +
                    modelFolderError +
                    "\n\nFull report:\n" + reportPath,
                    "OK");
                return;
            }

            if (!ValidatePrefabSpatialPlacement(generated, out string spatialError))
            {
                string reportPath = WriteSpatialPlacementFailureReport(spatialError);
                ShowDialogDeferred(
                    "Retinar Batch Builder",
                    "Packaging stopped: prefab placement is outside the SafeZone.\n\n" +
                    spatialError +
                    "\n\nFull report:\n" + reportPath,
                    "OK");
                return;
            }

            if (!ValidateExternalDependencies(generated, out string dependencyError))
            {
                string reportPath = WriteExternalDependencyFailureReport(dependencyError);
                string[] dependencyLines = dependencyError
                    .Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                string preview = string.Join("\n", dependencyLines.Take(8).ToArray());
                if (dependencyLines.Length > 8)
                {
                    preview += "\n... and " + (dependencyLines.Length - 8) + " more.";
                }

                ShowDialogDeferred(
                    "Retinar Batch Builder",
                    "Packaging stopped: unsupported external dependencies were found.\n\n" +
                    preview +
                    "\n\nFull report:\n" + reportPath,
                    "OK");
                return;
            }

            BuildAssetBundles(BuildTarget.Android);
            BuildAssetBundles(BuildTarget.iOS);
            int textureWarningCount = CopySourceFilesToDeliverables(generated);
            CopyBuiltBundlesToDeliverables(generated);
            ExportUnityPackages(generated);
            WriteDocsFiles(generated);

            string warningText = textureWarningCount > 0
                ? "\n\nTexture check: " + textureWarningCount + " texture issue(s). See 01_source/texture_size_report.txt."
                : "\n\nTexture check: all copied textures are power-of-two and <= 5 MB.";

            ShowDialogDeferred(
                "Retinar Batch Builder",
                "Done. Processed " + generated.Count + " asset(s).\n\nUnity prefabs: " + ArtRoot +
                "\nAssetBundles: " + AssetBundleRoot +
                "\nDeliverables: " + GetDeliverablesAbsolutePath() +
                warningText,
                "OK");
        }
        else
        {
            ShowDialogDeferred("Retinar Batch Builder", "No valid prefabs were generated. AssetBundles were not built.", "OK");
            return;
        }
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

        string assetName = MakeSafeName(Path.GetFileNameWithoutExtension(sourcePath));
        string assetFolder = ArtRoot + "/" + assetName;
        string prefabFolder = assetFolder + "/Prefab";
        string animationFolder = assetFolder + "/Animation";
        EnsureStandardAssetFolders(assetFolder);

        string prefabPath = CreatePackagePrefabCopy(sourcePath, prefabFolder + "/" + assetName + ".prefab");
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

    private static void TrySetModelImporterMaterialLocation(ModelImporter importer, string enumName)
    {
        System.Reflection.PropertyInfo property = typeof(ModelImporter).GetProperty("materialLocation");
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
            Debug.LogWarning("Could not set ModelImporter material location to " + enumName + ". Please confirm the Materials tab uses embedded materials: " + importer.assetPath);
        }
    }

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
            Debug.LogWarning("Texture source file is larger than 5 MB and should be optimized: " + line);
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

    // ValidateExternalDependencies / ValidateModelFoldersAreClean / WriteModelFolderFailureReport
    // 已迁移到 RetinarBatchModelBuilder.AssetResolution.cs。
    // 修复点：ValidateExternalDependencies 原先只要发现一个不在白名单目录下的依赖就整批打包终止，
    // 且报错信息里看不出这个依赖“本该”在哪、现在实际在哪——这正是“移动了文件就打包终止”排查困难的根源。
    // 新版本会先尝试自愈（把贴图/材质复制并重定向进该模型自己的 Art 目录），只有自愈也失败时才报错，
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

    private static string WriteSpatialPlacementFailureReport(string errorText)
    {
        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), DeliverableRoot, "_diagnostics");
        EnsureDiskDirectory(outputDir);
        string reportPath = Path.Combine(outputDir, "prefab_spatial_placement_failed.txt");
        File.WriteAllText(reportPath, errorText, new UTF8Encoding(false));
        return Path.GetFullPath(reportPath);
    }

    // WriteExternalDependencyFailureReport / IsApprovedRuntimeDependency
    // 已迁移到 RetinarBatchModelBuilder.AssetResolution.cs。

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

    private static void WriteAssetInfoWorkbook(GeneratedAsset asset, string outputPath)
    {
        outputPath = GetWritableWorkbookPath(outputPath);

        string templateDiskPath = Path.Combine(Directory.GetCurrentDirectory(), AssetInfoTemplatePath);
        if (File.Exists(templateDiskPath))
        {
            File.Copy(templateDiskPath, outputPath, true);
            UpdateTemplateWorkbook(outputPath, asset);
            return;
        }

        Debug.LogWarning("Asset info template not found. Falling back to generated workbook: " + AssetInfoTemplatePath);
        using (var stream = new FileStream(outputPath, FileMode.CreateNew))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            AddEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            AddEntry(archive, "_rels/.rels", BuildRootRelsXml());
            AddEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
            AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
            AddEntry(archive, "xl/styles.xml", BuildStylesXml());
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildSheetXml(BuildResourceInfoRows(asset), 4));
            AddEntry(archive, "xl/worksheets/sheet2.xml", BuildSheetXml(BuildAcceptanceRows(asset), 5));
            AddEntry(archive, "xl/worksheets/sheet3.xml", BuildSheetXml(BuildFileChecklistRows(asset), 5));
            AddEntry(archive, "docProps/core.xml", BuildCorePropsXml());
            AddEntry(archive, "docProps/app.xml", BuildAppPropsXml());
        }
    }

    private static void UpdateTemplateWorkbook(string workbookPath, GeneratedAsset asset)
    {
        using (var stream = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            UpdateWorksheetEntry(archive, "xl/worksheets/sheet1.xml", BuildCellMap(BuildResourceInfoRows(asset)));
            UpdateWorksheetEntry(archive, "xl/worksheets/sheet2.xml", BuildCellMap(BuildAcceptanceRows(asset)));
            UpdateWorksheetEntry(archive, "xl/worksheets/sheet3.xml", BuildCellMap(BuildFileChecklistRows(asset)));
        }
    }

    private static Dictionary<string, string> BuildCellMap(List<string[]> rows)
    {
        var cells = new Dictionary<string, string>();
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            string[] row = rows[rowIndex];
            for (int columnIndex = 0; columnIndex < row.Length; columnIndex++)
            {
                string value = row[columnIndex];
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                cells[ColumnName(columnIndex + 1) + (rowIndex + 1)] = value;
            }
        }

        return cells;
    }

    private static void UpdateWorksheetEntry(ZipArchive archive, string entryPath, Dictionary<string, string> values)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryPath);
        if (entry == null)
        {
            Debug.LogWarning("Worksheet entry not found in template: " + entryPath);
            return;
        }

        string xml;
        using (Stream stream = entry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            xml = reader.ReadToEnd();
        }

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument document = XDocument.Parse(xml);
        XElement sheetData = document.Root.Element(ns + "sheetData");
        if (sheetData == null)
        {
            Debug.LogWarning("sheetData not found in template worksheet: " + entryPath);
            return;
        }

        foreach (KeyValuePair<string, string> pair in values)
        {
            SetInlineStringCell(sheetData, ns, pair.Key, pair.Value);
        }

        entry.Delete();
        ZipArchiveEntry newEntry = archive.CreateEntry(entryPath);
        using (Stream stream = newEntry.Open())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            document.Save(writer, SaveOptions.DisableFormatting);
        }
    }

    private static void SetInlineStringCell(XElement sheetData, XNamespace ns, string cellReference, string value)
    {
        int rowNumber = GetRowNumber(cellReference);
        XElement row = sheetData.Elements(ns + "row")
            .FirstOrDefault(element => (int?)element.Attribute("r") == rowNumber);

        if (row == null)
        {
            row = new XElement(ns + "row", new XAttribute("r", rowNumber));
            sheetData.Add(row);
        }

        XElement cell = row.Elements(ns + "c")
            .FirstOrDefault(element => (string)element.Attribute("r") == cellReference);

        if (cell == null)
        {
            cell = new XElement(ns + "c", new XAttribute("r", cellReference));
            row.Add(cell);
        }

        XAttribute style = cell.Attribute("s");
        cell.RemoveAttributes();
        cell.Add(new XAttribute("r", cellReference));
        if (style != null)
        {
            cell.Add(new XAttribute("s", style.Value));
        }

        cell.Add(new XAttribute("t", "inlineStr"));
        cell.RemoveNodes();
        cell.Add(new XElement(ns + "is", new XElement(ns + "t", value)));
    }

    private static int GetRowNumber(string cellReference)
    {
        var digits = new string(cellReference.Where(char.IsDigit).ToArray());
        int.TryParse(digits, out int rowNumber);
        return rowNumber;
    }

    private static string GetWritableWorkbookPath(string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            return outputPath;
        }

        try
        {
            File.Delete(outputPath);
            return outputPath;
        }
        catch (IOException)
        {
            string directory = Path.GetDirectoryName(outputPath);
            string fileName = Path.GetFileNameWithoutExtension(outputPath);
            string extension = Path.GetExtension(outputPath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fallbackPath = Path.Combine(directory, fileName + "_" + timestamp + extension);
            Debug.LogWarning("asset_info.xlsx is open or locked. Wrote a timestamped copy instead: " + fallbackPath);
            return fallbackPath;
        }
        catch (System.UnauthorizedAccessException)
        {
            string directory = Path.GetDirectoryName(outputPath);
            string fileName = Path.GetFileNameWithoutExtension(outputPath);
            string extension = Path.GetExtension(outputPath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fallbackPath = Path.Combine(directory, fileName + "_" + timestamp + extension);
            Debug.LogWarning("asset_info.xlsx cannot be overwritten. Wrote a timestamped copy instead: " + fallbackPath);
            return fallbackPath;
        }
    }

    private static List<string[]> BuildResourceInfoRows(GeneratedAsset asset)
    {
        var rows = new List<string[]>();
        rows.Add(new[] { "航空航天素材资源信息表｜" + asset.AssetName + " v001", "", "", "" });
        rows.Add(new[] { "黄色单元格为制作人必须填写或核对的字段；未取得证据的项目明确标注为待补充/待测试。", "", "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "一、资源身份", "", "", "" });
        rows.Add(new[] { "asset_id", asset.AssetName.ToLowerInvariant(), "", "" });
        rows.Add(new[] { "中文标准名", asset.AssetName + " AR 教学模型", "", "" });
        rows.Add(new[] { "英文名", asset.AssetName + " AR asset", "", "" });
        rows.Add(new[] { "版本", "v001", "", "" });
        rows.Add(new[] { "分类", "航空航天 / 飞机 / 三维模型", "", "" });
        rows.Add(new[] { "标签", "aerospace; aircraft; AR; VR; education", "", "" });
        rows.Add(new[] { "制作人 / 指导老师", "待填写", "", "" });
        rows.Add(new[] { "技术审核 / 内容审核", "待填写", "", "" });
        rows.Add(new[] { "提交日期", System.DateTime.Now.ToString("yyyy-MM-dd"), "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "二、制作与运行环境", "", "", "" });
        rows.Add(new[] { "DCC 软件与版本", "FBX 已导入 Unity；原 DCC 软件与版本待制作方补充", "", "" });
        rows.Add(new[] { "Unity 版本", Application.unityVersion, "", "" });
        rows.Add(new[] { "渲染管线", "Built-in", "", "" });
        rows.Add(new[] { "PICO SDK / XR 插件", "当前工程未检测到专用 PICO SDK；加载端/插件版本待集成方确认", "", "" });
        rows.Add(new[] { "目标设备", "Android / PICO / 移动 AR 端，具体机型待项目指定", "", "" });
        rows.Add(new[] { "目标刷新率", "待真机测试；建议按 72 Hz 或项目目标刷新率验收", "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "三、模型与资源统计", "", "", "" });
        rows.Add(new[] { "指标", "LOD0", "LOD1", "LOD2" });
        rows.Add(new[] { "面数", asset.Stats.TriangleCount.ToString(), "待补充", "待补充" });
        rows.Add(new[] { "顶点", asset.Stats.VertexCount.ToString(), "待补充", "待补充" });
        rows.Add(new[] { "Renderer", asset.Stats.RendererCount.ToString(), "", "" });
        rows.Add(new[] { "材质", asset.Stats.MaterialCount.ToString(), "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "贴图数量 / 最大尺寸", asset.Stats.TextureSummary, "", "" });
        rows.Add(new[] { "Android 纹理压缩", "沿用 Unity 当前平台导入设置；如文字不清晰，优先检查贴图嵌入与原图分辨率", "", "" });
        rows.Add(new[] { "动画数量", "0 / 未检测到动画；如有机械演示需另行补充", "", "" });
        rows.Add(new[] { "Collider 类型与数量", "未自动生成 Collider；如需交互选取，建议补 Box/Capsule/低模 MeshCollider", "", "" });
        rows.Add(new[] { "Prefab 路径", asset.PrefabPath, "", "" });
        rows.Add(BlankRow(4));
        rows.Add(new[] { "四、版权、考据与已知问题", "", "", "" });
        rows.Add(new[] { "模型原创范围", "当前可确认：用户提供源模型并由工具生成 Unity prefab、材质副本与 AB；具体原创建模范围需制作方书面确认。", "", "" });
        rows.Add(new[] { "第三方内容", "包含用户提供的 FBX、贴图和材质；工程中未见独立授权文件，需补充作者、来源、授权协议或购买凭证。", "", "" });
        rows.Add(new[] { "授权范围", "待补充正式授权。建议先限定为高校教学/AR 展示项目内部使用；未获授权前不建议商用或公开二次分发。", "", "" });
        rows.Add(new[] { "关键参考", "参考来源需由制作方补充到 06_docs/references_copyright.md；当前表格不伪造版权来源。", "", "" });
        rows.Add(new[] { "已知问题", "已完成：FBX 导入、材质参数调整、SafeZone 尺寸归一、Prefab/AB/UnityPackage 流程。待补充：DCC 源文件、LOD、正式版权证明、真机性能记录、预览图/视频归档。", "", "" });
        return rows;
    }

    private static List<string[]> BuildAcceptanceRows(GeneratedAsset asset)
    {
        var rows = new List<string[]>();
        rows.Add(new[] { "PICO / Unity 性能验收记录", "", "", "", "" });
        rows.Add(new[] { "本页数值需来自目标设备真机；项目预算是起始门槛，最终以真实教学场景为准。", "", "", "", "" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "指标", "项目目标", "实测结果", "状态", "证据/备注" });
        rows.Add(new[] { "测试设备", "填写机型与系统版本", "待填写目标设备型号与系统版本", "待测试", "需在目标 Android/PICO/移动 AR 设备上记录" });
        rows.Add(new[] { "Unity / PICO SDK", "填写完整版本号", Application.unityVersion + " / Built-in", "已核对", "工程按 Built-in 处理" });
        rows.Add(new[] { "平均/最低帧率", "稳定达到项目刷新率；默认基线 72 FPS", "待真机测试", "待测试", "需记录平均/最低 FPS 与测试场景" });
        rows.Add(new[] { "可见面数", "常规教学场景建议 <= 300k", asset.Stats.TriangleCount.ToString(), "待复核", "由生成 prefab 统计，仍建议用 Unity/Profiler 复核" });
        rows.Add(new[] { "Draw Calls", "建议 <= 150", "待 Profiler 统计", "待测试", "材质 " + asset.Stats.MaterialCount + " 个，实际 Draw Calls 受合批/实例化影响" });
        rows.Add(new[] { "SetPass Calls", "建议 <= 100", "待 Profiler 统计", "待测试", "需真机或目标运行端 Profile" });
        rows.Add(new[] { "实时主方向光", "通常 <= 1", "Prefab 不强绑场景灯光", "通过", "运行端照明由场景控制" });
        rows.Add(new[] { "纹理", "常规单张 <= 2048；Android/PICO 优先 ASTC", asset.Stats.TextureSummary, "需复核", "如包体压力较大，可再做平台压缩策略" });
        rows.Add(new[] { "首次加载", "无明显长时间卡顿", "待测试", "待测试", "需记录首次加载耗时" });
        rows.Add(new[] { "测试时长/场景", "记录时长与同时可见资产", "待填写", "待测试", "建议记录测试时长、同时可见素材数量、截图/视频证据" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "完成度", "35%", "", "", "" });
        return rows;
    }

    private static List<string[]> BuildFileChecklistRows(GeneratedAsset asset)
    {
        var rows = new List<string[]>();
        rows.Add(new[] { "标准提交文件清单", "", "", "", "" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "序号", "目录/文件", "必需性", "状态", "说明" });
        rows.Add(new[] { "1", "01_source/DCC/<source>", "必须", "缺失", "未发现可编辑 DCC 源文件；需制作方补充" });
        rows.Add(new[] { "2", "01_source/Model/" + Path.GetFileName(asset.SourcePath), "必须", "已放置", "原始资源：" + asset.SourcePath });
        rows.Add(new[] { "3", "01_source/Model/" + asset.AssetName + "_lod1.fbx", "建议", "缺失", "正式提交建议补齐 LOD1" });
        rows.Add(new[] { "4", "01_source/Model/" + asset.AssetName + "_lod2.fbx", "建议", "缺失", "正式提交建议补齐 LOD2" });
        rows.Add(new[] { "5", "01_source/Textures/<source textures>", "必须", "需复核", "贴图数量/尺寸见资源信息页；来源授权需补充" });
        rows.Add(new[] { "6", "Assets/Art/" + asset.AssetName + "/Material/*.mat", "必须", "已生成", "保留制作方在 Unity 中调整后的材质；工具不再强制修改 Emission/Metallic/Smoothness" });
        rows.Add(new[] { "7", "02_unity/" + asset.AssetName + ".unitypackage", "必须", "已生成", "需在空工程回归导入验证" });
        rows.Add(new[] { "8", "03_assetbundles/Android/" + asset.BundleFileName, "必须", "已生成", "需确认文件为 MB 级且 manifest 包含 MeshRenderer/Texture 等类型，避免旧 1KB 空包" });
        rows.Add(new[] { "9", "03_assetbundles/iOS/" + asset.BundleFileName, "按需", "已生成", "按需提交；需目标平台验证" });
        rows.Add(new[] { "10", "04_Images/preview_*.png 或 jpg", "必须", "待补充", "建议补正面、侧面、俯视、细节、AR 真机预览" });
        rows.Add(new[] { "11", "05_video/demo_v001.mp4", "必须", "待补充", "建议补真机或 Unity 预览视频" });
        rows.Add(new[] { "12", "06_docs/asset_info.xlsx", "必须", "已生成", "本工作簿" });
        rows.Add(new[] { "13", "06_docs/CHANGELOG.md", "必须", "待补充", "建议记录每次打包、材质、贴图、位置调整" });
        rows.Add(new[] { "14", "06_docs/references_copyright.md", "必须", "待补充", "需补正式模型/贴图授权文件与参考来源清单" });
        rows.Add(new[] { "15", "06_docs/source_mapping.md", "必须", "待补充", "建议记录原始文件名、规范文件名、导入路径、AB 名称" });
        rows.Add(new[] { "16", "06_docs/acceptance_checklist.md", "必须", "待测试", "需真机性能数据、截图/视频证据和签字确认" });
        rows.Add(BlankRow(5));
        rows.Add(new[] { "必需项完成度", "50%", "", "", "" });
        return rows;
    }

    private static string[] BlankRow(int cols)
    {
        var row = new string[cols];
        for (int i = 0; i < cols; i++)
        {
            row[i] = "";
        }

        return row;
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using (Stream stream = entry.Open())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(content);
        }
    }

    private static string BuildSheetXml(List<string[]> rows, int columnCount)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        xml.Append("<sheetViews><sheetView showGridLines=\"0\" workbookViewId=\"0\"/></sheetViews>");
        xml.Append("<cols>");
        xml.Append("<col min=\"1\" max=\"1\" width=\"24\" customWidth=\"1\"/>");
        xml.Append("<col min=\"2\" max=\"").Append(columnCount).Append("\" width=\"24\" customWidth=\"1\"/>");
        xml.Append("</cols><sheetData>");

        var merges = new List<string>();
        for (int r = 0; r < rows.Count; r++)
        {
            string[] row = rows[r];
            bool titleRow = r == 0;
            bool noteRow = r == 1;
            bool sectionRow = IsSectionRow(row);
            bool headerRow = IsHeaderRow(row);
            int rowHeight = titleRow ? 28 : sectionRow ? 23 : noteRow ? 24 : 20;
            xml.Append("<row r=\"").Append(r + 1).Append("\" ht=\"").Append(rowHeight).Append("\" customHeight=\"1\">");
            for (int c = 0; c < row.Length; c++)
            {
                string value = row[c] ?? "";
                if (value.Length == 0)
                {
                    continue;
                }

                int style = GetCellStyle(row, r, c, titleRow, noteRow, sectionRow, headerRow);
                xml.Append("<c r=\"").Append(ColumnName(c + 1)).Append(r + 1).Append("\" t=\"inlineStr\" s=\"").Append(style).Append("\"><is><t>");
                xml.Append(XmlEscape(value));
                xml.Append("</t></is></c>");
            }
            xml.Append("</row>");

            if ((titleRow || noteRow || sectionRow) && columnCount > 1)
            {
                merges.Add("A" + (r + 1) + ":" + ColumnName(columnCount) + (r + 1));
            }
            else if (ShouldMergeValueRow(row) && columnCount > 2)
            {
                merges.Add("B" + (r + 1) + ":" + ColumnName(columnCount) + (r + 1));
            }
        }

        xml.Append("</sheetData>");
        if (merges.Count > 0)
        {
            xml.Append("<mergeCells count=\"").Append(merges.Count).Append("\">");
            foreach (string merge in merges)
            {
                xml.Append("<mergeCell ref=\"").Append(merge).Append("\"/>");
            }
            xml.Append("</mergeCells>");
        }

        xml.Append("</worksheet>");
        return xml.ToString();
    }

    private static bool IsSectionRow(string[] row)
    {
        return row.Length > 0 && (row[0].StartsWith("一、") || row[0].StartsWith("二、") || row[0].StartsWith("三、") || row[0].StartsWith("四、"));
    }

    private static bool IsHeaderRow(string[] row)
    {
        return row.Length > 1 && (row[0] == "指标" || row[0] == "序号");
    }

    private static bool ShouldMergeValueRow(string[] row)
    {
        if (row.Length < 3 || string.IsNullOrEmpty(row[0]) || string.IsNullOrEmpty(row[1]))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(row[2]))
        {
            return false;
        }

        return row[0] != "指标" && row[0] != "序号";
    }

    private static int GetCellStyle(string[] row, int rowIndex, int columnIndex, bool titleRow, bool noteRow, bool sectionRow, bool headerRow)
    {
        if (titleRow)
        {
            return 1;
        }

        if (sectionRow)
        {
            return 2;
        }

        if (noteRow)
        {
            return 3;
        }

        if (headerRow)
        {
            return 4;
        }

        if (columnIndex == 0)
        {
            return 5;
        }

        return 3;
    }

    private static string ColumnName(int index)
    {
        var name = "";
        while (index > 0)
        {
            int rem = (index - 1) % 26;
            name = (char)('A' + rem) + name;
            index = (index - rem - 1) / 26;
        }

        return name;
    }

    private static string BuildContentTypesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>" +
            "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>" +
            "</Types>";
    }

    private static string BuildRootRelsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
            "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
            "</Relationships>";
    }

    private static string BuildWorkbookXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets>" +
            "<sheet name=\"资源信息\" sheetId=\"1\" r:id=\"rId1\"/>" +
            "<sheet name=\"性能验收\" sheetId=\"2\" r:id=\"rId2\"/>" +
            "<sheet name=\"文件清单\" sheetId=\"3\" r:id=\"rId3\"/>" +
            "</sheets></workbook>";
    }

    private static string BuildWorkbookRelsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
            "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/>" +
            "<Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";
    }

    private static string BuildStylesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"16\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF16486B\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE9EFF5\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"2\"><border/><border><left style=\"thin\"><color rgb=\"FFD9E2EC\"/></left><right style=\"thin\"><color rgb=\"FFD9E2EC\"/></right><top style=\"thin\"><color rgb=\"FFD9E2EC\"/></top><bottom style=\"thin\"><color rgb=\"FFD9E2EC\"/></bottom></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"6\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf></cellXfs>" +
            "</styleSheet>";
    }

    private static string BuildCorePropsXml()
    {
        string now = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            "<dc:creator>Retinar Batch Builder</dc:creator><cp:lastModifiedBy>Retinar Batch Builder</cp:lastModifiedBy><dcterms:created xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:modified></cp:coreProperties>";
    }

    private static string BuildAppPropsXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><Application>Unity Retinar Batch Builder</Application></Properties>";
    }

    private static string XmlEscape(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

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
