using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L2 材质精准面板：范围 + 本机勾选 Op + 上次结果；高级设置进 L3。
// =====================================================================================

/// <summary>材质精准处理面板。</summary>
public class MaterialToolWindow : EditorWindow
{
    private const string PrefFoldTargets = "TOol.MaterialTool.Fold.Targets";
    private const string PrefFoldOperations = "TOol.MaterialTool.Fold.Operations";
    private const string PrefFoldResult = "TOol.MaterialTool.Fold.Result";
    private const string PrefScope = "TOol.MaterialTool.Scope";
    private const string PrefFolder = "TOol.MaterialTool.Folder";

    private MaterialProcessSettings settings;
    private MaterialTargetCollector.Scope scope = MaterialTargetCollector.Scope.Selection;
    private DefaultAsset targetFolder;

    private List<string> cachedTargets = new List<string>();
    private bool targetsDirty = true;

    private MaterialOperationRunSummary lastSummary;
    private AssetOperationScanSummary lastScan;
    private Vector2 mainScroll;
    private Vector2 resultScroll;
    private Vector2 targetListScroll;

    private bool foldTargets = true;
    private bool foldOperations = true;
    private bool foldResult = true;

    public static void ShowWindow()
    {
        GetWindow<MaterialToolWindow>("材质处理").minSize = new Vector2(520f, 360f);
    }

    private void OnEnable()
    {
        settings = MaterialProcessSettings.GetOrCreateAsset();
        targetsDirty = true;
        foldTargets = EditorPrefs.GetBool(PrefFoldTargets, true);
        foldOperations = EditorPrefs.GetBool(PrefFoldOperations, true);
        foldResult = EditorPrefs.GetBool(PrefFoldResult, true);
        scope = (MaterialTargetCollector.Scope)EditorPrefs.GetInt(
            PrefScope, (int)MaterialTargetCollector.Scope.Selection);

        string folderPath = EditorPrefs.GetString(PrefFolder, string.Empty);
        if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
        {
            targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        }
    }

    private void OnSelectionChange()
    {
        if (scope == MaterialTargetCollector.Scope.Selection)
        {
            targetsDirty = true;
            Repaint();
        }
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool(PrefFoldTargets, foldTargets);
        EditorPrefs.SetBool(PrefFoldOperations, foldOperations);
        EditorPrefs.SetBool(PrefFoldResult, foldResult);
        EditorPrefs.SetInt(PrefScope, (int)scope);
        string folderPath = targetFolder == null
            ? string.Empty
            : AssetDatabase.GetAssetPath(targetFolder);
        EditorPrefs.SetString(PrefFolder, folderPath ?? string.Empty);
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = MaterialProcessSettings.GetOrCreateAsset();
        }

        using (var scroll = new EditorGUILayout.ScrollViewScope(mainScroll))
        {
            mainScroll = scroll.scrollPosition;
            EditorGUILayout.HelpBox(
                "精准处理：选范围 → 勾选操作 → 扫描/执行。\n" +
                "范围与勾选为本机 EditorPrefs；目标 Shader / 主批量 Op 集合在「高级设置」（SO）。",
                MessageType.Info);

            List<string> targets = DrawTargetSection();
            DrawOperationSection(targets);
            DrawResultSection();

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("高级设置（目标 Shader / 主批量 Op）…", GUILayout.Height(28f)))
            {
                MaterialAdvancedSettingsWindow.ShowWindow();
            }
        }
    }

    private List<string> DrawTargetSection()
    {
        string pathHint = scope == MaterialTargetCollector.Scope.BatchByPath
            ? " · " + ResourceBatchFolderStore.FormatMasterPathsTitle(2)
            : string.Empty;
        foldTargets = EditorGUILayout.Foldout(
            foldTargets,
            "处理范围（命中 " + cachedTargets.Count + "）" + pathHint,
            true,
            EditorStyles.foldoutHeader);
        if (!foldTargets)
        {
            RefreshTargetsIfNeeded();
            return cachedTargets;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int scopeIndex = (int)scope;
            int newScopeIndex = EditorGUILayout.Popup(
                "范围", scopeIndex, MaterialTargetCollector.ScopeLabels);
            if (newScopeIndex != scopeIndex)
            {
                scope = (MaterialTargetCollector.Scope)newScopeIndex;
                targetsDirty = true;
            }

            if (scope == MaterialTargetCollector.Scope.Folder)
            {
                var newFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    "文件夹", targetFolder, typeof(DefaultAsset), false);
                if (newFolder != targetFolder)
                {
                    targetFolder = newFolder;
                    targetsDirty = true;
                }
            }

            if (scope == MaterialTargetCollector.Scope.BatchByPath)
            {
                ResourceBatchFolderListGui.DrawReadOnlyMasterPaths("主面板批量路径");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("命中材质数量", cachedTargets.Count.ToString());
                if (GUILayout.Button("重新扫描", GUILayout.Width(90f)))
                {
                    targetsDirty = true;
                }
            }

            RefreshTargetsIfNeeded();

            if (cachedTargets.Count > 0)
            {
                float listHeight = Mathf.Min(120f, 18f * cachedTargets.Count + 4f);
                using (var listScroll = new EditorGUILayout.ScrollViewScope(
                           targetListScroll, GUILayout.Height(listHeight)))
                {
                    targetListScroll = listScroll.scrollPosition;
                    for (int i = 0; i < cachedTargets.Count; i++)
                    {
                        EditorGUILayout.LabelField(cachedTargets[i], EditorStyles.miniLabel);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("当前范围下没有命中 .mat。", MessageType.None);
            }
        }

        return cachedTargets;
    }

    private void RefreshTargetsIfNeeded()
    {
        if (!targetsDirty)
        {
            return;
        }

        string folderPath = targetFolder == null ? null : AssetDatabase.GetAssetPath(targetFolder);
        cachedTargets = MaterialTargetCollector.Collect(
            scope, folderPath, ResourceBatchFolderStore.GetMasterFolders());
        targetsDirty = false;
    }

    private void DrawOperationSection(List<string> targets)
    {
        IList<IMaterialAssetOperation> operations = MaterialOperationRegistry.All;
        int selectedCount = CountManuallySelected(operations);
        foldOperations = EditorGUILayout.Foldout(
            foldOperations,
            "可执行操作（已勾选 " + selectedCount + " / " + operations.Count + "，本机）",
            true,
            EditorStyles.foldoutHeader);
        if (!foldOperations)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现 IMaterialAssetOperation 实现。", MessageType.Warning);
                return;
            }

            for (int i = 0; i < operations.Count; i++)
            {
                DrawOperationRow(operations[i], targets);
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(targets.Count == 0 || selectedCount == 0))
            {
                if (GUILayout.Button("仅扫描勾选的操作（不改文件）", GUILayout.Height(26f)))
                {
                    lastScan = MaterialOperationRunner.Scan(
                        CollectManuallySelected(operations),
                        targets,
                        settings,
                        true);
                    foldResult = true;
                }

                if (GUILayout.Button("执行勾选的操作", GUILayout.Height(28f)))
                {
                    lastSummary = MaterialOperationRunner.Run(
                        CollectManuallySelected(operations), targets, settings);
                    targetsDirty = true;
                    foldResult = true;
                }
            }

            if (targets.Count == 0)
            {
                EditorGUILayout.HelpBox("没有命中材质，无法执行。", MessageType.Warning);
            }
            else if (selectedCount == 0)
            {
                EditorGUILayout.HelpBox("请勾选操作，或点「只执行这一个」。", MessageType.Info);
            }
        }
    }

    private void DrawOperationRow(IMaterialAssetOperation operation, List<string> targets)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = ResourceManualOperationStore.IsSelected(
                    ResourceManualOperationStore.DomainMaterial, operation.Id);
                bool newSelected = EditorGUILayout.ToggleLeft(operation.DisplayName, selected);
                if (newSelected != selected)
                {
                    ResourceManualOperationStore.SetSelected(
                        ResourceManualOperationStore.DomainMaterial, operation.Id, newSelected);
                }

                using (new EditorGUI.DisabledScope(targets.Count == 0))
                {
                    if (GUILayout.Button("只执行这一个", GUILayout.Width(110f)))
                    {
                        lastSummary = MaterialOperationRunner.Run(
                            new List<IMaterialAssetOperation> { operation }, targets, settings);
                        targetsDirty = true;
                        foldResult = true;
                    }
                }
            }

            EditorGUILayout.LabelField("Id: " + operation.Id, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(operation.Description, EditorStyles.wordWrappedMiniLabel);
        }
    }

    private static int CountManuallySelected(IList<IMaterialAssetOperation> operations)
    {
        int count = 0;
        for (int i = 0; i < operations.Count; i++)
        {
            if (ResourceManualOperationStore.IsSelected(
                    ResourceManualOperationStore.DomainMaterial, operations[i].Id))
            {
                count++;
            }
        }

        return count;
    }

    private static List<IMaterialAssetOperation> CollectManuallySelected(
        IList<IMaterialAssetOperation> operations)
    {
        var result = new List<IMaterialAssetOperation>();
        for (int i = 0; i < operations.Count; i++)
        {
            if (ResourceManualOperationStore.IsSelected(
                    ResourceManualOperationStore.DomainMaterial, operations[i].Id))
            {
                result.Add(operations[i]);
            }
        }

        return result;
    }

    private void DrawResultSection()
    {
        foldResult = EditorGUILayout.Foldout(
            foldResult, "上次结果", true, EditorStyles.foldoutHeader);
        if (!foldResult)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (lastSummary == null && lastScan == null)
            {
                EditorGUILayout.HelpBox("还没有在这个窗口里执行或扫描过。", MessageType.None);
                return;
            }

            using (var scrollView = new EditorGUILayout.ScrollViewScope(resultScroll, GUILayout.Height(140f)))
            {
                resultScroll = scrollView.scrollPosition;
                if (lastSummary != null)
                {
                    EditorGUILayout.LabelField(
                        "执行：改动 " + lastSummary.ChangedCount +
                        " / 跳过 " + lastSummary.SkippedCount +
                        " / 失败 " + lastSummary.FailedCount +
                        (lastSummary.Canceled ? " / 已取消" : string.Empty),
                        EditorStyles.boldLabel);
                    for (int i = 0; i < lastSummary.ChangedLines.Count; i++)
                    {
                        EditorGUILayout.LabelField(
                            lastSummary.ChangedLines[i], EditorStyles.wordWrappedMiniLabel);
                    }

                    for (int i = 0; i < lastSummary.FailedLines.Count; i++)
                    {
                        EditorGUILayout.HelpBox(lastSummary.FailedLines[i], MessageType.Error);
                    }
                }

                if (lastScan != null)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(
                        "扫描：需处理 " + lastScan.NeedsWorkCount +
                        " / 跳过 " + lastScan.SkippedCount +
                        " / 不适用 " + lastScan.NotApplicableCount,
                        EditorStyles.boldLabel);
                    int show = Mathf.Min(30, lastScan.NeedsWorkLines.Count);
                    for (int i = 0; i < show; i++)
                    {
                        EditorGUILayout.LabelField(
                            lastScan.NeedsWorkLines[i], EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
        }
    }
}
