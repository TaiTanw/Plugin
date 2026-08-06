using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 批量 FBX 导入窗口：拖入外部文件夹→检索 FBX→面板标重名→无冲突时统一执行。
// 边界：只把 FBX 干净送进导入区；不建 Prefab / 不平铺 / 不导出。
// =====================================================================================
public class BatchFbxImportWindow : EditorWindow
{
    private BatchFbxImportSettings settings;
    private SerializedObject settingsSerialized;
    private readonly List<BatchFbxImportService.ImportItem> items =
        new List<BatchFbxImportService.ImportItem>();
    private Vector2 mainScroll;
    private Vector2 listScroll;
    private string lastSummary;
    private bool isRunning;

    [MenuItem("Tools/批量FBX导入")]
    public static void ShowWindow()
    {
        GetWindow<BatchFbxImportWindow>("批量FBX导入").minSize = new Vector2(640f, 420f);
    }

    private void OnEnable()
    {
        settings = BatchFbxImportSettings.GetOrCreateAsset();
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
                "本面板只负责把外部 FBX 干净送进导入区（一 FBX 一夹）。\n" +
                "夹名 = 自身向上连续 3 层目录名，斜杠位用下划线拼接（如 飞机模型待处理_模型名_fbx）；\n" +
                "不足 3 层用全路径消毒名（Warning，不禁用执行）。\n" +
                "同夹不同文件名仍算夹名冲突；可用单条移除或「移除全部冲突」后再执行。\n" +
                "交付文件名仍以人工改好的 Prefab 名为准；不自动建预设体、不平铺、不导出。\n" +
                "取消：当前这条 FBX 整段做完后再停。",
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

        Rect dropRect = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "拖入外部文件夹（递归检索 .fbx）；也可拖入单个 .fbx", EditorStyles.helpBox);

        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            bool accept = false;
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                accept = true;
            }

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
                lastSummary = null;
            }

            using (new EditorGUI.DisabledScope(isRunning || conflictCount == 0))
            {
                if (GUILayout.Button("移除全部冲突（" + conflictCount + "）", GUILayout.Width(160f)))
                {
                    RemoveAllConflicts();
                }
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
        using (new EditorGUI.DisabledScope(isRunning || blocked))
        {
            if (GUILayout.Button("执行导入（无重名警报时可用）", GUILayout.Height(32f)))
            {
                RunImport();
            }
        }

        if (blocked && items.Count > 0)
        {
            EditorGUILayout.HelpBox(
                reason + " 可用单条「移除」或「移除全部冲突」处理后再执行。",
                MessageType.Warning);
        }
        else if (!blocked && items.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "无重名/冲突警报，可统一执行。进度条可取消：当前 FBX 完成后停止。",
                MessageType.Info);
        }
    }

    private void AppendDropped(string[] paths)
    {
        List<BatchFbxImportService.ImportItem> collected =
            BatchFbxImportService.CollectFromDroppedPaths(paths, settings);

        var existing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (BatchFbxImportService.ImportItem item in items)
        {
            existing.Add(item.SourceFbxPath);
        }

        int added = 0;
        foreach (BatchFbxImportService.ImportItem item in collected)
        {
            if (existing.Add(item.SourceFbxPath))
            {
                items.Add(item);
                added++;
            }
        }

        RefreshItemStates();
        lastSummary = added > 0
            ? "新加入 " + added + " 个 FBX，列表共 " + items.Count + " 条。"
            : "未加入新 FBX（可能与列表重复或路径下无 .fbx）。";
        Repaint();
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
            lastSummary = result.SummaryMessage;
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
