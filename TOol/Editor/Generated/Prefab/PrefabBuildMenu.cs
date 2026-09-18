using UnityEditor;
using UnityEngine;

// =====================================================================================
// Generated / Prefab — 编辑器菜单（初步验证入口，无确认弹窗）
// =====================================================================================

/// <summary>
/// ③ 自动化预设体菜单。
/// </summary>
public static class PrefabBuildMenu
{
    private const string MenuPath = "Tools/手动操作栏/步骤/[③] Prefab/选中模型生成";

    [MenuItem(MenuPath, false, 33)]
    private static void BuildFromSelection()
    {
        PrefabBuildService.BuildPrefabsFromSelection();
    }

    [MenuItem(MenuPath, true)]
    private static bool BuildFromSelectionValidate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
