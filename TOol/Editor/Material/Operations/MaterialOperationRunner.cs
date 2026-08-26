using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>材质 Op 批量执行。</summary>
public static class MaterialOperationRunner
{
    private const string ProgressBarTitle = "材质处理";

    public static MaterialOperationRunSummary Run(
        IList<IMaterialAssetOperation> operations,
        IList<string> assetPaths,
        MaterialProcessSettings settings)
    {
        var summary = new MaterialOperationRunSummary();
        if (operations == null || assetPaths == null || settings == null)
        {
            return summary;
        }

        var pending = new List<PendingWork>();
        for (int o = 0; o < operations.Count; o++)
        {
            IMaterialAssetOperation operation = operations[o];
            if (operation == null)
            {
                continue;
            }

            for (int a = 0; a < assetPaths.Count; a++)
            {
                string path = assetPaths[a];
                AssetOperationEvaluation evaluation = operation.Evaluate(path, settings);
                if (evaluation.NeedsWork)
                {
                    pending.Add(new PendingWork(operation, path));
                }
            }
        }

        if (pending.Count == 0)
        {
            if (operations.Count > 0 && assetPaths.Count > 0)
            {
                Debug.LogWarning("[材质处理] 命中 " + assetPaths.Count +
                    " 个材质，但对当前勾选操作均为无需处理。");
            }

            return summary;
        }

        try
        {
            for (int i = 0; i < pending.Count; i++)
            {
                PendingWork work = pending[i];
                float progress = (float)i / pending.Count;
                if (!Application.isBatchMode &&
                    EditorUtility.DisplayCancelableProgressBar(
                        ProgressBarTitle,
                        work.Operation.DisplayName + " — " + work.AssetPath,
                        progress))
                {
                    summary.Canceled = true;
                    break;
                }

                var context = new MaterialOperationContext(
                    work.AssetPath,
                    settings,
                    (msg, p) => { });

                MaterialOperationResult result;
                try
                {
                    result = work.Operation.Execute(context);
                }
                catch (System.Exception ex)
                {
                    result = MaterialOperationResult.Failed(ex.GetType().Name + ": " + ex.Message);
                }

                string line = work.AssetPath + " — " + work.Operation.Id + ": " + result.Message;
                if (result.Status == MaterialOperationStatus.Changed)
                {
                    summary.ChangedCount++;
                    summary.ChangedLines.Add(line);
                }
                else if (result.Status == MaterialOperationStatus.Failed)
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
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (summary.ChangedCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log("[材质处理] 完成：改动 " + summary.ChangedCount +
                  "，跳过 " + summary.SkippedCount +
                  "，失败 " + summary.FailedCount +
                  (summary.Canceled ? "（已取消）" : string.Empty));
        return summary;
    }

    private struct PendingWork
    {
        public readonly IMaterialAssetOperation Operation;
        public readonly string AssetPath;

        public PendingWork(IMaterialAssetOperation operation, string assetPath)
        {
            Operation = operation;
            AssetPath = assetPath;
        }
    }
}
