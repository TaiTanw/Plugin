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

    public static void ShowWindow()
    {
        var window = GetWindow<TextureAdvancedSettingsWindow>("贴图高级设置");
        window.minSize = new Vector2(520f, 360f);
    }

    private void OnEnable()
    {
        settings = TextureProcessSettings.GetOrCreateAsset();
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
            settings = TextureProcessSettings.GetOrCreateAsset();
        }

        settings.EnsureMasterBatchDefaults();

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;
            EditorGUILayout.HelpBox(
                "本页为团队约定（ScriptableObject）。\n" +
                "「主面板批量包含」决定资源处理总面板执行/扫描跑哪些操作；\n" +
                "「导入后处理自动」仅导入区，需总面板后处理开关。",
                MessageType.Info);

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
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(settings, typeof(TextureProcessSettings), false);
                if (GUILayout.Button("在 Project 中定位", GUILayout.Width(120f)))
                {
                    EditorGUIUtility.PingObject(settings);
                }
            }

            ScriptableObjectSettingsGui.Draw(settings, ref settingsSerialized);
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
                    bool newMaster = EditorGUILayout.ToggleLeft("主面板批量包含（SO）", master);
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

                    bool importAuto = settings.importAutoOperationIds.Contains(operation.Id);
                    bool newImportAuto = EditorGUILayout.ToggleLeft(
                        "导入后处理自动（仅导入区；需总面板后处理开关）", importAuto);
                    if (newImportAuto != importAuto)
                    {
                        Undo.RecordObject(settings, "修改导入自动操作");
                        if (newImportAuto)
                        {
                            settings.importAutoOperationIds.Add(operation.Id);
                        }
                        else
                        {
                            settings.importAutoOperationIds.Remove(operation.Id);
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
