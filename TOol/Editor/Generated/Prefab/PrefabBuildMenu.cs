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
    private const string MenuPath = "Tools/自动化预设体（选中模型）";

    [MenuItem(MenuPath, false, 51)]
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
