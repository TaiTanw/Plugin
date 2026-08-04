using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// 贴图后处理流程控制：只收集路径，交给 ImportPostProcessScheduler（模型之后执行）。
// =====================================================================================
public class TextureSourceFileProcessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!ResourceProcessSwitches.IsTexturePostProcessEffective)
        {
            return;
        }

        if (ImportPostProcessScheduler.IsRunning)
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.Current;
        var queued = new List<string>();
        Collect(importedAssets, settings, queued);
        Collect(movedAssets, settings, queued);

        if (queued.Count > 0)
        {
            ImportPostProcessScheduler.EnqueueTexturePaths(queued);
        }
    }

    private static void Collect(string[] assetPaths, TextureProcessSettings settings, List<string> queued)
    {
        if (assetPaths == null)
        {
            return;
        }

        foreach (string assetPath in assetPaths)
        {
            if (!TextureCodecRegistry.IsSupported(assetPath) || settings.IsExcludedPath(assetPath))
            {
                continue;
            }

            if (AssetPathUtility.IsInsideEmbeddedMediaFolder(assetPath))
            {
                continue;
            }

            queued.Add(assetPath);
        }
    }
}
