using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 批量模型导入：收集 / Conflict 警告 / 执行入库，或把筛选结果输出到编排面板。
// 「执行导入」= 只做 1 入库（人工单步）。「输出到编排」= 不拷贝，填路径+ID2。
// 后缀筛选只缩小本次列表，不改内核识别、不影响 CLI。
// =====================================================================================
public class BatchFbxImportWindow : EditorWindow
{
    private BatchFbxImportSettings settings;
    private SerializedObject settingsSerialized;
    private readonly List<BatchFbxImportService.ImportItem> items =
        new List<BatchFbxImportService.ImportItem>();
    private readonly HashSet<string> enabledExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool filterInitialized;
    /// <summary>本次拖入/浏览的根路径。改勾选时按当前后缀重新收集，而不是从列表里删死。</summary>
    private readonly List<string> collectRoots = new List<string>();
    private Vector2 mainScroll;
    private Vector2 listScroll;
    private string lastSummary;
    private bool isRunning;

    /// <summary>与管线 [1]「打开批量选择器」同一窗口。</summary>
    [MenuItem("Tools/批量选择器")]
    public static void ShowWindow()
    {
        GetWindow<BatchFbxImportWindow>("批量选择器").minSize = new Vector2(640f, 460f);
    }

    private void OnEnable()
    {
        settings = BatchFbxImportSettings.GetOrCreateAsset();
        EnsureFilterDefaults();
    }

    private void OnDisable()
    {
        settingsSerialized = null;
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = BatchFbxImportSettings.GetOrCreateAsset();
        }

        using (var scroll = new EditorGUILayout.ScrollViewScope(mainScroll))
        {
            mainScroll = scroll.scrollPosition;

            EditorGUILayout.HelpBox(
                "内核可识别：" + ToolImportApi.FormatSupportedExtensionsDisplay() + "\n" +
                "下面勾选只过滤本次收集列表，不表示没勾的格式内核不认识；CLI 指定文件仍认全表。\n" +
                "改勾选会按上次拖入/浏览的路径重新收集（取消的文件再勾选会回来）。清空列表才忘掉这些路径。\n" +
                "「执行导入」：只把文件送进导入区（Conflict 仍拦住；Warning 可导）。不建 Prefab、不平铺、不导出。\n" +
                "「同夹多模型」Warning（本面板夹名）：只看当前列表里三层名是否撞车，不扫盘上未列出的文件。\n" +
                "「输出到编排」建议 ID2：扫父目录磁盘上全部内核格式（不管本面板勾选）。\n" +
                "「输出到编排面板」：筛选完成，把路径 + 建议 ID2 交给总面板，不拷贝。编排运行按行走全流程。\n" +
                "建议 ID2：父目录磁盘上还有其它内核格式文件、或三层不足时，三层+文件全名。\n" +
                "夹名（本面板自己导入时）：三层；同夹多文件追加文件名（Warning）。目标已存在 / 交付区 = Conflict。",
                MessageType.Info);

            DrawSettings();
            DrawDropArea();
            DrawList();
            DrawActions();

            if (!string.IsNullOrEmpty(lastSummary))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(lastSummary, MessageType.None);
            }
        }
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("配置（ConfigData）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "deliveryAlertPathPrefixes 只拦「入库目标」是否落在交付区；" +
                "与贴图/模型高级设置里的 excludedPathPrefixes 是另一份列表。",
                MessageType.None);

            DrawExtensionFilter();

            EditorGUI.BeginChangeCheck();
            ScriptableObjectSettingsGui.Draw(settings, ref settingsSerialized);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
                RefreshItemStates();
            }

            if (!settings.TryValidateImportRoot(out string rootError))
            {
                EditorGUILayout.HelpBox(rootError, MessageType.Error);
            }
        }
    }

    private void DrawDropArea()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("收集", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "拖入外部文件夹（递归检索已勾选格式）；也可拖入单个模型文件", EditorStyles.helpBox);
            HandleDropAreaEvents(dropRect);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(108f)))
            {
                GUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(isRunning))
                {
                    if (GUILayout.Button("选择文件夹…", GUILayout.Height(40f)))
                    {
                        BrowseFolderAndAppend();
                    }
                }
            }
        }
    }

    private void HandleDropAreaEvents(Rect dropRect)
    {
        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
        {
            return;
        }

        bool accept = DragAndDrop.paths != null && DragAndDrop.paths.Length > 0;
        DragAndDrop.visualMode = accept
            ? DragAndDropVisualMode.Copy
            : DragAndDropVisualMode.Rejected;

        if (accept && evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            AppendDropped(DragAndDrop.paths);
        }

        evt.Use();
    }

    private void BrowseFolderAndAppend()
    {
        string folder = EditorUtility.OpenFolderPanel("选择含模型的文件夹", "", "");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        AppendDropped(new[] { folder });
    }

    private void DrawList()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("待导入列表（" + items.Count + "）", EditorStyles.boldLabel);

        int conflictCount = CountConflicts();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("清空列表", GUILayout.Width(100f)))
            {
                items.Clear();
                collectRoots.Clear();
                lastSummary = null;
            }

            using (new EditorGUI.DisabledScope(isRunning || conflictCount == 0))
            {
                if (GUILayout.Button("移除全部冲突（" + conflictCount + "）", GUILayout.Width(160f)))
                {
                    RemoveAllConflicts();
                }
            }

            if (GUILayout.Button("按勾选重扫", GUILayout.Width(120f)))
            {
                RecollectFromRoots();
            }

            if (GUILayout.Button("刷新冲突检测", GUILayout.Width(120f)))
            {
                RefreshItemStates();
            }
        }

        using (var listScope = new EditorGUILayout.ScrollViewScope(listScroll, GUILayout.MinHeight(180f)))
        {
            listScroll = listScope.scrollPosition;
            if (items.Count == 0)
            {
                EditorGUILayout.LabelField("尚无条目。拖入文件夹后在此显示源路径、目标夹名与冲突状态。");
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                BatchFbxImportService.ImportItem item = items[i];
                Color prev = GUI.color;
                GUI.color = StatusColor(item.Status);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUI.color = prev;

                    string fileName = Path.GetFileName(item.SourceFbxPath);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            (i + 1) + ". [" + item.Status + "] " + item.FolderName + "  ·  " + fileName,
                            EditorStyles.boldLabel);
                        using (new EditorGUI.DisabledScope(isRunning))
                        {
                            if (GUILayout.Button("移除", GUILayout.Width(56f)))
                            {
                                RemoveAt(i);
                                break;
                            }
                        }
                    }

                    EditorGUILayout.LabelField("源: " + item.SourceFbxPath, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        "目标: " + item.TargetFolderAssetPath + "/",
                        EditorStyles.miniLabel);
                    if (!string.IsNullOrEmpty(item.Message))
                    {
                        MessageType mt = item.Status == BatchFbxImportService.ItemStatus.Conflict ||
                                         item.Status == BatchFbxImportService.ItemStatus.Failed
                            ? MessageType.Error
                            : item.Status == BatchFbxImportService.ItemStatus.Warning
                                ? MessageType.Warning
                                : MessageType.None;
                        if (mt != MessageType.None)
                        {
                            EditorGUILayout.HelpBox(item.Message, mt);
                        }
                        else
                        {
                            EditorGUILayout.LabelField(item.Message, EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }
    }

    private void DrawActions()
    {
        EditorGUILayout.Space(8f);
        bool blocked = BatchFbxImportService.HasBlockingAlerts(items, settings, out string reason);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(isRunning || blocked))
            {
                if (GUILayout.Button("执行导入（本单步；无 Conflict）", GUILayout.Height(32f)))
                {
                    RunImport();
                }
            }

            using (new EditorGUI.DisabledScope(isRunning || items.Count == 0))
            {
                if (GUILayout.Button("输出到编排面板（筛选完成）", GUILayout.Height(32f)))
                {
                    OutputToOrchestration();
                }
            }
        }

        if (blocked && items.Count > 0)
        {
            EditorGUILayout.HelpBox(
                reason + " 「执行导入」需先处理冲突。「输出到编排」仍可用（编排覆盖槽，不走本面板 Conflict）。",
                MessageType.Warning);
        }
        else if (!blocked && items.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "无 Conflict。「执行导入」只入库；「输出到编排」不拷贝。",
                MessageType.Info);
        }
    }

    private void AppendDropped(string[] paths)
    {
        RememberRoots(paths);
        MergeCollected(
            BatchFbxImportService.CollectFromDroppedPaths(paths, settings, EnabledExtensionList()),
            "新加入");
    }

    private void RecollectFromRoots()
    {
        items.Clear();
        if (collectRoots.Count == 0)
        {
            lastSummary = "没有记住的拖入/浏览路径。请先拖入或选择文件夹。";
            Repaint();
            return;
        }

        MergeCollected(
            BatchFbxImportService.CollectFromDroppedPaths(collectRoots, settings, EnabledExtensionList()),
            "按勾选重扫");
    }

    private void RememberRoots(IEnumerable<string> paths)
    {
        if (paths == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < collectRoots.Count; i++)
        {
            seen.Add(collectRoots[i]);
        }

        foreach (string raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string n = raw.Replace("\\", "/").Trim();
            if (seen.Add(n))
            {
                collectRoots.Add(n);
            }
        }
    }

    private void MergeCollected(List<BatchFbxImportService.ImportItem> collected, string verb)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (BatchFbxImportService.ImportItem item in items)
        {
            existing.Add(item.SourceFbxPath);
        }

        int added = 0;
        if (collected != null)
        {
            foreach (BatchFbxImportService.ImportItem item in collected)
            {
                if (item != null && existing.Add(item.SourceFbxPath))
                {
                    items.Add(item);
                    added++;
                }
            }
        }

        RefreshItemStates();
        lastSummary = verb + " " + added + " 个模型，列表共 " + items.Count + " 条。";
        Repaint();
    }

    private void OutputToOrchestration()
    {
        var paths = new List<string>();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && !string.IsNullOrEmpty(items[i].SourceFbxPath))
            {
                paths.Add(items[i].SourceFbxPath);
            }
        }

        List<PipelineSourceBinding> bindings = PipelineMaterialId.SuggestBindingsForSelection(paths);
        if (bindings.Count == 0)
        {
            lastSummary = "没有可输出的路径。";
            return;
        }

        PipelineSourceAccept.SendToOrchestration(bindings);
        lastSummary = "已输出 " + bindings.Count + " 条到编排面板（未入库）。建议 ID2：父目录仅一个内核文件用三层；还有其它则加文件全名。";
        Repaint();
    }

    private void DrawExtensionFilter()
    {
        EnsureFilterDefaults();
        EditorGUILayout.LabelField("本次收集格式（子集）", EditorStyles.miniBoldLabel);
        string[] all = ToolImportApi.GetSupportedModelExtensions();
        using (new EditorGUI.DisabledScope(isRunning))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < all.Length; i++)
                {
                    string ext = all[i];
                    bool on = enabledExtensions.Contains(ext);
                    bool next = EditorGUILayout.ToggleLeft(ext, on, GUILayout.Width(72f));
                    if (next == on)
                    {
                        continue;
                    }

                    if (next)
                    {
                        enabledExtensions.Add(ext);
                    }
                    else
                    {
                        enabledExtensions.Remove(ext);
                    }

                    RecollectFromRoots();
                }
            }
        }
    }

    private void EnsureFilterDefaults()
    {
        if (filterInitialized)
        {
            return;
        }

        string[] all = ToolImportApi.GetSupportedModelExtensions();
        for (int i = 0; i < all.Length; i++)
        {
            enabledExtensions.Add(all[i]);
        }

        filterInitialized = true;
    }

    private List<string> EnabledExtensionList()
    {
        EnsureFilterDefaults();
        return new List<string>(enabledExtensions);
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        string fileName = Path.GetFileName(items[index].SourceFbxPath);
        items.RemoveAt(index);
        RefreshItemStates();
        lastSummary = "已移除：" + fileName + "；列表剩 " + items.Count + " 条。";
        GUI.FocusControl(null);
        Repaint();
    }

    private void RemoveAllConflicts()
    {
        int before = items.Count;
        items.RemoveAll(item => item != null &&
            item.Status == BatchFbxImportService.ItemStatus.Conflict);
        int removed = before - items.Count;
        RefreshItemStates();
        lastSummary = "已移除全部冲突 " + removed + " 条；列表剩 " + items.Count + " 条。";
        Repaint();
    }

    private int CountConflicts()
    {
        int n = 0;
        foreach (BatchFbxImportService.ImportItem item in items)
        {
            if (item != null && item.Status == BatchFbxImportService.ItemStatus.Conflict)
            {
                n++;
            }
        }

        return n;
    }

    private void RefreshItemStates()
    {
        List<BatchFbxImportService.ImportItem> rebuilt =
            BatchFbxImportService.RebuildItems(items, settings);
        items.Clear();
        items.AddRange(rebuilt);
    }

    private void RunImport()
    {
        if (isRunning)
        {
            return;
        }

        RefreshItemStates();
        if (BatchFbxImportService.HasBlockingAlerts(items, settings, out string reason))
        {
            lastSummary = "未执行：" + reason;
            return;
        }

        isRunning = true;
        try
        {
            BatchFbxImportService.BatchResult result =
                BatchFbxImportService.ExecuteBatch(items, settings);

            int removedSuccess = items.RemoveAll(item =>
                item != null && item.Status == BatchFbxImportService.ItemStatus.Success);

            RefreshItemStates();

            lastSummary = result.SummaryMessage + "；已移出成功 " + removedSuccess + " 条";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            isRunning = false;
            Repaint();
        }
    }

    private static Color StatusColor(BatchFbxImportService.ItemStatus status)
    {
        switch (status)
        {
            case BatchFbxImportService.ItemStatus.Conflict:
            case BatchFbxImportService.ItemStatus.Failed:
                return new Color(1f, 0.75f, 0.75f);
            case BatchFbxImportService.ItemStatus.Warning:
                return new Color(1f, 0.95f, 0.7f);
            case BatchFbxImportService.ItemStatus.Success:
                return new Color(0.75f, 1f, 0.8f);
            case BatchFbxImportService.ItemStatus.Skipped:
                return new Color(0.85f, 0.85f, 0.85f);
            default:
                return Color.white;
        }
    }
}
