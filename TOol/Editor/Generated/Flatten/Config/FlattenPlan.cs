using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 一趟一行的冻结执行单。内核 Run(plan) 只读本对象，不读 ctx。
// 步骤 1：类型 + 七步转调。FromContext / 编排换口是步骤 2。
// =====================================================================================

/// <summary>旧整行分支标记；统一④已改用每个模型的策略。</summary>
public enum FlattenBranch
{
    SplitDependencies = 0,
    RelocateAtomic = 1
}

/// <summary>一个模型依赖在④中的复制方式。</summary>
public enum FlattenModelCopyStrategy
{
    Categorized = 0,
    PreserveRelativeFiles = 1
}

/// <summary>每个模型独立的冻结复制计划；需保留相对路径的文件属于该模型。</summary>
public sealed class FlattenModelUnit
{
    public string ModelPath;
    public FlattenModelCopyStrategy Strategy;
    public List<string> SidecarPaths = new List<string>();
    public List<string> MissingReferences = new List<string>();
}

/// <summary>
/// 一行平铺的冻结入参；自动线与手动端在各自入口生成它。
/// </summary>
public sealed class FlattenPlan
{
    /// <summary>IncomingPrefab（或已在 Art 内的 Prefab）路径。</summary>
    public string SourcePrefabPath;

    /// <summary>旧调用兼容字段；④执行由 ModelUnits 决定，不再按整行二选一。</summary>
    public FlattenBranch Branch = FlattenBranch.SplitDependencies;

    /// <summary>Prefab 中每个模型的复制策略。空表由 Run 从 Prefab 依赖补建。</summary>
    public List<FlattenModelUnit> ModelUnits = new List<FlattenModelUnit>();

    /// <summary>是否跑 Art ModelImporter 设置 + Extract。false 时跳过 E（与 ScriptedImporter 现网跳过相同）。</summary>
    public bool ApplyArtModelImporter = true;

    /// <summary>分类、清夹、碰撞体等本趟 SO 快照。</summary>
    public FlattenOperationPolicy OperationPolicy;

    /// <summary>true：平铺前只删本次 <c>Art/&lt;名&gt;/</c>。</summary>
    public bool ClearDestinationArtFolder;

    /// <summary>true：套空外壳时给内容节点叠 −90°X。</summary>
    public bool ConvertZUpToYUp;

    /// <summary>旧调用兼容的主文件提示；Run 仅在补建 ModelUnits 时使用。</summary>
    public string PrimaryAssetPath;

    /// <summary>旧调用兼容的伴生列表；统一④按 ModelUnits 处理。</summary>
    public List<string> SidecarPaths = new List<string>();

    /// <summary>glTF 已声明但不存在的必需伴生。非空时 Run 在 Begin 前失败（与编排现网闸相同）。</summary>
    public List<string> MissingUris = new List<string>();

    /// <summary>④ 写出根。空则内核用 FlattenBuildSettings.ArtRoot。</summary>
    public string ArtRoot;
}
