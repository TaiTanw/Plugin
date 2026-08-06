using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ModelOperationRunner
{
    private const string ProgressBarTitle = "模型处理";

    public static ModelOperationRunSummary Run(
        IList<IModelAssetOperation> operations,
        IList<string> assetPaths,
        ModelProcessSettings settings,
        bool triggeredByImport)
    {
        return Run(operations, assetPaths, settings, triggeredByImport, null);
    }

    /// <param name="importRoot">
    /// OnPostprocessModel 传入的根节点；非 null 时写入 Context.ImportRoot，供顶点色等操作在库未就绪时从层级取 Mesh。
    /// </param>
    public static ModelOperationRunSummary Run(
        IList<IModelAssetOperation> operations,
        IList<string> assetPaths,
        ModelProcessSettings settings,
        bool triggeredByImport,
        GameObject importRoot)
    {
        var summary = new ModelOperationRunSummary();
        List<PendingWork> pendingWork = CollectPendingWork(operations, assetPaths, settings);
        if (pendingWork.Count == 0)
        {
            if (!triggeredByImport &&
                operations != null && operations.Count > 0 &&
                assetPaths != null && assetPaths.Count > 0)
            {
                Debug.LogWarning("[模型处理] 命中 " + assetPaths.Count +
                    " 个模型，但对当前勾选的操作都不适用，未执行。");
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
            // 只 Save，不要 Refresh。
            // Refresh 会让 ModelImporter 从 FBX 二进制重建 Mesh，刚写入的顶点色被清掉；
            // 且此时若处在 ImportPostProcessScheduler.IsRunning 中，OnPostprocessAllAssets
            // 会拒收入队，无法再跑第二遍——表现为「导入自动顶点色完全没生效」。
            AssetDatabase.SaveAssets();
        }

        LogSummary(summary, triggeredByImport);
        return summary;
    }

    private static List<PendingWork> CollectPendingWork(
        IList<IModelAssetOperation> operations,
        IList<string> assetPaths,
        ModelProcessSettings settings)
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
                if (string.IsNullOrEmpty(assetPath) || !operation.CanProcess(assetPath, settings))
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
