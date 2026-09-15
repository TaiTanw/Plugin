using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Service
// 中文：④ 平铺执行层。步骤 1 起内核可 Run(plan)；现网分步窄口仍可读 ctx。
// 内核仍是 RetinarBatchModelBuilder（本目录 Service/）。
// =====================================================================================

/// <summary>④ 平铺服务。不引用步骤开关；开不开④由编排决定。</summary>
public static class FlattenBuildService
{
    /// <summary>
    /// 冻结 plan → 现网七步。不改搬文件算法。管线④已走本口（步骤 2）。
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
        if (plan.MissingUris != null && plan.MissingUris.Count > 0)
        {
            return FlattenRowResult.Failed(
                FlattenStep.MissingSidecars,
                "④ glTF 缺必需伴生，无法执行 B′: " +
                (string.IsNullOrEmpty(plan.PrimaryAssetPath) ? sourcePrefabPath : plan.PrimaryAssetPath) +
                "（缺 " + plan.MissingUris.Count + "）",
                sourcePrefabPath);
        }

        RetinarFlattenOptions options = CreateOptionsFromPlan(plan);
        RetinarFlattenWork work;
        if (!RetinarBatchModelBuilder.TryBeginPackagedFlatten(sourcePrefabPath, options, out work))
        {
            return FlattenRowResult.Failed(FlattenStep.Begin, "平铺 Begin 失败: " + sourcePrefabPath, sourcePrefabPath);
        }

        if (plan.Branch == FlattenBranch.RelocateAtomic)
        {
            if (!RetinarBatchModelBuilder.FlattenRelocateAtomic(work))
            {
                return FlattenRowResult.Failed(
                    FlattenStep.RelocateAtomic,
                    "B′ 原子搬迁失败: " + sourcePrefabPath,
                    sourcePrefabPath,
                    work != null ? work.PrefabPath : null);
            }
        }
        else if (!RetinarBatchModelBuilder.FlattenSplitDependencies(work))
        {
            return FlattenRowResult.Failed(
                FlattenStep.SplitDependencies,
                "B 拆依赖失败: " + sourcePrefabPath,
                sourcePrefabPath,
                work != null ? work.PrefabPath : null);
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
            return FlattenRowResult.Failed(
                FlattenStep.Finish,
                "平铺 Finish 失败: " + sourcePrefabPath,
                sourcePrefabPath,
                work != null ? work.PrefabPath : null);
        }

        return FlattenRowResult.Succeeded(sourcePrefabPath, work.PrefabPath);
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

        if (plan.Branch == FlattenBranch.RelocateAtomic)
        {
            options.SkipDependencySplit = true;
            options.PrimaryAssetPath = plan.PrimaryAssetPath;
            if (plan.SidecarPaths != null && plan.SidecarPaths.Count > 0)
            {
                options.SidecarPaths = new List<string>(plan.SidecarPaths);
            }

            if (plan.MissingUris != null && plan.MissingUris.Count > 0)
            {
                options.MissingUris = new List<string>(plan.MissingUris);
            }
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
        plan.Branch = ShouldRelocateAtomic(ctx)
            ? FlattenBranch.RelocateAtomic
            : FlattenBranch.SplitDependencies;
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

    /// <summary>有外 URI（相对 .bin / 外图）时禁止按后缀拆夹，改走原子搬迁。</summary>
    public static bool ShouldRelocateAtomic(PipelineJobContext ctx)
    {
        return ctx != null && ctx.HasExternalUris;
    }

    /// <summary>
    /// Art 副本上的 ModelImporter 设置 + Extract 内嵌贴图。
    /// ScriptedImporter（gltf/glb）现网 Extract 本就会空转；ctx 为 null（菜单）时仍跑，与旧菜单一致。
    /// </summary>
    public static bool ShouldApplyArtModelImporter(PipelineJobContext ctx)
    {
        return ctx == null || ctx.ImporterKind == PipelineImporterKind.ModelImporter;
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

    public static bool TryBegin(
        string sourcePrefabPath,
        PipelineJobContext ctx,
        ToolFlattenRequest request,
        out RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.TryBeginPackagedFlatten(
            sourcePrefabPath, CreateOptions(ctx, request), out work);
    }

    public static bool SplitDependencies(RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.FlattenSplitDependencies(work);
    }

    public static bool RelocateAtomic(RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.FlattenRelocateAtomic(work);
    }

    /// <summary>E。ctx 标明非 ModelImporter 时跳过（避免对 gltf 空转 Extract）。</summary>
    public static void ApplyImportAndExtract(RetinarFlattenWork work, PipelineJobContext ctx)
    {
        if (!ShouldApplyArtModelImporter(ctx))
        {
            return;
        }

        RetinarBatchModelBuilder.FlattenApplyImportAndExtract(work);
    }

    public static void Remap(RetinarFlattenWork work)
    {
        RetinarBatchModelBuilder.FlattenRemap(work);
    }

    public static void CopyRendererMaterials(RetinarFlattenWork work)
    {
        RetinarBatchModelBuilder.FlattenCopyRendererMaterials(work);
    }

    public static bool TryFinish(RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.TryFinishPackagedFlatten(work);
    }
}
