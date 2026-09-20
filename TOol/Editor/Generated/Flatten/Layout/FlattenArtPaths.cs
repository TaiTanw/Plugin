// =====================================================================================
// Generated / Flatten / Layout
// 中文：④ 交付根路径（只拼，不写盘）。单元内 Prefab/Model/… 见 FlattenLayout。
// =====================================================================================

/// <summary>Art 根与单元夹。夹名与 ③ Prefab / Incoming ID2 同一套。
/// <see cref="ArtRoot"/> 钉死常量（导入跳过 Art）。人工④在 plan 上覆盖为选择器交付根。</summary>
public static class FlattenArtPaths
{
    public static string ArtRoot
    {
        get { return FlattenBuildSettings.ArtRoot.Replace("\\", "/").TrimEnd('/'); }
    }

    /// <summary>编排传入非空则用之；空则钉死 <see cref="FlattenBuildSettings.ArtRoot"/>（导入跳过 Art）。</summary>
    public static string ResolveOverride(string overrideRoot)
    {
        if (string.IsNullOrWhiteSpace(overrideRoot))
        {
            return ArtRoot;
        }

        return overrideRoot.Replace("\\", "/").TrimEnd('/');
    }

    /// <summary><c>Art/&lt;名&gt;</c>。人工默认根。</summary>
    public static string UnitFolder(string assetName)
    {
        string name = (assetName ?? string.Empty).Replace("\\", "/").Trim('/');
        return ArtRoot + "/" + name;
    }
}
