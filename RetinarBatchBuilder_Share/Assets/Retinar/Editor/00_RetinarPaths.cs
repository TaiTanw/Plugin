// =====================================================================================
// 00 — 路径与命名常量（全插件唯一真源）
//
// 阅读顺序：先看本文件，再看 01_RetinarMenu → 10_Flatten / 20_Package →
// 根目录 RetinarBatchModelBuilder*.cs（Legacy 规范化实现，暂不拆碎）。
// =====================================================================================

/// <summary>
/// Retinar 工程内 / 工程外路径常量。新增代码请引用此处，避免再写魔法字符串。
/// Legacy <see cref="RetinarBatchModelBuilder"/> 内仍保留同名 private const，取值须与本类一致。
/// </summary>
public static class RetinarPaths
{
    /// <summary>平铺与规范化工作区根目录。</summary>
    public const string ArtRoot = "Assets/Art";

    /// <summary>工程根下的对外交付目录名。</summary>
    public const string DeliverableRoot = "Deliverables";

    /// <summary>工程根下 BuildPipeline 原始 AB 输出目录名（再拷到 Deliverables）。</summary>
    public const string AssetBundleRoot = "AssetBundles";

    /// <summary>与历史交付一致的 AssetBundle Variant（文件名形如 name.assetbundle）。</summary>
    public const string AssetBundleVariant = "assetbundle";

    public const string DeliverableUnityFolder = "02_unity";
    public const string DeliverableAssetBundlesFolder = "03_assetbundles";
    public const string PlatformAndroid = "Android";
    public const string PlatformIOS = "iOS";
}
