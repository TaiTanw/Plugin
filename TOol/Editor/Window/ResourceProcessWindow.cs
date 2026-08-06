using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 资源处理总面板：自动化与后处理批量入口。
//   - 总开关 + 贴图/模型【设置自动】【后处理自动】
//   - 总批量执行（贴图→模型，平铺后手动）+ 各类「是否纳入总批量」开关
//   - 分项按批量路径执行；打开子面板；可跳转批量 FBX 入库
// =====================================================================================
public class ResourceProcessWindow : EditorWindow
{
    private Vector2 scroll;
    private string lastBatchMessage;

    [MenuItem("Tools/资源处理总面板")]
    public static void ShowWindow()
    {
        GetWindow<ResourceProcessWindow>("资源处理").minSize = new Vector2(460f, 460f);
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
                "导入后处理自动阶段：模型 → 贴图。\n" +
                "平铺后手动总批量：贴图 → 模型（避免贴图收尾冲掉顶点色）。",
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

            DrawMasterBatchSection();

            DrawResourceBlock(
                "贴图",
                () => ResourceProcessSwitches.TextureSettingsAuto,
                v => ResourceProcessSwitches.TextureSettingsAuto = v,
                () => ResourceProcessSwitches.TexturePostProcessAuto,
                v => ResourceProcessSwitches.TexturePostProcessAuto = v,
                TextureToolWindow.ShowWindow,
                () =>
                {
                    lastBatchMessage = RunTextureBatchCore();
                    Repaint();
                },
                ResourceBatchFolderStore.GetTextureFolders());

            DrawResourceBlock(
                "模型",
                () => ResourceProcessSwitches.ModelSettingsAuto,
                v => ResourceProcessSwitches.ModelSettingsAuto = v,
                () => ResourceProcessSwitches.ModelPostProcessAuto,
                v => ResourceProcessSwitches.ModelPostProcessAuto = v,
                ModelToolWindow.ShowWindow,
                () =>
                {
                    lastBatchMessage = RunModelBatchCore();
                    Repaint();
                },
                ResourceBatchFolderStore.GetModelFolders());

            if (!string.IsNullOrEmpty(lastBatchMessage))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(lastBatchMessage, MessageType.None);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("入库", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "批量 FBX 导入只把外部 FBX 送进导入区；交付名仍以人工 Prefab 名为准。",
                    MessageType.None);
                if (GUILayout.Button("打开批量FBX导入"))
                {
                    BatchFbxImportWindow.ShowWindow();
                }
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

                if (GUILayout.Button("确保批量FBX导入配置资产存在"))
                {
                    Selection.activeObject = BatchFbxImportSettings.GetOrCreateAsset();
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }
        }
    }

    private void DrawMasterBatchSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("手动总批量（用子面板批量路径）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "平铺后手动总批量，顺序固定：贴图 → 模型。\n" +
                "下方「纳入总批量」只影响本按钮，不影响各资源块的分项执行。\n" +
                "某类已纳入但批量路径为空时会 Warning 并跳过该类。",
                MessageType.None);

            bool includeModel = EditorGUILayout.ToggleLeft(
                "总批量纳入模型", ResourceProcessSwitches.MasterBatchIncludeModel);
            if (includeModel != ResourceProcessSwitches.MasterBatchIncludeModel)
            {
                ResourceProcessSwitches.MasterBatchIncludeModel = includeModel;
            }

            bool includeTexture = EditorGUILayout.ToggleLeft(
                "总批量纳入贴图", ResourceProcessSwitches.MasterBatchIncludeTexture);
            if (includeTexture != ResourceProcessSwitches.MasterBatchIncludeTexture)
            {
                ResourceProcessSwitches.MasterBatchIncludeTexture = includeTexture;
            }

            bool anyIncluded = ResourceProcessSwitches.MasterBatchIncludeModel ||
                               ResourceProcessSwitches.MasterBatchIncludeTexture;
            using (new EditorGUI.DisabledScope(!anyIncluded))
            {
                if (GUILayout.Button("按批量路径执行全部（贴图→模型）", GUILayout.Height(30f)))
                {
                    RunMasterBatch();
                }
            }

            if (!anyIncluded)
            {
                EditorGUILayout.HelpBox("两类均未纳入总批量，按钮已禁用。", MessageType.Info);
            }
        }
    }

    private void RunMasterBatch()
    {
        var report = new StringBuilder();
        report.AppendLine("[总批量] 开始（贴图→模型，平铺后手动）");

        // 平铺后：先贴图后模型。贴图若 Refresh/重导会冲 Mesh 顶点色；模型放最后写入更稳。
        if (ResourceProcessSwitches.MasterBatchIncludeTexture)
        {
            List<string> textureFolders = ResourceBatchFolderStore.GetValidFolders(
                ResourceBatchFolderStore.GetTextureFolders());
            if (textureFolders.Count == 0)
            {
                string warn = "[总批量] 贴图已纳入，但批量路径为空，已跳过。请在贴图子面板配置「依据文件路径批量」。";
                Debug.LogWarning(warn);
                report.AppendLine(warn);
            }
            else
            {
                report.AppendLine(RunTextureBatchCore());
            }
        }
        else
        {
            report.AppendLine("[总批量] 已跳过贴图（未纳入）。");
        }

        if (ResourceProcessSwitches.MasterBatchIncludeModel)
        {
            List<string> modelFolders = ResourceBatchFolderStore.GetValidFolders(
                ResourceBatchFolderStore.GetModelFolders());
            if (modelFolders.Count == 0)
            {
                string warn = "[总批量] 模型已纳入，但批量路径为空，已跳过。请在模型子面板配置「依据文件路径批量」。";
                Debug.LogWarning(warn);
                report.AppendLine(warn);
            }
            else
            {
                report.AppendLine(RunModelBatchCore());
            }
        }
        else
        {
            report.AppendLine("[总批量] 已跳过模型（未纳入）。");
        }

        lastBatchMessage = report.ToString().TrimEnd();
        Repaint();
    }

    private static void DrawResourceBlock(
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

    private static string RunTextureBatchCore()
    {
        List<string> targets = TextureTargetCollector.CollectFromBatchFolders();
        List<ITextureAssetOperation> operations = ResourceManualOperationStore.CollectSelectedTextureOperations();
        if (operations.Count == 0)
        {
            string msg = "[贴图] 没有勾选任何手动操作（请在贴图子面板勾选）。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[贴图] 批量路径下没有命中贴图。";
            Debug.LogWarning(msg);
            return msg;
        }

        TextureProcessSettings settings = TextureProcessSettings.GetOrCreateAsset();
        TextureOperationRunSummary summary = TextureOperationRunner.Run(operations, targets, settings, false);
        return FormatTextureSummary(summary, targets.Count);
    }

    private static string RunModelBatchCore()
    {
        List<string> targets = ModelTargetCollector.CollectFromBatchFolders();
        List<IModelAssetOperation> operations = ResourceManualOperationStore.CollectSelectedModelOperations();
        if (operations.Count == 0)
        {
            string msg = "[模型] 没有勾选任何手动操作（请在模型子面板勾选）。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[模型] 批量路径下没有命中模型。";
            Debug.LogWarning(msg);
            return msg;
        }

        ModelProcessSettings settings = ModelProcessSettings.GetOrCreateAsset();
        ModelOperationRunSummary summary = ModelOperationRunner.Run(operations, targets, settings, false);
        return "[模型] 批量完成：目标 " + targets.Count +
               "，改动 " + summary.ChangedCount +
               "，跳过 " + summary.SkippedCount +
               "，失败 " + summary.FailedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
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
