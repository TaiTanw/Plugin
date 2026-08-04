using System.Collections.Generic;

// =====================================================================================
// 职责边界：
//   只承载一次批量执行的结果。窗口用它渲染结果面板，Runner 用它拼汇总日志。
//   不做任何执行逻辑。
// =====================================================================================
/// <summary>
/// 执行结果数据
/// </summary>
public class TextureOperationRunSummary
{
    public int ChangedCount;
    public int SkippedCount;
    public int FailedCount;

    /// <summary>用户中途点了进度条上的取消按钮。这时候已处理的部分是生效的，剩下的没跑。</summary>
    public bool Canceled;

    /// <summary>每一条实际改动的记录，按执行顺序。</summary>
    public readonly List<string> ChangedLines = new List<string>();

    /// <summary>每一条失败记录，按执行顺序。</summary>
    public readonly List<string> FailedLines = new List<string>();

    /// <summary>
    /// 每一条跳过记录。以前不存这个，窗口只显示"跳过 N 项"却没有任何原因，
    /// 用户会以为点了执行却完全没反应（亮度写 Alpha 对 .fbm / 已有 Alpha 的图全是跳过）。
    /// </summary>
    public readonly List<string> SkippedLines = new List<string>();

    public int TotalHandled
    {
        get { return ChangedCount + SkippedCount + FailedCount; }
    }

    public bool HasAnythingToReport
    {
        get { return ChangedCount > 0 || FailedCount > 0 || SkippedCount > 0; }
    }
}
