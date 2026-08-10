using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L2 模型精准面板：处理范围 + 本机勾选操作 + 上次结果；高级设置进 L3。
// =====================================================================================
public class ModelToolWindow : EditorWindow
{
    private const string PrefScope = "TOol.ModelTool.Scope";

    private ModelProcessSettings settings;
    private ModelTargetCollector.Scope scope = ModelTargetCollector.Scope.Selection;
    private DefaultAsset targetFolder;
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
        targetsDirty = true;
        scope = (ModelTargetCollector.Scope)EditorPrefs.GetInt(PrefScope, (int)ModelTargetCollector.Scope.Selection);
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
        EditorPrefs.SetInt(PrefScope, (int)scope);
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
                "精准处理：选范围 → 勾选手动操作 → 扫描/执行（勾选为本机 EditorPrefs）。\n" +
                "主面板批量路径/操作集合在总面板与「高级设置」。自动流跳过 Art。",
                MessageType.Info);

            List<string> targets = DrawTargets();
            DrawOperations(targets);
            DrawResult();

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("高级设置（子处理配置 / 操作集合）…", GUILayout.Height(28f)))
            {
                ModelAdvancedSettingsWindow.ShowWindow();
            }
        }
    }

    private List<string> DrawTargets()
    {
        string pathHint = scope == ModelTargetCollector.Scope.BatchByPath
            ? " · " + ResourceBatchFolderStore.FormatMasterPathsTitle(2)
            : string.Empty;
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "处理范围（命中 " + cachedTargets.Count + "）" + pathHint,
            EditorStyles.boldLabel);
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
                ResourceBatchFolderListGui.DrawReadOnlyMasterPaths("主面板批量路径");
            }

            if (GUILayout.Button("重新扫描", GUILayout.Width(100f)))
            {
                targetsDirty = true;
            }

            if (targetsDirty)
            {
                cachedTargets = ModelTargetCollector.Collect(
                    scope, targetFolder, ResourceBatchFolderStore.GetMasterFolders());
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
        EditorGUILayout.LabelField("可执行操作（本机勾选）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            foreach (IModelAssetOperation operation in ModelOperationRegistry.All)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(operation.DisplayName + "  [" + operation.Id + "]", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(operation.Description, MessageType.None);

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
                if (GUILayout.Button("仅扫描勾选的手动操作（不改文件）", GUILayout.Height(26f)))
                {
                    ModelOperationRunner.Scan(
                        ResourceManualOperationStore.CollectSelectedModelOperations(),
                        targets,
                        settings,
                        true);
                }

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
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("上次执行结果", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (lastSummary == null)
            {
                EditorGUILayout.HelpBox("还没有在这个窗口里执行过操作。", MessageType.None);
                return;
            }

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
}
