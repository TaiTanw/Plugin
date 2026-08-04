using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// 模型后处理流程控制：只收集路径并交给 ImportPostProcessScheduler。
// 真正执行在 Shared 调度器里（固定阶段：模型 → 贴图）。
// =====================================================================================
public class ModelSourceFileProcessor : AssetPostprocessor
{
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
