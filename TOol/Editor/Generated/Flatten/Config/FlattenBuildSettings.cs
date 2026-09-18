// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 平铺到 Art — 配置（交付根、管线默认是否清单元夹）。
// 层级：L-配置。不执行拷贝 / Importer。
// =====================================================================================

/// <summary>
/// 平铺配置（人工 / 内核默认根）。编排④读 PipelineStepSettings.artRootPath，经 Options 传入。
/// </summary>
public static class FlattenBuildSettings
{
    /// <summary>
    /// 人工平铺默认交付根。编排不读此处。⑥ 无 Options.ArtRoot 时仍回落 RetinarPaths.ArtRoot。
    /// </summary>
    public const string ArtRoot = "Assets/Art";

    /// <summary>管线④默认：平铺前只删本次 <c>Art/&lt;名&gt;/</c>。菜单 Default 不清。</summary>
    public const bool PipelineClearsDestinationUnit = true;
}
