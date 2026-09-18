// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 人给的输入 / 编排目的。不是 ctx（ctx 只记可验证的事实）。
// =====================================================================================

/// <summary>
/// 平铺请求。轴向、是否清 Art 单元夹都属于「这趟想做什么」，不进
/// <see cref="PipelineJobContext"/>。
/// </summary>
public sealed class ToolFlattenRequest
{
    /// <summary>菜单 / 无编排：不清 Art，不转轴向。</summary>
    public static readonly ToolFlattenRequest MenuDefault = new ToolFlattenRequest();

    /// <summary>管线④：清本次单元夹；轴向由调用方再写。</summary>
    public static ToolFlattenRequest ForPipeline(FlattenOperationPolicy operationPolicy = null)
    {
        operationPolicy = operationPolicy ??
                          FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Pipeline);
        return new ToolFlattenRequest
        {
            OperationPolicy = operationPolicy,
            ClearDestinationArtFolder = operationPolicy.ClearDestinationArtFolder,
            ConvertZUpToYUp = false
        };
    }

    /// <summary>人工操作：行为来自拖入的手动/管线 SO 快照。</summary>
    public static ToolFlattenRequest ForManual(FlattenOperationPolicy operationPolicy)
    {
        operationPolicy = operationPolicy ??
                          FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);
        return new ToolFlattenRequest
        {
            OperationPolicy = operationPolicy,
            ClearDestinationArtFolder = operationPolicy.ClearDestinationArtFolder,
            ConvertZUpToYUp = false
        };
    }

    /// <summary>分类、清夹和碰撞体等本趟 SO 快照。</summary>
    public FlattenOperationPolicy OperationPolicy;

    /// <summary>true：平铺前只删本次 <c>Art/&lt;名&gt;/</c>，不扫整棵 Art。</summary>
    public bool ClearDestinationArtFolder;

    /// <summary>
    /// true：套空外壳时给内容节点叠 −90°X。人给的输入，不猜文件。
    /// </summary>
    public bool ConvertZUpToYUp;

    /// <summary>④ 写出根。空则内核用 FlattenBuildSettings.ArtRoot。</summary>
    public string ArtRoot;
}
