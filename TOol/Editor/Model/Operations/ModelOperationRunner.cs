using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ModelOperationRunner
{
    private const string ProgressBarTitle = "模型处理";
    private const string ScanProgressBarTitle = "模型扫描";

    public static ModelOperationRunSummary Run(
        IList<IModelAssetOperation> operations,
        IList<string> assetPaths,
        ModelProcessSettings settings,
        bool triggeredByImport)
    {
        return Run(operations, assetPaths, settings, triggeredByImport, null);
    }

    /// <param name="importRoot">
    /// OnPostprocessModel 传入的根节点；非 null 时写入 Context.ImportRoot，并参与 Evaluate 探测。
    /// </param>
    public static ModelOperationRunSummary Run(
        IList<IModelAssetOperation> operations,
        IList<string> assetPaths,
        ModelProcessSettings settings,
        bool triggeredByImport,
        GameObject importRoot)
    {
        var summary = new ModelOperationRunSummary();
        List<PendingWork> pendingWork = CollectPendingWork(operations, assetPaths, settings, importRoot);
        if (pendingWork.Count == 0)
        {
            if (!triggeredByImport &&
                operations != null && operations.Count > 0 &&
                assetPaths != null && assetPaths.Count > 0)
            {
                Debug.LogWarning("[模型处理] 命中 " + assetPaths.Count +
                    " 个模型，但对当前勾选的操作 Evaluate 均为无需处理，未执行。");
            }

            return summary;
        }

        try
        {
            ExecuteAll(pendingWork, settings, triggeredByImport, summary, importRoot);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (summary.ChangedCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        LogSummary(summary, triggeredByImport);
        return summary;
    }

    /// <summary>仅扫描：Evaluate dry-run，不改文件。</summary>
    public static AssetOperationScanSummary Scan(
        IList<IModelAssetOperation> operations,
        IList<string> assetPaths,
        ModelProcessSettings settings,
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
            foreach (IModelAssetOperation operation in operations)
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

                    AssetOperationEvaluation evaluation = operation.Evaluate(assetPath, settings, null);
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
        IList<IModelAssetOperation> operations,
        IList<string> assetPaths,
        ModelProcessSettings settings,
        GameObject importRoot)
    {
        var pendingWork = new List<PendingWork>();
        if (operations == null || assetPaths == null)
        {
            return pendingWork;
        }

        foreach (IModelAssetOperation operation in operations)
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

                if (!operation.Evaluate(assetPath, settings, importRoot).NeedsWork)
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
        ModelProcessSettings settings,
        bool triggeredByImport,
        ModelOperationRunSummary summary,
        GameObject importRoot)
    {
        for (int i = 0; i < pendingWork.Count; i++)
        {
            PendingWork work = pendingWork[i];
            int currentIndex = i;
            System.Action<float, string> subProgress = (ratio, detail) =>
            {
                float progress = (currentIndex + Mathf.Clamp01(ratio)) / pendingWork.Count;
                EditorUtility.DisplayProgressBar(
                    ProgressBarTitle,
                    work.Operation.DisplayName + "：" + work.AssetPath +
                    (string.IsNullOrEmpty(detail) ? string.Empty : "\n" + detail),
                    progress);
            };

            if (EditorUtility.DisplayCancelableProgressBar(
                    ProgressBarTitle,
                    work.Operation.DisplayName + "：" + work.AssetPath,
                    (float)currentIndex / pendingWork.Count))
            {
                summary.Canceled = true;
                return;
            }

            var context = new ModelOperationContext(
                work.AssetPath, settings, triggeredByImport, subProgress, importRoot);
            ModelOperationResult result;
            try
            {
                result = work.Operation.Execute(context);
            }
            catch (System.Exception exception)
            {
                result = ModelOperationResult.Failed(exception.GetType().Name + ": " + exception.Message);
            }

            string line = work.Operation.DisplayName + " | " + work.AssetPath + " | " + result.Message;
            if (result.Status == ModelOperationStatus.Changed)
            {
                summary.ChangedCount++;
                summary.ChangedLines.Add(line);
            }
            else if (result.Status == ModelOperationStatus.Failed)
            {
                summary.FailedCount++;
                summary.FailedLines.Add(line);
            }
            else
            {
                summary.SkippedCount++;
            }
        }
    }

    private static void LogSummary(ModelOperationRunSummary summary, bool triggeredByImport)
    {
        if (!summary.HasAnythingToReport)
        {
            return;
        }

        string trigger = triggeredByImport ? "导入触发" : "手动触发";
        var report = new List<string>
        {
            "[模型处理] " + trigger + " 完成：改动 " + summary.ChangedCount +
            " 项，跳过 " + summary.SkippedCount + " 项，失败 " + summary.FailedCount + " 项" +
            (summary.Canceled ? "（用户中途取消）" : string.Empty)
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
            Debug.LogError("[模型处理] 有 " + summary.FailedCount + " 项失败:\n" +
                string.Join("\n", summary.FailedLines.ToArray()));
        }
    }

    private static void LogScanSummary(AssetOperationScanSummary summary)
    {
        var report = new List<string>
        {
            "[模型扫描] 需处理 " + summary.NeedsWorkCount +
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
            builder.Append("当前勾选操作下没有需要处理的模型。");
        }

        EditorUtility.DisplayDialog("模型仅扫描", builder.ToString(), "OK");
    }

    private struct PendingWork
    {
        public readonly IModelAssetOperation Operation;
        public readonly string AssetPath;

        public PendingWork(IModelAssetOperation operation, string assetPath)
        {
            Operation = operation;
            AssetPath = assetPath;
        }
    }
}
