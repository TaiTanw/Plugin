using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>材质 Op 批量执行与扫描。</summary>
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

    /// <summary>仅扫描：Evaluate dry-run，不改文件。</summary>
    public static AssetOperationScanSummary Scan(
        IList<IMaterialAssetOperation> operations,
        IList<string> assetPaths,
        MaterialProcessSettings settings,
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
            for (int o = 0; o < operations.Count; o++)
            {
                IMaterialAssetOperation operation = operations[o];
                if (operation == null)
                {
                    continue;
                }

                for (int a = 0; a < assetPaths.Count; a++)
                {
                    string assetPath = assetPaths[a];
                    if (!Application.isBatchMode &&
                        EditorUtility.DisplayCancelableProgressBar(
                            "材质扫描",
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

        Debug.Log("[材质扫描] 需处理 " + summary.NeedsWorkCount +
                  "，跳过 " + summary.SkippedCount +
                  "，不适用 " + summary.NotApplicableCount +
                  (summary.Canceled ? "（已取消）" : string.Empty));

        if (showDialog && !Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "材质扫描",
                "需处理 " + summary.NeedsWorkCount +
                "\n跳过 " + summary.SkippedCount +
                "\n不适用 " + summary.NotApplicableCount +
                (summary.Canceled ? "\n（已取消）" : string.Empty),
                "OK");
        }

        return summary;
    }

    /// <summary>命中列表：当前勾选 Op 至少一个 Evaluate.NeedsWork。</summary>
    public static List<string> FilterNeedsWork(
        IList<IMaterialAssetOperation> operations,
        IList<string> assetPaths,
        MaterialProcessSettings settings)
    {
        var result = new List<string>();
        if (operations == null || assetPaths == null || operations.Count == 0)
        {
            return result;
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < assetPaths.Count; i++)
        {
            string assetPath = assetPaths[i];
            if (string.IsNullOrEmpty(assetPath) || seen.Contains(assetPath))
            {
                continue;
            }

            for (int o = 0; o < operations.Count; o++)
            {
                IMaterialAssetOperation operation = operations[o];
                if (operation == null)
                {
                    continue;
                }

                if (operation.Evaluate(assetPath, settings).NeedsWork)
                {
                    seen.Add(assetPath);
                    result.Add(assetPath);
                    break;
                }
            }
        }

        return result;
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
