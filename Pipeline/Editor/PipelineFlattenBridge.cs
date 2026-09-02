using System.Collections.Generic;

// =====================================================================================
// Pipeline — D23a 事实 → D23b 平铺开关。两边类型不互相引用。
// =====================================================================================

/// <summary>
/// 编排层映射。JobContext 不知道平铺；FlattenOptions 不知道 ctx。
/// </summary>
public static class PipelineFlattenBridge
{
    /// <summary>
    /// HasExternalUris → SkipDependencySplit + 主文件/伴生路径（供 B′）。
    /// </summary>
    public static RetinarFlattenOptions ToFlattenOptions(PipelineJobContext ctx)
    {
        var options = new RetinarFlattenOptions();
        if (ctx == null)
        {
            return options;
        }

        if (ctx.HasExternalUris)
        {
            options.SkipDependencySplit = true;
            options.PrimaryAssetPath = ctx.PrimaryAssetPath;
            if (ctx.SidecarPaths != null && ctx.SidecarPaths.Count > 0)
            {
                options.SidecarPaths = new List<string>(ctx.SidecarPaths);
            }
        }

        return options;
    }
}
