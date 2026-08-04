using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// 职责边界：
//   Shared 层。导入后处理的【唯一】延迟执行入口。
//   贴图 / 模型的 SourceFileProcessor 只负责把路径排进这里；真正跑 Operation 的顺序
//   固定为：模型阶段 → 贴图阶段（为以后材质驱动贴图派生预留，v1 不做拖拽排序）。
//
// delayCall 原因与贴图侧相同：脱离 OnPostprocessAllAssets 的导入调用栈，避免嵌套导入。
// =====================================================================================
public static class ImportPostProcessScheduler
{
    private static readonly HashSet<string> pendingModelPaths = new HashSet<string>();
    private static readonly HashSet<string> pendingTexturePaths = new HashSet<string>();
    private static bool deferredRunScheduled;
    private static bool isRunning;

    /// <summary>后处理正在执行时为 true；SourceFileProcessor 用它忽略自触发的再导入。</summary>
    public static bool IsRunning
    {
        get { return isRunning; }
    }

    public static void EnqueueModelPaths(IEnumerable<string> assetPaths)
    {
        if (!EnqueueInto(pendingModelPaths, assetPaths))
        {
            return;
        }

        ScheduleDeferredRun();
    }

    public static void EnqueueTexturePaths(IEnumerable<string> assetPaths)
    {
        if (!EnqueueInto(pendingTexturePaths, assetPaths))
        {
            return;
        }

        ScheduleDeferredRun();
    }

    private static bool EnqueueInto(HashSet<string> bucket, IEnumerable<string> assetPaths)
    {
        if (assetPaths == null)
        {
            return false;
        }

        bool added = false;
        foreach (string path in assetPaths)
        {
            if (!string.IsNullOrEmpty(path) && bucket.Add(path))
            {
                added = true;
            }
        }

        return added;
    }

    private static void ScheduleDeferredRun()
    {
        if (deferredRunScheduled)
        {
            return;
        }

        deferredRunScheduled = true;
        EditorApplication.delayCall += RunPendingWork;
    }

    private static void RunPendingWork()
    {
        deferredRunScheduled = false;

        var modelPaths = new List<string>(pendingModelPaths);
        var texturePaths = new List<string>(pendingTexturePaths);
        pendingModelPaths.Clear();
        pendingTexturePaths.Clear();

        if (modelPaths.Count == 0 && texturePaths.Count == 0)
        {
            return;
        }

        isRunning = true;
        try
        {
            RunModelPhase(modelPaths);
            RunTexturePhase(texturePaths);
        }
        finally
        {
            isRunning = false;
        }
    }

    private static void RunModelPhase(List<string> modelPaths)
    {
        if (!ResourceProcessSwitches.IsModelPostProcessEffective || modelPaths.Count == 0)
        {
            return;
        }

        ModelProcessSettings settings = ModelProcessSettings.Current;
        List<IModelAssetOperation> operations = ModelOperationRegistry.GetImportAutoOperations(settings);
        if (operations.Count == 0)
        {
            return;
        }

        ModelOperationRunner.Run(operations, modelPaths, settings, true);
    }

    private static void RunTexturePhase(List<string> texturePaths)
    {
        if (!ResourceProcessSwitches.IsTexturePostProcessEffective || texturePaths.Count == 0)
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.Current;
        List<ITextureAssetOperation> operations = TextureOperationRegistry.GetImportAutoOperations(settings);
        if (operations.Count == 0)
        {
            return;
        }

        TextureOperationRunner.Run(operations, texturePaths, settings, true);
    }
}
