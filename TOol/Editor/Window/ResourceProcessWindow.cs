using UnityEditor;
using UnityEngine;

// =====================================================================================
// 资源处理总面板：唯一菜单入口。
//   - 总开关 + 贴图/模型【设置自动】【后处理自动】（均本机 EditorPrefs）
//   - 打开贴图 / 模型子面板（手动执行不受总开关影响）
//   - 后处理阶段顺序固定：模型 → 贴图（v1 不提供拖拽）
// =====================================================================================
public class ResourceProcessWindow : EditorWindow
{
    private Vector2 scroll;

    [MenuItem("Tools/资源处理总面板")]
    public static void ShowWindow()
    {
        GetWindow<ResourceProcessWindow>("资源处理").minSize = new Vector2(460f, 360f);
    }

    private void OnGUI()
    {
        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;

            EditorGUILayout.LabelField("自动化开关（本机 EditorPrefs，不进版本库）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "总开关：关掉后，所有导入设置自动与后处理自动都不跑；手动在子面板执行不受影响。\n" +
                "设置自动：导入前改 Importer 参数。\n" +
                "后处理自动：导入结束后 delayCall 跑 Operation（还需在子面板勾选具体操作）。\n" +
                "后处理阶段顺序固定为：模型 → 贴图。",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool master = EditorGUILayout.ToggleLeft("总开关（默认开启）", ResourceProcessSwitches.MasterEnabled);
                if (master != ResourceProcessSwitches.MasterEnabled)
                {
                    ResourceProcessSwitches.MasterEnabled = master;
                }

                if (!ResourceProcessSwitches.MasterEnabled)
                {
                    EditorGUILayout.HelpBox("总开关已关闭：下方分项即使勾选也不会自动执行。", MessageType.Warning);
                }
            }

            DrawResourceBlock(
                "贴图",
                () => ResourceProcessSwitches.TextureSettingsAuto,
                v => ResourceProcessSwitches.TextureSettingsAuto = v,
                () => ResourceProcessSwitches.TexturePostProcessAuto,
                v => ResourceProcessSwitches.TexturePostProcessAuto = v,
                TextureToolWindow.ShowWindow);

            DrawResourceBlock(
                "模型",
                () => ResourceProcessSwitches.ModelSettingsAuto,
                v => ResourceProcessSwitches.ModelSettingsAuto = v,
                () => ResourceProcessSwitches.ModelPostProcessAuto,
                v => ResourceProcessSwitches.ModelPostProcessAuto = v,
                ModelToolWindow.ShowWindow);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("配置资产", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("确保贴图配置资产存在"))
                {
                    Selection.activeObject = TextureProcessSettings.GetOrCreateAsset();
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }

                if (GUILayout.Button("确保模型配置资产存在"))
                {
                    Selection.activeObject = ModelProcessSettings.GetOrCreateAsset();
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }
        }
    }

    private static void DrawResourceBlock(
        string title,
        System.Func<bool> getSettings,
        System.Action<bool> setSettings,
        System.Func<bool> getPost,
        System.Action<bool> setPost,
        System.Action openPanel)
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!ResourceProcessSwitches.MasterEnabled))
            {
                bool settingsAuto = EditorGUILayout.ToggleLeft("设置自动（Importer）", getSettings());
                if (settingsAuto != getSettings())
                {
                    setSettings(settingsAuto);
                }

                bool postAuto = EditorGUILayout.ToggleLeft("后处理自动（Operation）", getPost());
                if (postAuto != getPost())
                {
                    setPost(postAuto);
                }
            }

            if (GUILayout.Button("打开 " + title + " 处理面板", GUILayout.Height(26f)))
            {
                openPanel();
            }
        }
    }
}
