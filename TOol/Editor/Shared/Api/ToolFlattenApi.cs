// =====================================================================================
// Shared / Api — ④ 平铺窄口（对齐 ToolPrefabApi）
//
// 中间层只调本类。Run(plan) 不读 ctx。ctx → plan 只在 FromContext。
// =====================================================================================

/// <summary>插件 2 · 平铺到 Art 对外接口。</summary>
public static class ToolFlattenApi
{
    public static string ArtRoot
    {
        get { return FlattenArtPaths.ArtRoot; }
    }

    /// <summary>
    /// 一行完整④：Begin→B|B′→E?→D→C→Finish。不读 ctx。
    /// 管线 FromContext 后进本口；人工组 plan 后进本口。
    /// </summary>
    public static FlattenRowResult Run(FlattenPlan plan)
    {
        return FlattenBuildService.Run(plan);
    }

    /// <summary>编排把 ctx + 请求译成 plan。内核只收 plan。</summary>
    public static FlattenPlan FromContext(
        PipelineJobContext ctx,
        ToolFlattenRequest request,
        string sourcePrefabPath)
    {
        return FlattenBuildService.FromContext(ctx, request, sourcePrefabPath);
    }
}
