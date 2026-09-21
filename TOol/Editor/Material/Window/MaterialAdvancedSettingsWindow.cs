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

    [MenuItem("Tools/手动操作栏/设置/[⑤] 材质", false, 53)]
    public static void ShowWindow()
    {
        ShowWindow(MaterialProcessSettings.GetOrCreateAsset(), false);
    }

    public static void ShowPipelineWindow()
    {
        ShowWindow(MaterialProcessSettings.GetOrCreatePipelineAsset(), true);
    }

    private static void ShowWindow(MaterialProcessSettings target, bool pipelineScope)
    {
        var window = GetWindow<MaterialAdvancedSettingsWindow>(
            pipelineScope ? "材质高级设置（编排）" : "材质高级设置");
        window.minSize = new Vector2(520f, 320f);
        window.Bind(target, pipelineScope);
    }

    private bool pipelineScope;

    private void Bind(MaterialProcessSettings target, bool pipeline)
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
                ? MaterialProcessSettings.GetOrCreatePipelineAsset()
                : MaterialProcessSettings.GetOrCreateAsset();
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
                ? MaterialProcessSettings.GetOrCreatePipelineAsset()
                : MaterialProcessSettings.GetOrCreateAsset();
        }

        settings.EnsureMasterBatchDefaults();

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;
            EditorGUILayout.HelpBox(
                pipelineScope
                    ? "本页为编排⑤设置（Pipeline/ConfigData）。目标 Shader 与主批量 Op 只影响管线⑤。"
                    : "本页为人工设置（TOol/ConfigData）。目标 Shader 与主批量 Op 只影响资源处理总面板。",
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
            SettingsAssetPathGui.DrawPinned(settings);

            EditorGUILayout.HelpBox(
                "⑤ 先看 Shader 资产位置：本单元 Art 内（④ 拷入）或 Unity 内置则跳过；" +
                "其余（Packages/UnityGLTF 等）烤到 targetShaderName（默认 Standard）。\n" +
                "allowedShaderNames：额外白名单。sourceShaderNameSubstrings 不再单独开闸。",
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
                    bool newMaster = EditorGUILayout.ToggleLeft("批量包含（本份 SO）", master);
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
