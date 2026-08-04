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
    /// 快速判断这个资产要不要处理。必须足够便宜——导入回调会对一整批资产逐个调用它，
    /// 所以这里只允许看扩展名、看文件体积这类不需要解码的信息。
    /// </summary>
    bool CanProcess(string assetPath, TextureProcessSettings settings);

    /// <summary>真正干活。不要抛异常，用 TextureOperationResult.Failed 表达失败。</summary>
    TextureOperationResult Execute(TextureOperationContext context);
}
