// =====================================================================================
// 10_Flatten — 资源平铺：流程调度
//
// 职责：把选中的 Prefab/模型送入人工完整④；普通 B 与原子 B′ 是互斥入口。
// 具体搬文件 / Importer / SafeZone 仍在 RetinarBatchModelBuilder（后续按能力拆文件）。
// =====================================================================================

/// <summary>平铺到 Art 的菜单调度入口。</summary>
public static class RetinarFlattenScheduler
{
    /// <summary>
    /// 批量汇总：普通 B 分支完成整趟④。不打 AB、不写 Deliverables。
    /// </summary>
    public static void FlattenSelectedToArt()
    {
        ManualFlattenResult result = FlattenSelectedToArt(
            FlattenOperationSettings.LoadManualOrDefaults());
        DebugResult(result, "普通平铺");
    }

    public static ManualFlattenResult FlattenSelectedToArt(FlattenOperationSettings settings)
    {
        return ManualFlattenService.RunSelection(settings, ManualFlattenMode.SplitDependencies);
    }

    public static void RelocateSelectedToArtAtomically()
    {
        ManualFlattenResult result = RelocateSelectedToArtAtomically(
            FlattenOperationSettings.LoadManualOrDefaults());
        DebugResult(result, "原子迁移");
    }

    public static ManualFlattenResult RelocateSelectedToArtAtomically(
        FlattenOperationSettings settings)
    {
        return ManualFlattenService.RunSelection(settings, ManualFlattenMode.RelocateAtomic);
    }

    public static bool ValidateFlattenSelectedToArt()
    {
        return RetinarBatchModelBuilder.ValidateFlattenSelectedToArt();
    }

    private static void DebugResult(ManualFlattenResult result, string label)
    {
        if (result == null)
        {
            return;
        }

        if (result.Errors.Count > 0)
        {
            UnityEngine.Debug.LogWarning("[ManualFlatten] " + label + "：" + result.Summary);
        }
        else
        {
            UnityEngine.Debug.Log("[ManualFlatten] " + label + "：" + result.Summary);
        }
    }
}
