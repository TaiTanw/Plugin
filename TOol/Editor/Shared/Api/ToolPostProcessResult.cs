// =====================================================================================
// Shared / Api — ⑤ 总批量轻量结果（D16）
// FailedCount 复用三层 OperationRunSummary.FailedCount 之和，不解析报告字符串。
// =====================================================================================

/// <summary>
/// ⑤ 窄口返回值。报告给人读；<see cref="FailedCount"/> 给编排映射退出码。
/// 失败口径：Evaluate 为 NeedsWork 后 Execute 为 Failed（打不开、硬限制、抛异常）。
/// Skip / NotApplicable / 未命中文件 / 未勾选大类 不算失败。
/// </summary>
public sealed class ToolPostProcessResult
{
    /// <summary>贴图+材质+模型 Execute Failed 条数之和。</summary>
    public int FailedCount;

    /// <summary>用户取消进度条（已跑部分仍生效）。单独取消不算硬失败。</summary>
    public bool Canceled;

    /// <summary>给人读的拼接报告（面板 / 日志）。</summary>
    public string Report = string.Empty;

    public bool HasHardFailure
    {
        get { return FailedCount > 0; }
    }
}
