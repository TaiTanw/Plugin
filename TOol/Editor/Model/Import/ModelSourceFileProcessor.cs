using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 模型后处理流程控制（导入期自动流 · 只服务导入区）：
//   1) OnPostprocessAllAssets → 入队 ImportPostProcessScheduler（delayCall，模型→贴图）
//   2) OnPostprocessModel → 在「每一次」模型导入结束时立刻跑 importAuto 操作
//
// 为何要有 (2)：
//   顶点色改的是 Mesh 子资产。若同一次导入还触发了贴图后处理里的 Refresh，
//   或其它原因导致 FBX 再导入，Mesh 会被源数据重建。仅靠 delayCall 一轮时，
//   再导入往往落在 Scheduler.IsRunning==true 窗口内，入队被跳过，颜色就丢了。
//   OnPostprocessModel 在每次（含重导）导入末尾执行，才能稳定留下全白顶点色。
//
// Art / excludedPathPrefixes：
//   本文件是「导入期自动流」，必须整段跳过 Art（规则 33：不改交付区 Importer，
//   也不在钩子里对 Art 跑 Op）。交付区刷白/压图走 L1 手动总批量，或中间层⑤
//   代调同一 RunMasterBatch（triggeredByImport=false），不是本钩子。
// =====================================================================================
public class ModelSourceFileProcessor : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject root)
    {
        if (!ResourceProcessSwitches.IsModelPostProcessEffective)
        {
            return;
        }

        ModelProcessSettings settings = ModelProcessSettings.Current;
        if (!settings.IsSupportedModelExtension(assetPath) || settings.IsExcludedPath(assetPath))
        {
            return;
        }

        List<IModelAssetOperation> operations = ModelOperationRegistry.GetImportAutoOperations(settings);
        if (operations.Count == 0)
        {
            return;
        }

        // 传入 root：OnPostprocessModel 时 LoadAllAssetsAtPath 常为空，必须从层级取 Mesh。
        ModelOperationRunner.Run(operations, new[] { assetPath }, settings, true, root);
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!ResourceProcessSwitches.IsModelPostProcessEffective)
        {
            return;
        }

        if (ImportPostProcessScheduler.IsRunning)
        {
            return;
        }

        ModelProcessSettings settings = ModelProcessSettings.Current;
        var queued = new List<string>();
        Collect(importedAssets, settings, queued);
        Collect(movedAssets, settings, queued);

        if (queued.Count > 0)
        {
            ImportPostProcessScheduler.EnqueueModelPaths(queued);
        }
    }

    private static void Collect(string[] assetPaths, ModelProcessSettings settings, List<string> queued)
    {
        if (assetPaths == null)
        {
            return;
        }

        foreach (string assetPath in assetPaths)
        {
            if (!settings.IsSupportedModelExtension(assetPath) || settings.IsExcludedPath(assetPath))
            {
                continue;
            }

            queued.Add(assetPath);
        }
    }
}
