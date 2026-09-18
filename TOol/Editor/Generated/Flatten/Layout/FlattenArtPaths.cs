// =====================================================================================
// Generated / Flatten / Layout
// 中文：④ 交付根路径（只拼，不写盘）。单元内 Prefab/Model/… 见 FlattenLayout。
// =====================================================================================

/// <summary>Art 根与单元夹。夹名与 ③ Prefab / Incoming ID2 同一套。人工默认读 FlattenBuildSettings。</summary>
public static class FlattenArtPaths
{
    public static string ArtRoot
    {
        get { return FlattenBuildSettings.ArtRoot.Replace("\\", "/").TrimEnd('/'); }
    }

    /// <summary>编排传入非空则用之；否则人工默认根。</summary>
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
