using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 一趟一行的冻结执行单。内核 Run(plan) 只读本对象，不读 ctx。
// 步骤 1：类型 + 七步转调。FromContext / 编排换口是步骤 2。
// =====================================================================================

/// <summary>B 按后缀拆，还是 B′ 原子搬迁。由编排写入，不猜后缀。</summary>
public enum FlattenBranch
{
    SplitDependencies = 0,
    RelocateAtomic = 1
}

/// <summary>
/// 一行平铺的冻结入参。字段与现网 CreateOptions(ctx)+Request 对齐，不增加新行为。
/// </summary>
public sealed class FlattenPlan
{
    /// <summary>IncomingPrefab（或已在 Art 内的 Prefab）路径。</summary>
    public string SourcePrefabPath;

    /// <summary>B 或 B′。互斥。</summary>
    public FlattenBranch Branch = FlattenBranch.SplitDependencies;

    /// <summary>是否跑 Art ModelImporter 设置 + Extract。false 时跳过 E（与 ScriptedImporter 现网跳过相同）。</summary>
    public bool ApplyArtModelImporter = true;

    /// <summary>分类、清夹、碰撞体等本趟 SO 快照。</summary>
    public FlattenOperationPolicy OperationPolicy;

    /// <summary>true：平铺前只删本次 <c>Art/&lt;名&gt;/</c>。</summary>
    public bool ClearDestinationArtFolder;

    /// <summary>true：套空外壳时给内容节点叠 −90°X。</summary>
    public bool ConvertZUpToYUp;

    /// <summary>B′ 主模型 Assets 路径。B 可空。</summary>
    public string PrimaryAssetPath;

    /// <summary>B′ 相对主文件的伴生。B 可空。</summary>
    public List<string> SidecarPaths = new List<string>();

    /// <summary>glTF 已声明但不存在的必需伴生。非空时 Run 在 Begin 前失败（与编排现网闸相同）。</summary>
    public List<string> MissingUris = new List<string>();

    /// <summary>④ 写出根。空则内核用 FlattenBuildSettings.ArtRoot。</summary>
    public string ArtRoot;
}
