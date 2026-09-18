using UnityEditor;
using UnityEngine;

// =====================================================================================
// 平铺操作配置窗：人工 / 管线同一绘制，各绑各的 SO。
// =====================================================================================

public sealed class FlattenSettingsWindow : EditorWindow
{
    private Vector2 scroll;
    private FlattenOperationSettings settings;
    private FlattenSettingsScope panelScope = FlattenSettingsScope.Manual;

    [MenuItem("Tools/手动操作栏/设置/[④] 平铺（人工）", false, 51)]
    public static void OpenManualMenu()
    {
        OpenManual();
    }

    public static void OpenManual()
    {
        Open(FlattenOperationSettings.GetOrCreateManualAsset(), FlattenSettingsScope.Manual);
    }

    public static void OpenPipeline()
    {
        Open(FlattenOperationSettings.GetOrCreatePipelineAsset(), FlattenSettingsScope.Pipeline);
    }

    private static void Open(FlattenOperationSettings target, FlattenSettingsScope scope)
    {
        var window = GetWindow<FlattenSettingsWindow>(
            scope == FlattenSettingsScope.Pipeline ? "平铺设置（编排）" : "平铺设置（人工）");
        window.minSize = new Vector2(420f, 360f);
        window.Bind(target, scope);
        window.Show();
    }

    private void Bind(FlattenOperationSettings target, FlattenSettingsScope scope)
    {
        panelScope = scope;
        settings = target;
        Repaint();
    }

    private void OnEnable()
    {
        if (settings == null)
        {
            settings = panelScope == FlattenSettingsScope.Pipeline
                ? FlattenOperationSettings.GetOrCreatePipelineAsset()
                : FlattenOperationSettings.GetOrCreateManualAsset();
        }
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = panelScope == FlattenSettingsScope.Pipeline
                ? FlattenOperationSettings.GetOrCreatePipelineAsset()
                : FlattenOperationSettings.GetOrCreateManualAsset();
        }

        EditorGUILayout.LabelField(
            panelScope == FlattenSettingsScope.Pipeline ? "编排平铺设置" : "人工平铺设置",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            panelScope == FlattenSettingsScope.Pipeline
                ? "只影响管线④。运行前冻结快照。人工 B/B′ 不读本资产。路径固定，缺则创建。"
                : "只影响人工平铺（选中 → B / B′）。管线④不读本资产。路径固定，缺则创建。",
            MessageType.Info);

        SettingsAssetPathGui.DrawPinned(settings);

        using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;
            FlattenOperationSettingsGui.Draw(settings, panelScope, drawCategories: true);
        }
    }
}
