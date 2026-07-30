// 放在 Editor 文件夹下，例如 Assets/Editor/SwitchManagerWindow.cs
// 内容与原实现保持一致，未做修改——三个 AssetPostprocessor 脚本都读取
// SwitchManagerWindow.switchValue 这个静态字段来决定是否介入导入流程。
using UnityEditor;
using UnityEngine;

public class SwitchManagerWindow : EditorWindow
{
    // 静态开关变量（全局共享）
    public static bool switchValue = false;
    // 静态开关显示名称
    public static string switchName = "资源处理开关";

    // 菜单项，用于打开窗口
    [MenuItem("Tools/Switch Manager")]
    public static void ShowWindow()
    {
        GetWindow<SwitchManagerWindow>("Switch Manager");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Switch Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 显示开关控件（Toggle），使用当前名称作为标签
        bool newValue = EditorGUILayout.Toggle(switchName, switchValue);
        if (newValue != switchValue)
            switchValue = newValue;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Current value: " + switchValue, MessageType.Info);
    }
}
