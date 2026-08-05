using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 资源处理总面板：唯一菜单入口。
//   - 总开关 + 贴图/模型【设置自动】【后处理自动】（本机 EditorPrefs）
//   - 按子面板批量路径手动执行（与子面板当前范围下拉无关）
//   - 打开贴图 / 模型子面板
// =====================================================================================
public class ResourceProcessWindow : EditorWindow
{
    private Vector2 scroll;
    private string lastBatchMessage;

    [MenuItem("Tools/资源处理总面板")]
    public static void ShowWindow()
    {
        GetWindow<ResourceProcessWindow>("资源处理").minSize = new Vector2(460f, 420f);
    }

    private void OnGUI()
    {
        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;

            EditorGUILayout.LabelField("自动化开关（本机 EditorPrefs，不进版本库）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "总开关：关掉后设置自动/后处理自动都不跑；手动执行不受影响。\n" +
                "设置自动：导入前改 Importer（导入区建议按需开启）。\n" +
                "后处理自动：导入后跑 Operation——不保证交付生效（Art 被排除；" +
                "内嵌贴图/顶点色须平铺后再到贴图·模型面板或总面板批量路径手动处理）。默认请保持关闭。\n" +
                "后处理阶段顺序：模型 → 贴图。",
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
                TextureToolWindow.ShowWindow,
                RunTextureBatch,
                ResourceBatchFolderStore.GetTextureFolders());

            DrawResourceBlock(
                "模型",
                () => ResourceProcessSwitches.ModelSettingsAuto,
                v => ResourceProcessSwitches.ModelSettingsAuto = v,
                () => ResourceProcessSwitches.ModelPostProcessAuto,
                v => ResourceProcessSwitches.ModelPostProcessAuto = v,
                ModelToolWindow.ShowWindow,
                RunModelBatch,
                ResourceBatchFolderStore.GetModelFolders());

            if (!string.IsNullOrEmpty(lastBatchMessage))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(lastBatchMessage, MessageType.None);
            }

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

    private void DrawResourceBlock(
        string title,
        System.Func<bool> getSettings,
        System.Action<bool> setSettings,
        System.Func<bool> getPost,
        System.Action<bool> setPost,
        System.Action openPanel,
        System.Action runBatch,
        List<string> batchFolders)
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

                bool postAuto = EditorGUILayout.ToggleLeft(
                    "后处理自动（不保证交付；平铺后请手动）", getPost());
                if (postAuto != getPost())
                {
                    setPost(postAuto);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("手动批量（用子面板批量路径）", EditorStyles.miniBoldLabel);
            List<string> valid = ResourceBatchFolderStore.GetValidFolders(batchFolders);
            if (valid.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "尚未配置批量文件夹。请打开" + title + "处理面板 → 范围选「依据文件路径批量」→ 添加文件夹。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField("路径 " + valid.Count + " 个：", EditorStyles.miniLabel);
                for (int i = 0; i < valid.Count; i++)
                {
                    EditorGUILayout.LabelField("  " + valid[i], EditorStyles.miniLabel);
                }
            }

            using (new EditorGUI.DisabledScope(valid.Count == 0))
            {
                if (GUILayout.Button("按批量路径执行勾选的" + title + "操作", GUILayout.Height(28f)))
                {
                    runBatch();
                }
            }

            if (GUILayout.Button("打开 " + title + " 处理面板", GUILayout.Height(26f)))
            {
                openPanel();
            }
        }
    }

    private void RunTextureBatch()
    {
        List<string> targets = TextureTargetCollector.CollectFromBatchFolders();
        List<ITextureAssetOperation> operations = ResourceManualOperationStore.CollectSelectedTextureOperations();
        if (operations.Count == 0)
        {
            lastBatchMessage = "[贴图] 没有勾选任何手动操作（请在贴图子面板勾选）。";
            Debug.LogWarning(lastBatchMessage);
            return;
        }

        if (targets.Count == 0)
        {
            lastBatchMessage = "[贴图] 批量路径下没有命中贴图。";
            Debug.LogWarning(lastBatchMessage);
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.GetOrCreateAsset();
        TextureOperationRunSummary summary = TextureOperationRunner.Run(operations, targets, settings, false);
        lastBatchMessage = FormatTextureSummary(summary, targets.Count);
        Repaint();
    }

    private void RunModelBatch()
    {
        List<string> targets = ModelTargetCollector.CollectFromBatchFolders();
        List<IModelAssetOperation> operations = ResourceManualOperationStore.CollectSelectedModelOperations();
        if (operations.Count == 0)
        {
            lastBatchMessage = "[模型] 没有勾选任何手动操作（请在模型子面板勾选）。";
            Debug.LogWarning(lastBatchMessage);
            return;
        }

        if (targets.Count == 0)
        {
            lastBatchMessage = "[模型] 批量路径下没有命中模型。";
            Debug.LogWarning(lastBatchMessage);
            return;
        }

        ModelProcessSettings settings = ModelProcessSettings.GetOrCreateAsset();
        ModelOperationRunSummary summary = ModelOperationRunner.Run(operations, targets, settings, false);
        lastBatchMessage =
            "[模型] 批量完成：目标 " + targets.Count +
            "，改动 " + summary.ChangedCount +
            "，跳过 " + summary.SkippedCount +
            "，失败 " + summary.FailedCount +
            (summary.Canceled ? "（已取消）" : string.Empty);
        Repaint();
    }

    private static string FormatTextureSummary(TextureOperationRunSummary summary, int targetCount)
    {
        return "[贴图] 批量完成：目标 " + targetCount +
               "，改动 " + summary.ChangedCount +
               "，跳过 " + summary.SkippedCount +
               "，失败 " + summary.FailedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
    }
}
