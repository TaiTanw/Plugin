using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Config — 内核执行闸。不引用 PipelineJobContext。
// 由 FlattenBuildService.CreateOptions(ctx, request) 填写。
// =====================================================================================

/// <summary>
/// 管线④传给平铺内核的选项。默认与菜单行为一致（按后缀拆依赖）。
/// </summary>
public sealed class RetinarFlattenOptions
{
    public static readonly RetinarFlattenOptions Default = new RetinarFlattenOptions();

    /// <summary>本趟冻结的 SO 快照；分类和碰撞体只读这里。</summary>
    public FlattenOperationPolicy OperationPolicy =
        FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);

    /// <summary>
    /// true：整段不跑按后缀拆依赖（拷贝循环）。改走 B′ 原子搬迁。
    /// 由 FlattenBuildService 把 ctx.HasExternalUris 映射过来，本类不读 ctx。
    /// </summary>
    public bool SkipDependencySplit;

    /// <summary>主模型 Assets 路径（B′ 原子树根文件）。菜单 Default 为空。</summary>
    public string PrimaryAssetPath;

    /// <summary>相对主文件的伴生（.bin / 外图等）。菜单 Default 为空。</summary>
    public List<string> SidecarPaths = new List<string>();

    /// <summary>
    /// glTF 已声明但 2.5 探测时不存在的必需伴生。B′ 必须拒绝执行，不能只拷主文件后报成功。
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
