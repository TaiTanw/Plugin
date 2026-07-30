using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   这个类是"流程控制"的核心，也是窗口手动执行和导入自动执行【唯一】共用的入口。
//   它只做四件事：筛出真正要处理的工作、按顺序执行、管进度条与取消、汇总结果。
//   它自己不认识任何一种贴图格式，也不知道"压缩"是怎么压的——那些在 Operations
//   的具体实现和 Codec 层里。
//
// 为什么手动和自动一定要共用这一个入口：
//   之前的问题是"导入时压一套逻辑、手动再压一套逻辑"，两边行为不一致，
//   出问题时分不清是哪条路径干的。统一到这里之后，唯一的区别只有
//   context.TriggeredByImport 这一个标记，行为完全相同。
// =====================================================================================
public static class TextureOperationRunner
{
    private const string ProgressBarTitle = "贴图处理";

    /// <summary>
    /// 总体流程：先算出真正需要处理的（资产 × 操作）组合，再逐个执行并刷进度条，
    /// 最后统一保存刷新一次并把结果汇总成日志。
    /// </summary>
    public static TextureOperationRunSummary Run(
        IList<ITextureAssetOperation> operations,
        IList<string> assetPaths,
        TextureProcessSettings settings,
        bool triggeredByImport)
    {
        var summary = new TextureOperationRunSummary();
        List<PendingWork> pendingWork = CollectPendingWork(operations, assetPaths, settings);
        if (pendingWork.Count == 0)
        {
            return summary;
        }

        try
        {
            ExecuteAll(pendingWork, settings, triggeredByImport, summary);
        }
        finally
        {
            // 中途抛异常也必须清掉进度条，否则编辑器会一直卡着一个不会消失的进度框。
            EditorUtility.ClearProgressBar();
        }

        FlushAssetDatabase(summary);
        LogSummary(summary, triggeredByImport);
        return summary;
    }

    // ---------------------------------------------------------------------------
    // 1) 筛选：哪些资产的哪些操作真的需要跑
    // ---------------------------------------------------------------------------

    /// <summary>
    /// 先把工作量算清楚再开始跑，而不是边跑边判断。这样进度条的分母是准确的，
    /// 也避免了"扫了 2000 个资产，其实只有 3 个要处理，进度条却在 0% 停很久"。
    /// </summary>
    private static List<PendingWork> CollectPendingWork(
        IList<ITextureAssetOperation> operations,
        IList<string> assetPaths,
        TextureProcessSettings settings)
    {
        var pendingWork = new List<PendingWork>();
        if (operations == null || assetPaths == null)
        {
            return pendingWork;
        }

        foreach (ITextureAssetOperation operation in operations)
        {
            if (operation == null)
            {
                continue;
            }

            foreach (string assetPath in assetPaths)
            {
                if (string.IsNullOrEmpty(assetPath) || !operation.CanProcess(assetPath, settings))
                {
                    continue;
                }

                pendingWork.Add(new PendingWork(operation, assetPath));
            }
        }

        return pendingWork;
    }

    // ---------------------------------------------------------------------------
    // 2) 执行：两级进度 + 可取消 + 单条失败不影响整批
    // ---------------------------------------------------------------------------

    private static void ExecuteAll(
        List<PendingWork> pendingWork,
        TextureProcessSettings settings,
        bool triggeredByImport,
        TextureOperationRunSummary summary)
    {
        for (int i = 0; i < pendingWork.Count; i++)
        {
            PendingWork work = pendingWork[i];
            int currentIndex = i;

            // 两级进度：外层是"第几个文件"，内层是操作自己上报的"这个文件处理到哪了"。
            // 二分搜索一张 8K 图要编码十几次，没有内层进度会看起来像卡死。
            System.Action<float, string> subProgress = (ratio, detail) =>
                UpdateProgressBar(pendingWork.Count, currentIndex, ratio, work, detail);

            // 只在每个文件开始时用可取消的版本刷一次，给用户留出中断的机会；
            // 文件内部的子进度用不可取消的版本，避免一次执行里反复检测取消按钮拖慢速度。
            if (EditorUtility.DisplayCancelableProgressBar(
                    ProgressBarTitle, BuildProgressText(work, null), (float)currentIndex / pendingWork.Count))
            {
                summary.Canceled = true;
                return;
            }

            var context = new TextureOperationContext(work.AssetPath, settings, triggeredByImport, subProgress);
            Accumulate(summary, work, ExecuteSingle(work, context));
        }
    }

    /// <summary>
    /// 单条执行的异常兜底。一个损坏的贴图不应该让整批处理中断——这正是插件 1
    /// 里"一个模型有问题就整批终止"那种体验糟糕的做法，这里不重复它。
    /// </summary>
    private static TextureOperationResult ExecuteSingle(PendingWork work, TextureOperationContext context)
    {
        try
        {
            return work.Operation.Execute(context);
        }
        catch (System.Exception exception)
        {
            return TextureOperationResult.Failed("执行时抛出异常: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static void Accumulate(TextureOperationRunSummary summary, PendingWork work, TextureOperationResult result)
    {
        string line = work.Operation.DisplayName + " | " + work.AssetPath + " | " + result.Message;
        if (result.Status == TextureOperationStatus.Changed)
        {
            summary.ChangedCount++;
            summary.ChangedLines.Add(line);
        }
        else if (result.Status == TextureOperationStatus.Failed)
        {
            summary.FailedCount++;
            summary.FailedLines.Add(line);
        }
        else
        {
            summary.SkippedCount++;
        }
    }

    private static void UpdateProgressBar(int total, int index, float subRatio, PendingWork work, string detail)
    {
        float progress = (index + Mathf.Clamp01(subRatio)) / total;
        EditorUtility.DisplayProgressBar(ProgressBarTitle, BuildProgressText(work, detail), progress);
    }

    private static string BuildProgressText(PendingWork work, string detail)
    {
        string text = work.Operation.DisplayName + "：" + work.AssetPath;
        return string.IsNullOrEmpty(detail) ? text : text + "\n" + detail;
    }

    // ---------------------------------------------------------------------------
    // 3) 收尾：统一落盘 + 汇总日志
    // ---------------------------------------------------------------------------

    /// <summary>
    /// 兜底的统一保存刷新。各个操作已经在自己内部对改动过的资产调过 ImportAsset，
    /// 这里再收一次是为了覆盖"操作里新建/删除了资产"这类需要整体刷新的情况。
    /// 没有任何改动时不刷新，避免白白触发一次全工程的导入检查。
    /// </summary>
    private static void FlushAssetDatabase(TextureOperationRunSummary summary)
    {
        if (summary.ChangedCount == 0)
        {
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 汇总成一条 Log 加一条 Error，而不是每个文件打一行。
    /// 一批几百张贴图时逐条打印会把 Console 冲干净，真正的失败反而被淹掉。
    /// </summary>
    private static void LogSummary(TextureOperationRunSummary summary, bool triggeredByImport)
    {
        if (!summary.HasAnythingToReport)
        {
            return;
        }

        string trigger = triggeredByImport ? "导入触发" : "手动触发";
        var report = new List<string>
        {
            "[贴图处理] " + trigger + " 完成：改动 " + summary.ChangedCount +
            " 项，跳过 " + summary.SkippedCount + " 项，失败 " + summary.FailedCount + " 项" +
            (summary.Canceled ? "（用户中途取消，剩余未处理）" : string.Empty)
        };

        if (summary.ChangedLines.Count > 0)
        {
            report.Add("");
            report.Add("已改动:");
            report.AddRange(summary.ChangedLines);
        }

        Debug.Log(string.Join("\n", report.ToArray()));

        if (summary.FailedLines.Count > 0)
        {
            Debug.LogError("[贴图处理] 有 " + summary.FailedCount + " 项处理失败，需要人工确认:\n" +
                string.Join("\n", summary.FailedLines.ToArray()));
        }
    }

    private struct PendingWork
    {
        public readonly ITextureAssetOperation Operation;
        public readonly string AssetPath;

        public PendingWork(ITextureAssetOperation operation, string assetPath)
        {
            Operation = operation;
            AssetPath = assetPath;
        }
    }
}
