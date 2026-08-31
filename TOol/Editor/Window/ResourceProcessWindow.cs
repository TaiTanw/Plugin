using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L1 资源处理总面板：自动化开关 + 共用批量路径 + 总/分项批量（路径×L3 Op）。
// =====================================================================================
public class ResourceProcessWindow : EditorWindow
{
    private const string PrefFoldAdvanced = "TOol.Master.Fold.AdvancedOps";

    private Vector2 scroll;
    private string lastBatchMessage;
    private List<string> masterFolders = new List<string>();
    private bool foldAdvancedOps;

    [MenuItem("Tools/资源处理总面板")]
    public static void ShowWindow()
    {
        GetWindow<ResourceProcessWindow>("资源处理").minSize = new Vector2(460f, 520f);
    }

    private void OnEnable()
    {
        masterFolders = ResourceBatchFolderStore.GetMasterFolders();
        foldAdvancedOps = EditorPrefs.GetBool(PrefFoldAdvanced, false);
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool(PrefFoldAdvanced, foldAdvancedOps);
    }

    private void OnGUI()
    {
        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;

            EditorGUILayout.LabelField("自动化开关（本机 EditorPrefs，不进版本库）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "总开关：关掉后设置自动/后处理自动都不跑；手动「执行全部」不受影响。" +
                "与【自动化管线】导入区「总自动化处理」是同一 Prefs。\n" +
                "设置自动：导入前改 Importer（导入区建议按需开启；Art 被 exclude，不会改交付 Importer）。\n" +
                "后处理自动：导入后跑 Operation——默认跳过 Art，不保证交付生效；" +
                "内嵌贴图/顶点色须平铺后再用下方批量路径（默认 Art）点「执行全部」。" +
                "自动化管线⑤走的是同一按钮内核，不是这条导入自动流。\n" +
                "日常：配路径（默认可含 Assets/Art）→ 执行全部。分项开关/精准面板/入库等在「高级操作」。",
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

            DrawMasterPathSection();
            DrawMasterBatchSection();

            if (!string.IsNullOrEmpty(lastBatchMessage))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(lastBatchMessage, MessageType.None);
            }

            EditorGUILayout.Space(8f);
            bool newFold = EditorGUILayout.Foldout(
                foldAdvancedOps, "高级操作", true, EditorStyles.foldoutHeader);
            if (newFold != foldAdvancedOps)
            {
                foldAdvancedOps = newFold;
                EditorPrefs.SetBool(PrefFoldAdvanced, foldAdvancedOps);
            }

            if (foldAdvancedOps)
            {
                DrawAdvancedOpsSection();
            }
        }
    }

    private void DrawAdvancedOpsSection()
    {
        DrawResourceBlock(
            "贴图",
            () => ResourceProcessSwitches.TextureSettingsAuto,
            v => ResourceProcessSwitches.TextureSettingsAuto = v,
            () => ResourceProcessSwitches.TexturePostProcessAuto,
            v => ResourceProcessSwitches.TexturePostProcessAuto = v,
            TextureToolWindow.ShowWindow,
            TextureAdvancedSettingsWindow.ShowWindow,
            () =>
            {
                lastBatchMessage = RunTextureBatchCore();
                Repaint();
            },
            () =>
            {
                lastBatchMessage = ScanTextureBatchCore();
                Repaint();
            });

        DrawResourceBlock(
            "模型",
            () => ResourceProcessSwitches.ModelSettingsAuto,
            v => ResourceProcessSwitches.ModelSettingsAuto = v,
            () => ResourceProcessSwitches.ModelPostProcessAuto,
            v => ResourceProcessSwitches.ModelPostProcessAuto = v,
            ModelToolWindow.ShowWindow,
            ModelAdvancedSettingsWindow.ShowWindow,
            () =>
            {
                lastBatchMessage = RunModelBatchCore();
                Repaint();
            },
            () =>
            {
                lastBatchMessage = ScanModelBatchCore();
                Repaint();
            });

        DrawMaterialBlock();

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

            if (GUILayout.Button("确保材质配置资产存在"))
            {
                Selection.activeObject = MaterialProcessSettings.GetOrCreateAsset();
                EditorGUIUtility.PingObject(Selection.activeObject);
            }

            if (GUILayout.Button("确保批量FBX导入配置资产存在"))
            {
                Selection.activeObject = BatchFbxImportSettings.GetOrCreateAsset();
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
        }
    }

    private void DrawMaterialBlock()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("材质", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "无导入期设置自动。交付 Shader 规范化走精准面板 / 本处分项 / 总批量。\n" +
                "范围与手动勾选 = 本机 Prefs；目标 Shader 与主批量 Op = MaterialProcessSettings（SO）。",
                MessageType.None);

            List<string> valid = ResourceBatchFolderStore.GetValidMasterFolders();
            if (valid.Count == 0)
            {
                EditorGUILayout.HelpBox("请先在上方配置主面板批量路径。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "路径：" + ResourceBatchFolderStore.FormatMasterPathsTitle(2),
                    EditorStyles.miniLabel);
            }

            using (new EditorGUI.DisabledScope(valid.Count == 0))
            {
                if (GUILayout.Button("按批量路径执行材质", GUILayout.Height(28f)))
                {
                    lastBatchMessage = RunMaterialBatchCore();
                    Repaint();
                }
            }

            if (GUILayout.Button("打开材质精准面板", GUILayout.Height(26f)))
            {
                MaterialToolWindow.ShowWindow();
            }

            if (GUILayout.Button("高级设置（目标 Shader / 主批量 Op）", GUILayout.Height(26f)))
            {
                MaterialAdvancedSettingsWindow.ShowWindow();
            }
        }
    }

    private void DrawMasterPathSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("主面板批量路径（贴图/材质/模型共用，本机）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (ResourceBatchFolderListGui.DrawEditableList("文件夹列表", masterFolders))
            {
                // Save 会丢掉空路径；空行是 UI 占位，需在写回后补回，否则「添加文件夹」像没反应。
                int emptyPlaceholders = 0;
                for (int i = 0; i < masterFolders.Count; i++)
                {
                    if (string.IsNullOrEmpty(masterFolders[i]))
                    {
                        emptyPlaceholders++;
                    }
                }

                ResourceBatchFolderStore.SetMasterFolders(masterFolders);
                masterFolders = ResourceBatchFolderStore.GetMasterFolders();
                for (int i = 0; i < emptyPlaceholders; i++)
                {
                    masterFolders.Add(string.Empty);
                }
            }
        }
    }

    private void DrawMasterBatchSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("手动总批量", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "平铺后手动总批量，顺序固定：贴图 → 材质 → 模型。\n" +
                "路径用上方共用列表（默认 Art）；材质 Op 见 MaterialProcessSettings。\n" +
                "「纳入总批量」只影响本按钮。",
                MessageType.None);

            bool includeTexture = EditorGUILayout.ToggleLeft(
                "总批量纳入贴图", ResourceProcessSwitches.MasterBatchIncludeTexture);
            if (includeTexture != ResourceProcessSwitches.MasterBatchIncludeTexture)
            {
                ResourceProcessSwitches.MasterBatchIncludeTexture = includeTexture;
            }

            bool includeMaterial = EditorGUILayout.ToggleLeft(
                "总批量纳入材质（交付 Shader）", ResourceProcessSwitches.MasterBatchIncludeMaterial);
            if (includeMaterial != ResourceProcessSwitches.MasterBatchIncludeMaterial)
            {
                ResourceProcessSwitches.MasterBatchIncludeMaterial = includeMaterial;
            }

            bool includeModel = EditorGUILayout.ToggleLeft(
                "总批量纳入模型", ResourceProcessSwitches.MasterBatchIncludeModel);
            if (includeModel != ResourceProcessSwitches.MasterBatchIncludeModel)
            {
                ResourceProcessSwitches.MasterBatchIncludeModel = includeModel;
            }

            bool anyIncluded = ResourceProcessSwitches.MasterBatchIncludeModel ||
                               ResourceProcessSwitches.MasterBatchIncludeTexture ||
                               ResourceProcessSwitches.MasterBatchIncludeMaterial;
            List<string> valid = ResourceBatchFolderStore.GetValidMasterFolders();
            using (new EditorGUI.DisabledScope(!anyIncluded || valid.Count == 0))
            {
                if (GUILayout.Button("按批量路径执行全部（贴图→材质→模型）", GUILayout.Height(30f)))
                {
                    RunMasterBatch();
                }
            }

            if (!anyIncluded)
            {
                EditorGUILayout.HelpBox("均未纳入总批量，按钮已禁用。", MessageType.Info);
            }
            else if (valid.Count == 0)
            {
                EditorGUILayout.HelpBox("主面板批量路径为空或无效。", MessageType.Warning);
            }
        }
    }

    private void RunMasterBatch()
    {
        lastBatchMessage = ResourcePostProcessService.RunMasterBatch().Report;
        Repaint();
    }

    private static void DrawResourceBlock(
        string title,
        System.Func<bool> getSettings,
        System.Action<bool> setSettings,
        System.Func<bool> getPost,
        System.Action<bool> setPost,
        System.Action openPanel,
        System.Action openAdvanced,
        System.Action runBatch,
        System.Action scanBatch)
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
            EditorGUILayout.LabelField("手动批量（主路径 + 高级设置 Op）", EditorStyles.miniBoldLabel);
            List<string> valid = ResourceBatchFolderStore.GetValidMasterFolders();
            if (valid.Count == 0)
            {
                EditorGUILayout.HelpBox("请先在上方配置主面板批量路径。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "路径：" + ResourceBatchFolderStore.FormatMasterPathsTitle(2),
                    EditorStyles.miniLabel);
            }

            using (new EditorGUI.DisabledScope(valid.Count == 0))
            {
                if (GUILayout.Button("按批量路径仅扫描" + title + "（不改文件）", GUILayout.Height(26f)))
                {
                    scanBatch();
                }

                if (GUILayout.Button("按批量路径执行" + title, GUILayout.Height(28f)))
                {
                    runBatch();
                }
            }

            if (GUILayout.Button("打开 " + title + " 精准处理面板", GUILayout.Height(26f)))
            {
                openPanel();
            }

            if (openAdvanced != null &&
                GUILayout.Button("高级设置（子处理配置 / 操作集合）", GUILayout.Height(26f)))
            {
                openAdvanced();
            }
        }
    }

    private static string RunTextureBatchCore()
    {
        List<string> targets = TextureTargetCollector.CollectFromBatchFolders();
        TextureProcessSettings settings = TextureProcessSettings.GetOrCreateAsset();
        List<ITextureAssetOperation> operations = TextureOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[贴图] 高级设置中未勾选任何「主面板批量包含」操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[贴图] 批量路径下没有命中贴图。";
            Debug.LogWarning(msg);
            return msg;
        }

        TextureOperationRunSummary summary = TextureOperationRunner.Run(operations, targets, settings, false);
        return FormatTextureSummary(summary, targets.Count);
    }

    private static string ScanTextureBatchCore()
    {
        List<string> targets = TextureTargetCollector.CollectFromBatchFolders();
        TextureProcessSettings settings = TextureProcessSettings.GetOrCreateAsset();
        List<ITextureAssetOperation> operations = TextureOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[贴图扫描] 高级设置中未勾选主面板批量操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[贴图扫描] 批量路径下没有命中贴图。";
            Debug.LogWarning(msg);
            return msg;
        }

        AssetOperationScanSummary summary = TextureOperationRunner.Scan(operations, targets, settings, true);
        return "[贴图扫描] 目标 " + targets.Count + "，需处理 " + summary.NeedsWorkCount +
               "，跳过 " + summary.SkippedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
    }

    private static string RunModelBatchCore()
    {
        List<string> targets = ModelTargetCollector.CollectFromBatchFolders();
        ModelProcessSettings settings = ModelProcessSettings.GetOrCreateAsset();
        List<IModelAssetOperation> operations = ModelOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[模型] 高级设置中未勾选任何「主面板批量包含」操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[模型] 批量路径下没有命中模型。";
            Debug.LogWarning(msg);
            return msg;
        }

        ModelOperationRunSummary summary = ModelOperationRunner.Run(operations, targets, settings, false);
        return "[模型] 批量完成：目标 " + targets.Count +
               "，改动 " + summary.ChangedCount +
               "，跳过 " + summary.SkippedCount +
               "，失败 " + summary.FailedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
    }

    private static string ScanModelBatchCore()
    {
        List<string> targets = ModelTargetCollector.CollectFromBatchFolders();
        ModelProcessSettings settings = ModelProcessSettings.GetOrCreateAsset();
        List<IModelAssetOperation> operations = ModelOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[模型扫描] 高级设置中未勾选主面板批量操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[模型扫描] 批量路径下没有命中模型。";
            Debug.LogWarning(msg);
            return msg;
        }

        AssetOperationScanSummary summary = ModelOperationRunner.Scan(operations, targets, settings, true);
        return "[模型扫描] 目标 " + targets.Count + "，需处理 " + summary.NeedsWorkCount +
               "，跳过 " + summary.SkippedCount +
               (summary.Canceled ? "（已取消）" : string.Empty);
    }

    private static string RunMaterialBatchCore()
    {
        List<string> targets = MaterialTargetCollector.CollectFromBatchFolders();
        MaterialProcessSettings settings = MaterialProcessSettings.GetOrCreateAsset();
        List<IMaterialAssetOperation> operations =
            MaterialOperationRegistry.GetMasterBatchOperations(settings);
        if (operations.Count == 0)
        {
            string msg = "[材质] 配置中未勾选任何主批量操作。";
            Debug.LogWarning(msg);
            return msg;
        }

        if (targets.Count == 0)
        {
            string msg = "[材质] 批量路径下没有命中 .mat。";
            Debug.LogWarning(msg);
            return msg;
        }

        MaterialOperationRunSummary summary = MaterialOperationRunner.Run(operations, targets, settings);
        return "[材质] 批量完成：目标 " + targets.Count +
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
