using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L3 模型高级设置：子处理配置（SO）+ 操作集合（导入自动 / 主批量 Op）。
// =====================================================================================
public class ModelAdvancedSettingsWindow : EditorWindow
{
    private const string PrefFoldConfig = "TOol.ModelAdv.Fold.Config";
    private const string PrefFoldOps = "TOol.ModelAdv.Fold.Ops";

    private ModelProcessSettings settings;
    private SerializedObject settingsSerialized;
    private Vector2 scroll;
    private bool foldConfig = true;
    private bool foldOps = true;

    [MenuItem("Tools/手动操作栏/设置/[⑤] 模型", false, 54)]
    public static void ShowWindow()
    {
        ShowWindow(ModelProcessSettings.GetOrCreateAsset(), false);
    }

    public static void ShowPipelineWindow()
    {
        ShowWindow(ModelProcessSettings.GetOrCreatePipelineAsset(), true);
    }

    private static void ShowWindow(ModelProcessSettings target, bool pipelineScope)
    {
        var window = GetWindow<ModelAdvancedSettingsWindow>(
            pipelineScope ? "模型高级设置（编排）" : "模型高级设置");
        window.minSize = new Vector2(520f, 360f);
        window.Bind(target, pipelineScope);
    }

    private bool pipelineScope;

    private void Bind(ModelProcessSettings target, bool pipeline)
    {
        pipelineScope = pipeline;
        settings = target;
        settingsSerialized = null;
        Repaint();
    }

    private void OnEnable()
    {
        if (settings == null)
        {
            settings = pipelineScope
                ? ModelProcessSettings.GetOrCreatePipelineAsset()
                : ModelProcessSettings.GetOrCreateAsset();
        }
        foldConfig = EditorPrefs.GetBool(PrefFoldConfig, true);
        foldOps = EditorPrefs.GetBool(PrefFoldOps, true);
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool(PrefFoldConfig, foldConfig);
        EditorPrefs.SetBool(PrefFoldOps, foldOps);
        settingsSerialized = null;
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = pipelineScope
                ? ModelProcessSettings.GetOrCreatePipelineAsset()
                : ModelProcessSettings.GetOrCreateAsset();
        }

        settings.EnsureMasterBatchDefaults();

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;
            EditorGUILayout.HelpBox(
                pipelineScope
                    ? "本页为编排⑤设置（Pipeline/ConfigData）。「批量包含」只影响管线⑤。导入期 External / 基线在「全局导入设置（2）」。"
                    : "本页为人工设置（TOol/ConfigData）。「批量包含」只影响资源处理总面板。",
                MessageType.Info);

            ResourceRecognitionGui.DrawModel(settings);

            DrawConfigFoldout();
            DrawOperationsFoldout();
        }
    }

    private void DrawConfigFoldout()
    {
        EditorGUILayout.Space(4f);
        foldConfig = EditorGUILayout.Foldout(foldConfig, "子处理配置", true, EditorStyles.foldoutHeader);
        if (!foldConfig)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            SettingsAssetPathGui.DrawPinned(settings);

            EditorGUILayout.HelpBox(
                pipelineScope
                    ? "⑤ 总批量参数与识别后缀。导入期基线 / External / 不介入目录在「全局导入设置（2）」。"
                    : "人工设置只含识别后缀等 Op 相关项。导入期自动化与排除表在「全局导入设置（2）」。",
                MessageType.None);

            if (pipelineScope)
            {
                ScriptableObjectSettingsGui.Draw(
                    settings,
                    ref settingsSerialized,
                    "modelStripLightsAndCameras",
                    "modelCalculateNormalsForObj",
                    "modelUseExternalMaterials",
                    "excludedPathPrefixes",
                    "importAutoOperationIds");
            }
            else
            {
                ScriptableObjectSettingsGui.Draw(
                    settings,
                    ref settingsSerialized,
                    "modelStripLightsAndCameras",
                    "modelCalculateNormalsForObj",
                    "modelUseExternalMaterials",
                    "excludedPathPrefixes",
                    "importAutoOperationIds");
            }
            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }

    private void DrawOperationsFoldout()
    {
        EditorGUILayout.Space(4f);
        foldOps = EditorGUILayout.Foldout(foldOps, "操作集合配置", true, EditorStyles.foldoutHeader);
        if (!foldOps)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            IList<IModelAssetOperation> operations = ModelOperationRegistry.All;
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现 IModelAssetOperation 实现。", MessageType.Warning);
                return;
            }

            if (settings.importAutoOperationIds == null)
            {
                settings.importAutoOperationIds = new List<string>();
            }

            if (settings.masterBatchOperationIds == null)
            {
                settings.masterBatchOperationIds = new List<string>();
            }

            foreach (IModelAssetOperation operation in operations)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(operation.DisplayName + "  [" + operation.Id + "]", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(operation.Description, MessageType.None);

                    bool master = settings.masterBatchOperationIds.Contains(operation.Id);
                    bool newMaster = EditorGUILayout.ToggleLeft("批量包含（本份 SO）", master);
                    if (newMaster != master)
                    {
                        Undo.RecordObject(settings, "修改主面板批量操作");
                        if (newMaster)
                        {
                            settings.masterBatchOperationIds.Add(operation.Id);
                        }
                        else
                        {
                            settings.masterBatchOperationIds.Remove(operation.Id);
                        }

                        EditorUtility.SetDirty(settings);
                    }
                }
            }

            if (GUI.changed)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
