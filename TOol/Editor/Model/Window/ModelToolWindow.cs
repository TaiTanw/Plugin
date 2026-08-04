using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 模型处理子面板。无独立菜单入口——只能从资源处理总面板打开。
// 自动开关（设置/后处理）在总面板；这里管配置、范围、操作执行。
// =====================================================================================
public class ModelToolWindow : EditorWindow
{
    private ModelProcessSettings settings;
    private Editor settingsInspector;
    private ModelTargetCollector.Scope scope = ModelTargetCollector.Scope.Selection;
    private DefaultAsset targetFolder;
    private List<string> cachedTargets = new List<string>();
    private bool targetsDirty = true;
    private readonly Dictionary<string, bool> manualSelection = new Dictionary<string, bool>();
    private ModelOperationRunSummary lastSummary;
    private Vector2 mainScroll;
    private Vector2 resultScroll;

    public static void ShowWindow()
    {
        GetWindow<ModelToolWindow>("模型处理").minSize = new Vector2(520f, 360f);
    }

    private void OnEnable()
    {
        settings = ModelProcessSettings.GetOrCreateAsset();
        targetsDirty = true;
    }

    private void OnSelectionChange()
    {
        if (scope == ModelTargetCollector.Scope.Selection)
        {
            targetsDirty = true;
            Repaint();
        }
    }

    private void OnDisable()
    {
        if (settingsInspector != null)
        {
            DestroyImmediate(settingsInspector);
            settingsInspector = null;
        }
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = ModelProcessSettings.GetOrCreateAsset();
        }

        using (var scroll = new EditorGUILayout.ScrollViewScope(mainScroll))
        {
            mainScroll = scroll.scrollPosition;
            EditorGUILayout.HelpBox(
                "导入自动开关在「资源处理总面板」。本面板只负责配置与手动执行。\n" +
                "自动流跳过 Assets/Art/；从 Art 导 GLB 前请选中 Art 下的 FBX 在此手动执行。",
                MessageType.Info);

            DrawSettings();
            List<string> targets = DrawTargets();
            DrawOperations(targets);
            DrawResult();
        }
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.ObjectField(settings, typeof(ModelProcessSettings), false);
            if (settingsInspector == null || settingsInspector.target != settings)
            {
                if (settingsInspector != null)
                {
                    DestroyImmediate(settingsInspector);
                }

                settingsInspector = Editor.CreateEditor(settings);
            }

            settingsInspector.OnInspectorGUI();
            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }

    private List<string> DrawTargets()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("处理范围（G1：选中 / 单文件夹）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var newScope = (ModelTargetCollector.Scope)EditorGUILayout.EnumPopup("范围", scope);
            if (newScope != scope)
            {
                scope = newScope;
                targetsDirty = true;
            }

            if (scope == ModelTargetCollector.Scope.Folder)
            {
                var newFolder = (DefaultAsset)EditorGUILayout.ObjectField("文件夹", targetFolder, typeof(DefaultAsset), false);
                if (newFolder != targetFolder)
                {
                    targetFolder = newFolder;
                    targetsDirty = true;
                }
            }

            if (GUILayout.Button("重新扫描", GUILayout.Width(100f)))
            {
                targetsDirty = true;
            }

            if (targetsDirty)
            {
                cachedTargets = ModelTargetCollector.Collect(scope, targetFolder);
                targetsDirty = false;
            }

            EditorGUILayout.LabelField("命中 " + cachedTargets.Count + " 个模型");
        }

        return cachedTargets;
    }

    private void DrawOperations(List<string> targets)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            foreach (IModelAssetOperation operation in ModelOperationRegistry.All)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(operation.DisplayName + "  [" + operation.Id + "]", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(operation.Description, MessageType.None);

                    bool isAuto = settings.importAutoOperationIds != null &&
                                  settings.importAutoOperationIds.Contains(operation.Id);
                    bool newAuto = EditorGUILayout.ToggleLeft("导入后处理自动执行（需总面板后处理开关开启）", isAuto);
                    if (newAuto != isAuto)
                    {
                        if (settings.importAutoOperationIds == null)
                        {
                            settings.importAutoOperationIds = new List<string>();
                        }

                        if (newAuto)
                        {
                            settings.importAutoOperationIds.Add(operation.Id);
                        }
                        else
                        {
                            settings.importAutoOperationIds.Remove(operation.Id);
                        }

                        EditorUtility.SetDirty(settings);
                    }

                    bool selected;
                    if (!manualSelection.TryGetValue(operation.Id, out selected))
                    {
                        selected = true;
                        manualSelection[operation.Id] = true;
                    }

                    manualSelection[operation.Id] = EditorGUILayout.ToggleLeft("手动执行时包含", selected);

                    using (new EditorGUI.DisabledScope(targets == null || targets.Count == 0))
                    {
                        if (GUILayout.Button("仅执行此操作"))
                        {
                            lastSummary = ModelOperationRunner.Run(
                                new List<IModelAssetOperation> { operation }, targets, settings, false);
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledScope(targets == null || targets.Count == 0))
            {
                if (GUILayout.Button("执行勾选的手动操作", GUILayout.Height(28f)))
                {
                    var list = new List<IModelAssetOperation>();
                    foreach (IModelAssetOperation operation in ModelOperationRegistry.All)
                    {
                        bool selected;
                        if (manualSelection.TryGetValue(operation.Id, out selected) && selected)
                        {
                            list.Add(operation);
                        }
                    }

                    lastSummary = ModelOperationRunner.Run(list, targets, settings, false);
                }
            }
        }
    }

    private void DrawResult()
    {
        if (lastSummary == null)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("上次结果", EditorStyles.boldLabel);
        using (var scroll = new EditorGUILayout.ScrollViewScope(resultScroll, GUILayout.Height(120f)))
        {
            resultScroll = scroll.scrollPosition;
            EditorGUILayout.LabelField(
                "改动 " + lastSummary.ChangedCount +
                " / 跳过 " + lastSummary.SkippedCount +
                " / 失败 " + lastSummary.FailedCount +
                (lastSummary.Canceled ? " / 已取消" : string.Empty));
            foreach (string line in lastSummary.ChangedLines)
            {
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            }

            foreach (string line in lastSummary.FailedLines)
            {
                EditorGUILayout.HelpBox(line, MessageType.Error);
            }
        }
    }
}
