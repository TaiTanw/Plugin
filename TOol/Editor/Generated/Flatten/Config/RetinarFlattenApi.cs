using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Config — 菜单 / 兼容窄口（管线④请用 ToolFlattenApi）
// =====================================================================================

/// <summary>平铺兼容入口。管线④走 <see cref="ToolFlattenApi"/>（接 ctx）。</summary>
public static class RetinarFlattenApi
{
    /// <summary>
    /// 按路径平铺到 Art。quiet=true 时无 DisplayDialog（编排默认）。
    /// 菜单与兼容路径用这个；管线④请用 <see cref="ToolFlattenApi"/>。
    /// </summary>
    public static int FlattenPaths(IList<string> sourcePaths, bool quiet = true)
    {
        List<string> artPrefabPaths;
        return FlattenPaths(sourcePaths, quiet, out artPrefabPaths);
    }

    public static int FlattenPaths(IList<string> sourcePaths, bool quiet, out List<string> artPrefabPaths)
    {
        FlattenOperationSettings settings = FlattenOperationSettings.LoadManualOrDefaults();
        FlattenOperationPolicy policy = settings != null
            ? settings.CreatePolicy()
            : FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);
        RetinarFlattenOptions options = FlattenBuildService.CreateOptions(
            null, ToolFlattenRequest.ForManual(policy));
        return FlattenPaths(sourcePaths, quiet, options, out artPrefabPaths);
    }

    public static int FlattenPaths(
        IList<string> sourcePaths,
        bool quiet,
        RetinarFlattenOptions flattenOptions,
        out List<string> artPrefabPaths)
    {
        List<string> unknownLines;
        return RetinarBatchModelBuilder.FlattenSourcePaths(
            sourcePaths, quiet, flattenOptions, out unknownLines, out artPrefabPaths);
    }

    /// <summary>0 清单元夹 + A 写 Art Prefab。失败返回 false，work 无效。</summary>
    public static bool TryBegin(string sourcePrefabPath, RetinarFlattenOptions options, out RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.TryBeginPackagedFlatten(sourcePrefabPath, options, out work);
    }

    /// <summary>B 按后缀拆依赖。SkipDependencySplit 时不要调。</summary>
    public static bool SplitDependencies(RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.FlattenSplitDependencies(work);
    }

    /// <summary>B′ 原子搬迁。仅 SkipDependencySplit 时调。</summary>
    public static bool RelocateAtomic(RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.FlattenRelocateAtomic(work);
    }

    /// <summary>E 导入设置 + Extract 内嵌贴图。</summary>
    public static void ApplyImportAndExtract(RetinarFlattenWork work)
    {
        RetinarBatchModelBuilder.FlattenApplyImportAndExtract(work);
    }

    /// <summary>D 重映射引用。</summary>
    public static void Remap(RetinarFlattenWork work)
    {
        RetinarBatchModelBuilder.FlattenRemap(work);
    }

    /// <summary>C 另存 Renderer .mat。</summary>
    public static void CopyRendererMaterials(RetinarFlattenWork work)
    {
        RetinarBatchModelBuilder.FlattenCopyRendererMaterials(work);
    }

    /// <summary>收尾：自愈、空壳（含轴向）、碰撞盒、动画、AB 名。失败返回 false。</summary>
    public static bool TryFinish(RetinarFlattenWork work)
    {
        return RetinarBatchModelBuilder.TryFinishPackagedFlatten(work);
    }

    /// <summary>菜单兼容：选中项平铺（可弹窗）。</summary>
    public static void FlattenSelectedToArt()
    {
        RetinarFlattenScheduler.FlattenSelectedToArt();
    }
}
