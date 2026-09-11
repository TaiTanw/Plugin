using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Service
// 中文：④ 平铺执行层。中间层只组合窄口；本类读 ctx 做能力分支。
// 内核仍是 RetinarBatchModelBuilder（本目录 Service/），本类负责：
//   ctx.HasExternalUris → B′；ImporterKind → 是否跑 Art ModelImporter Extract；
//   请求里的轴向 / 清夹 → Options。
// =====================================================================================

/// <summary>④ 平铺服务。不引用步骤开关；开不开④由编排决定。</summary>
public static class FlattenBuildService
{
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
        request = request ?? ToolFlattenRequest.MenuDefault;
        var options = new RetinarFlattenOptions();
        options.OperationPolicy = request.OperationPolicy ??
                                  FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);
        options.ClearDestinationArtFolder = request.ClearDestinationArtFolder;
        options.ConvertZUpToYUp = request.ConvertZUpToYUp;
        options.AddBoxCollider = options.OperationPolicy.AddBoxCollider;

        if (ShouldRelocateAtomic(ctx))
        {
            options.SkipDependencySplit = true;
            options.PrimaryAssetPath = ctx.PrimaryAssetPath;
            if (ctx.SidecarPaths != null && ctx.SidecarPaths.Count > 0)
            {
                options.SidecarPaths = new List<string>(ctx.SidecarPaths);
            }

            if (ctx.MissingUris != null && ctx.MissingUris.Count > 0)
            {
                options.MissingUris = new List<string>(ctx.MissingUris);
            }
        }

        return options;
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
