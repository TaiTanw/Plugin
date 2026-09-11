using UnityEditor;
using UnityEngine;

// =====================================================================================
// 人工平铺操作面板：拖入同类 SO；按资产目录决定本面板可改/只读。
// =====================================================================================

public sealed class FlattenWindow : EditorWindow
{
    private Vector2 scroll;
    private FlattenOperationSettings settings;

    public static void Open()
    {
        GetWindow<FlattenWindow>("平铺操作").Show();
    }

    private void OnEnable()
    {
        settings = FlattenOperationSettings.GetOrCreateManualAsset();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("人工平铺", EditorStyles.boldLabel);
        settings = (FlattenOperationSettings)EditorGUILayout.ObjectField(
            "操作配置 SO", settings, typeof(FlattenOperationSettings), false);

        if (settings == null)
        {
            EditorGUILayout.HelpBox("请拖入平铺操作 SO，或恢复默认人工 SO。", MessageType.Error);
            if (GUILayout.Button("恢复默认人工 SO"))
            {
                settings = FlattenOperationSettings.GetOrCreateManualAsset();
            }

            return;
        }

        EditorGUILayout.HelpBox(
            "普通平铺使用 B（按分类拆依赖）；若检测到相对 URI，会拒绝并提示改用原子迁移。\n" +
            "原子迁移使用 B′，接受 .gltf 或恰好依赖一个外部 URI glTF 的 Prefab；缺伴生直接失败。\n" +
            "两种操作都执行完整④收尾，不是可乱序的 Begin/B/D 单步。",
            MessageType.Info);

        using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;
            FlattenOperationSettingsGui.Draw(
                settings, FlattenSettingsScope.Manual, drawCategories: true);
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(
                   EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("普通平铺（B）", GUILayout.Height(34f)))
            {
                Run(ManualFlattenMode.SplitDependencies);
            }

            if (GUILayout.Button("原子迁移（B′）", GUILayout.Height(34f)))
            {
                Run(ManualFlattenMode.RelocateAtomic);
            }
        }

        EditorGUILayout.HelpBox(
            "人工配置目录：" + FlattenOperationSettings.ManualConfigRoot + "（本面板可编辑）\n" +
            "管线配置目录：" + FlattenOperationSettings.PipelineConfigRoot + "（本面板只读）\n" +
            "其它目录：未归类，只读；仍可按快照执行。",
            MessageType.None);
    }

    private void Run(ManualFlattenMode mode)
    {
        AssetDatabase.SaveAssets();
        ManualFlattenResult result = ManualFlattenService.RunSelection(settings, mode);
        ShowNotification(new GUIContent(result.Summary));
        if (result.Outputs.Count > 0)
        {
            Object output = AssetDatabase.LoadMainAssetAtPath(result.Outputs[0]);
            if (output != null)
            {
                Selection.activeObject = output;
                EditorGUIUtility.PingObject(output);
            }
        }
    }
}
