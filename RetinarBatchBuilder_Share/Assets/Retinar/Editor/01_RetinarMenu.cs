using UnityEditor;

// =====================================================================================
// 01 — 菜单入口（仅 MenuItem，不含业务逻辑）
//
// 两类入口：
//   批量汇总 — 平铺到 Art；平铺分类面板；从 Art 规范化导出（全套 Deliverables）
//   成品直达 — 选中 Prefab 最净打包（仅 02_unity + 03_assetbundles，不改 Art）
//
// 规范化 / Extract / SafeZone 等重逻辑仍在 RetinarBatchModelBuilder*.cs（Legacy）。
// =====================================================================================

/// <summary>Retinar 菜单栏唯一挂载点。</summary>
public static class RetinarMenu
{
    // ----- 批量汇总 -----

    [MenuItem("Tools/Retinar/批量汇总/平铺到 Art（选中）", false, 10)]
    public static void MenuFlattenSelectedToArt()
    {
        RetinarFlattenScheduler.FlattenSelectedToArt();
    }

    [MenuItem("Tools/Retinar/批量汇总/平铺到 Art（选中）", true)]
    public static bool MenuFlattenSelectedToArtValidate()
    {
        return RetinarFlattenScheduler.ValidateFlattenSelectedToArt();
    }

    [MenuItem("Tools/Retinar/批量汇总/平铺分类面板", false, 11)]
    public static void MenuFlattenCategoryWindow()
    {
        FlattenWindow.Open();
    }

    [MenuItem("Tools/Retinar/批量汇总/从 Art 导出（规范化）/导出全部", false, 20)]
    public static void MenuExportAllArt()
    {
        RetinarPackageScheduler.ExportAllArtPrefabs();
    }

    [MenuItem("Tools/Retinar/批量汇总/从 Art 导出（规范化）/导出全部", true)]
    public static bool MenuExportAllArtValidate()
    {
        return RetinarPackageScheduler.ValidateExportAllArtPrefabs();
    }

    [MenuItem("Tools/Retinar/批量汇总/从 Art 导出（规范化）/导出选中", false, 21)]
    public static void MenuExportSelectedArt()
    {
        RetinarPackageScheduler.ExportSelectedArtPrefabs();
    }

    [MenuItem("Tools/Retinar/批量汇总/从 Art 导出（规范化）/导出选中", true)]
    public static bool MenuExportSelectedArtValidate()
    {
        return RetinarPackageScheduler.ValidateExportSelectedArtPrefabs();
    }

    // ----- 成品直达 -----

    [MenuItem("Tools/Retinar/成品直达/选中预制体直通打包", false, 40)]
    public static void MenuDirectPackageSelected()
    {
        RetinarDirectPackage.PackageSelectedPrefabsDirect();
    }

    [MenuItem("Tools/Retinar/成品直达/选中预制体直通打包", true)]
    public static bool MenuDirectPackageSelectedValidate()
    {
        return RetinarDirectPackage.ValidatePackageSelectedPrefabsDirect();
    }

    // ----- 共用 -----

    [MenuItem("Tools/Retinar/打开交付文件夹", false, 90)]
    public static void MenuOpenDeliverables()
    {
        RetinarEditorUtil.OpenDeliverablesFolder();
    }
}
