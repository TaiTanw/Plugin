using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// =====================================================================================
// Legacy — 规范化平铺（暂不拆碎）
//
// 菜单入口已迁到 01_RetinarMenu.cs。请先读 Editor/README_EDITOR.md。
//
// 入口只剩两个：
//   RetinarFlattenApi.FlattenPaths  → 管线④（PipelineRunner 唯一调用方）
//   RetinarFlattenScheduler         → 菜单「平铺到交付中间区 Art（选中）」
//
// 2026-09-03（backlog D24-7）删除「【遗产】从 Art 规范化导出」与「成品直达」两条链：
//   一阶段：两条菜单 + 调度器 + DirectPackage + 出包前三道校验 + 30_Business 整层。
//   二阶段：交付物写盘子树 + AssetInfoWorkbook + CollectAssetStats +
//           无调用方的 SafeZone 副本 NormalizePreparedPrefabBounds。已删完。
// 出包与交付物统一走 ⑥ RetinarAbApi + RetinarDeliverableIo，本文件不再落盘任何交付物。
//
// partial 分文件：
//   RetinarBatchModelBuilder.cs                  主流程：选模型 -> 规范化 -> 平铺
//   RetinarBatchModelBuilder.AssetResolution.cs  源资产发现 + 自愈
// =====================================================================================
public static partial class RetinarBatchModelBuilder
{
    private const string ArtRoot = "Assets/Art";
    private const string AssetBundleVariant = "assetbundle";
    private const float SafeZonePadding = 0.8f;
    private const string FbxNormalizedModelChildSuffix = "_Model";
    private const float EmissionIntensity = 0.3f;
    private const float MetallicValue = 0.4f;
    private const float SmoothnessValue = 0.4f;

    private static readonly Vector3 SafeZoneCenter = new Vector3(0f, 0.15f, 0f);
    private static readonly Vector3 SafeZoneSize = Vector3.one;
    private static readonly Color EmissionColor = Color.white;

    // Z-up → Y-up。与 Unity 读到 FBX 头里 up-axis 时自己写的那个旋转一致。
    // OBJ 格式没有 up-axis 字段，Unity 一律当 Y-up 读，所以只能由人在绑定行上指定。
    private static readonly Quaternion ZUpToYUpRotation = Quaternion.Euler(-90f, 0f, 0f);

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

    /// <summary>由 RetinarFlattenScheduler / 菜单「批量汇总/平铺到 Art」调用。</summary>
    public static void FlattenSelectedToArt()
    {
        if (StopIfEditorIsPlaying())
        {
            return;
        }

        List<string> sourcePaths = GetSelectedModelPaths();
        if (sourcePaths.Count == 0)
        {
            ShowDialogDeferred(
                "Retinar",
                "请在 Project 中选中一个或多个 Prefab / FBX（可多选），再执行平铺。\n" +
                "本菜单只写入 Assets/Art/<名>/，不打 AssetBundle、不出 Deliverables。",
                "OK");
            return;
        }

        List<string> unknownLines;
        List<string> artPrefabPaths;
        int generatedCount = FlattenSourcePaths(sourcePaths, false, out unknownLines, out artPrefabPaths);

        string unknownHint = string.Empty;
        if (unknownLines != null && unknownLines.Count > 0)
        {
            unknownHint = "\n\n有 " + unknownLines.Count +
                " 个未归类文件（Unknown/ 或 image/Unknown/）。包不完整，但已继续平铺，不阻断。\n" +
                "可手拆后删除 Unknown 再导出。清单：\n" +
                BuildDialogPreview(string.Join("\n", unknownLines.ToArray()), 10);
        }

        ShowDialogDeferred(
            "Retinar 平铺到 Art",
            "完成。已平铺 " + generatedCount + " / " + sourcePaths.Count + " 个资产到 " + ArtRoot +
            unknownHint + "\n\n" +
            "下一步：用插件 2（资源处理）对 Art 下贴图按后缀递归压缩 / 刷顶点色，再执行「批量汇总 > 从 Art 导出（规范化）」。",
            "OK");
    }

    /// <summary>
    /// 按路径平铺到 Art。quiet=true 时不弹窗、不进度条确认阻塞以外的 Dialog。
    /// 供编排窄口调用。
    /// </summary>
    public static int FlattenSourcePaths(IList<string> sourcePaths, bool quiet)
    {
        List<string> unknownLines;
        List<string> artPrefabPaths;
        return FlattenSourcePaths(sourcePaths, quiet, RetinarFlattenOptions.Default, out unknownLines, out artPrefabPaths);
    }

    /// <summary>按路径平铺；返回成功数，并输出未归类清单与 Art Prefab 路径。</summary>
    public static int FlattenSourcePaths(
        IList<string> sourcePaths,
        bool quiet,
        out List<string> unknownLines,
        out List<string> artPrefabPaths)
    {
        return FlattenSourcePaths(sourcePaths, quiet, RetinarFlattenOptions.Default, out unknownLines, out artPrefabPaths);
    }

    /// <summary>带执行闸的平铺。菜单路径请传 Default。</summary>
    public static int FlattenSourcePaths(
        IList<string> sourcePaths,
        bool quiet,
        RetinarFlattenOptions flattenOptions,
        out List<string> unknownLines,
        out List<string> artPrefabPaths)
    {
        flattenOptions = flattenOptions ?? RetinarFlattenOptions.Default;
        unknownLines = new List<string>();
        artPrefabPaths = new List<string>();
        if (sourcePaths == null || sourcePaths.Count == 0)
        {
            if (!quiet)
            {
                ShowDialogDeferred("Retinar", "平铺路径列表为空。", "OK");
            }
            else
            {
                Debug.LogWarning("[Retinar] FlattenSourcePaths: 路径列表为空");
            }

            return 0;
        }

        if (StopIfEditorIsPlaying())
        {
            return 0;
        }

        EnsureAssetFolder(ArtRoot);

        int generatedCount = 0;
        try
        {
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                string sourcePath = sourcePaths[i];
                if (!quiet)
                {
                    EditorUtility.DisplayProgressBar(
                        "Retinar 平铺到 Art",
                        "平铺: " + sourcePath,
                        (float)i / sourcePaths.Count);
                }

                GeneratedAsset asset = CreateNormalizedPrefab(sourcePath, flattenOptions);
                if (asset.IsValid)
                {
                    generatedCount++;
                    if (!string.IsNullOrEmpty(asset.PrefabPath))
                    {
                        artPrefabPaths.Add(asset.PrefabPath);
                    }

                    List<string> unknowns = FlattenCopyRunner.CollectUnknownAssetPaths(asset.AssetFolder);
                    for (int u = 0; u < unknowns.Count; u++)
                    {
                        unknownLines.Add(asset.AssetName + "  " + unknowns[u]);
                    }
                }
            }
        }
        finally
        {
            if (!quiet)
            {
                EditorUtility.ClearProgressBar();
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (quiet)
        {
            Debug.Log("[Retinar] FlattenSourcePaths quiet: " + generatedCount + " / " + sourcePaths.Count +
                      " ArtPrefab=" + artPrefabPaths.Count);
        }

        return generatedCount;
    }

    public static bool ValidateFlattenSelectedToArt()
    {
        return !EditorApplication.isCompiling;
    }

    /// <summary>兼容旧调用；菜单入口已迁到 RetinarMenu → RetinarEditorUtil.OpenDeliverablesFolder。</summary>
    public static void OpenDeliverablesFolder()
    {
        RetinarEditorUtil.OpenDeliverablesFolder();
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
            "Retinar",
            "当前处于 Play Mode，已先退出。请回到 Edit Mode 后再执行菜单。",
            "OK");
        return true;
    }
    /// <summary>
    /// 模型选中得到路径
    /// </summary>
    /// <returns></returns>
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


    /// <summary>
    /// 弹窗里塞不下太长的文本，超过 maxLines 行就截断并说明还有多少条。
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

    private static void ApplyModelImportSettings(string sourcePath)
    {
        var importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        // 交付区口径只在 ModelImporterProfiles.ApplyArtDelivery。
        // Processor 对 Art 硬跳过，SaveAndReimport 不会再被导入区策略改回 External。
        ModelImporterProfiles.ApplyArtDelivery(importer, sourcePath);
        SaveAndReimportPreservingMeshVertexColors(importer);
    }

    private static GeneratedAsset CreateNormalizedPrefab(string sourcePath, RetinarFlattenOptions flattenOptions = null)
    {
        flattenOptions = flattenOptions ?? RetinarFlattenOptions.Default;
        if (Path.GetExtension(sourcePath).ToLowerInvariant() == ".prefab")
        {
            return CreatePackagedAdjustedPrefab(sourcePath, flattenOptions);
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogWarning("Could not load model asset: " + sourcePath);
            return GeneratedAsset.Invalid;
        }

        string assetName = MakeSafeName(Path.GetFileNameWithoutExtension(sourcePath));
        string assetFolder = ArtRoot + "/" + assetName;
        if (!TryClearArtUnitFolderIfRequested(assetFolder, sourcePath, flattenOptions))
        {
            return GeneratedAsset.Invalid;
        }

        string modelFolder = FlattenLayout.ModelFolder(assetFolder);
        string textureFolder = FlattenLayout.TextureFolder(assetFolder);
        string prefabFolder = FlattenLayout.PrefabFolder(assetFolder);
        string materialFolder = FlattenLayout.MaterialFolder(assetFolder);
        string animationFolder = FlattenLayout.AnimationFolder(assetFolder);
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
        model.name = assetName + FbxNormalizedModelChildSuffix;
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

        var generated = new GeneratedAsset(assetName, assetFolder, sourcePath, unityModelPath, prefabPath, bundleName + "." + AssetBundleVariant);
        List<string> healedPaths;
        if (TryHealExternalDependencies(generated, out healedPaths) && healedPaths.Count > 0)
        {
            Debug.Log("[Retinar] " + assetName + "：平铺结束自愈 " + healedPaths.Count + " 条：\n" +
                string.Join("\n", healedPaths.ToArray()));
        }

        FlattenCopyRunner.LogUnknownIfAny(assetFolder, assetName);
        FlattenAnimationClipRemapper.CopyAndRemapPrefabClips(prefabPath, assetFolder, assetName);
        RemapAllArtMaterialsToLocalTextures(assetFolder);
        return generated;
    }

    private static GeneratedAsset CreatePackagedAdjustedPrefab(string sourcePath, RetinarFlattenOptions flattenOptions = null)
    {
        RetinarFlattenWork work;
        if (!TryBeginPackagedFlatten(sourcePath, flattenOptions, out work))
        {
            return GeneratedAsset.Invalid;
        }

        bool relocated = work.Options != null && work.Options.SkipDependencySplit
            ? FlattenRelocateAtomic(work)
            : FlattenSplitDependencies(work);
        if (!relocated)
        {
            return GeneratedAsset.Invalid;
        }

        FlattenApplyImportAndExtract(work);
        FlattenRemap(work);
        FlattenCopyRendererMaterials(work);
        if (!TryFinishPackagedFlatten(work))
        {
            return GeneratedAsset.Invalid;
        }

        return ToGeneratedAsset(work);
    }

    /// <summary>0 清单元夹 + A 写 Art Prefab。管线④与菜单共用。</summary>
    public static bool TryBeginPackagedFlatten(string sourcePath, RetinarFlattenOptions flattenOptions, out RetinarFlattenWork work)
    {
        work = null;
        flattenOptions = flattenOptions ?? RetinarFlattenOptions.Default;
        if (string.IsNullOrEmpty(sourcePath) ||
            !string.Equals(Path.GetExtension(sourcePath), ".prefab", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[Retinar] TryBeginPackagedFlatten 只接受 Prefab: " + sourcePath);
            return false;
        }

        string sourceModelPath = FindMainModelDependency(sourcePath);
        if (string.IsNullOrEmpty(sourceModelPath))
        {
            GameObject probe = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (probe == null || probe.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                Debug.LogWarning(
                    "Selected prefab has no FBX/OBJ/GLB model dependency and no renderers: " + sourcePath);
                return false;
            }

            Debug.LogWarning(
                "[Retinar] Prefab 无独立模型文件依赖（无 .fbx/.obj/.glb），将仅按 Renderer/材质依赖平铺: " +
                sourcePath);
        }

        string assetName;
        string assetFolder;
        ResolvePackagedAssetIdentity(sourcePath, out assetName, out assetFolder);
        if (!TryClearArtUnitFolderIfRequested(assetFolder, sourcePath, flattenOptions))
        {
            return false;
        }

        string prefabFolder = FlattenLayout.PrefabFolder(assetFolder);
        EnsureStandardAssetFolders(assetFolder);
        FlattenReferenceAudit.LogSourcePrefabMissingReferences(sourcePath, assetName);

        string prefabPath = PreparePackagePrefab(sourcePath, prefabFolder, assetName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        work = new RetinarFlattenWork
        {
            SourcePath = sourcePath,
            SourceModelPath = sourceModelPath,
            AssetName = assetName,
            AssetFolder = assetFolder,
            PrefabPath = prefabPath,
            CopiedDependencies = new Dictionary<string, string>(),
            Options = flattenOptions
        };
        return true;
    }

    /// <summary>B 按后缀拆依赖 + OBJ .mtl 跟拷 + Model 伴生夹整理。</summary>
    public static bool FlattenSplitDependencies(RetinarFlattenWork work)
    {
        if (work == null || string.IsNullOrEmpty(work.PrefabPath))
        {
            return false;
        }

        work.CopiedDependencies = CopyAdjustedPrefabDependencies(work.PrefabPath, work.AssetFolder);
        CopyObjMaterialLibrariesBesideCopiedModels(work.CopiedDependencies);
        FlattenModelCompanionFolders(work.AssetFolder);
        FlattenCopyRunner.LogUnknownIfAny(work.AssetFolder, work.AssetName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return work.CopiedDependencies != null;
    }

    /// <summary>B′ 原子搬迁。不跑伴生夹整理。</summary>
    public static bool FlattenRelocateAtomic(RetinarFlattenWork work)
    {
        if (work == null || work.Options == null)
        {
            return false;
        }

        work.CopiedDependencies = RelocateAtomicPackage(
            work.AssetFolder, work.AssetName, work.Options, work.SourceModelPath);
        if (work.CopiedDependencies == null || work.CopiedDependencies.Count == 0)
        {
            Debug.LogError("[Retinar] B′ 原子搬迁未写入文件: " + work.SourcePath);
            return false;
        }

        CopyObjMaterialLibrariesBesideCopiedModels(work.CopiedDependencies);
        FlattenCopyRunner.LogUnknownIfAny(work.AssetFolder, work.AssetName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return true;
    }

    /// <summary>E1 导入设置 + E2 Extract 内嵌贴图并绑回。</summary>
    public static void FlattenApplyImportAndExtract(RetinarFlattenWork work)
    {
        if (work == null)
        {
            return;
        }

        ApplyImportSettingsToPackagedModels(work.AssetFolder);
        Debug.Log("[Retinar] " + work.AssetName + "：开始 ExtractTextures/重绑（打包流程第 1 次） -> " +
                  FlattenLayout.TextureFolder(work.AssetFolder));
        ExtractAndBindPackagedModelTextures(work.AssetFolder);
        RemapPackagedModelImporterMaterials(work.AssetFolder, work.CopiedDependencies);
    }

    /// <summary>D 重映射拷贝后的资产与 Prefab 模型引用。</summary>
    public static void FlattenRemap(RetinarFlattenWork work)
    {
        if (work == null)
        {
            return;
        }

        RemapCopiedAssetReferences(work.CopiedDependencies, work.AssetFolder);
        RemapCopiedPrefabModelReferences(work.PrefabPath, work.CopiedDependencies);
    }

    /// <summary>C 另存 Renderer 上的 .mat。</summary>
    public static void FlattenCopyRendererMaterials(RetinarFlattenWork work)
    {
        if (work == null)
        {
            return;
        }

        CopyPrefabRendererMaterials(work.PrefabPath, work.AssetFolder, work.AssetName);
    }

    /// <summary>自愈 / 空壳（含轴向）/ 碰撞盒 / 动画 / AB 名。</summary>
    public static bool TryFinishPackagedFlatten(RetinarFlattenWork work)
    {
        if (work == null || string.IsNullOrEmpty(work.PrefabPath))
        {
            return false;
        }

        var healTarget = new GeneratedAsset(
            work.AssetName, work.AssetFolder, work.SourcePath, string.Empty, work.PrefabPath, string.Empty);
        List<string> healedPaths;
        if (TryHealExternalDependencies(healTarget, out healedPaths) && healedPaths.Count > 0)
        {
            Debug.Log("[Retinar] " + work.AssetName + "：平铺结束自愈 " + healedPaths.Count + " 条：\n" +
                string.Join("\n", healedPaths.ToArray()));
        }

        List<string> leftoverFbm = CollectExternalFbmTextureDependencies(work.AssetFolder, work.PrefabPath);
        if (leftoverFbm.Count > 0)
        {
            Debug.LogWarning("[Retinar] " + work.AssetName + "：平铺结束后仍有 " + leftoverFbm.Count +
                " 条外部 .fbm 依赖（已无后续兜底，将原样进 AB）：\n" + string.Join("\n", leftoverFbm.ToArray()));
        }
        else
        {
            Debug.Log("[Retinar] " + work.AssetName + "：打包流程后未发现外部 .fbm 依赖");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        FlattenAnimationClipRemapper.CopyAndRemapPrefabClips(work.PrefabPath, work.AssetFolder, work.AssetName);
        if (RemapAllArtMaterialsToLocalTextures(work.AssetFolder))
        {
            Debug.Log("[Retinar] " + work.AssetName + "：动画重绑后再次收敛材质贴图到本包");
        }

        AssetDatabase.SaveAssets();

        bool convertZUp = work.Options != null && work.Options.ConvertZUpToYUp;
        WrapIncomingPrefabInEmptyShell(work.PrefabPath, work.AssetName, convertZUp);
        AddOrUpdateBoxColliderInPrefab(work.PrefabPath);
        NormalizePreparedPrefabAnimations(
            work.PrefabPath, FlattenLayout.AnimationFolder(work.AssetFolder), work.AssetName);

        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(work.PrefabPath);
        if (savedPrefab == null || savedPrefab.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            ClearBundleName(work.PrefabPath);
            Debug.LogError("Prepared prefab has no renderers and will not be bundled: " + work.PrefabPath);
            return false;
        }

        string bundleName = work.AssetName.ToLowerInvariant();
        AssetImporter prefabImporter = AssetImporter.GetAtPath(work.PrefabPath);
        prefabImporter.assetBundleName = bundleName;
        prefabImporter.assetBundleVariant = AssetBundleVariant;
        prefabImporter.SaveAndReimport();
        ClearDuplicateBundleNames(FlattenLayout.PrefabFolder(work.AssetFolder), work.PrefabPath, bundleName);
        return true;
    }

    private static GeneratedAsset ToGeneratedAsset(RetinarFlattenWork work)
    {
        string finalModelPath = FindMainModelDependency(work.PrefabPath);
        if (string.IsNullOrEmpty(finalModelPath) && !string.IsNullOrEmpty(work.SourceModelPath))
        {
            finalModelPath = FlattenLayout.ModelFolder(work.AssetFolder) + "/" + Path.GetFileName(work.SourceModelPath);
        }
        if (string.IsNullOrEmpty(finalModelPath))
        {
            finalModelPath = work.PrefabPath;
        }

        string bundleFileName = work.AssetName.ToLowerInvariant() + "." + AssetBundleVariant;
        return new GeneratedAsset(
            work.AssetName, work.AssetFolder, finalModelPath, finalModelPath, work.PrefabPath, bundleFileName);
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
    /// 管线④：只删本次 Art/&lt;名&gt;/。源已在该夹内则不清，避免删掉正在平铺的 Prefab。
    /// 不扫整棵 Assets/Art。菜单 Default 不调用。
    /// </summary>
    private static bool TryClearArtUnitFolderIfRequested(
        string assetFolder,
        string sourcePath,
        RetinarFlattenOptions flattenOptions)
    {
        if (flattenOptions == null || !flattenOptions.ClearDestinationArtFolder)
        {
            return true;
        }

        string folder = (assetFolder ?? string.Empty).Replace("\\", "/");
        while (folder.EndsWith("/", System.StringComparison.Ordinal) && folder.Length > 1)
        {
            folder = folder.Substring(0, folder.Length - 1);
        }

        string src = (sourcePath ?? string.Empty).Replace("\\", "/");
        if (!string.IsNullOrEmpty(src) &&
            (src.Equals(folder, System.StringComparison.OrdinalIgnoreCase) ||
             src.StartsWith(folder + "/", System.StringComparison.OrdinalIgnoreCase)))
        {
            Debug.LogWarning("[Retinar] 源已在本次 Art 单元内，跳过清夹: " + sourcePath);
            return true;
        }

        if (!AssetUnitFolder.TryDeleteImmediateChildFolder(ArtRoot, folder))
        {
            Debug.LogError("[Retinar] 无法清空 Art 单元夹: " + folder);
            return false;
        }

        return true;
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

            string targetFolder = FlattenCopyRunner.ResolveRelativeFolder(path);
            if (string.IsNullOrEmpty(targetFolder))
            {
                continue;
            }

            FlattenLayout.EnsureFolder(assetFolder + "/" + targetFolder);
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

    // OBJ 的 .mtl 不是 AssetDatabase 依赖，GetDependencies 拿不到；平铺分类表里也没有它
    // （ModelFlattenProcessor 只认 fbx/obj/glb/gltf）。所以必须在这里显式跟拷到副本旁边。
    // 少了它，Unity 会忽略 usemtl 改按 group 生成默认材质，交付副本的材质集和源对不上
    // （歼15 这份是 35 vs 111），MapSubAssetsBetweenCopies 判非同源，Material 这一类的
    // 引用改写整个跳过，交付副本的模型只剩白材质。
    private static void CopyObjMaterialLibrariesBesideCopiedModels(Dictionary<string, string> copiedDependencies)
    {
        if (copiedDependencies == null || copiedDependencies.Count == 0)
        {
            return;
        }

        bool copiedAny = false;
        foreach (KeyValuePair<string, string> pair in copiedDependencies)
        {
            if (!pair.Key.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string libraryName in ReadObjMaterialLibraryNames(pair.Key))
            {
                string sourceLibrary = GetSiblingAssetPath(pair.Key, libraryName);
                string targetLibrary = GetSiblingAssetPath(pair.Value, libraryName);
                if (string.IsNullOrEmpty(sourceLibrary) || string.IsNullOrEmpty(targetLibrary))
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<Object>(sourceLibrary) == null)
                {
                    Debug.LogWarning("[Retinar] OBJ 声明的 mtllib 不在源目录，交付副本材质将退化成按组的默认白材质: " +
                        sourceLibrary);
                    continue;
                }

                if (CopyAssetToExactPath(sourceLibrary, targetLibrary) != sourceLibrary)
                {
                    copiedAny = true;
                }
            }
        }

        if (copiedAny)
        {
            AssetDatabase.Refresh();
        }
    }

    // 只读到第一行几何数据为止：mtllib 按 OBJ 规范出现在几何之前，
    // 为一条声明整份读进上百 MB 的模型不划算。
    private static List<string> ReadObjMaterialLibraryNames(string objAssetPath)
    {
        var names = new List<string>();
        string fullPath = AssetPathToFullPath(objAssetPath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return names;
        }

        try
        {
            using (var reader = new StreamReader(fullPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("v ", System.StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (!trimmed.StartsWith("mtllib ", System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // 规范允许一条 mtllib 后跟多个库名，以空格分隔。
                    foreach (string name in trimmed.Substring("mtllib ".Length)
                                 .Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!names.Contains(name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Retinar] 读取 OBJ mtllib 失败: " + objAssetPath + " " + ex.Message);
        }

        return names;
    }

    private static string GetSiblingAssetPath(string assetPath, string fileName)
    {
        string folder = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        return folder.Replace("\\", "/") + "/" + fileName;
    }

    // GetPreparedPrefabDependencyFolder 已由 FlattenCopyRunner.ResolveRelativeFolder 取代。

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

        // 两遍流程：Art/Texture 可能已手动压小；导入区 .fbm 再导入后时间戳更新，
        // 不能仅凭“源更新”把更大的内嵌原图盖回已压缩副本。
        if (sourceInfo.Length > targetInfo.Length)
        {
            Debug.Log("[Retinar] SyncNewer 跳过（保留更小的 Art 贴图）: " + targetPath +
                "  Art=" + FormatBytes(targetInfo.Length) + "  源=" + FormatBytes(sourceInfo.Length) +
                "  源路径=" + sourcePath);
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
            if (IsModelAsset(copiedPath) || IsTextureAsset(copiedPath) || IsTextAsset(copiedPath))
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

        RemapAnimationClipsInAssetFolder(assetFolder, objectMap);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void RemapAnimationClipsInAssetFolder(
        string assetFolder,
        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        string animationFolder = FlattenLayout.AnimationFolder(assetFolder);
        if (!AssetDatabase.IsValidFolder(animationFolder) || objectMap == null || objectMap.Count == 0)
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { animationFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || Path.GetExtension(path).ToLowerInvariant() != ".anim")
            {
                continue;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int a = 0; a < assets.Length; a++)
            {
                if (assets[a] is AnimationClip)
                {
                    RemapSerializedObjectReferences(assets[a], objectMap);
                }
            }
        }
    }

    private static void CopyPrefabRendererMaterials(string prefabPath, string assetFolder, string assetName)
    {
        string materialFolder = FlattenLayout.MaterialFolder(assetFolder);
        string textureFolder = FlattenLayout.TextureFolder(assetFolder);
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

    // 把"源资产里的子对象"映射到"交付副本里的同一个子对象"，
    // 供后面改写预制体的 Mesh / 材质 / 贴图引用使用。
    //
    // 这里绝对不能按名字匹配。FBX 的节点名不保证唯一：3ds Max 里常按材质给物体命名，
    // Plane_Zhi18 的 fbx-all.FBX 就有 117 个节点却只有 88 个不同名字，
    // 其中 yy3d-zhi18-0012 重复 6 次、yy3d-zhi18-0003 重复 5 次。
    // Unity 导入时不会给重名的 Mesh 改名，于是"按名字 + FirstOrDefault"会让
    // 6 个不同的 Mesh 全部映射到副本里的第一个同名 Mesh，
    // 剩下 5 个节点就拿到了别人的几何体。因为这些节点带着镜像（负缩放）和
    // 高达 37 倍的非等比缩放，错配的表现是：主体外冒出碎片状物体、镜像节点绕序
    // 翻转导致破面。源模型直接拖进场景时是正确的，只有打包产物坏掉，非常难查。
    //
    // 正确做法：源文件和交付副本是同一份字节、同一套导入设置，导入结果是确定的，
    // 所以先按 localFileIdentifier 精确配对；万一副本的 ID 表没能保留（例如 .meta
    // 没跟着拷过去、Unity 重新生成了 ID），退化成"同类型内按序号配对"。
    // 两条路径都保证一对一，不会再出现多个源对象塌缩到同一个副本上。
    private static Dictionary<UnityEngine.Object, UnityEngine.Object> BuildCopiedObjectMap(Dictionary<string, string> copiedDependencies)
    {
        var objectMap = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        foreach (KeyValuePair<string, string> pair in copiedDependencies)
        {
            MapSubAssetsBetweenCopies(pair.Key, pair.Value, objectMap);
        }

        return objectMap;
    }

    private static void MapSubAssetsBetweenCopies(
        string sourcePath,
        string copyPath,
        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        List<UnityEngine.Object> originals = LoadSubAssetsForMapping(sourcePath);
        List<UnityEngine.Object> copies = LoadSubAssetsForMapping(copyPath);
        if (originals.Count == 0 || copies.Count == 0)
        {
            return;
        }

        foreach (IGrouping<System.Type, UnityEngine.Object> group in originals.GroupBy(item => item.GetType()))
        {
            List<UnityEngine.Object> originalsOfType = group.ToList();
            List<UnityEngine.Object> copiesOfType = copies.Where(item => item.GetType() == group.Key).ToList();

            // 数量对不上说明两边不是同一份资产（或者导入设置不一致），
            // 此时任何配对都是猜的。宁可不改写引用，也不要改错——
            // 引用没改写只会被后面的外部依赖校验拦下来报错，改错了却会静默产出坏几何体。
            if (originalsOfType.Count != copiesOfType.Count)
            {
                Debug.LogError(
                    "[Retinar] 源资产与交付副本的子对象数量不一致，已跳过这一类的引用改写，请检查两者是否同源：\n" +
                    "  类型: " + group.Key.Name + "\n" +
                    "  源路径: " + sourcePath + "（" + originalsOfType.Count + " 个）\n" +
                    "  副本路径: " + copyPath + "（" + copiesOfType.Count + " 个）");
                continue;
            }

            if (!TryPairByLocalFileIdentifier(originalsOfType, copiesOfType, objectMap))
            {
                PairByOrdinalIndex(originalsOfType, copiesOfType, objectMap);
            }
        }
    }

    // 只取需要改写引用的子对象。模型资产里的 GameObject 是导入器生成的层级节点，
    // 预制体引用的是它们的 Mesh 而不是节点本身，纳入映射没有意义还会干扰按序号配对。
    private static List<UnityEngine.Object> LoadSubAssetsForMapping(string assetPath)
    {
        var result = new List<UnityEngine.Object>();
        foreach (UnityEngine.Object candidate in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (candidate != null && !(candidate is GameObject) && !(candidate is Transform))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    // 首选路径：交付副本是连 .meta 一起拷过去的，Unity 会沿用同一套 localFileIdentifier，
    // 于是可以精确配对，与 LoadAllAssetsAtPath 的返回顺序无关。
    // 只有两边的 ID 集合完全一致时才认这条路径，否则交给按序号配对。
    private static bool TryPairByLocalFileIdentifier(
        List<UnityEngine.Object> originals,
        List<UnityEngine.Object> copies,
        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        var copiesById = new Dictionary<long, UnityEngine.Object>();
        foreach (UnityEngine.Object copy in copies)
        {
            if (!TryGetLocalFileIdentifier(copy, out long copyId) || copiesById.ContainsKey(copyId))
            {
                return false;
            }

            copiesById.Add(copyId, copy);
        }

        var pairs = new List<KeyValuePair<UnityEngine.Object, UnityEngine.Object>>();
        foreach (UnityEngine.Object original in originals)
        {
            if (!TryGetLocalFileIdentifier(original, out long originalId) ||
                !copiesById.TryGetValue(originalId, out UnityEngine.Object matched))
            {
                return false;
            }

            pairs.Add(new KeyValuePair<UnityEngine.Object, UnityEngine.Object>(original, matched));
        }

        foreach (KeyValuePair<UnityEngine.Object, UnityEngine.Object> entry in pairs)
        {
            if (!objectMap.ContainsKey(entry.Key))
            {
                objectMap.Add(entry.Key, entry.Value);
            }
        }

        return true;
    }

    // 退化路径：同一份字节、同一套导入设置，导入是确定性的，
    // 所以同类型子对象在两边的出现顺序一致，按序号配对即可，且天然一对一。
    // 名字不一致说明顺序假设不成立，此时报错并放弃这一类的改写。
    private static void PairByOrdinalIndex(
        List<UnityEngine.Object> originals,
        List<UnityEngine.Object> copies,
        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        for (int i = 0; i < originals.Count; i++)
        {
            if (originals[i].name != copies[i].name)
            {
                Debug.LogError(
                    "[Retinar] 源资产与交付副本的子对象顺序不一致，已跳过这一类的引用改写：\n" +
                    "  序号 " + i + " 源为 \"" + originals[i].name + "\"，副本为 \"" + copies[i].name + "\"");
                return;
            }
        }

        for (int i = 0; i < originals.Count; i++)
        {
            if (!objectMap.ContainsKey(originals[i]))
            {
                objectMap.Add(originals[i], copies[i]);
            }
        }
    }

    private static bool TryGetLocalFileIdentifier(UnityEngine.Object target, out long localId)
    {
        localId = 0L;
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string _, out localId);
    }

    private static void RemapCopiedMaterials(string assetFolder, Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
    {
        string materialFolder = FlattenLayout.MaterialFolder(assetFolder);
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
                    string expectedTexturePath = FlattenLayout.ResolveExistingOrDefaultTexturePath(
                        assetFolder, Path.GetFileName(sourceTexturePath));
                    if (AssetDatabase.LoadAssetAtPath<Texture>(expectedTexturePath) == null &&
                        IsTextureAsset(sourceTexturePath))
                    {
                        FlattenLayout.EnsureFolder(FlattenLayout.TextureFolder(assetFolder));
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
        string modelFolder = FlattenLayout.ModelFolder(assetFolder);
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
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".meta")
            {
                continue;
            }

            // .mtl 是 OBJ 的必需伴生，必须留在模型旁边（规则 20：Model 只放模型文件
            // **及其必需伴生**，禁的是自动生成的子文件夹）。搬走它等于让 Unity 忽略
            // usemtl，副本材质集立刻和源脱钩。
            if (extension == ".mtl")
            {
                continue;
            }

            string assetPath = FullPathToAssetPath(filePath);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(modelFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string targetFolder = FlattenCopyRunner.ResolveRelativeFolder(assetPath);
            if (string.IsNullOrEmpty(targetFolder))
            {
                continue;
            }

            FlattenLayout.EnsureFolder(assetFolder + "/" + targetFolder);
            string targetPath = assetFolder + "/" + targetFolder + "/" + Path.GetFileName(assetPath);
            MoveAssetToExactPath(assetPath, targetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        DeleteEmptySubfolders(modelFolder);
    }

    // GetModelCompanionTargetFolder 已由 FlattenCopyRunner.ResolveRelativeFolder 取代。
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

    /// <summary>
    /// 外来 Prefab：套一层空外壳（Identity），内容节点保持源 TRS / 名字 / Animator。
    /// 不缩放、不居中、不 Bake 子节点。已是外壳则不再套。禁止嵌套预制体资产。
    ///
    /// convertZUpToYUp 为真时，内容节点在源 TRS 之上左乘 −90°X（规则 23 的唯一例外，
    /// 外壳根仍 Identity）。开关来自绑定行，本方法不猜、不读文件。
    /// </summary>
    private static void WrapIncomingPrefabInEmptyShell(string prefabPath, string assetName, bool convertZUpToYUp)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (source == null)
        {
            return;
        }

        if (IsIncomingPrefabDeliveryShell(source))
        {
            Debug.Log("[Retinar] " + assetName + "：已是交付空外壳，跳过再套层");
            return;
        }

        GameObject content = Object.Instantiate(source);
        GameObject shell = null;
        try
        {
            content.name = source.name;
            if (PrefabUtility.GetPrefabInstanceStatus(content) != PrefabInstanceStatus.NotAPrefab)
            {
                PrefabUtility.UnpackPrefabInstance(content, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            shell = new GameObject(assetName);
            Vector3 localPos = content.transform.localPosition;
            Quaternion localRot = content.transform.localRotation;
            Vector3 localScale = content.transform.localScale;
            if (convertZUpToYUp)
            {
                localRot = ZUpToYUpRotation * localRot;
            }

            content.transform.SetParent(shell.transform, false);
            content.transform.localPosition = localPos;
            content.transform.localRotation = localRot;
            content.transform.localScale = localScale;
            shell.transform.position = Vector3.zero;
            shell.transform.rotation = Quaternion.identity;
            shell.transform.localScale = Vector3.one;

            PrefabUtility.SaveAsPrefabAsset(shell, prefabPath);
            Debug.Log("[Retinar] " + assetName + "：外来 Prefab 已套空父外壳，不缩放、不 Bake 子节点" +
                      (convertZUpToYUp ? "；内容节点已叠 −90°X 轴向修正" : string.Empty));
        }
        finally
        {
            if (shell != null)
            {
                Object.DestroyImmediate(shell);
            }
            else if (content != null)
            {
                Object.DestroyImmediate(content);
            }
        }
    }

    private static bool IsIncomingPrefabDeliveryShell(GameObject root)
    {
        if (root == null || root.transform.childCount != 1)
        {
            return false;
        }

        return root.GetComponent<Renderer>() == null &&
               root.GetComponent<Animator>() == null &&
               root.GetComponent<Animation>() == null &&
               root.GetComponent<MeshFilter>() == null &&
               root.GetComponent<Canvas>() == null;
    }

    /// <summary>
    /// 只按交付命名改 Clip / Controller 文件名。循环与否沿用源 Clip 的 Loop Time，不得按名字猜测或改写。
    /// </summary>
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
            bool loop = clip.isLooping;
            string clipPath = AssetDatabase.GetAssetPath(clip);
            if (!string.IsNullOrEmpty(clipPath) && clipPath.StartsWith(animationFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                string clipName = "Anim_" + assetName + "_" + CleanAnimationName(clip.name) + "_" + (loop ? "loop" : "once");
                AssetDatabase.RenameAsset(clipPath, clipName);
            }
        }

        AnimationClip primaryClip = clips.FirstOrDefault();
        string suffix = primaryClip != null && primaryClip.isLooping ? "loop" : "once";
        string controllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
        if (!string.IsNullOrEmpty(controllerPath) && controllerPath.StartsWith(animationFolder + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            AssetDatabase.RenameAsset(controllerPath, "Anim_" + assetName + "_" + CleanAnimationName(primaryClip != null ? primaryClip.name : "default") + "_" + suffix);
        }
    }

    private static string CleanAnimationName(string animationName)
    {
        string name = animationName.Replace("Anim_", "");
        name = name.Replace("_loop", "");
        name = name.Replace("_once", "");
        return MakeSafeName(name);
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

    private static void ApplyImportSettingsToPackagedModels(string assetFolder)
    {
        string modelFolder = FlattenLayout.ModelFolder(assetFolder);
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
        string modelFolder = FlattenLayout.ModelFolder(assetFolder);
        string materialFolder = FlattenLayout.MaterialFolder(assetFolder);
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
                SaveAndReimportPreservingMeshVertexColors(importer);
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

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "Unknown size";
        }

        return (bytes / 1024f / 1024f).ToString("0.00") + " MB";
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

    private static void AddOrUpdateBoxCollider(GameObject root)
    {
        if (!FlattenPostProcessSettings.AddBoxCollider)
        {
            return;
        }

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

    private static bool RemapMaterialTexturesToArtFolder(Material material, string textureFolder)
    {
        bool changed = false;
        foreach (string propertyName in material.GetTexturePropertyNames())
        {
            Texture texture = material.GetTexture(propertyName);
            if (texture == null)
            {
                continue;
            }

            string sourceTexturePath = AssetDatabase.GetAssetPath(texture).Replace("\\", "/");
            // 内嵌贴图（无路径）：跳过。
            // GLB/FBX/gltf 等容器路径：GetAssetPath(子资源贴图) 仍是容器文件；
            // 不得 CopyAsset 整包进 image/Texture（否则 Project 里像「贴图夹变成了模型」）。
            // 与 CreateMaterialCopyPreserveSettings 一致，只拷独立贴图后缀。
            if (string.IsNullOrEmpty(sourceTexturePath) || !IsTextureAsset(sourceTexturePath))
            {
                continue;
            }

            string assetFolder = FlattenLayout.AssetFolderFromTextureFolder(textureFolder);
            if (FlattenLayout.IsLocalArtTexture(assetFolder, sourceTexturePath))
            {
                // 已经在本包 image 单元 / 旧 Texture/ 里，跳过。
                continue;
            }

            string existingPath = FlattenLayout.ResolveExistingOrDefaultTexturePath(
                assetFolder, Path.GetFileName(sourceTexturePath));
            Texture copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(existingPath);
            if (copiedTexture == null)
            {
                string targetTexturePath = FlattenLayout.TextureFolder(assetFolder) + "/" +
                    Path.GetFileName(sourceTexturePath);
                FlattenLayout.EnsureFolder(FlattenLayout.TextureFolder(assetFolder));
                // 目标文件夹里没有副本——很可能是因为源贴图/伴生文件夹被移动过，
                // CollectSourceAssets 没找到它。这里做兜底：现在就地补一份副本，
                // 而不是让材质继续指向工程里的外部路径（那样后面校验会把整批打包判为失败）。
                string copiedPath = CopyAssetToExactPath(sourceTexturePath, targetTexturePath);
                copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(copiedPath);
                if (copiedTexture == null)
                {
                    Debug.LogWarning("Texture could not be relocated into " + FlattenLayout.TextureFolder(assetFolder) + ": " + sourceTexturePath +
                        "\n如果该贴图最近被人为移动过位置，请确认它仍然存在于工程内，或手动把它放回模型旁边的 Texture/Materials 文件夹后重新执行打包。");
                    continue;
                }
            }

            material.SetTexture(propertyName, copiedTexture);
            changed = true;
        }

        return changed;
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
        FlattenLayout.EnsureStandardFolders(assetFolder);
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
        public static readonly GeneratedAsset Invalid = new GeneratedAsset(null, null, null, null, null, null);

        public readonly string AssetName;
        public readonly string AssetFolder;
        public readonly string SourcePath;
        public readonly string UnityModelPath;
        public readonly string PrefabPath;
        public readonly string BundleFileName;

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(PrefabPath); }
        }

        public GeneratedAsset(string assetName, string assetFolder, string sourcePath, string unityModelPath, string prefabPath, string bundleFileName)
        {
            AssetName = assetName;
            AssetFolder = assetFolder;
            SourcePath = sourcePath;
            UnityModelPath = unityModelPath;
            PrefabPath = prefabPath;
            BundleFileName = bundleFileName;
        }
    }
}
