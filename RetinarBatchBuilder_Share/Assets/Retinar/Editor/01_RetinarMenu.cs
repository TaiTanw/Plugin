using UnityEditor;

// =====================================================================================
// 01 — 菜单入口（仅 MenuItem，不含业务逻辑）
//
//   批量汇总 — 平铺到交付中间区；平铺分类；【遗产】规范化全套导出
//   成品直达 — 推荐出包口（02_unity + 03_assetbundles；读 RetinarExportSettings）
// =====================================================================================

/// <summary>Retinar 菜单栏唯一挂载点。</summary>
public static class RetinarMenu
{
    // ----- 批量汇总 -----

    [MenuItem("Tools/Retinar/批量汇总/平铺到交付中间区 Art（选中）", false, 10)]
    public static void MenuFlattenSelectedToArt()
    {
        RetinarFlattenScheduler.FlattenSelectedToArt();
    }

    [MenuItem("Tools/Retinar/批量汇总/平铺到交付中间区 Art（选中）", true)]
    public static bool MenuFlattenSelectedToArtValidate()
    {
        return RetinarFlattenScheduler.ValidateFlattenSelectedToArt();
    }

    [MenuItem("Tools/Retinar/批量汇总/平铺分类面板", false, 11)]
    public static void MenuFlattenCategoryWindow()
    {
        FlattenWindow.Open();
    }

    // 遗产：全套 Deliverables（00/01/02/03/06）；后续日常出包走「成品直达」或管线⑥。
    [MenuItem("Tools/Retinar/批量汇总/【遗产】从 Art 规范化导出/导出全部", false, 20)]
    public static void MenuExportAllArt()
    {
        RetinarPackageScheduler.ExportAllArtPrefabs();
    }

    [MenuItem("Tools/Retinar/批量汇总/【遗产】从 Art 规范化导出/导出全部", true)]
    public static bool MenuExportAllArtValidate()
    {
        return RetinarPackageScheduler.ValidateExportAllArtPrefabs();
    }

    [MenuItem("Tools/Retinar/批量汇总/【遗产】从 Art 规范化导出/导出选中", false, 21)]
    public static void MenuExportSelectedArt()
    {
        RetinarPackageScheduler.ExportSelectedArtPrefabs();
    }

    [MenuItem("Tools/Retinar/批量汇总/【遗产】从 Art 规范化导出/导出选中", true)]
    public static bool MenuExportSelectedArtValidate()
    {
        return RetinarPackageScheduler.ValidateExportSelectedArtPrefabs();
    }

    // ----- 成品直达（推荐出包） -----

    [MenuItem("Tools/Retinar/成品直达/选中预制体直通打包（推荐）", false, 40)]
    public static void MenuDirectPackageSelected()
    {
        RetinarDirectPackage.PackageSelectedPrefabsDirect();
    }

    [MenuItem("Tools/Retinar/成品直达/选中预制体直通打包（推荐）", true)]
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
