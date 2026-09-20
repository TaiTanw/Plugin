// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 平铺到 Art — 配置（交付根、管线默认是否清单元夹）。
// 层级：L-配置。不执行拷贝 / Importer。
// =====================================================================================

/// <summary>
/// 平铺配置。导入钩子认 Art 钉死 FlattenBuildSettings.ArtRoot。
/// 人工④写出根读 BatchFbxImportSettings.artRootPath；编排④读 PipelineStepSettings.artRootPath。
/// </summary>
public static class FlattenBuildSettings
{
    /// <summary>
    /// 导入钩子认交付区、以及 plan.ArtRoot 为空时的回落。人工④写出根读选择器 SO artRootPath。
    /// 编排④读 PipelineStepSettings.artRootPath，经 Options 传入。
    /// </summary>
    public const string ArtRoot = "Assets/Art";

    /// <summary>管线④默认：平铺前只删本次 <c>Art/&lt;名&gt;/</c>。菜单 Default 不清。</summary>
    public const bool PipelineClearsDestinationUnit = true;
}
