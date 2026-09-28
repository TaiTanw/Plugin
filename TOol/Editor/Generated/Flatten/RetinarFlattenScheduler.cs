// =====================================================================================
// 10_Flatten — 资源平铺：流程调度
//
// 职责：把选中的 Prefab/模型送入手动④；每个模型独立选择复制策略。
// 具体搬文件 / Importer 仍在 RetinarBatchModelBuilder（后续按能力拆文件）。
// =====================================================================================

/// <summary>平铺到 Art 的菜单调度入口。</summary>
public static class RetinarFlattenScheduler
{
    /// <summary>
    /// 手动入口完成整趟④。不打 AB、不写 Deliverables。
    /// </summary>
    public static void FlattenSelectedToArt()
    {
        ManualFlattenResult result = FlattenSelectedToArt(
            FlattenOperationSettings.LoadManualOrDefaults());
        DebugResult(result, "手动平铺（逐模型策略）");
    }

    public static ManualFlattenResult FlattenSelectedToArt(FlattenOperationSettings settings)
    {
        return ManualFlattenService.RunSelection(settings, ManualFlattenMode.PerModel);
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
