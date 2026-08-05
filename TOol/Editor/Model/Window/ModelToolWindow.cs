using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 模型处理子面板。无独立菜单——由资源处理总面板打开。
// 手动范围：选中 / 单文件夹 / 依据文件路径批量；批量路径与总面板共用 Store。
// =====================================================================================
public class ModelToolWindow : EditorWindow
{
    private ModelProcessSettings settings;
    private Editor settingsInspector;
    private ModelTargetCollector.Scope scope = ModelTargetCollector.Scope.Selection;
    private DefaultAsset targetFolder;
    private List<string> batchFolders = new List<string>();
    private List<string> cachedTargets = new List<string>();
    private bool targetsDirty = true;
    private ModelOperationRunSummary lastSummary;
    private Vector2 mainScroll;
    private Vector2 resultScroll;
    private Vector2 targetListScroll;

    public static void ShowWindow()
    {
        GetWindow<ModelToolWindow>("模型处理").minSize = new Vector2(520f, 360f);
    }

    private void OnEnable()
    {
        settings = ModelProcessSettings.GetOrCreateAsset();
        batchFolders = ResourceBatchFolderStore.GetModelFolders();
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
        ResourceBatchFolderStore.SetModelFolders(batchFolders);
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
                "自动流跳过 Assets/Art/；导 GLB 前请用批量路径或选中 Art Model/Prefab 手动刷顶点色。\n" +
                "「导入后处理自动」仅导入区；不代表 Art 交付已处理。",
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
        EditorGUILayout.LabelField("处理范围（命中 " + cachedTargets.Count + "）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int scopeIndex = (int)scope;
            int newScopeIndex = EditorGUILayout.Popup("范围", scopeIndex, ModelTargetCollector.ScopeLabels);
            if (newScopeIndex != scopeIndex)
            {
                scope = (ModelTargetCollector.Scope)newScopeIndex;
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

            if (scope == ModelTargetCollector.Scope.BatchByPath)
            {
                if (ResourceBatchFolderListGui.DrawEditableList("批量文件夹路径", batchFolders))
                {
                    ResourceBatchFolderStore.SetModelFolders(batchFolders);
                    targetsDirty = true;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "总面板「按批量路径执行」始终使用「依据文件路径批量」里配置的路径，不受当前范围影响。",
                    MessageType.None);
            }

            if (GUILayout.Button("重新扫描", GUILayout.Width(100f)))
            {
                targetsDirty = true;
            }

            if (targetsDirty)
            {
                cachedTargets = ModelTargetCollector.Collect(scope, targetFolder, batchFolders);
                targetsDirty = false;
            }

            EditorGUILayout.LabelField("命中 " + cachedTargets.Count + " 个模型");
            if (cachedTargets.Count > 0)
            {
                float listHeight = Mathf.Min(100f, 18f * cachedTargets.Count + 4f);
                using (var listScroll = new EditorGUILayout.ScrollViewScope(targetListScroll, GUILayout.Height(listHeight)))
                {
                    targetListScroll = listScroll.scrollPosition;
                    for (int i = 0; i < cachedTargets.Count; i++)
                    {
                        EditorGUILayout.LabelField(cachedTargets[i], EditorStyles.miniLabel);
                    }
                }
            }
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
                    bool newAuto = EditorGUILayout.ToggleLeft(
                        "导入后处理自动执行（仅导入区；Art 仍须手动；需总面板后处理开关）", isAuto);
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

                    bool selected = ResourceManualOperationStore.IsSelected(
                        ResourceManualOperationStore.DomainModel, operation.Id);
                    bool newSelected = EditorGUILayout.ToggleLeft("手动执行时包含", selected);
                    if (newSelected != selected)
                    {
                        ResourceManualOperationStore.SetSelected(
                            ResourceManualOperationStore.DomainModel, operation.Id, newSelected);
                    }

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

            int selectedCount = 0;
            foreach (IModelAssetOperation operation in ModelOperationRegistry.All)
            {
                if (ResourceManualOperationStore.IsSelected(
                        ResourceManualOperationStore.DomainModel, operation.Id))
                {
                    selectedCount++;
                }
            }

            using (new EditorGUI.DisabledScope(targets == null || targets.Count == 0 || selectedCount == 0))
            {
                if (GUILayout.Button("执行勾选的手动操作", GUILayout.Height(28f)))
                {
                    lastSummary = ModelOperationRunner.Run(
                        ResourceManualOperationStore.CollectSelectedModelOperations(),
                        targets,
                        settings,
                        false);
                    targetsDirty = true;
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
