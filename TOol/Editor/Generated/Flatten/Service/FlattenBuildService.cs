using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Generated / Flatten / Service
// 中文：④ 平铺执行层。产品只 Run(plan)；ctx → plan 只在 FromContext。
// 内核仍是 RetinarBatchModelBuilder（本目录 Service/）。
// =====================================================================================

/// <summary>④ 平铺服务。不引用步骤开关；开不开④由编排决定。公开口是 Run / FromContext。</summary>
public static class FlattenBuildService
{
    /// <summary>
    /// 冻结 plan → 现网七步。不改搬文件算法。不进菜单 FBX 直平铺入口。
    /// 管线④已走本口（步骤 2）。
    /// </summary>
    public static FlattenRowResult Run(FlattenPlan plan)
    {
        if (plan == null || string.IsNullOrEmpty(plan.SourcePrefabPath))
        {
            return FlattenRowResult.Failed(
                FlattenStep.Begin,
                "FlattenPlan 无效（缺 SourcePrefabPath）");
        }

        string sourcePrefabPath = plan.SourcePrefabPath;
        if (plan.ModelUnits == null || plan.ModelUnits.Count == 0)
        {
            plan.ModelUnits = BuildModelUnits(sourcePrefabPath, plan.PrimaryAssetPath);
        }

        for (int i = 0; i < plan.ModelUnits.Count; i++)
        {
            FlattenModelUnit unit = plan.ModelUnits[i];
            if (unit != null && unit.MissingReferences != null && unit.MissingReferences.Count > 0)
            {
                return FlattenRowResult.Failed(
                    FlattenStep.MissingSidecars,
                    "④ 模型缺必需伴生: " + unit.ModelPath + "（缺 " + unit.MissingReferences.Count +
                    "：" + string.Join(", ", unit.MissingReferences.ToArray()) + "）",
                    sourcePrefabPath);
            }
        }

        if (plan.MissingUris != null && plan.MissingUris.Count > 0)
        {
            return FlattenRowResult.Failed(
                FlattenStep.MissingSidecars,
                "④ 模型缺必需伴生: " +
                (string.IsNullOrEmpty(plan.PrimaryAssetPath) ? sourcePrefabPath : plan.PrimaryAssetPath) +
                "（缺 " + plan.MissingUris.Count + "）",
                sourcePrefabPath);
        }

        RetinarFlattenOptions options = CreateOptionsFromPlan(plan);
        RetinarBatchModelBuilder.ResetExtractTexturesInvokeCount();
        RetinarFlattenWork work;
        string beginError;
        if (!RetinarBatchModelBuilder.TryBeginPackagedFlatten(
                sourcePrefabPath, options, out work, out beginError))
        {
            return FlattenRowResult.Failed(
                FlattenStep.Begin,
                string.IsNullOrEmpty(beginError)
                    ? ("平铺 Begin 失败: " + sourcePrefabPath)
                    : beginError,
                sourcePrefabPath);
        }

        if (!RetinarBatchModelBuilder.FlattenSplitDependencies(work))
        {
            return WithFacts(
                FlattenRowResult.Failed(
                    FlattenStep.SplitDependencies,
                    "模型依赖复制失败: " + sourcePrefabPath,
                    sourcePrefabPath,
                    work != null ? work.PrefabPath : null),
                work);
        }

        if (plan.ApplyArtModelImporter)
        {
            RetinarBatchModelBuilder.FlattenApplyImportAndExtract(work);
        }

        RetinarBatchModelBuilder.FlattenRemap(work);
        RetinarBatchModelBuilder.FlattenCopyRendererMaterials(work);
        if (!RetinarBatchModelBuilder.TryFinishPackagedFlatten(work) ||
            work == null ||
            string.IsNullOrEmpty(work.PrefabPath))
        {
            return WithFacts(
                FlattenRowResult.Failed(
                    FlattenStep.Finish,
                    "平铺 Finish 失败: " + sourcePrefabPath,
                    sourcePrefabPath,
                    work != null ? work.PrefabPath : null),
                work);
        }

        return WithFacts(
            FlattenRowResult.Succeeded(sourcePrefabPath, work.PrefabPath),
            work);
    }

    /// <summary>plan → 内核 Options。不读 ctx。</summary>
    public static RetinarFlattenOptions CreateOptionsFromPlan(FlattenPlan plan)
    {
        plan = plan ?? new FlattenPlan();
        var options = new RetinarFlattenOptions();
        options.OperationPolicy = plan.OperationPolicy ??
                                  FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);
        options.ClearDestinationArtFolder = plan.ClearDestinationArtFolder;
        options.ConvertZUpToYUp = plan.ConvertZUpToYUp;
        options.AddBoxCollider = options.OperationPolicy.AddBoxCollider;
        options.StripMissingScripts = options.OperationPolicy.StripMissingScripts;
        options.ArtRoot = plan.ArtRoot;
        options.PrimaryAssetPath = plan.PrimaryAssetPath;
        if (plan.SidecarPaths != null) options.SidecarPaths = new List<string>(plan.SidecarPaths);
        if (plan.MissingUris != null) options.MissingUris = new List<string>(plan.MissingUris);

        if (plan.ModelUnits != null)
        {
            for (int i = 0; i < plan.ModelUnits.Count; i++)
            {
                FlattenModelUnit unit = plan.ModelUnits[i];
                if (unit == null) continue;
                options.ModelUnits.Add(new FlattenModelUnit
                {
                    ModelPath = unit.ModelPath,
                    Strategy = unit.Strategy,
                    SidecarPaths = unit.SidecarPaths != null
                        ? new List<string>(unit.SidecarPaths) : new List<string>(),
                    MissingReferences = unit.MissingReferences != null
                        ? new List<string>(unit.MissingReferences) : new List<string>()
                });
            }
        }

        if (options.ModelUnits.Count == 0 && plan.Branch == FlattenBranch.RelocateAtomic)
        {
            options.SkipDependencySplit = true;
        }

        return options;
    }

    /// <summary>编排把 ctx + 请求译成 plan。内核 Run 不再收 ctx。</summary>
    public static FlattenPlan FromContext(
        PipelineJobContext ctx,
        ToolFlattenRequest request,
        string sourcePrefabPath)
    {
        request = request ?? ToolFlattenRequest.MenuDefault;
        var plan = new FlattenPlan();
        plan.SourcePrefabPath = sourcePrefabPath;
        plan.OperationPolicy = request.OperationPolicy;
        plan.ClearDestinationArtFolder = request.ClearDestinationArtFolder;
        plan.ConvertZUpToYUp = request.ConvertZUpToYUp;
        plan.ArtRoot = request.ArtRoot;
        plan.Branch = ShouldRelocateAtomic(ctx)
            ? FlattenBranch.RelocateAtomic
            : FlattenBranch.SplitDependencies;
        plan.ModelUnits = BuildModelUnits(sourcePrefabPath, ctx != null ? ctx.PrimaryAssetPath : null);
        plan.ApplyArtModelImporter = ShouldApplyArtModelImporter(ctx);
        if (ctx != null)
        {
            plan.PrimaryAssetPath = ctx.PrimaryAssetPath;
            if (ctx.SidecarPaths != null && ctx.SidecarPaths.Count > 0)
            {
                plan.SidecarPaths = new List<string>(ctx.SidecarPaths);
            }

            if (ctx.MissingUris != null && ctx.MissingUris.Count > 0)
            {
                plan.MissingUris = new List<string>(ctx.MissingUris);
            }
        }

        return plan;
    }

    /// <summary>从 Prefab 的每个模型依赖生成策略；ctx 只补充直接模型入口。</summary>
    public static List<FlattenModelUnit> BuildModelUnits(string prefabPath, string primaryHint = null)
    {
        var paths = new List<string>();
        if (!string.IsNullOrEmpty(prefabPath))
        {
            string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            if (dependencies != null) paths.AddRange(dependencies);
        }

        if (!string.IsNullOrEmpty(primaryHint)) paths.Add(primaryHint);
        return BuildModelUnitsFromPaths(paths);
    }

    /// <summary>Unity 依赖定位模型；.bin 和外图由 glTF URI 扫描。</summary>
    public static List<FlattenModelUnit> BuildModelUnitsFromPaths(IList<string> paths)
    {
        var result = new List<FlattenModelUnit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (paths == null) return result;

        for (int i = 0; i < paths.Count; i++)
        {
            string path = (paths[i] ?? string.Empty).Replace("\\", "/");
            string extension = Path.GetExtension(path);
            if (!FlattenSidecarFacts.IsKernelModelExtension(extension) || !seen.Add(path)) continue;

            var unit = new FlattenModelUnit { ModelPath = path };
            string full = path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Directory.GetCurrentDirectory(), path) : path;
            ModelRelativeFileScan scan;
            if (ModelRelativeFileProbe.TryScan(full, out scan) && scan.HasRelativeFiles)
            {
                unit.Strategy = FlattenModelCopyStrategy.PreserveRelativeFiles;
                if (!scan.FileOk) unit.MissingReferences.Add("模型文件不可读取");
                unit.MissingReferences.AddRange(scan.MissingReferences);
                string project = Directory.GetCurrentDirectory().Replace("\\", "/").TrimEnd('/') + "/";
                for (int s = 0; s < scan.SidecarFullPaths.Count; s++)
                {
                    string sidecar = scan.SidecarFullPaths[s].Replace("\\", "/");
                    unit.SidecarPaths.Add(sidecar.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                        ? sidecar.Substring(project.Length) : sidecar);
                }
            }

            result.Add(unit);
        }

        result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.ModelPath, b.ModelPath));
        return result;
    }

    static bool ShouldRelocateAtomic(PipelineJobContext ctx)
    {
        return ctx != null && ctx.HasExternalUris;
    }

    /// <summary>
    /// Art 副本上的 ModelImporter 设置 + Extract 内嵌贴图。
    /// ScriptedImporter（gltf/glb）现网 Extract 本就会空转；ctx 为 null（菜单）时仍跑，与旧菜单一致。
    /// </summary>
    static bool ShouldApplyArtModelImporter(PipelineJobContext ctx)
    {
        // Prefab 自身是 Unknown；E 仍须逐个检查包内实际的 ModelImporter。
        // 单独导入的 ScriptedImporter 不进入 ModelImporter 阶段；Prefab 要检查其中的模型。
        return ctx == null || ctx.ImporterKind == PipelineImporterKind.ModelImporter ||
               (!ctx.HasExternalUris && string.Equals(
                   System.IO.Path.GetExtension(ctx.PrimaryAssetPath), ".prefab",
                   System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>2.5 探针是否发现 glTF 声明了不存在的必需伴生。</summary>
    public static bool HasMissingSidecars(PipelineJobContext ctx)
    {
        return ctx != null && ctx.MissingUris != null && ctx.MissingUris.Count > 0;
    }

    /// <summary>事实 + 请求 → 内核 Options。菜单可传 ctx=null + MenuDefault。</summary>
    public static RetinarFlattenOptions CreateOptions(
        PipelineJobContext ctx,
        ToolFlattenRequest request)
    {
        string sourcePrefabPath = ctx != null ? ctx.PrimaryAssetPath : null;
        return CreateOptionsFromPlan(FromContext(ctx, request, sourcePrefabPath));
    }

    static FlattenRowResult WithFacts(FlattenRowResult row, RetinarFlattenWork work)
    {
        if (row == null || work == null)
        {
            return row;
        }

        row.CopiedDependencyCount = work.CopiedDependencies != null ? work.CopiedDependencies.Count : 0;
        row.ExtractTexturesCallCount = RetinarBatchModelBuilder.ExtractTexturesInvokeCount;
        if (work.TextureIdentity != null)
            row.TextureIdentityWarnings.AddRange(work.TextureIdentity.Warnings);
        List<string> leftover = RetinarBatchModelBuilder.ListExternalFbmTextureDependencies(
            work.AssetFolder, work.PrefabPath);
        for (int i = 0; leftover != null && i < leftover.Count; i++)
        {
            row.LeftoverExternalFbm.Add(leftover[i]);
        }

        List<string> unbound = CollectUnboundTextureSlots(work.AssetFolder);
        for (int i = 0; i < unbound.Count; i++)
        {
            row.UnboundTextureSlots.Add(unbound[i]);
        }

        Debug.Log(
            "[Flatten] facts source=" + (work.SourcePath ?? row.SourcePrefabPath) +
            " copied=" + row.CopiedDependencyCount +
            " extractTextures=" + row.ExtractTexturesCallCount +
            " leftoverFbm=" + row.LeftoverExternalFbm.Count +
            " unboundSlots=" + row.UnboundTextureSlots.Count +
            " textureIdentityWarnings=" + row.TextureIdentityWarnings.Count);
        return row;
    }

    static List<string> CollectUnboundTextureSlots(string assetFolder)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(assetFolder))
        {
            return result;
        }

        string materialFolder = FlattenLayout.MaterialFolder(assetFolder);
        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            return result;
        }

        string unitPrefix = assetFolder.Replace("\\", "/") + "/";
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        for (int i = 0; guids != null && i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace("\\", "/");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                continue;
            }

            Texture main = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            if (main == null && material.HasProperty("_BaseMap"))
            {
                main = material.GetTexture("_BaseMap");
            }

            if (main == null)
            {
                result.Add(path + "._MainTex empty");
            }

            string[] props = material.GetTexturePropertyNames();
            for (int p = 0; props != null && p < props.Length; p++)
            {
                Texture tex = material.GetTexture(props[p]);
                if (tex == null)
                {
                    continue;
                }

                string texPath = AssetDatabase.GetAssetPath(tex).Replace("\\", "/");
                if (string.IsNullOrEmpty(texPath))
                {
                    result.Add(path + "." + props[p] + " embedded");
                    continue;
                }

                if (!texPath.StartsWith(unitPrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(path + "." + props[p] + " -> " + texPath);
                }
            }
        }

        return result;
    }
}
