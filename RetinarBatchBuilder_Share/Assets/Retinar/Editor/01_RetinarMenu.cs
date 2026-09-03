using UnityEditor;

// =====================================================================================
// 01 — 菜单入口（仅 MenuItem，不含业务逻辑）
//
//   批量汇总 — 平铺到交付中间区；平铺分类
//
// 2026-09-03（backlog D24-7）删除「【遗产】从 Art 规范化导出」与「成品直达」两组菜单：
// 管线（PipelineRunner ①→⑥）不经过它们，出包统一走 ⑥ RetinarAbApi。
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

    // ----- 共用 -----

    [MenuItem("Tools/Retinar/打开交付文件夹", false, 90)]
    public static void MenuOpenDeliverables()
    {
        RetinarEditorUtil.OpenDeliverablesFolder();
    }
}
