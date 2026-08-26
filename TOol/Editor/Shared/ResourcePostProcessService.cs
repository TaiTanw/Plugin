using System.Collections.Generic;
using System.Text;
using UnityEngine;

// =====================================================================================
// Shared — ⑤ 资源后处理执行核（供 L1 面板与 Api 共用）
// =====================================================================================

/// <summary>
/// L1 子流程内核：按批量路径跑贴图→材质→模型主批量 Op（集合来自 L3）。
/// </summary>
public static class ResourcePostProcessService
{
    /// <summary>
    /// 执行总批量。folderPaths 为 null 时用 L1 Store 当前有效路径。
    /// 顺序：贴图 → 材质 → 模型（材质烤 Shader 宜在出包前；压图与烤正交）。
    /// </summary>
    public static string RunMasterBatch(
        IList<string> folderPaths = null,
        bool? includeTexture = null,
        bool? includeModel = null,
        bool? includeMaterial = null)
    {
        bool runTex = includeTexture ?? ResourceProcessSwitches.MasterBatchIncludeTexture;
        bool runMat = includeMaterial ?? ResourceProcessSwitches.MasterBatchIncludeMaterial;
        bool runModel = includeModel ?? ResourceProcessSwitches.MasterBatchIncludeModel;

        List<string> folders = folderPaths != null
            ? NormalizeFolders(folderPaths)
            : ResourceBatchFolderStore.GetValidMasterFolders();

        var report = new StringBuilder();
        report.AppendLine("[总批量] 开始（贴图→材质→模型）");

        if (runTex)
        {
            if (folders.Count == 0)
            {
                string warn = "[总批量] 贴图已纳入，但批量路径为空，已跳过。";
                Debug.LogWarning(warn);
                report.AppendLine(warn);
            }
            else
            {
                report.AppendLine(RunTextureBatch(folders));
            }
        }
        else
        {
            report.AppendLine("[总批量] 已跳过贴图（未纳入）。");
        }

        if (runMat)
        {
            if (folders.Count == 0)
            {
                string warn = "[总批量] 材质已纳入，但批量路径为空，已跳过。";
                Debug.LogWarning(warn);
                report.AppendLine(warn);
            }
            else
            {
                report.AppendLine(RunMaterialBatch(folders));
            }
        }
        else
        {
            report.AppendLine("[总批量] 已跳过材质（未纳入）。");
        }

        if (runModel)
        {
            if (folders.Count == 0)
            {
                string warn = "[总批量] 模型已纳入，但批量路径为空，已跳过。";
                Debug.LogWarning(warn);
                report.AppendLine(warn);
            }
            else
            {
                report.AppendLine(RunModelBatch(folders));
            }
        }
        else
        {
            report.AppendLine("[总批量] 已跳过模型（未纳入）。");
        }

        return report.ToString().TrimEnd();
    }

    private static List<string> NormalizeFolders(IList<string> folderPaths)
    {
        var list = new List<string>();
        if (folderPaths == null)
        {
            return list;
        }

        for (int i = 0; i < folderPaths.Count; i++)
        {
            string p = (folderPaths[i] ?? string.Empty).Replace("\\", "/").TrimEnd('/');
            if (!string.IsNullOrEmpty(p) && !list.Contains(p))
            {
                list.Add(p);
            }
        }

        return list;
    }

    private static string RunTextureBatch(IList<string> folders)
    {
        List<string> targets = TextureTargetCollector.Collect(
            TextureTargetCollector.Scope.BatchByPath, null, folders);
        TextureProcessSettings settings = TextureProcessSettings.GetOrCreateAsset();
        List<ITextureAssetOperation> operations = TextureOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[贴图] 高级设置中未勾选任何「主面板批量包含」操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[贴图] 批量路径下没有命中贴图。";
            Debug.LogWarning(msg);
            return msg;
        }

        TextureOperationRunSummary summary = TextureOperationRunner.Run(operations, targets, settings, false);
        return "[贴图] 批量完成：目标 " + targets.Count +
               "，改动 " + summary.ChangedCount +
               "，跳过 " + summary.SkippedCount +
               "，失败 " + summary.FailedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
    }

    private static string RunMaterialBatch(IList<string> folders)
    {
        List<string> targets = MaterialTargetCollector.CollectFromFolders(folders);
        MaterialProcessSettings settings = MaterialProcessSettings.GetOrCreateAsset();
        List<IMaterialAssetOperation> operations =
            MaterialOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[材质] 配置中未勾选任何主批量操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[材质] 批量路径下没有命中 .mat。";
            Debug.LogWarning(msg);
            return msg;
        }

        MaterialOperationRunSummary summary = MaterialOperationRunner.Run(operations, targets, settings);
        return "[材质] 批量完成：目标 " + targets.Count +
               "，改动 " + summary.ChangedCount +
               "，跳过 " + summary.SkippedCount +
               "，失败 " + summary.FailedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
    }

    private static string RunModelBatch(IList<string> folders)
    {
        List<string> targets = ModelTargetCollector.Collect(
            ModelTargetCollector.Scope.BatchByPath, null, folders);
        ModelProcessSettings settings = ModelProcessSettings.GetOrCreateAsset();
        List<IModelAssetOperation> operations = ModelOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[模型] 高级设置中未勾选任何「主面板批量包含」操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[模型] 批量路径下没有命中模型。";
            Debug.LogWarning(msg);
            return msg;
        }

        ModelOperationRunSummary summary = ModelOperationRunner.Run(operations, targets, settings, false);
        return "[模型] 批量完成：目标 " + targets.Count +
               "，改动 " + summary.ChangedCount +
               "，跳过 " + summary.SkippedCount +
               "，失败 " + summary.FailedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
    }
}
