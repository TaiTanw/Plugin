using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L3 贴图高级设置：子处理配置（SO）+ 操作集合（导入自动 / 主批量 Op）。
// 由 L2 贴图精准面板打开；与主面板共用 TextureProcessSettings。
// =====================================================================================
public class TextureAdvancedSettingsWindow : EditorWindow
{
    private const string PrefFoldConfig = "TOol.TextureAdv.Fold.Config";
    private const string PrefFoldOps = "TOol.TextureAdv.Fold.Ops";

    private TextureProcessSettings settings;
    private SerializedObject settingsSerialized;
    private Vector2 scroll;
    private bool foldConfig = true;
    private bool foldOps = true;

    [MenuItem("Tools/手动操作栏/设置/[⑤] 贴图", false, 52)]
    public static void ShowWindow()
    {
        ShowWindow(TextureProcessSettings.GetOrCreateAsset(), false);
    }

    public static void ShowPipelineWindow()
    {
        ShowWindow(TextureProcessSettings.GetOrCreatePipelineAsset(), true);
    }

    private static void ShowWindow(TextureProcessSettings target, bool pipelineScope)
    {
        var window = GetWindow<TextureAdvancedSettingsWindow>(
            pipelineScope ? "贴图高级设置（编排）" : "贴图高级设置");
        window.minSize = new Vector2(520f, 360f);
        window.Bind(target, pipelineScope);
    }

    private bool pipelineScope;

    private void Bind(TextureProcessSettings target, bool pipeline)
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
                ? TextureProcessSettings.GetOrCreatePipelineAsset()
                : TextureProcessSettings.GetOrCreateAsset();
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
                ? TextureProcessSettings.GetOrCreatePipelineAsset()
                : TextureProcessSettings.GetOrCreateAsset();
        }

        settings.EnsureMasterBatchDefaults();

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;
            EditorGUILayout.HelpBox(
                pipelineScope
                    ? "本页为编排⑤设置（Pipeline/ConfigData，进版本库）。「批量包含」只影响管线⑤。导入期字段在「全局导入设置（2）」。"
                    : "本页为人工设置（TOol/ConfigData）。「批量包含」只影响资源处理总面板执行/扫描。",
                MessageType.Info);

            ResourceRecognitionGui.DrawTexture();

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
                    ? "⑤ 总批量参数。导入期 Importer / 不介入目录在「全局导入设置（2）」。"
                    : "人工设置只含 Op 参数。导入期自动化与排除表在「全局导入设置（2）」。",
                MessageType.None);

            if (pipelineScope)
            {
                ScriptableObjectSettingsGui.Draw(
                    settings,
                    ref settingsSerialized,
                    "applyImporterSettingsOnImport",
                    "textureDisableReadWrite",
                    "excludedPathPrefixes",
                    "importAutoOperationIds");
            }
            else
            {
                ScriptableObjectSettingsGui.Draw(
                    settings,
                    ref settingsSerialized,
                    "applyImporterSettingsOnImport",
                    "textureDisableReadWrite",
                    "excludedPathPrefixes",
                    "importAutoOperationIds");
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
            IList<ITextureAssetOperation> operations = TextureOperationRegistry.All;
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现 ITextureAssetOperation 实现。", MessageType.Warning);
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

            foreach (ITextureAssetOperation operation in operations)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(operation.DisplayName + "  [" + operation.Id + "]", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(operation.Description, EditorStyles.wordWrappedMiniLabel);

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
