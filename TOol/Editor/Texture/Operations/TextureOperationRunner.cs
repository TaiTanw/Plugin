using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   这个类是"流程控制"的核心，也是窗口手动执行和导入自动执行【唯一】共用的入口。
//   它只做四件事：筛出真正要处理的工作、按顺序执行、管进度条与取消、汇总结果。
//   筛选口径与「仅扫描」共用 Operation.Evaluate，避免 CanProcess / Execute 两套规则。
// =====================================================================================
/// <summary>
/// 纹理处理器（流程控制，文件与执行调度）
/// </summary>
public static class TextureOperationRunner
{
    private const string ProgressBarTitle = "贴图处理";
    private const string ScanProgressBarTitle = "贴图扫描";

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
            if (operations != null && operations.Count > 0 && assetPaths != null && assetPaths.Count > 0)
            {
                if (!triggeredByImport)
                {
                    Debug.LogWarning("[贴图处理] 命中 " + assetPaths.Count +
                        " 张贴图，但对当前勾选的操作 Evaluate 均为无需处理，未执行任何处理。");
                }
            }

            return summary;
        }

        try
        {
            ExecuteAll(pendingWork, settings, triggeredByImport, summary);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        FlushAssetDatabase(summary);
        LogSummary(summary, triggeredByImport);
        return summary;
    }

    /// <summary>
    /// 仅扫描：对勾选操作跑 Evaluate，不改文件。命中项写入 Console，并可弹对话框。
    /// </summary>
    public static AssetOperationScanSummary Scan(
        IList<ITextureAssetOperation> operations,
        IList<string> assetPaths,
        TextureProcessSettings settings,
        bool showDialog)
    {
        var summary = new AssetOperationScanSummary();
        if (operations == null || assetPaths == null || settings == null)
        {
            return summary;
        }

        try
        {
            int total = operations.Count * Mathf.Max(assetPaths.Count, 1);
            int done = 0;
            foreach (ITextureAssetOperation operation in operations)
            {
                if (operation == null)
                {
                    continue;
                }

                foreach (string assetPath in assetPaths)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(
                            ScanProgressBarTitle,
                            operation.DisplayName + "：" + assetPath,
                            total > 0 ? (float)done / total : 0f))
                    {
                        summary.Canceled = true;
                        break;
                    }

                    done++;
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    AssetOperationEvaluation evaluation = operation.Evaluate(assetPath, settings);
                    string line = operation.DisplayName + " | " + assetPath + " | " + evaluation.Reason;
                    if (evaluation.NeedsWork)
                    {
                        summary.NeedsWorkCount++;
                        summary.NeedsWorkLines.Add(line);
                    }
                    else if (evaluation.Eligibility == AssetOperationEligibility.Skip)
                    {
                        summary.SkippedCount++;
                        if (summary.SkippedLines.Count < 40)
                        {
                            summary.SkippedLines.Add(line);
                        }
                    }
                    else
                    {
                        summary.NotApplicableCount++;
                    }
                }

                if (summary.Canceled)
                {
                    break;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        LogScanSummary(summary);
        if (showDialog)
        {
            ShowScanDialog(summary);
        }

        return summary;
    }

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
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                if (!operation.Evaluate(assetPath, settings).NeedsWork)
                {
                    continue;
                }

                pendingWork.Add(new PendingWork(operation, assetPath));
            }
        }

        return pendingWork;
    }

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

            System.Action<float, string> subProgress = (ratio, detail) =>
                UpdateProgressBar(pendingWork.Count, currentIndex, ratio, work, detail);

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
            summary.SkippedLines.Add(line);
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

    private static void FlushAssetDatabase(TextureOperationRunSummary summary)
    {
        if (summary.ChangedCount == 0)
        {
            return;
        }

        AssetDatabase.SaveAssets();
    }

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

        if (summary.SkippedLines.Count > 0)
        {
            report.Add("");
            report.Add("已跳过:");
            report.AddRange(summary.SkippedLines);
        }

        if (summary.ChangedCount == 0 && summary.FailedCount == 0 && summary.SkippedCount > 0)
        {
            Debug.LogWarning(string.Join("\n", report.ToArray()));
        }
        else
        {
            Debug.Log(string.Join("\n", report.ToArray()));
        }

        if (summary.FailedLines.Count > 0)
        {
            Debug.LogError("[贴图处理] 有 " + summary.FailedCount + " 项处理失败，需要人工确认:\n" +
                string.Join("\n", summary.FailedLines.ToArray()));
        }
    }

    private static void LogScanSummary(AssetOperationScanSummary summary)
    {
        var report = new List<string>
        {
            "[贴图扫描] 需处理 " + summary.NeedsWorkCount +
            " 项，已达标/策略跳过 " + summary.SkippedCount +
            " 项，不适用 " + summary.NotApplicableCount + " 项" +
            (summary.Canceled ? "（已取消）" : string.Empty)
        };

        if (summary.NeedsWorkLines.Count > 0)
        {
            report.Add("");
            report.Add("需处理:");
            report.AddRange(summary.NeedsWorkLines);
        }

        Debug.Log(string.Join("\n", report.ToArray()));
    }

    private static void ShowScanDialog(AssetOperationScanSummary summary)
    {
        var builder = new StringBuilder();
        builder.Append("需处理 ").Append(summary.NeedsWorkCount).Append(" 项");
        if (summary.Canceled)
        {
            builder.Append("（扫描已取消）");
        }

        builder.Append("。\n\n");
        int preview = Mathf.Min(summary.NeedsWorkLines.Count, 25);
        for (int i = 0; i < preview; i++)
        {
            builder.AppendLine(summary.NeedsWorkLines[i]);
        }

        if (summary.NeedsWorkLines.Count > preview)
        {
            builder.AppendLine("…其余见 Console");
        }

        if (summary.NeedsWorkCount == 0)
        {
            builder.Append("当前勾选操作下没有需要处理的贴图。");
        }

        EditorUtility.DisplayDialog("贴图仅扫描", builder.ToString(), "OK");
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
