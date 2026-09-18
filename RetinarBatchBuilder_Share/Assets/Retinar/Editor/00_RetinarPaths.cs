// =====================================================================================
// 00 — 路径与命名常量（全插件唯一真源）
//
// 阅读顺序：先看本文件，再看 01_RetinarMenu / 20_Package / 40_Api（⑥）。
// ④ 平铺在插件 2 TOol/Editor/Generated/Flatten/。
// =====================================================================================

/// <summary>
/// Retinar 工程内 / 工程外路径常量。新增代码请引用此处，避免再写魔法字符串。
/// Legacy 平铺内核已迁插件 2。⑥ 无 Options.ArtRoot 时回落本常量；编排传入则用之。
/// </summary>
public static class RetinarPaths
{
    /// <summary>⑥ 无 override 时的 Art 前缀。编排真源在 PipelineStepSettings.artRootPath。</summary>
    public const string ArtRoot = "Assets/Art";

    /// <summary>工程根下的对外交付目录名。</summary>
    public const string DeliverableRoot = "Deliverables";

    /// <summary>工程根下对外 AB 目录。产品文件平铺于此：name_android.assetbundle / name_ios.assetbundle。</summary>
    public const string AssetBundleRoot = "AssetBundles";

    /// <summary>文件后缀 variant。产品名是 stem_android.assetbundle，不是平台夹。</summary>
    public const string AssetBundleVariant = "assetbundle";

    /// <summary>磁盘文件夹名（可改字面量），不得把语义改成别的产物。</summary>
    public const string DeliverableRuntimeFolder = "00_runtime_requirements";
    public const string DeliverableSourceFolder = "01_source";
    public const string DeliverableUnityFolder = "02_unity";
    public const string DeliverableAssetBundlesFolder = "03_assetbundles";
    public const string DeliverableDocsFolder = "06_docs";
    public const string DeliverableDiagnosticsFolder = "_diagnostics";
}
