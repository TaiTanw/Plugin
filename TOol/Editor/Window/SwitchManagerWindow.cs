using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   只是总开关的一个轻量 GUI 入口，状态本身存在 AssetProcessSwitch 里。
//   保留这个窗口是为了不破坏你们已有的操作习惯（Tools/Switch Manager）；
//   功能更全的面板在 Tools/贴图处理工具。
//
// 关于 switchValue：
//   原来这是一个真正的 static 字段，没有持久化，编辑器每次重载程序集都会静默变回 false。
//   现在它只是 AssetProcessSwitch.IsEnabled 的转发属性，读写都落到 EditorPrefs。
//   保留这个名字是为了让工程里其它可能引用过 SwitchManagerWindow.switchValue 的代码
//   继续编译通过，新代码请直接用 AssetProcessSwitch.IsEnabled。
// =====================================================================================
public class SwitchManagerWindow : EditorWindow
{
    public static bool switchValue
    {
        get { return AssetProcessSwitch.IsEnabled; }
        set { AssetProcessSwitch.IsEnabled = value; }
    }

    [MenuItem("Tools/Switch Manager")]
    public static void ShowWindow()
    {
        GetWindow<SwitchManagerWindow>("Switch Manager");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Switch Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        bool newValue = EditorGUILayout.ToggleLeft(AssetProcessSwitch.DisplayName, AssetProcessSwitch.IsEnabled);
        if (newValue != AssetProcessSwitch.IsEnabled)
        {
            AssetProcessSwitch.IsEnabled = newValue;
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("当前值: " + AssetProcessSwitch.IsEnabled +
            "\n（已持久化到 EditorPrefs，脚本重编译和重启编辑器都不会重置）", MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("打开贴图处理工具"))
        {
            TextureToolWindow.ShowWindow();
        }
    }
}
