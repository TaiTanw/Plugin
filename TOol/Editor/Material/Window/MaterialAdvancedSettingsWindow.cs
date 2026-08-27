using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L3 材质高级设置：目标 Shader / 白名单（SO）+ 主批量 Op 勾选。
// 可由 L1 总面板或 L2 精准面板直接打开。
// =====================================================================================

/// <summary>材质高级设置窗。</summary>
public class MaterialAdvancedSettingsWindow : EditorWindow
{
    private const string PrefFoldConfig = "TOol.MaterialAdv.Fold.Config";
    private const string PrefFoldOps = "TOol.MaterialAdv.Fold.Ops";

    private MaterialProcessSettings settings;
    private SerializedObject settingsSerialized;
    private Vector2 scroll;
    private bool foldConfig = true;
    private bool foldOps = true;

    public static void ShowWindow()
    {
        var window = GetWindow<MaterialAdvancedSettingsWindow>("材质高级设置");
        window.minSize = new Vector2(520f, 320f);
    }

    private void OnEnable()
    {
        settings = MaterialProcessSettings.GetOrCreateAsset();
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
            settings = MaterialProcessSettings.GetOrCreateAsset();
        }

        settings.EnsureMasterBatchDefaults();

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;
            EditorGUILayout.HelpBox(
                "本页为团队约定（ScriptableObject，进版本库）。\n" +
                "「主面板批量包含」决定资源总面板 / 管线⑤跑哪些材质 Op。\n" +
                "L2 精准面板的勾选是本机 Prefs，与本页主批量勾选是两套。",
                MessageType.Info);

            ResourceRecognitionGui.DrawMaterial();

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
                EditorGUILayout.ObjectField(settings, typeof(MaterialProcessSettings), false);
                if (GUILayout.Button("在 Project 中定位", GUILayout.Width(120f)))
                {
                    EditorGUIUtility.PingObject(settings);
                }
            }

            EditorGUILayout.HelpBox(
                "targetShaderName：不合规材质烤到的目标（默认 Standard）。\n" +
                "allowedShaderNames：已合规则跳过。\n" +
                "sourceShaderNameSubstrings：源名子串命中则烤（如 PBRGraph）。",
                MessageType.None);

            if (settingsSerialized == null || settingsSerialized.targetObject != settings)
            {
                settingsSerialized = new SerializedObject(settings);
            }

            settingsSerialized.Update();
            SerializedProperty iterator = settingsSerialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script" || iterator.name == "masterBatchOperationIds")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            settingsSerialized.ApplyModifiedProperties();
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
            IList<IMaterialAssetOperation> operations = MaterialOperationRegistry.All;
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现 IMaterialAssetOperation 实现。", MessageType.Warning);
                return;
            }

            if (settings.masterBatchOperationIds == null)
            {
                settings.masterBatchOperationIds = new List<string>();
            }

            for (int i = 0; i < operations.Count; i++)
            {
                IMaterialAssetOperation operation = operations[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        operation.DisplayName + "  [" + operation.Id + "]", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        operation.Description, EditorStyles.wordWrappedMiniLabel);

                    bool master = settings.masterBatchOperationIds.Contains(operation.Id);
                    bool newMaster = EditorGUILayout.ToggleLeft("主面板批量包含（SO）", master);
                    if (newMaster != master)
                    {
                        Undo.RecordObject(settings, "修改材质主面板批量操作");
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
        }
    }
}
