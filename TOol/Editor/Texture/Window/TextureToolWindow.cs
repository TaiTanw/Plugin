using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L2 贴图精准面板：处理范围 + 本机勾选操作 + 上次结果；高级设置进 L3。
// =====================================================================================
public class TextureToolWindow : EditorWindow
{
    private const string PrefFoldTargets = "Retinar.TextureTool.Fold.Targets";
    private const string PrefFoldOperations = "Retinar.TextureTool.Fold.Operations";
    private const string PrefFoldResult = "Retinar.TextureTool.Fold.Result";
    private const string PrefScope = "TOol.TextureTool.Scope";

    private TextureProcessSettings settings;

    private TextureTargetCollector.Scope scope = TextureTargetCollector.Scope.Selection;
    private DefaultAsset targetFolder;

    private List<string> cachedTargets = new List<string>();
    private bool targetsDirty = true;

    private TextureOperationRunSummary lastSummary;
    private Vector2 mainScroll;
    private Vector2 resultScroll;
    private Vector2 targetListScroll;

    private bool foldTargets = true;
    private bool foldOperations = true;
    private bool foldResult = true;

    public static void ShowWindow()
    {
        GetWindow<TextureToolWindow>("贴图处理").minSize = new Vector2(520f, 360f);
    }

    private void OnEnable()
    {
        settings = TextureProcessSettings.GetOrCreateAsset();
        targetsDirty = true;
        foldTargets = EditorPrefs.GetBool(PrefFoldTargets, true);
        foldOperations = EditorPrefs.GetBool(PrefFoldOperations, true);
        foldResult = EditorPrefs.GetBool(PrefFoldResult, true);
        scope = (TextureTargetCollector.Scope)EditorPrefs.GetInt(PrefScope, (int)TextureTargetCollector.Scope.Selection);
    }

    private void OnSelectionChange()
    {
        if (scope == TextureTargetCollector.Scope.Selection)
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
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = TextureProcessSettings.GetOrCreateAsset();
        }

        using (var scroll = new EditorGUILayout.ScrollViewScope(mainScroll))
        {
            mainScroll = scroll.scrollPosition;

            EditorGUILayout.HelpBox(
                "精准处理：选范围 → 勾选操作 → 扫描/执行（勾选为本机 EditorPrefs）。\n" +
                "主面板批量路径/操作集合在总面板与「高级设置」；自动流跳过 Art 与 .fbm。",
                MessageType.Info);

            List<string> targets = DrawTargetSection();
            DrawOperationSection(targets);
            DrawResultSection();

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("高级设置（子处理配置 / 操作集合）…", GUILayout.Height(28f)))
            {
                TextureAdvancedSettingsWindow.ShowWindow();
            }
        }
    }

    private List<string> DrawTargetSection()
    {
        string pathHint = scope == TextureTargetCollector.Scope.BatchByPath
            ? " · " + ResourceBatchFolderStore.FormatMasterPathsTitle(2)
            : string.Empty;
        foldTargets = DrawFoldoutHeader(
            foldTargets,
            "处理范围（命中 " + cachedTargets.Count + "）" + pathHint);
        if (!foldTargets)
        {
            RefreshTargetsIfNeeded();
            return cachedTargets;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            int scopeIndex = (int)scope;
            int newScopeIndex = EditorGUILayout.Popup("范围", scopeIndex, TextureTargetCollector.ScopeLabels);
            if (newScopeIndex != scopeIndex)
            {
                scope = (TextureTargetCollector.Scope)newScopeIndex;
                targetsDirty = true;
            }

            if (scope == TextureTargetCollector.Scope.Folder)
            {
                var newFolder = (DefaultAsset)EditorGUILayout.ObjectField("文件夹", targetFolder, typeof(DefaultAsset), false);
                if (newFolder != targetFolder)
                {
                    targetFolder = newFolder;
                    targetsDirty = true;
                }
            }

            if (scope == TextureTargetCollector.Scope.BatchByPath)
            {
                ResourceBatchFolderListGui.DrawReadOnlyMasterPaths("主面板批量路径");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("命中贴图数量", cachedTargets.Count.ToString());
                if (GUILayout.Button("重新扫描", GUILayout.Width(90f)))
                {
                    targetsDirty = true;
                }
            }

            RefreshTargetsIfNeeded();

            if (cachedTargets.Count > 0)
            {
                float listHeight = Mathf.Min(120f, 18f * cachedTargets.Count + 4f);
                using (var listScroll = new EditorGUILayout.ScrollViewScope(targetListScroll, GUILayout.Height(listHeight)))
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
                EditorGUILayout.HelpBox("当前范围下没有命中可贴图处理的资产。", MessageType.None);
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
        cachedTargets = TextureTargetCollector.Collect(
            scope, folderPath, ResourceBatchFolderStore.GetMasterFolders());
        targetsDirty = false;
    }

    private void DrawOperationSection(List<string> targets)
    {
        IList<ITextureAssetOperation> operations = TextureOperationRegistry.All;
        int selectedCount = CountManuallySelected(operations);
        foldOperations = DrawFoldoutHeader(
            foldOperations,
            "可执行操作（已勾选 " + selectedCount + " / " + operations.Count + "，本机）");
        if (!foldOperations)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (operations.Count == 0)
            {
                EditorGUILayout.HelpBox("工程里没有找到任何 ITextureAssetOperation 实现。", MessageType.Warning);
                return;
            }

            foreach (ITextureAssetOperation operation in operations)
            {
                DrawOperationRow(operation, targets);
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(targets.Count == 0 || selectedCount == 0))
            {
                if (GUILayout.Button("仅扫描勾选的操作（不改文件）", GUILayout.Height(26f)))
                {
                    TextureOperationRunner.Scan(
                        CollectManuallySelectedOperations(operations),
                        targets,
                        TextureProcessSettings.GetOrCreateAsset(),
                        true);
                }

                if (GUILayout.Button("执行勾选的操作", GUILayout.Height(28f)))
                {
                    RunOperations(CollectManuallySelectedOperations(operations), targets);
                }
            }

            if (targets.Count == 0)
            {
                EditorGUILayout.HelpBox("没有命中贴图，无法执行。请先配置处理范围。", MessageType.Warning);
            }
            else if (selectedCount == 0)
            {
                EditorGUILayout.HelpBox("请勾选要执行的操作，或点某一行的「只执行这一个」。", MessageType.Info);
            }
        }
    }

    private void DrawOperationRow(ITextureAssetOperation operation, List<string> targets)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = ResourceManualOperationStore.IsSelected(
                    ResourceManualOperationStore.DomainTexture, operation.Id);
                bool newSelected = EditorGUILayout.ToggleLeft(operation.DisplayName, selected);
                if (newSelected != selected)
                {
                    ResourceManualOperationStore.SetSelected(
                        ResourceManualOperationStore.DomainTexture, operation.Id, newSelected);
                }

                using (new EditorGUI.DisabledScope(targets.Count == 0))
                {
                    if (GUILayout.Button("只执行这一个", GUILayout.Width(110f)))
                    {
                        RunOperations(new List<ITextureAssetOperation> { operation }, targets);
                    }
                }
            }

            EditorGUILayout.LabelField("Id: " + operation.Id, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(operation.Description, EditorStyles.wordWrappedMiniLabel);
        }
    }

    private static int CountManuallySelected(IList<ITextureAssetOperation> operations)
    {
        int count = 0;
        foreach (ITextureAssetOperation operation in operations)
        {
            if (ResourceManualOperationStore.IsSelected(
                    ResourceManualOperationStore.DomainTexture, operation.Id))
            {
                count++;
            }
        }

        return count;
    }

    private static List<ITextureAssetOperation> CollectManuallySelectedOperations(
        IList<ITextureAssetOperation> operations)
    {
        var result = new List<ITextureAssetOperation>();
        foreach (ITextureAssetOperation operation in operations)
        {
            if (ResourceManualOperationStore.IsSelected(
                    ResourceManualOperationStore.DomainTexture, operation.Id))
            {
                result.Add(operation);
            }
        }

        return result;
    }

    private void RunOperations(List<ITextureAssetOperation> operations, List<string> targets)
    {
        if (operations.Count == 0)
        {
            ShowNotification(new GUIContent("没有勾选任何操作"));
            return;
        }

        if (targets.Count == 0)
        {
            ShowNotification(new GUIContent("没有命中贴图"));
            return;
        }

        lastSummary = TextureOperationRunner.Run(operations, targets, settings, false);
        foldResult = true;

        if (lastSummary.TotalHandled == 0)
        {
            ShowNotification(new GUIContent("没有可处理项（命中贴图对当前操作都不适用）"));
        }
        else if (lastSummary.ChangedCount == 0 && lastSummary.FailedCount == 0 && lastSummary.SkippedCount > 0)
        {
            ShowNotification(new GUIContent("全部跳过 " + lastSummary.SkippedCount + " 项，见下方结果"));
        }
        else if (lastSummary.ChangedCount > 0)
        {
            ShowNotification(new GUIContent("已改动 " + lastSummary.ChangedCount + " 项"));
        }

        targetsDirty = true;
        Repaint();
    }

    private void DrawResultSection()
    {
        string title = lastSummary == null
            ? "上次执行结果"
            : "上次执行结果（改动 " + lastSummary.ChangedCount +
              " / 跳过 " + lastSummary.SkippedCount +
              " / 失败 " + lastSummary.FailedCount + "）";
        foldResult = DrawFoldoutHeader(foldResult, title);
        if (!foldResult)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (lastSummary == null)
            {
                EditorGUILayout.HelpBox("还没有在这个窗口里执行过操作。", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(
                "改动 " + lastSummary.ChangedCount + " 项，跳过 " + lastSummary.SkippedCount +
                " 项，失败 " + lastSummary.FailedCount + " 项" +
                (lastSummary.Canceled ? "（中途取消）" : string.Empty));

            if (lastSummary.ChangedCount == 0 && lastSummary.FailedCount == 0 && lastSummary.SkippedCount > 0)
            {
                EditorGUILayout.HelpBox(
                    "全部被跳过了，所以磁盘上不会有变化。常见原因：贴图在 .fbm 内、已达标、或操作不适用。",
                    MessageType.Warning);
            }

            int lineCount = lastSummary.FailedLines.Count + lastSummary.ChangedLines.Count + lastSummary.SkippedLines.Count;
            if (lineCount == 0)
            {
                EditorGUILayout.HelpBox("没有产出任何明细。", MessageType.Info);
                return;
            }

            float resultHeight = Mathf.Clamp(18f * lineCount + 8f, 80f, 220f);
            using (var scrollView = new EditorGUILayout.ScrollViewScope(resultScroll, GUILayout.Height(resultHeight)))
            {
                resultScroll = scrollView.scrollPosition;
                foreach (string line in lastSummary.FailedLines)
                {
                    EditorGUILayout.LabelField("失败  " + line, EditorStyles.wordWrappedMiniLabel);
                }

                foreach (string line in lastSummary.ChangedLines)
                {
                    EditorGUILayout.LabelField("改动  " + line, EditorStyles.wordWrappedMiniLabel);
                }

                foreach (string line in lastSummary.SkippedLines)
                {
                    EditorGUILayout.LabelField("跳过  " + line, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }

    private static bool DrawFoldoutHeader(bool expanded, string title)
    {
        EditorGUILayout.Space(4f);
        return EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
    }
}
