using UnityEditor;

// =====================================================================================
// 01 — 菜单入口（仅 MenuItem，不含业务逻辑）
//
// 平铺实现已在插件 2 Generated/Flatten；本文件只挂 MenuItem。
// 管线（PipelineRunner ①→⑥）④ 走 ToolFlattenApi，不经过本菜单。
// 出包统一走 ⑥ RetinarAbApi。
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

    [MenuItem("Tools/Retinar/批量汇总/原子迁移到 Art（选中 glTF／Prefab）", false, 11)]
    public static void MenuRelocateSelectedToArtAtomically()
    {
        RetinarFlattenScheduler.RelocateSelectedToArtAtomically();
    }

    [MenuItem("Tools/Retinar/批量汇总/原子迁移到 Art（选中 glTF／Prefab）", true)]
    public static bool MenuRelocateSelectedToArtAtomicallyValidate()
    {
        return RetinarFlattenScheduler.ValidateFlattenSelectedToArt();
    }

    [MenuItem("Tools/Retinar/批量汇总/平铺操作与配置面板", false, 12)]
    public static void MenuFlattenCategoryWindow()
    {
        FlattenWindow.Open();
    }

    // ----- 共用 -----

    [MenuItem("Tools/Retinar/打开交付文件夹", false, 90)]
    public static void MenuOpenDeliverables()
    {
        RetinarEditorUtil.OpenDeliverablesFolder();
    }
}
