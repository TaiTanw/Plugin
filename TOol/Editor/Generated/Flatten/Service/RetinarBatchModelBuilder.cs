using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// ④ 平铺内核 — 七步实现与共用引用/路径工具
//
// 菜单入口已迁到 01_RetinarMenu.cs。请先读 Editor/README_EDITOR.md。
//
// 入口：
//   ToolFlattenApi.Run(plan)（管线 / 人工④）
//   步骤 12 已删除停用的路径入口、SafeZone 创建链及其专用辅助方法。
//   步骤 13：公开层不再转发七步；只由 FlattenBuildService.Run 调用本文件。
// 本文件是插件 2 Generated/Flatten 内核，不再给 Pipeline 直接调用。
//
// 2026-09-03（backlog D24-7）删除「【遗产】从 Art 规范化导出」与「成品直达」两条链：
//   一阶段：两条菜单 + 调度器 + DirectPackage + 出包前三道校验 + 30_Business 整层。
//   二阶段：交付物写盘子树 + AssetInfoWorkbook + CollectAssetStats +
//           无调用方的 SafeZone 副本 NormalizePreparedPrefabBounds。已删完。
// 出包与交付物统一走 ⑥ RetinarAbApi + RetinarDeliverableIo，本文件不再落盘任何交付物。
//
// partial 分文件：
//   RetinarBatchModelBuilder.cs                  七步实现 + 共用引用 / 路径工具
//   RetinarBatchModelBuilder.AssetResolution.cs  E 抽取 + 收尾引用整理
//   RetinarBatchModelBuilder.TextureBinding.cs   已知来源的贴图绑定
//   RetinarBatchModelBuilder.AtomicRelocate.cs   B′ 相对 URI 整树迁移
// =====================================================================================
public static partial class RetinarBatchModelBuilder
{
    /// <summary>本趟 <see cref="ModelImporter.ExtractTextures"/> 实际调用次数。Run(plan) 开头清零。</summary>
    public static int ExtractTexturesInvokeCount { get; private set; }

    public static void ResetExtractTexturesInvokeCount()
    {
        ExtractTexturesInvokeCount = 0;
    }

    private static string s_artRoot = FlattenBuildSettings.ArtRoot;

    private static string ArtRoot
    {
        get
        {
            return string.IsNullOrEmpty(s_artRoot) ? FlattenBuildSettings.ArtRoot : s_artRoot;
        }
    }

    // Z-up → Y-up。与 Unity 读到 FBX 头里 up-axis 时自己写的那个旋转一致。
    // OBJ 格式没有 up-axis 字段，Unity 一律当 Y-up 读，所以只能由人在绑定行上指定。
    private static readonly Quaternion ZUpToYUpRotation = Quaternion.Euler(-90f, 0f, 0f);

    public static bool ValidateFlattenSelectedToArt()
    {
        return !EditorApplication.isCompiling;
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

    /// <summary>Begin：剥/拒 Missing 后再清本次 Art 单元并写 Prefab。</summary>
    public static bool TryBeginPackagedFlatten(
        string sourcePath,
        RetinarFlattenOptions flattenOptions,
        out RetinarFlattenWork work)
    {
        string unused;
        return TryBeginPackagedFlatten(sourcePath, flattenOptions, out work, out unused);
    }

    /// <summary>
    /// 先在内存剥/拒 Missing Script，通过后再清本次 Art 单元并写入 Prefab。
    /// 管线④与菜单共用。
    /// </summary>
    public static bool TryBeginPackagedFlatten(
        string sourcePath,
        RetinarFlattenOptions flattenOptions,
        out RetinarFlattenWork work,
        out string error)
    {
        work = null;
        error = null;
        flattenOptions = flattenOptions ?? RetinarFlattenOptions.Default;
        s_artRoot = FlattenArtPaths.ResolveOverride(flattenOptions.ArtRoot);
        if (string.IsNullOrEmpty(sourcePath) ||
            !string.Equals(Path.GetExtension(sourcePath), ".prefab", System.StringComparison.OrdinalIgnoreCase))
        {
            error = "[Retinar] TryBeginPackagedFlatten 只接受 Prefab: " + sourcePath;
            Debug.LogWarning(error);
            return false;
        }

        string sourceModelPath = FindMainModelDependency(sourcePath);
        if (string.IsNullOrEmpty(sourceModelPath))
        {
            GameObject probe = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (probe == null || probe.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                error = "Selected prefab has no FBX/OBJ/GLB model dependency and no renderers: " + sourcePath;
                Debug.LogWarning(error);
                return false;
            }

            Debug.LogWarning(
                "[Retinar] Prefab 无独立模型文件依赖（无 .fbx/.obj/.glb），将仅按 Renderer/材质依赖平铺: " +
                sourcePath);
        }

        string assetName;
        string assetFolder;
        ResolvePackagedAssetIdentity(sourcePath, out assetName, out assetFolder);
        // B′ 保持相对 URI 整树规则；仅普通平铺建立迁移前贴图身份。
        FlattenTextureIdentity textureIdentity = flattenOptions.SkipDependencySplit
            ? null : FlattenTextureIdentity.Capture(sourcePath, assetFolder);

        string prefabFolder = FlattenLayout.PrefabFolder(assetFolder);
        string normalizedSource = sourcePath.Replace("\\", "/");
        bool inPlace = normalizedSource.StartsWith(
            prefabFolder + "/", System.StringComparison.OrdinalIgnoreCase);
        string prefabPath = inPlace
            ? normalizedSource
            : prefabFolder + "/" + assetName + ".prefab";

        GameObject instance = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            UnpackNestedPrefabInstances(instance);
            if (!ApplyMissingScriptPolicy(
                    instance, sourcePath, flattenOptions.StripMissingScripts, out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = FlattenReferenceAudit.FormatMissingScriptRefusal(sourcePath, null);
                }

                Debug.LogError(error);
                return false;
            }

            if (!TryClearArtUnitFolderIfRequested(assetFolder, sourcePath, flattenOptions))
            {
                error = "无法清空本次 Art 单元夹: " + assetFolder;
                return false;
            }

            EnsureStandardAssetFolders(assetFolder);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            if (saved == null)
            {
                error = "Failed to create package prefab copy: " + prefabPath;
                Debug.LogWarning(error);
                return false;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }

        if (string.IsNullOrEmpty(prefabPath) ||
            !prefabPath.StartsWith(prefabFolder + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            error = "[Retinar] Begin 拒绝继续：未生成目标 Art Prefab。源=" + sourcePath +
                    " 目标夹=" + prefabFolder;
            Debug.LogError(error);
            return false;
        }

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
            Options = flattenOptions,
            TextureIdentity = textureIdentity
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

        work.CopiedDependencies = CopyAdjustedPrefabDependencies(
            work.PrefabPath, work.AssetFolder, work.Options.OperationPolicy, work.TextureIdentity);
        work.TextureIdentity?.RegisterCopies(work.CopiedDependencies);
        CopyObjMaterialLibrariesBesideCopiedModels(work.CopiedDependencies);
        FlattenModelCompanionFolders(work.AssetFolder, work.Options.OperationPolicy, work.TextureIdentity);
        FlattenCopyRunner.LogUnknownIfAny(work.AssetFolder, work.AssetName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        work.TextureIdentity?.TrustMatchingUnitTextures();
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
        if (work.CopiedDependencies == null)
        {
            Debug.LogError("[Retinar] B′ 原子搬迁未完整写入必需文件: " + work.SourcePath);
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
        Debug.Log("[Retinar] " + work.AssetName + "：开始 ExtractTextures/重绑（E，本趟唯一 Extract 口） -> " +
                  FlattenLayout.TextureFolder(work.AssetFolder));
        ExtractAndBindPackagedModelTextures(work.AssetFolder, work.Options.OperationPolicy, work.TextureIdentity);
        RemapPackagedModelImporterMaterials(work.AssetFolder, work.CopiedDependencies);
    }

    /// <summary>D 重映射拷贝后的资产与 Prefab 模型引用。</summary>
    public static void FlattenRemap(RetinarFlattenWork work)
    {
        if (work == null)
        {
            return;
        }

        RemapCopiedAssetReferences(work.CopiedDependencies, work.AssetFolder, work.TextureIdentity);
        RemapCopiedPrefabModelReferences(work.PrefabPath, work.CopiedDependencies, work.TextureIdentity);
    }

    /// <summary>C 另存 Renderer 上的 .mat。</summary>
    public static void FlattenCopyRendererMaterials(RetinarFlattenWork work)
    {
        if (work == null)
        {
            return;
        }

        CopyPrefabRendererMaterials(work.PrefabPath, work.AssetFolder, work.AssetName, work.TextureIdentity);
    }

    /// <summary>自愈 / 空壳（含轴向）/ 碰撞盒 / 动画。不写 Prefab Importer 的 AB 标签。</summary>
    public static bool TryFinishPackagedFlatten(RetinarFlattenWork work)
    {
        if (work == null || string.IsNullOrEmpty(work.PrefabPath))
        {
            return false;
        }

        var healTarget = new GeneratedAsset(
            work.AssetName, work.AssetFolder, work.PrefabPath);
        List<string> healedPaths;
        if (TryHealExternalDependencies(
                healTarget, work.Options.OperationPolicy, out healedPaths, work.TextureIdentity) && healedPaths.Count > 0)
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
        FlattenAnimationClipRemapper.CopyAndRemapPrefabClips(
            work.PrefabPath, work.AssetFolder, work.AssetName, work.Options.OperationPolicy);
        if (RemapAllArtMaterialsToLocalTextures(work.AssetFolder, work.TextureIdentity))
        {
            Debug.Log("[Retinar] " + work.AssetName + "：动画重绑后再次收敛材质贴图到本包");
        }

        AssetDatabase.SaveAssets();

        bool convertZUp = work.Options != null && work.Options.ConvertZUpToYUp;
        WrapIncomingPrefabInEmptyShell(work.PrefabPath, work.AssetName, convertZUp);
        AddOrUpdateBoxColliderInPrefab(work.PrefabPath, work.Options.AddBoxCollider);
        NormalizePreparedPrefabAnimations(
            work.PrefabPath, FlattenLayout.AnimationFolder(work.AssetFolder), work.AssetName);

        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(work.PrefabPath);
        if (savedPrefab == null || savedPrefab.GetComponentsInChildren<Renderer>(true).Length == 0)
        {
            Debug.LogError("Prepared prefab has no renderers and will not be bundled: " + work.PrefabPath);
            return false;
        }

        return true;
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

        // Begin 此时仍持有 LoadPrefabContents，禁止 Refresh。
        if (!AssetUnitFolder.TryDeleteImmediateChildFolder(ArtRoot, folder, false))
        {
            Debug.LogError("[Retinar] 无法清空 Art 单元夹: " + folder);
            return false;
        }

        return true;
    }

    private static bool ApplyMissingScriptPolicy(
        GameObject instance,
        string sourcePath,
        bool stripMissingScripts,
        out string error)
    {
        error = null;
        List<string> scripts = FlattenReferenceAudit.ListMissingScripts(instance);
        FlattenReferenceAudit.LogObjectSlotMisses(instance, sourcePath);
        if (scripts.Count == 0)
        {
            return true;
        }

        if (!stripMissingScripts)
        {
            error = FlattenReferenceAudit.FormatMissingScriptRefusal(sourcePath, scripts);
            return false;
        }

        int stripped = FlattenReferenceAudit.StripMissingScripts(instance);
        Debug.LogWarning(
            "[Retinar] 已剥 Missing Script × " + stripped +
            "，写入 Art 副本（源 Prefab 不改）。源=" + sourcePath);
        List<string> remain = FlattenReferenceAudit.ListMissingScripts(instance);
        if (remain.Count > 0)
        {
            error = FlattenReferenceAudit.FormatMissingScriptRefusal(sourcePath, remain);
            return false;
        }

        return true;
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

    private static Dictionary<string, string> CopyAdjustedPrefabDependencies(
        string prefabPath,
        string assetFolder,
        FlattenOperationPolicy operationPolicy,
        FlattenTextureIdentity identity = null)
    {
        var copied = new Dictionary<string, string>();
        foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
        {
            string path = dependency.Replace("\\", "/");
            if (identity != null && FlattenTextureIdentity.IsTextureFile(path) && !identity.IsOriginalSource(path))
            {
                identity.Warn("不是迁移前引用的贴图，跳过补拷", prefabPath, path);
                continue;
            }
            if (path == prefabPath || path.StartsWith(assetFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string requestedTargetPath = FlattenCopyRunner.ResolveDestAssetPath(assetFolder, path, operationPolicy);
            if (string.IsNullOrEmpty(requestedTargetPath))
            {
                continue;
            }

            FlattenLayout.EnsureFolder(Path.GetDirectoryName(requestedTargetPath).Replace("\\", "/"));
            string copiedPath = CopyAssetToExactPath(path, requestedTargetPath, identity);
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

    private static string CopyAssetToExactPath(string sourcePath, string requestedDestinationPath, FlattenTextureIdentity identity = null)
    {
        sourcePath = sourcePath.Replace("\\", "/");
        requestedDestinationPath = requestedDestinationPath.Replace("\\", "/");

        if (sourcePath.Equals(requestedDestinationPath, System.StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        // 同名已占坑则不覆盖（Texture/ 与 .fbm 抢同一文件名时先到先得）。
        if (AssetDatabase.LoadAssetAtPath<Object>(requestedDestinationPath) != null)
        {
            return requestedDestinationPath;
        }

        var importBefore = identity?.SnapshotModelImport(sourcePath, requestedDestinationPath);
        if (!AssetDatabase.CopyAsset(sourcePath, requestedDestinationPath))
        {
            Debug.LogWarning("Failed to copy asset: " + sourcePath + " -> " + requestedDestinationPath);
            return sourcePath;
        }

        if (importBefore != null)
        {
            // 新生成 .fbm 可能已落盘但尚未注册，先同步导入再记录其 GUID。
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            identity.RegisterModelImport(sourcePath, requestedDestinationPath, importBefore);
        }

        return requestedDestinationPath;
    }

    private static void RemapCopiedPrefabModelReferences(string prefabPath, Dictionary<string, string> copiedDependencies, FlattenTextureIdentity identity = null)
    {
        if (copiedDependencies == null || copiedDependencies.Count == 0)
        {
            return;
        }

        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap = BuildCopiedObjectMap(copiedDependencies, identity);

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
        string assetFolder,
        FlattenTextureIdentity identity = null)
    {
        Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap =
            copiedDependencies != null && copiedDependencies.Count > 0
                ? BuildCopiedObjectMap(copiedDependencies, identity)
                : new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        RemapCopiedMaterials(assetFolder, objectMap, identity);

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

    private static void CopyPrefabRendererMaterials(string prefabPath, string assetFolder, string assetName, FlattenTextureIdentity identity = null)
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
                        copiedMaterial = CreateMaterialCopyPreserveSettings(sourceMaterial, materialFolder, textureFolder, assetName, materialMap.Count + 1, identity);
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

    private static Material CreateMaterialCopyPreserveSettings(Material source, string materialFolder, string textureFolder, string assetName, int index, FlattenTextureIdentity identity = null)
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

            if (identity != null)
            {
                BindKnownTexture(material, propertyName, texture, identity);
                continue;
            }
            string copiedTexturePath = CopyAssetToExactPath(texturePath, textureFolder + "/" + Path.GetFileName(texturePath));
            Texture copiedTexture = AssetDatabase.LoadAssetAtPath<Texture>(copiedTexturePath);
            if (copiedTexture != null)
            {
                material.SetTexture(propertyName, copiedTexture);
            }
        }

        NormalizeDeliverableShaderOperation.TryApplyFadeIfMainTexMarksTransparency(material);
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
    private static Dictionary<UnityEngine.Object, UnityEngine.Object> BuildCopiedObjectMap(Dictionary<string, string> copiedDependencies, FlattenTextureIdentity identity = null)
    {
        var objectMap = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        foreach (KeyValuePair<string, string> pair in copiedDependencies)
        {
            MapSubAssetsBetweenCopies(pair.Key, pair.Value, objectMap);
        }

        if (identity != null)
        {
            foreach (var pair in objectMap.ToArray())
            {
                if (!(pair.Key is Texture) || !FlattenTextureIdentity.IsTextureFile(AssetDatabase.GetAssetPath(pair.Key))) continue;
                string target = identity.ResolveExact(AssetDatabase.GetAssetPath(pair.Key));
                if (!string.Equals(target, AssetDatabase.GetAssetPath(pair.Value), System.StringComparison.OrdinalIgnoreCase))
                    objectMap.Remove(pair.Key);
            }
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

    private static void RemapCopiedMaterials(string assetFolder, Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap, FlattenTextureIdentity identity = null)
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

                if (identity != null && FlattenTextureIdentity.IsTextureFile(AssetDatabase.GetAssetPath(texture)))
                {
                    BindKnownTexture(material, propertyName, texture, identity);
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

    private static void FlattenModelCompanionFolders(
        string assetFolder,
        FlattenOperationPolicy operationPolicy,
        FlattenTextureIdentity identity = null)
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

        string textureFolder = FlattenLayout.TextureFolder(assetFolder);
        var destBefore = new Dictionary<string, Dictionary<string, string>>(System.StringComparer.OrdinalIgnoreCase);
        if (identity != null)
        {
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelFolder });
            for (int i = 0; i < modelGuids.Length; i++)
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[i]).Replace("\\", "/");
                if (!IsModelAsset(modelPath) ||
                    !modelPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                destBefore[modelPath] = identity.SnapshotExtractFolder(
                    FlattenTextureIdentity.DestFbmFolder(modelPath, textureFolder));
            }
        }

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

            string targetPath = FlattenCopyRunner.ResolveDestAssetPath(assetFolder, assetPath, operationPolicy);
            if (string.IsNullOrEmpty(targetPath))
            {
                continue;
            }

            FlattenLayout.EnsureFolder(Path.GetDirectoryName(targetPath).Replace("\\", "/"));
            MoveAssetToExactPath(assetPath, targetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        DeleteEmptySubfolders(modelFolder);

        if (identity != null)
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> pair in destBefore)
            {
                identity.RegisterExtracted(
                    pair.Key,
                    FlattenTextureIdentity.DestFbmFolder(pair.Key, textureFolder),
                    pair.Value);
            }
        }
    }

    // GetModelCompanionTargetFolder 已由 FlattenCopyRunner.ResolveDestAssetPath 取代。
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

    private static void AddOrUpdateBoxColliderInPrefab(string prefabPath, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            AddOrUpdateBoxCollider(instance, true);
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

    private static void AddOrUpdateBoxCollider(GameObject root, bool enabled)
    {
        if (!enabled)
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

    private static bool RemapMaterialTexturesToArtFolder(Material material, string textureFolder, FlattenTextureIdentity identity = null)
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

            if (identity != null)
            {
                changed |= BindKnownTexture(material, propertyName, texture, identity);
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
                // B′ 未采用步骤 11 的普通平铺身份账本，此处保留其原有补拷行为，
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

    // 收尾引用整理使用的最小目标；不再承载旧导出链的 AB 名和源模型字段。
    private struct GeneratedAsset
    {
        public readonly string AssetName;
        public readonly string AssetFolder;
        public readonly string PrefabPath;

        public GeneratedAsset(string assetName, string assetFolder, string prefabPath)
        {
            AssetName = assetName;
            AssetFolder = assetFolder;
            PrefabPath = prefabPath;
        }
    }
}
