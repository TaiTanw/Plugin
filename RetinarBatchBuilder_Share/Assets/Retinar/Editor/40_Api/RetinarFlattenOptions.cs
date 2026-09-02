using System.Collections.Generic;

// =====================================================================================
// 40_Api — ④ 平铺执行开关（目的/能力闸）。不引用 PipelineJobContext。
// =====================================================================================

/// <summary>
/// 管线④传给平铺内核的选项。默认与菜单行为一致（按后缀拆依赖）。
/// </summary>
public sealed class RetinarFlattenOptions
{
    public static readonly RetinarFlattenOptions Default = new RetinarFlattenOptions();

    /// <summary>
    /// true：整段不跑按后缀拆依赖（拷贝循环）。改走 B′ 原子搬迁。
    /// 由编排把「有外 URI」映射过来，本类不读 ctx。
    /// </summary>
    public bool SkipDependencySplit;

    /// <summary>主模型 Assets 路径（B′ 原子树根文件）。菜单 Default 为空。</summary>
    public string PrimaryAssetPath;

    /// <summary>相对主文件的伴生（.bin / 外图等）。菜单 Default 为空。</summary>
    public List<string> SidecarPaths = new List<string>();
}
