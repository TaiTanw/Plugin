using System.Collections.Generic;

// =====================================================================================
// Shared / Api — ④ 平铺窄口（对齐 ToolPrefabApi）
//
// 中间层只调本类。分步与现网一致；逻辑分支读 ctx（事实），人给的输入走 ToolFlattenRequest。
// =====================================================================================

/// <summary>插件 2 · 平铺到 Art 对外接口。</summary>
public static class ToolFlattenApi
{
    public static string ArtRoot
    {
        get { return FlattenArtPaths.ArtRoot; }
    }

    /// <summary>B′（原子搬迁）还是 B（按后缀拆）。只看 <see cref="PipelineJobContext.HasExternalUris"/>。</summary>
    public static bool ShouldRelocateAtomic(PipelineJobContext ctx)
    {
        return FlattenBuildService.ShouldRelocateAtomic(ctx);
    }

    /// <summary>是否对 Art 副本跑 ModelImporter 设置 + Extract。</summary>
    public static bool ShouldApplyArtModelImporter(PipelineJobContext ctx)
    {
        return FlattenBuildService.ShouldApplyArtModelImporter(ctx);
    }

    /// <summary>2.5 探针是否发现 glTF 声明了不存在的必需伴生。</summary>
    public static bool HasMissingSidecars(PipelineJobContext ctx)
    {
        return FlattenBuildService.HasMissingSidecars(ctx);
    }

    /// <summary>0 清单元夹 + A 写 Art Prefab。失败则 work 无效。</summary>
    public static bool TryBegin(
        string sourcePrefabPath,
        PipelineJobContext ctx,
        ToolFlattenRequest request,
        out RetinarFlattenWork work)
    {
        return FlattenBuildService.TryBegin(sourcePrefabPath, ctx, request, out work);
    }

    /// <summary>B 按后缀拆依赖。ShouldRelocateAtomic 为 true 时不要调。</summary>
    public static bool SplitDependencies(RetinarFlattenWork work)
    {
        return FlattenBuildService.SplitDependencies(work);
    }

    /// <summary>B′ 原子搬迁。仅 ShouldRelocateAtomic 为 true 时调。</summary>
    public static bool RelocateAtomic(RetinarFlattenWork work)
    {
        return FlattenBuildService.RelocateAtomic(work);
    }

    /// <summary>E 导入设置 + Extract。非 ModelImporter 时本口直接返回。</summary>
    public static void ApplyImportAndExtract(RetinarFlattenWork work, PipelineJobContext ctx)
    {
        FlattenBuildService.ApplyImportAndExtract(work, ctx);
    }

    /// <summary>D 重映射引用。</summary>
    public static void Remap(RetinarFlattenWork work)
    {
        FlattenBuildService.Remap(work);
    }

    /// <summary>C 另存 Renderer .mat。</summary>
    public static void CopyRendererMaterials(RetinarFlattenWork work)
    {
        FlattenBuildService.CopyRendererMaterials(work);
    }

    /// <summary>收尾：自愈、空壳（含轴向）、碰撞盒、动画、AB 名。</summary>
    public static bool TryFinish(RetinarFlattenWork work)
    {
        return FlattenBuildService.TryFinish(work);
    }

    /// <summary>菜单兼容：选中 Prefab/FBX 平铺（可弹窗）。管线不要调。</summary>
    public static void FlattenSelectedToArt()
    {
        RetinarFlattenScheduler.FlattenSelectedToArt();
    }

    /// <summary>菜单兼容：按路径平铺。管线不要调（会走 FBX SafeZone 那条）。</summary>
    public static int FlattenPaths(IList<string> sourcePaths, bool quiet = true)
    {
        List<string> artPrefabPaths;
        return RetinarFlattenApi.FlattenPaths(sourcePaths, quiet, out artPrefabPaths);
    }
}
