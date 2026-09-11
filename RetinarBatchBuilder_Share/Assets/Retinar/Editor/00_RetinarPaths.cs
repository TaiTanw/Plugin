// =====================================================================================
// 00 — 路径与命名常量（全插件唯一真源）
//
// 阅读顺序：先看本文件，再看 01_RetinarMenu / 20_Package / 40_Api（⑥）。
// ④ 平铺在插件 2 TOol/Editor/Generated/Flatten/。
// =====================================================================================

/// <summary>
/// Retinar 工程内 / 工程外路径常量。新增代码请引用此处，避免再写魔法字符串。
/// Legacy 平铺内核已迁插件 2；<see cref="ArtRoot"/> 须与 <c>FlattenBuildSettings.ArtRoot</c> 同字面量（⑥ 仍读本类）。
/// </summary>
public static class RetinarPaths
{
    /// <summary>平铺与规范化工作区根目录。须与 FlattenBuildSettings.ArtRoot 一致。</summary>
    public const string ArtRoot = "Assets/Art";

    /// <summary>工程根下的对外交付目录名。</summary>
    public const string DeliverableRoot = "Deliverables";

    /// <summary>工程根下 BuildPipeline 原始 AB 输出目录名（再拷到 Deliverables）。</summary>
    public const string AssetBundleRoot = "AssetBundles";

    /// <summary>与历史交付一致的 AssetBundle Variant（文件名形如 name.assetbundle）。</summary>
    public const string AssetBundleVariant = "assetbundle";

    /// <summary>磁盘文件夹名（可改字面量），不得把语义改成别的产物。</summary>
    public const string DeliverableRuntimeFolder = "00_runtime_requirements";
    public const string DeliverableSourceFolder = "01_source";
    public const string DeliverableUnityFolder = "02_unity";
    public const string DeliverableAssetBundlesFolder = "03_assetbundles";
    public const string DeliverableDocsFolder = "06_docs";
    public const string DeliverableDiagnosticsFolder = "_diagnostics";
    public const string PlatformAndroid = "Android";
    public const string PlatformIOS = "iOS";
}
