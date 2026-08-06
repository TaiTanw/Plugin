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
/// <summary>
/// 纹理处理器（流程控制，文件与执行调度）
/// </summary>
public static class TextureOperationRunner
{
    private const string ProgressBarTitle = "贴图处理";

    /// <summary>
    /// 总体流程：先算出真正需要处理的（资产 × 操作）组合，再逐个执行并刷进度条，
    /// 最后统一保存刷新一次并把结果汇总成日志
    /// </summary>
    /// <param name="operations">操作集合</param>
    /// <param name="assetPaths">资源路径</param>
    /// <param name="settings">配置</param>
    /// <param name="triggeredByImport">手动或自动</param>
    /// <returns></returns>
    public static TextureOperationRunSummary Run(
        IList<ITextureAssetOperation> operations,
        IList<string> assetPaths,
        TextureProcessSettings settings,
        bool triggeredByImport)
    {
        var summary = new TextureOperationRunSummary();
        //获得待处理工作内容
        List<PendingWork> pendingWork = CollectPendingWork(operations, assetPaths, settings);
        if (pendingWork.Count == 0)
        {
            // 命中了贴图但 CanProcess 全否时，以前直接返回空结果，窗口和 Console 都没声音。
            // 留一条可诊断信息，让人知道是"操作不适用"而不是"没点到"。
            if (operations != null && operations.Count > 0 && assetPaths != null && assetPaths.Count > 0)
            {
                // 导入自动：重开工程常入队大量已达标贴图，CanProcess 全否属预期，勿 Warning 刷屏。
                // 手动执行仍提示，避免误以为点了却没反应。
                if (!triggeredByImport)
                {
                    Debug.LogWarning("[贴图处理] 命中 " + assetPaths.Count +
                        " 张贴图，但对当前勾选的操作都不适用（CanProcess 全否），未执行任何处理。");
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
            // 中途抛异常也必须清掉进度条，否则编辑器会一直卡着一个不会消失的进度框。
            EditorUtility.ClearProgressBar();
        }

        //数据读取

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
    /// <summary>
    /// 执行全部工作
    /// </summary>
    /// <param name="pendingWork"></param>
    /// <param name="settings"></param>
    /// <param name="triggeredByImport"></param>
    /// <param name="summary"></param>
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

            //构建内层操作进度更新逻辑委托
            // 两级进度：外层是"第几个文件"，内层是操作自己上报的"这个文件处理到哪了"。
            // 二分搜索一张 8K 图要编码十几次，没有内层进度会看起来像卡死。
            System.Action<float, string> subProgress = (ratio, detail) =>
                UpdateProgressBar(pendingWork.Count, currentIndex, ratio, work, detail);

            //进度条相关
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
    /// <summary>
    /// 执行结果汇总：单文件操作情况，执行结果数据写入
    /// </summary>
    /// <param name="summary"></param>
    /// <param name="work"></param>
    /// <param name="result"></param>
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
    /// <summary>
    /// 更新进度条
    /// </summary>
    /// <param name="total"></param>
    /// <param name="index"></param>
    /// <param name="subRatio"></param>
    /// <param name="work"></param>
    /// <param name="detail"></param>
    private static void UpdateProgressBar(int total, int index, float subRatio, PendingWork work, string detail)
    {
        float progress = (index + Mathf.Clamp01(subRatio)) / total;
        EditorUtility.DisplayProgressBar(ProgressBarTitle, BuildProgressText(work, detail), progress);
    }
    /// <summary>
    /// 构建进度文本
    /// </summary>
    /// <param name="work"></param>
    /// <param name="detail">附加说明</param>
    /// <returns></returns>
    private static string BuildProgressText(PendingWork work, string detail)
    {
        string text = work.Operation.DisplayName + "：" + work.AssetPath;
        return string.IsNullOrEmpty(detail) ? text : text + "\n" + detail;
    }

    // ---------------------------------------------------------------------------
    // 3) 收尾：统一落盘 + 汇总日志
    // ---------------------------------------------------------------------------

    /// <summary>
    /// 兜底统一落盘。各操作已对改动贴图调过 ImportAsset；此处只 SaveAssets。
    /// 禁止 AssetDatabase.Refresh()：会触发 FBX 重导、冲掉同批（或总批量里）刚写入的 Mesh 顶点色。
    /// 无改动时不 Save，避免空跑。
    /// </summary>
    private static void FlushAssetDatabase(TextureOperationRunSummary summary)
    {
        if (summary.ChangedCount == 0)
        {
            return;
        }

        AssetDatabase.SaveAssets();
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

        if (summary.SkippedLines.Count > 0)
        {
            report.Add("");
            report.Add("已跳过:");
            report.AddRange(summary.SkippedLines);
        }

        // 全是跳过时用 Warning，避免用户以为"完全没跑"。
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

    /// <summary>
    /// 待处理工作的结构体数据
    /// </summary>
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
