using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Config — 内核执行闸。不引用 PipelineJobContext。
// 由 FlattenBuildService.CreateOptions(ctx, request) 填写。
// =====================================================================================

/// <summary>
/// ④传给平铺内核的选项。手动与管线使用各自的配置快照。
/// </summary>
public sealed class RetinarFlattenOptions
{
    public static readonly RetinarFlattenOptions Default = new RetinarFlattenOptions();

    /// <summary>本趟冻结的 SO 快照；分类和碰撞体只读这里。</summary>
    public FlattenOperationPolicy OperationPolicy =
        FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);

    /// <summary>
    /// 旧直接调用的纹理身份兼容开关。统一④的 ModelUnits 非空时始终为 false。
    /// </summary>
    public bool SkipDependencySplit;

    /// <summary>④按模型执行的复制计划。</summary>
    public List<FlattenModelUnit> ModelUnits = new List<FlattenModelUnit>();

    /// <summary>旧调用兼容的主文件提示；逐模型复制不读取此字段。</summary>
    public string PrimaryAssetPath;

    /// <summary>旧调用兼容的伴生列表；逐模型复制使用 ModelUnits。</summary>
    public List<string> SidecarPaths = new List<string>();

    /// <summary>
    /// 旧调用兼容的缺失列表；ModelUnits 中每个模型另有 MissingReferences。
    /// </summary>
    public List<string> MissingUris = new List<string>();

    /// <summary>
    /// true：平铺前只删本次 <c>Art/&lt;名&gt;/</c>，再按现网拷。菜单 Default 为 false（不清 Art）。
    /// 不扫整棵 <c>Assets/Art</c>。
    /// </summary>
    public bool ClearDestinationArtFolder;

    /// <summary>
    /// true：套空外壳时给内容节点叠 −90°X，把 Z-up 源摆正。
    /// 由编排从绑定行映射过来（人给的输入），本类不猜、不读文件。
    /// 规则 23 锁的是外壳根必须 Identity，内容节点带轴向旋转是 Unity 对 FBX 的既有形态。
    /// </summary>
    public bool ConvertZUpToYUp;

    /// <summary>是否在最终 Prefab 根节点添加/更新 BoxCollider。</summary>
    public bool AddBoxCollider;

    /// <summary>
    /// true：Begin 存 Art 前剥 Missing Script。false：源上仍有缺脚本则 Begin 失败。
    /// 由 policy 冻结抄入；本类不猜人工/管线。
    /// </summary>
    public bool StripMissingScripts;

    /// <summary>④ 写出根。空则 <see cref="FlattenBuildSettings.ArtRoot"/>。</summary>
    public string ArtRoot;
}
