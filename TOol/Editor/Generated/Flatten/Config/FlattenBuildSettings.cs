// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 平铺到 Art — 配置（交付根、管线默认是否清单元夹）。
// 层级：L-配置。不执行拷贝 / Importer。
// =====================================================================================

/// <summary>
/// 平铺配置（常量起步；后续可改为 ScriptableObject）。
/// </summary>
public static class FlattenBuildSettings
{
    /// <summary>
    /// 交付区根。须与 <c>RetinarPaths.ArtRoot</c> 字面量一致（⑥ 仍读那一处）。
    /// </summary>
    public const string ArtRoot = "Assets/Art";

    /// <summary>管线④默认：平铺前只删本次 <c>Art/&lt;名&gt;/</c>。菜单 Default 不清。</summary>
    public const bool PipelineClearsDestinationUnit = true;
}
