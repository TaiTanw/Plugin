using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Pipeline — D3 自动化管线总面板（步骤开关走 SO）
// 可收单文件，也可收批量「输出到编排」的路径+ID2 表。Runner 按行 1→2.5→③。
// =====================================================================================

/// <summary>
/// 流程编排人机入口。[1] 平铺展示；[2]～[⑥] 收入「步骤详情」。
/// </summary>
public class PipelineWindow : EditorWindow
{
    private const string StepsDetailFoldoutPrefsKey = "Pipeline.Window.StepsDetailFoldout";

    private PipelineStepSettings settings;
    private RetinarExportSettings exportSettings;
    private FlattenOperationSettings flattenSettings;
    private bool stepsDetailFoldout;
    private string sourcePath = string.Empty;
    private string materialId = string.Empty;
    /// <summary>上次已为 materialId 同步过的源路径；源变化时才重写默认 Id。</summary>
    private string materialIdSyncedForSource = string.Empty;
    private readonly List<PipelineSourceBinding> sourceBindings = new List<PipelineSourceBinding>();

    /// <summary>源路径 → OBJ 文件头里的 Z-up 导出器署名。绑定变化时算一次，OnGUI 不读盘。</summary>
    private readonly Dictionary<string, string> axisHints = new Dictionary<string, string>();
    private Vector2 bindingsScroll;
    private string lastResultText = string.Empty;
    private Vector2 scroll;
    private Vector2 resultScroll;

    [MenuItem("Tools/自动化管线总面板", false, 20)]
    public static void ShowWindow()
    {
        GetWindow<PipelineWindow>("自动化管线").minSize = new Vector2(480f, 620f);
    }

    /// <summary>
    /// 批量面板「输出到编排」入口：整表替换路径+建议 ID2。不入库。
    /// Conflict 不拦本调用。
    /// </summary>
    public static void AcceptBindings(IList<PipelineSourceBinding> bindings)
    {
        PipelineWindow window = GetWindow<PipelineWindow>("自动化管线");
        window.minSize = new Vector2(480f, 620f);
        window.ApplyBindings(bindings);
        window.Focus();
        window.Repaint();
    }

    private void OnEnable()
    {
        settings = PipelineStepSettings.GetOrCreateAsset();
        exportSettings = RetinarExportSettings.GetOrCreateAsset();
        flattenSettings = FlattenOperationSettings.GetOrCreatePipelineAsset();
        TextureProcessSettings.GetOrCreatePipelineAsset();
        MaterialProcessSettings.GetOrCreatePipelineAsset();
        ModelProcessSettings.GetOrCreatePipelineAsset();
        ImportPipelineSettings.GetOrCreateAsset();
        stepsDetailFoldout = EditorPrefs.GetBool(StepsDetailFoldoutPrefsKey, false);
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = PipelineStepSettings.GetOrCreateAsset();
        }

        if (flattenSettings == null)
        {
            flattenSettings = FlattenOperationSettings.GetOrCreatePipelineAsset();
        }

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;

            EditorGUILayout.LabelField("自动化管线", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "选源后运行。模型 " + ToolImportApi.FormatSupportedExtensionsDisplay() +
                "。unitypackage 用「浏览…」（表上只一条 pack）。拖入与选择器不收 pack。",
                MessageType.Info);

            DrawStepImport();
            DrawRunPipelineButton();
            DrawStepsDetail();

            if (!string.IsNullOrEmpty(lastResultText))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("上次结果", EditorStyles.boldLabel);
                using (var rs = new EditorGUILayout.ScrollViewScope(resultScroll, GUILayout.Height(160f)))
                {
                    resultScroll = rs.scrollPosition;
                    EditorGUILayout.TextArea(lastResultText, GUILayout.ExpandHeight(true));
                }
            }
        }
    }

    private void DrawSourceSection()
    {
        Rect drop = GUILayoutUtility.GetRect(0f, 56f, GUILayout.ExpandWidth(true));
        GUI.Box(drop, sourceBindings.Count > 1
            ? "已收 " + sourceBindings.Count + " 条"
            : (string.IsNullOrEmpty(sourcePath) ? "拖入模型（文件夹用批量选择器）" : Path.GetFileName(sourcePath)));
        HandleDrag(drop);

        EditorGUILayout.BeginHorizontal();
        if (sourceBindings.Count == 0)
        {
            EditorGUI.BeginChangeCheck();
            string edited = EditorGUILayout.TextField(sourcePath);
            if (EditorGUI.EndChangeCheck())
            {
                SetSourcePath(edited);
            }
        }

        if (GUILayout.Button("浏览…", GUILayout.Width(64f)))
        {
            string picked = EditorUtility.OpenFilePanel(
                "选择模型或 unitypackage",
                string.IsNullOrEmpty(sourcePath) ? "" : Path.GetDirectoryName(sourcePath),
                PipelinePreviewSources.OpenFileFilter);
            if (!string.IsNullOrEmpty(picked))
            {
                SetSourcePath(picked);
            }
        }

        if (GUILayout.Button("清除", GUILayout.Width(48f)))
        {
            SetSourcePath(string.Empty);
        }

        EditorGUILayout.EndHorizontal();
        if (sourceBindings.Count > 0)
        {
            EditorGUILayout.LabelField("路径只读，换源请拖入 / 浏览 / 批量输出。ID2 可改。", EditorStyles.miniLabel);
        }
    }

    private void DrawStepImport()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("[1] 入库 · 拷入导入根", EditorStyles.boldLabel);
            if (GUILayout.Button("打开批量选择器", GUILayout.Height(26f)))
            {
                BatchFbxImportWindow.ShowWindow();
            }

            DrawSourceSection();
            DrawBindingsTable();
            DrawWorkspaceRoots();
            if (IsGltfSourcePath(ActiveSourcePath()))
            {
                EditorGUILayout.HelpBox("gltf 整包入库，④ 按伴生搬迁，不必先转 GLB。", MessageType.Info);
            }
        }
    }

    private void DrawWorkspaceRoots()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("工作路径", EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();
        settings.importRootPath = EditorGUILayout.TextField("导入根", settings.importRootPath);
        settings.prefabRootPath = EditorGUILayout.TextField("Prefab 根", settings.prefabRootPath);
        settings.artRootPath = EditorGUILayout.TextField("交付根", settings.artRootPath);
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
        }

        var probe = new PipelineOptions();
        probe.ImportRoot = settings.importRootPath;
        probe.PrefabRoot = settings.prefabRootPath;
        probe.ArtRoot = settings.artRootPath;
        if (!PipelineWorkspace.TryValidate(probe, out string workspaceError))
        {
            EditorGUILayout.HelpBox(workspaceError, MessageType.Error);
        }
    }

    private void DrawRunPipelineButton()
    {
        EditorGUILayout.Space(10f);
        if (PipelinePreviewSources.TableHasPack(sourceBindings))
        {
            EditorGUILayout.HelpBox(
                "pack 的 ID2 只当信封夹名。运行后按根 Prefab 拆行（子 Prefab 不单独一批）。",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(sourcePath)))
        {
            string runLabel = sourceBindings.Count > 1
                ? "运行管线（" + sourceBindings.Count + " 行）"
                : "运行管线";
            if (GUILayout.Button(runLabel, GUILayout.Height(36f)))
            {
                EditorApplication.delayCall += RunPipeline;
            }
        }
    }

    private void DrawStepsDetail()
    {
        EditorGUILayout.Space(8f);
        bool open = EditorGUILayout.Foldout(stepsDetailFoldout, "步骤详情", true, EditorStyles.foldoutHeader);
        if (open != stepsDetailFoldout)
        {
            stepsDetailFoldout = open;
            EditorPrefs.SetBool(StepsDetailFoldoutPrefsKey, open);
        }

        if (!stepsDetailFoldout)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawStepImportPolicy();
            DrawStepPrefab();
            DrawStepFlatten();
            DrawStepPostProcess();
            DrawStepExport();
        }
    }

    private void DrawStepImportPolicy()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("[2] 导入期 · 全局钩子", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Unity OnPreprocess 只看全局导入设置总开关与本页分项。不读批量选择器导入根。",
                MessageType.None);
            if (GUILayout.Button("打开全局导入设置（2）", GUILayout.Height(24f)))
            {
                ImportPipelineSettingsWindow.ShowWindow();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "选中 SO 只展示位置，改动请通过面板修改。",
                    EditorStyles.miniLabel);
                if (GUILayout.Button("选中步骤 SO", GUILayout.Width(110f), GUILayout.Height(22f)))
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
        }
    }

    private void DrawStepPrefab()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            settings.runPrefab = EditorGUILayout.ToggleLeft("[③] Prefab · 按 ID2 写盘", settings.runPrefab);
            if (!settings.runPrefab)
            {
                settings.runFlatten = false;
                settings.runPostProcess = false;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.HelpBox("写入 Prefab 根，不改模型文件名。空 ID2 用三层夹名。", MessageType.None);
        }
    }

    private void DrawStepFlatten()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(!settings.runPrefab))
            {
                settings.runFlatten = EditorGUILayout.ToggleLeft("[④] 平铺 · 写入交付根", settings.runFlatten);
            }

            if (!settings.runFlatten)
            {
                settings.runPostProcess = false;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.HelpBox("分类 / 清夹 / 碰撞体在管线平铺 SO。", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("平铺设置", GUILayout.Height(22f)))
                {
                    FlattenSettingsWindow.OpenPipeline();
                }

                if (GUILayout.Button("选中 SO", GUILayout.Height(22f)))
                {
                    Selection.activeObject = flattenSettings;
                    EditorGUIUtility.PingObject(flattenSettings);
                }
            }
        }
    }

    private void DrawStepPostProcess()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(!settings.runPrefab || !settings.runFlatten))
            {
                settings.runPostProcess = EditorGUILayout.ToggleLeft(
                    "[⑤] 总批量 · 处理 Art 单元", settings.runPostProcess);
            }

            EditorGUILayout.HelpBox("纳入与设置只改编排 SO（⑤ Op）。导入期在「全局导入设置（2）」。", MessageType.None);
            using (new EditorGUI.DisabledScope(!settings.runPostProcess))
            {
                DrawPostProcessIncludeRow(
                    "贴图",
                    ref settings.postProcessIncludeTexture,
                    TextureAdvancedSettingsWindow.ShowPipelineWindow);
                DrawPostProcessIncludeRow(
                    "材质",
                    ref settings.postProcessIncludeMaterial,
                    MaterialAdvancedSettingsWindow.ShowPipelineWindow);
                DrawPostProcessIncludeRow(
                    "模型",
                    ref settings.postProcessIncludeModel,
                    ModelAdvancedSettingsWindow.ShowPipelineWindow);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }

    private static void DrawPostProcessIncludeRow(string label, ref bool value, System.Action openSettings)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            value = EditorGUILayout.ToggleLeft(label, value);
            if (GUILayout.Button("设置", GUILayout.Width(52f), GUILayout.Height(20f)))
            {
                if (openSettings != null)
                {
                    openSettings();
                }
            }
        }
    }

    private void DrawStepExport()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            settings.runAb = EditorGUILayout.ToggleLeft("[⑥] 导出 · 打 AB", settings.runAb);
            settings.quiet = EditorGUILayout.ToggleLeft("Quiet（无确认框）", settings.quiet);
            settings.cleanupImportRootsAfterRun = EditorGUILayout.ToggleLeft(
                "本趟结束后清空导入区（Incoming + IncomingPrefab）",
                settings.cleanupImportRootsAfterRun);
            settings.cleanupArtAfterRun = EditorGUILayout.ToggleLeft(
                "本趟结束后清空交付根（Art）",
                settings.cleanupArtAfterRun);
            EditorGUILayout.HelpBox(
                "产物路径在导出 SO。开④时打的是交付根 Prefab。清空只删勾选根下的文件，根夹留下；中途硬失败不清。不弹确认框，与 Quiet 无关。先清 Art，删完再刷新，避免半截 glTF 重导。",
                MessageType.None);
            DrawExportSettingsSummary();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }

    private void DrawExportSettingsSummary()
    {
        if (exportSettings == null)
        {
            exportSettings = RetinarExportSettings.GetOrCreateAsset();
        }

        if (GUILayout.Button("导出 SO", GUILayout.Height(22f)))
        {
            Selection.activeObject = exportSettings;
            EditorGUIUtility.PingObject(exportSettings);
        }

        if (exportSettings == null)
        {
            return;
        }

        string products = "Android/iOS AB";
        if (exportSettings.copyAbToDeliverables)
        {
            products += " → 交付夹";
        }

        if (exportSettings.exportUnityPackage)
        {
            products += " + UP";
        }

        EditorGUILayout.LabelField(
            exportSettings.deliverableRoot + "  |  " + exportSettings.assetBundleRoot + "  |  " + products,
            EditorStyles.miniLabel);
    }

    private void DrawBindingsTable()
    {
        EditorGUILayout.LabelField("源与 ID2（" + sourceBindings.Count + " 行）", EditorStyles.miniBoldLabel);
        if (sourceBindings.Count == 0)
        {
            EditorGUILayout.LabelField("拖入、浏览，或从批量选择器输出到编排。", EditorStyles.miniLabel);
            return;
        }

        using (var bs = new EditorGUILayout.ScrollViewScope(bindingsScroll, GUILayout.MinHeight(96f), GUILayout.MaxHeight(280f)))
        {
            bindingsScroll = bs.scrollPosition;
            for (int i = 0; i < sourceBindings.Count; i++)
            {
                PipelineSourceBinding row = sourceBindings[i];
                if (row == null)
                {
                    continue;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            (i + 1) + ". " + Path.GetFileName(row.SourcePath ?? string.Empty),
                            EditorStyles.boldLabel);
                        if (GUILayout.Button("移除", GUILayout.Width(48f)))
                        {
                            sourceBindings.RemoveAt(i);
                            SyncLegacyFieldsFromRowZero();
                            GUI.FocusControl(null);
                            break;
                        }
                    }

                    EditorGUILayout.LabelField("路径", EditorStyles.miniLabel);
                    EditorGUILayout.SelectableLabel(
                        row.SourcePath ?? string.Empty,
                        EditorStyles.wordWrappedLabel,
                        GUILayout.MinHeight(32f));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("ID2", GUILayout.Width(32f));
                        EditorGUI.BeginChangeCheck();
                        string id = EditorGUILayout.TextField(row.MaterialId ?? string.Empty);
                        if (EditorGUI.EndChangeCheck())
                        {
                            row.MaterialId = (id ?? string.Empty).Trim();
                            if (i == 0)
                            {
                                SyncLegacyFieldsFromRowZero();
                            }
                        }
                    }

                    DrawAxisRow(row);
                }
            }
        }
    }

    /// <summary>
    /// 只对 .obj 出现：FBX / glTF 头里有 up-axis，Unity 自己会转，勾了反而转过头。
    /// </summary>
    private void DrawAxisRow(PipelineSourceBinding row)
    {
        string path = row.SourcePath ?? string.Empty;
        if (!path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        row.ConvertZUpToYUp = EditorGUILayout.ToggleLeft("OBJ 轴向 −90°X", row.ConvertZUpToYUp);
        string hint;
        if (axisHints.TryGetValue(path, out hint))
        {
            EditorGUILayout.LabelField("导出器「" + hint + "」默认 Z-up", EditorStyles.miniLabel);
        }
    }

    private void HandleDrag(Rect dropArea)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
        {
            return;
        }

        List<string> files = CollectDroppedModelPaths();
        if (files.Count == 0)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            ApplyBindings(PipelineMaterialId.SuggestBindingsForSelection(files));
            evt.Use();
            Repaint();
        }
    }

    private static List<string> CollectDroppedModelPaths()
    {
        var files = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                string p = DragAndDrop.paths[i];
                if (string.IsNullOrEmpty(p) || !ToolImportApi.IsSupportedExtension(Path.GetExtension(p)))
                {
                    continue;
                }

                string n = p.Replace("\\", "/");
                if (seen.Add(n))
                {
                    files.Add(n);
                }
            }
        }

        if (files.Count == 0 && DragAndDrop.objectReferences != null)
        {
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                Object obj = DragAndDrop.objectReferences[i];
                string ap = AssetDatabase.GetAssetPath(obj);
                if (!ToolImportApi.IsSupportedExtension(Path.GetExtension(ap)))
                {
                    continue;
                }

                string n = ap.Replace("\\", "/");
                if (seen.Add(n))
                {
                    files.Add(n);
                }
            }
        }

        return files;
    }

    private static bool IsGltfSourcePath(string path)
    {
        return !string.IsNullOrEmpty(path) &&
               string.Equals(Path.GetExtension(path), ".gltf", System.StringComparison.OrdinalIgnoreCase);
    }

    private string ActiveSourcePath()
    {
        if (sourceBindings.Count > 0 && sourceBindings[0] != null &&
            !string.IsNullOrEmpty(sourceBindings[0].SourcePath))
        {
            return sourceBindings[0].SourcePath;
        }

        return sourcePath;
    }

    private void ApplyBindings(IList<PipelineSourceBinding> bindings)
    {
        sourceBindings.Clear();
        List<PipelineSourceBinding> table = PipelinePreviewSources.NormalizeTable(bindings);
        for (int i = 0; i < table.Count; i++)
        {
            sourceBindings.Add(table[i]);
        }

        RefreshAxisHints();
        SyncLegacyFieldsFromRowZero();
    }

    private void RefreshAxisHints()
    {
        axisHints.Clear();
        for (int i = 0; i < sourceBindings.Count; i++)
        {
            PipelineSourceBinding row = sourceBindings[i];
            if (row == null || string.IsNullOrEmpty(row.SourcePath) || axisHints.ContainsKey(row.SourcePath))
            {
                continue;
            }

            string note = PipelineObjAxisProbe.SniffZUpExporter(row.SourcePath);
            if (!string.IsNullOrEmpty(note))
            {
                axisHints[row.SourcePath] = note;
            }
        }
    }

    private void SyncLegacyFieldsFromRowZero()
    {
        if (sourceBindings.Count == 0)
        {
            sourcePath = string.Empty;
            materialId = string.Empty;
            materialIdSyncedForSource = string.Empty;
            return;
        }

        PipelineSourceBinding row = sourceBindings[0];
        sourcePath = row.SourcePath ?? string.Empty;
        materialId = row.MaterialId ?? string.Empty;
        materialIdSyncedForSource = sourcePath;
    }

    private void SyncRowZeroFromLegacyFields()
    {
        if (sourceBindings.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            sourceBindings.Add(new PipelineSourceBinding(sourcePath, materialId));
            materialIdSyncedForSource = sourcePath;
            RefreshAxisHints();
            return;
        }

        sourceBindings[0].SourcePath = sourcePath;
        sourceBindings[0].MaterialId = materialId ?? string.Empty;
        materialIdSyncedForSource = sourcePath;
        RefreshAxisHints();
    }

    /// <summary>改源路径：整表换成建议 ID2 的一行；清空则清表。</summary>
    private void SetSourcePath(string path)
    {
        string normalized = (path ?? string.Empty).Replace("\\", "/").Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            sourceBindings.Clear();
            sourcePath = string.Empty;
            materialId = string.Empty;
            materialIdSyncedForSource = string.Empty;
            return;
        }

        ApplyBindings(PipelineMaterialId.SuggestBindingsForSelection(new[] { normalized }));
    }

    private void RunPipeline()
    {
        SyncRowZeroFromLegacyFields();
        if (sourceBindings.Count == 0 && !string.IsNullOrWhiteSpace(sourcePath))
        {
            ApplyBindings(PipelineMaterialId.SuggestBindingsForSelection(new[] { sourcePath }));
            if (!string.IsNullOrWhiteSpace(materialId) && sourceBindings.Count > 0)
            {
                sourceBindings[0].MaterialId = materialId.Trim();
            }
        }

        string runSource = ActiveSourcePath();
        string runId = sourceBindings.Count > 0 ? sourceBindings[0].MaterialId : materialId;

        AssetDatabase.SaveAssets();
        PipelineOptions opt = PipelineOptions.FromSettings(settings, runSource);
        if (!string.IsNullOrWhiteSpace(runId))
        {
            opt.MaterialId = runId.Trim();
        }

        opt.SourceBindings = CopyBindings();

        PipelineResult result = PipelineRunner.Run(opt);
        lastResultText = result.ToString();
        Repaint();

        if (!opt.Quiet && !Application.isBatchMode)
        {
            string body = lastResultText;
            const int dialogCap = 1500;
            if (body.Length > dialogCap)
            {
                body = body.Substring(0, dialogCap) + "\n…其余见面板「上次结果」/ Console";
            }

            EditorUtility.DisplayDialog(
                result.Ok ? "自动化管线完成" : "自动化管线失败",
                body,
                "OK");
        }
    }

    private List<PipelineSourceBinding> CopyBindings()
    {
        var copy = new List<PipelineSourceBinding>(sourceBindings.Count);
        for (int i = 0; i < sourceBindings.Count; i++)
        {
            PipelineSourceBinding row = sourceBindings[i];
            if (row == null)
            {
                continue;
            }

            copy.Add(row.CloneWith(null));
        }

        return copy;
    }
}
