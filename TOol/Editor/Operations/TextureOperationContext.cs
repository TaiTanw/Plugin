using System;

// =====================================================================================
// 职责边界：
//   把"执行一个操作需要知道的一切"打成一个包传进去。
//
// 为什么不直接给 Execute 传 (assetPath, settings) 两个参数：
//   进度上报需要第三个参数，以后可能还要加"是不是导入回调触发的""是否 dry-run"之类。
//   每加一个都改接口签名，所有实现类跟着编译报错。用一个 context 结构体之后，
//   加字段不会破坏任何已有的操作实现——这是"提供功能拓展的基本接口"的关键一环。
// =====================================================================================
public struct TextureOperationContext
{
    public readonly string AssetPath;
    public readonly TextureProcessSettings Settings;

    /// <summary>true 表示这次执行是由 AssetPostprocessor 导入流程触发的，false 表示用户在窗口里手动点的。</summary>
    public readonly bool TriggeredByImport;

    private readonly Action<float, string> subProgressReporter;

    public TextureOperationContext(
        string assetPath,
        TextureProcessSettings settings,
        bool triggeredByImport,
        Action<float, string> subProgressReporter)
    {
        AssetPath = assetPath;
        Settings = settings;
        TriggeredByImport = triggeredByImport;
        this.subProgressReporter = subProgressReporter;
    }

    /// <summary>
    /// 上报"当前这一个文件内部的进度"。二分搜索一张 8K 图要编码十几次、可能好几秒，
    /// 没有这个上报的话进度条会长时间停在同一格，看起来像卡死了。
    /// ratio01 是 0-1 之间的比例，detail 是显示在进度条上的一行说明。
    /// </summary>
    public void ReportSubProgress(float ratio01, string detail)
    {
        if (subProgressReporter != null)
        {
            subProgressReporter(ratio01, detail);
        }
    }
}
