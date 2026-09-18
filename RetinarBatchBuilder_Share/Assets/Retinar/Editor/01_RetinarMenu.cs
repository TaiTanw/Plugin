using UnityEditor;

// =====================================================================================
// 01 — 菜单入口（仅 MenuItem，不含业务逻辑）
//
// 平铺实现已在插件 2 Generated/Flatten；本文件只挂 MenuItem。
// 管线（PipelineRunner ①→⑥）④ 走 ToolFlattenApi，不经过本菜单。
// 出包统一走 ⑥ RetinarAbApi。
// =====================================================================================

/// <summary>Retinar 菜单栏挂载：交付夹、人工④ 两条、⑥ 导出。</summary>
public static class RetinarMenu
{
    [MenuItem("Tools/打开交付文件夹", false, 10)]
    public static void MenuOpenDeliverables()
    {
        RetinarEditorUtil.OpenDeliverablesFolder();
    }

    [MenuItem("Tools/手动操作栏/步骤/[④] 平铺/原子迁移（选中）", false, 34)]
    public static void MenuRelocateSelectedToArtAtomically()
    {
        RetinarFlattenScheduler.RelocateSelectedToArtAtomically();
    }

    [MenuItem("Tools/手动操作栏/步骤/[④] 平铺/原子迁移（选中）", true)]
    public static bool MenuRelocateSelectedToArtAtomicallyValidate()
    {
        return RetinarFlattenScheduler.ValidateFlattenSelectedToArt();
    }

    [MenuItem("Tools/手动操作栏/步骤/[④] 平铺/平铺（选中）", false, 35)]
    public static void MenuFlattenSelectedToArt()
    {
        RetinarFlattenScheduler.FlattenSelectedToArt();
    }

    [MenuItem("Tools/手动操作栏/步骤/[④] 平铺/平铺（选中）", true)]
    public static bool MenuFlattenSelectedToArtValidate()
    {
        return RetinarFlattenScheduler.ValidateFlattenSelectedToArt();
    }

    [MenuItem("Tools/手动操作栏/步骤/[⑥] 导出/导出选中预设体", false, 37)]
    public static void MenuExportSelectedPrefabs()
    {
        RetinarAbApi.BuildFromSelection();
    }

    [MenuItem("Tools/手动操作栏/步骤/[⑥] 导出/导出选中预设体", true)]
    public static bool MenuExportSelectedPrefabsValidate()
    {
        return RetinarAbApi.CollectSelectedPrefabAssetPaths().Count > 0;
    }

    [MenuItem("Tools/手动操作栏/步骤/[⑥] 导出/批量导出文件夹预设体", false, 38)]
    public static void MenuExportDeliveryRootPrefabs()
    {
        RetinarAbApi.BuildFromPickedFolder();
    }

    [MenuItem("Tools/手动操作栏/设置/[⑥] 导出 SO", false, 55)]
    public static void MenuPingExportSettings()
    {
        RetinarExportSettings asset = RetinarExportSettings.GetOrCreateAsset();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
