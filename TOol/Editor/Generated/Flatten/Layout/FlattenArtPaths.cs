// =====================================================================================
// Generated / Flatten / Layout
// 中文：④ 交付根路径（只拼，不写盘）。单元内 Prefab/Model/… 见 FlattenLayout。
// =====================================================================================

/// <summary>Art 根与单元夹。夹名与 ③ Prefab / Incoming ID2 同一套。</summary>
public static class FlattenArtPaths
{
    public static string ArtRoot
    {
        get { return FlattenBuildSettings.ArtRoot.Replace("\\", "/").TrimEnd('/'); }
    }

    /// <summary><c>Assets/Art/&lt;名&gt;</c>。</summary>
    public static string UnitFolder(string assetName)
    {
        string name = (assetName ?? string.Empty).Replace("\\", "/").Trim('/');
        return ArtRoot + "/" + name;
    }
}
