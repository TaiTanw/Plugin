// =====================================================================================
// 10_Flatten — 资源平铺：流程调度
//
// 职责：选中外部 Prefab/FBX → 调用 Legacy CreateNormalizedPrefab 写入 Assets/Art。
// 具体搬文件 / Importer / SafeZone 仍在 RetinarBatchModelBuilder（暂不拆碎）。
// =====================================================================================

/// <summary>平铺到 Art 的菜单调度入口。</summary>
public static class RetinarFlattenScheduler
{
    /// <summary>
    /// 批量汇总：平铺选中资源到 Art。不打 AB、不写 Deliverables。
    /// </summary>
    public static void FlattenSelectedToArt()
    {
        // 委托 Legacy，保持与历史完全相同的平铺行为（含规范化副作用）。
        RetinarBatchModelBuilder.FlattenSelectedToArt();
    }

    public static bool ValidateFlattenSelectedToArt()
    {
        return RetinarBatchModelBuilder.ValidateFlattenSelectedToArt();
    }
}
