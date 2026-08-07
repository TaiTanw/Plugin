// =====================================================================================
// 这是整套贴图处理工具的【扩展点】。以后所有"对贴图资产做一件事"的需求都走这里，
// 不要再新写一个独立的 AssetPostprocessor 或独立的菜单项。
//
// 新增一个操作的完整步骤：
//   1) 在 Operations 目录下建一个类，实现 ITextureAssetOperation，带无参构造函数；
//   2) 什么都不用注册——TextureOperationRegistry 会用反射自动发现它，
//      它会立刻出现在"贴图处理工具"窗口的操作列表里，可以手动执行；
//   3) 如果这个操作还需要在导入时自动跑，把它的 Id 填进
//      TextureProcessSettings.importAutoOperationIds（窗口里有勾选框）。
//
// 职责约定（务必遵守，否则就回到了"一个方法里啥都干"的老问题）：
//   - 具体的文件读写、编解码、缩放，全部通过 Codec 层完成，不要在操作里手写格式解析；
//   - 阈值、质量这类数值一律从 context.Settings 读，不要在操作里写死常量；
//   - 不要在操作里弹窗、不要在操作里刷进度条（进度交给 context.ReportSubProgress，
//     弹窗和汇总交给 TextureOperationRunner），这样同一个操作既能被窗口调用，
//     也能被导入回调调用，行为完全一致。
//   - Evaluate：扫描与 Runner 筛选用；CanProcess 应委托 Evaluate.NeedsWork，保持单一口径。
// =====================================================================================
/// <summary>
/// 图片处理器行为
/// </summary>
public interface ITextureAssetOperation
{
    /// <summary>
    /// 稳定的字符串标识，会被写进配置资产（importAutoOperationIds）和 EditorPrefs。
    /// 一旦发布就不要再改，否则用户已有的勾选状态会失效。
    /// </summary>
    string Id { get; }

    /// <summary>窗口里显示的操作名。</summary>
    string DisplayName { get; }

    /// <summary>窗口里显示的说明，写清楚它会改动什么、有什么代价。</summary>
    string Description { get; }

    /// <summary>窗口列表和批量执行的排序权重，小的在前。</summary>
    int Order { get; }

    /// <summary>
    /// 统一评估：是否适用、是否需要改。扫描 dry-run 与 Runner 收集待处理项都走这里。
    /// 预筛应尽量便宜；需要读像素/解码的探测也写在本方法内（由实现决定深度）。
    /// </summary>
    AssetOperationEvaluation Evaluate(string assetPath, TextureProcessSettings settings);

    /// <summary>
    /// 兼容旧调用：等价于 Evaluate(...).NeedsWork。
    /// </summary>
    bool CanProcess(string assetPath, TextureProcessSettings settings);

    /// <summary>真正干活。不要抛异常，用 TextureOperationResult.Failed 表达失败。</summary>
    TextureOperationResult Execute(TextureOperationContext context);
}
