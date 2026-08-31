using System.Collections.Generic;

// =====================================================================================
// Shared / Api — ⑤ 后处理窄口（L1 子流程对外）
// =====================================================================================

/// <summary>
/// 插件 2 · 资源处理子流程对外接口。
/// 与 L1「按批量路径执行全部」同一内核（手动路径，不读 excludedPathPrefixes）。
/// 中间层⑤只是代调本口，不是导入期 AssetPostprocessor 自动流。
/// </summary>
public static class ToolPostProcessApi
{
    /// <summary>
    /// 跑总批量（贴图→材质→模型）。folders 为 null 时用 L1 当前批量路径。
    /// 返回轻量结果：FailedCount 复用各层 Summary；细节在 Report。
    /// </summary>
    public static ToolPostProcessResult RunMasterBatch(
        IList<string> folders = null,
        bool? includeTexture = null,
        bool? includeModel = null,
        bool? includeMaterial = null)
    {
        return ResourcePostProcessService.RunMasterBatch(
            folders, includeTexture, includeModel, includeMaterial);
    }
}
