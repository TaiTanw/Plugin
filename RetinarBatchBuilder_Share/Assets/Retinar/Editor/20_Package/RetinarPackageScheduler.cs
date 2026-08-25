// =====================================================================================
// 20_Package — 批量「从 Art 规范化导出」流程调度
//
// 完整 Deliverables（00/01/02/03/06）仍由 Legacy ExportArtPrefabPaths 写出。
// 本类只负责菜单语义转发，便于与「成品直达」对照阅读。
// =====================================================================================

/// <summary>导出调度层：把「从 Art 规范化出包」转给 Legacy，本类不写 Deliverables、不跑门禁。</summary>
public static class RetinarPackageScheduler
{
    public static void ExportAllArtPrefabs()
    {
        RetinarBatchModelBuilder.ExportAllArtPrefabs();
    }

    public static void ExportSelectedArtPrefabs()
    {
        RetinarBatchModelBuilder.ExportSelectedArtPrefabs();
    }

    public static bool ValidateExportAllArtPrefabs()
    {
        return RetinarBatchModelBuilder.ValidateExportAllArtPrefabs();
    }

    public static bool ValidateExportSelectedArtPrefabs()
    {
        return RetinarBatchModelBuilder.ValidateExportSelectedArtPrefabs();
    }
}
