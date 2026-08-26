using System.Collections.Generic;

// =====================================================================================
// Shared / Api — ⑤ 后处理窄口（L1 子流程对外）
// =====================================================================================

/// <summary>
/// 插件 2 · 资源处理子流程对外接口。
/// 流程编排只决定「要不要⑤」；本口按 L1 纳入开关 + L3 Op 集合执行。
/// </summary>
public static class ToolPostProcessApi
{
    /// <summary>
    /// 跑总批量（贴图→模型）。folders 为 null 时用 L1 当前批量路径。
    /// </summary>
    public static string RunMasterBatch(
        IList<string> folders = null,
        bool? includeTexture = null,
        bool? includeModel = null)
    {
        return ResourcePostProcessService.RunMasterBatch(folders, includeTexture, includeModel);
    }
}
