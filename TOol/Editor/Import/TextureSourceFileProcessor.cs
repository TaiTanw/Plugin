using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// 职责边界（这个类是本次重构的核心，改动前请先读完）：
//   它【只做流程控制】——收集这一批导入进来的贴图路径、把它们排进队列、
//   在合适的时机交给 TextureOperationRunner 执行。
//   它不解码任何图片、不判断体积、不缩放、不写文件。所有实际动作都在
//   Operations/ 层的操作实现里，那些操作同时也能被"贴图处理工具"窗口手动调用，
//   两条路径共用同一份代码，行为完全一致。
//
// 为什么选 OnPostprocessAllAssets 作为触发点：
//   OnPreprocessTexture / OnPostprocessTexture 是"每个资产各自"的回调，触发时
//   Unity 正走在这个资产自己的导入流程中间。在那里改写它自己的源文件字节，
//   等于在导入过程中把输入换掉，同一次导入的前后状态会不一致。
//   OnPostprocessAllAssets 是静态回调，在这一批资产【全部】导入结束后统一触发一次，
//   天然适合"导入结束后再回头处理源文件"。
//
// 为什么还要再用 EditorApplication.delayCall 推迟一次（关键）：
//   即使是 OnPostprocessAllAssets，它仍然处于 Unity 这一轮导入流程的调用栈里。
//   压缩完成后必须调 AssetDatabase.ImportAsset 让新字节生效，而在导入回调的栈上
//   再发起导入就是嵌套导入：Unity 内部的导入状态机会被重入，表现为偶发的
//   "压缩了但编辑器里还是旧图""同一个文件被处理两次""导入卡住"。
//   delayCall 会把回调推到下一次编辑器 tick，那时这一轮导入已经彻底结束、
//   AssetDatabase 处于空闲可写状态，再做源文件改写和重新导入就是安全的。
//   这也是把"收集"和"执行"分成两个阶段的根本原因。
//
// 关于死循环：
//   处理完会强制重新导入，于是 OnPostprocessAllAssets 会再触发一次。但那时
//   源文件已经达标，各操作的 CanProcess 会直接返回 false，不会有任何工作被排队。
//   另外 isRunning 这个标记会让"我们自己发起的那次重新导入"完全不参与排队，
//   相当于加了第二道保险。
// =====================================================================================
public class TextureSourceFileProcessor : AssetPostprocessor
{
    private static readonly HashSet<string> pendingAssetPaths = new HashSet<string>();
    private static bool deferredRunScheduled;
    private static bool isRunning;

    /// <summary>
    /// 静态方法名和签名是 Unity 约定好的固定写法，不需要手动注册，
    /// 只要类继承 AssetPostprocessor 并放在 Editor 目录下就会被自动调用。
    ///
    /// 总体流程：开关关闭直接退出 → 把这一批里可能需要处理的路径排进队列 →
    /// 安排一次延迟执行（真正的处理在下一个编辑器 tick 才发生）。
    /// </summary>
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!AssetProcessSwitch.IsEnabled)
        {
            return;
        }

        // 这一次导入是我们自己刚刚触发的，它的结果不需要再排队，否则等于自己追自己。
        if (isRunning)
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.Current;
        int queuedCount = QueueCandidates(importedAssets, settings);

        // 移动过的文件内容没变，但路径变了。原来的实现完全不看 movedAssets，
        // 于是"把一张超标的贴图从别处挪进工程目录"这种操作不会触发任何处理。
        // 这也是之前"只是移动了文件位置，行为就变了"这类问题的一个来源。
        queuedCount += QueueCandidates(movedAssets, settings);

        if (queuedCount > 0)
        {
            ScheduleDeferredRun();
        }
    }

    /// <summary>
    /// 只做最便宜的两层过滤：有没有对应的编解码器、是不是在"不介入"的目录里。
    /// 真正"要不要处理"的判断交给每个操作的 CanProcess，在延迟执行阶段才做。
    ///
    /// 注意目录排除只作用于导入自动处理这条路径。在"贴图处理工具"窗口里手动选中
    /// 某个文件去执行，是明确的用户意图，不受这里限制——那个窗口存在的意义就是
    /// 让你能在需要的时候越过自动规则手动处理。
    /// </summary>
    private static int QueueCandidates(string[] assetPaths, TextureProcessSettings settings)
    {
        if (assetPaths == null)
        {
            return 0;
        }

        int queuedCount = 0;
        foreach (string assetPath in assetPaths)
        {
            if (!TextureCodecRegistry.IsSupported(assetPath) || settings.IsExcludedPath(assetPath))
            {
                continue;
            }

            // <FBX名>.fbm 是 Unity 的内嵌媒体抽取缓存，模型每次重新导入都会照原始数据覆盖回去。
            // 自动流程改它纯属白做，还会在被还原之前先让材质掉一半清晰度，所以直接不排队。
            if (TextureAssetPathUtility.IsInsideEmbeddedMediaFolder(assetPath))
            {
                continue;
            }

            if (pendingAssetPaths.Add(assetPath))
            {
                queuedCount++;
            }
        }

        return queuedCount;
    }

    private static void ScheduleDeferredRun()
    {
        // 一批导入可能触发多次 OnPostprocessAllAssets，但只需要安排一次延迟执行，
        // 队列会把这几次的路径合并起来一起处理。
        if (deferredRunScheduled)
        {
            return;
        }

        deferredRunScheduled = true;
        EditorApplication.delayCall += RunPendingWork;
    }

    /// <summary>
    /// 已经脱离导入调用栈，这里可以安全地读写文件、调 ImportAsset。
    /// </summary>
    private static void RunPendingWork()
    {
        deferredRunScheduled = false;

        var assetPaths = new List<string>(pendingAssetPaths);
        pendingAssetPaths.Clear();

        // 从排队到执行之间隔了一个 tick，用户可能刚好在这期间关掉了开关。
        if (!AssetProcessSwitch.IsEnabled || assetPaths.Count == 0)
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.Current;
        List<ITextureAssetOperation> operations = TextureOperationRegistry.GetImportAutoOperations(settings);
        if (operations.Count == 0)
        {
            return;
        }

        isRunning = true;
        try
        {
            TextureOperationRunner.Run(operations, assetPaths, settings, true);
        }
        finally
        {
            // Runner 内部的 ImportAsset 是同步的，所以它引发的
            // OnPostprocessAllAssets 一定在 isRunning 还是 true 的时候就已经跑完了。
            isRunning = false;
        }
    }
}
