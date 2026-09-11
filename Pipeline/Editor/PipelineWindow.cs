using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Pipeline — D3 自动化管线总面板（步骤开关走 SO）
// 可收单文件，也可收批量「输出到编排」的路径+ID2 表。Runner 按行 1→2.5→③。
// =====================================================================================

/// <summary>
/// 流程编排人机入口。导入区 1 入库无开关、2 总闸=MasterEnabled；分项自动在资源处理总面板。
/// </summary>
public class PipelineWindow : EditorWindow
{
    private PipelineStepSettings settings;
    private RetinarExportSettings exportSettings;
    private FlattenOperationSettings flattenSettings;
    private bool flattenSettingsFoldout;
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

    [MenuItem("Tools/自动化管线总面板", false, 40)]
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
                "三区：导入（1 入库 / 2 总自动化）→ 处理（③④⑤）→ 输出（⑥）。\n" +
                "内核格式：" + ToolImportApi.FormatSupportedExtensionsDisplay() +
                "（编排始终认全表；批量勾选只筛那次收集）。\n" +
                "处理区须开前一步才能开后一步：③开才能④，④开才能⑤。\n" +
                "导入区 2 只开总闸（与资源总面板「总开关」同一 Prefs）；" +
                "哪些资源、设置自动/后处理自动仍在资源总面板。\n" +
                "多文件：用批量面板「输出到编排」填表。行号由 Runner 调度：每行 1 入库 → 2.5 ctx → ③（该行 ID2）。",
                MessageType.Info);

            DrawImportZone();
            DrawProcessZone();
            DrawOutputZone();

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(sourcePath)))
            {
                string runLabel = sourceBindings.Count > 1
                    ? "运行管线（" + sourceBindings.Count + " 行）"
                    : "运行管线";
                if (GUILayout.Button(runLabel, GUILayout.Height(36f)))
                {
                    // 不要在 OnGUI 里同步跑完整管线：入库/重导会嵌套 IMGUI，出现 GUILayout / GUIClip 不平衡。
                    EditorApplication.delayCall += RunPipeline;
                }
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开批量选择器", GUILayout.Height(26f)))
                {
                    BatchFbxImportWindow.ShowWindow();
                }

                if (GUILayout.Button("打开资源处理总面板", GUILayout.Height(26f)))
                {
                    ResourceProcessWindow.ShowWindow();
                }

                if (GUILayout.Button("选中步骤 SO", GUILayout.Height(26f)))
                {
                    Selection.activeObject = settings;
                }

                if (GUILayout.Button("选中导出 SO", GUILayout.Height(26f)))
                {
                    Selection.activeObject = RetinarExportSettings.GetOrCreateAsset();
                }
            }

            if (GUILayout.Button("选中管线平铺 SO", GUILayout.Height(24f)))
            {
                Selection.activeObject = flattenSettings;
                EditorGUIUtility.PingObject(flattenSettings);
            }

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
            ? "已收 " + sourceBindings.Count + " 条；再拖入将整表替换"
            : (string.IsNullOrEmpty(sourcePath)
                ? "拖放模型文件到此处（可多选；文件夹请用批量选择器）"
                : Path.GetFileName(sourcePath)));
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
                "选择模型",
                string.IsNullOrEmpty(sourcePath) ? "" : Path.GetDirectoryName(sourcePath),
                "fbx,glb,gltf,obj");
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
            EditorGUILayout.LabelField(
                "表内路径只读。换文件请拖入、浏览，或从批量面板重新输出。ID2 可改。",
                EditorStyles.miniLabel);
        }
    }

    /// <summary>导入区：1 入库（无勾选）+ 2 总自动化处理（MasterEnabled）。</summary>
    private void DrawImportZone()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("导入区", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSourceSection();
            DrawBindingsTable();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("1 入库（导入器，无开关）", EditorStyles.miniBoldLabel);
            BatchFbxImportSettings importSettings = BatchFbxImportSettings.Current;
            string importRoot = importSettings != null
                ? importSettings.NormalizedImportRoot
                : "Assets/Incoming";
            EditorGUILayout.HelpBox(
                "工程外文件始终拷入导入根并 ImportAsset（已在 Assets 内则复用、不拷）。\n" +
                "ID2 非空时 Incoming 槽 = Incoming/<ID2>/（D18 只清该子夹）；空则仍用三层夹名。\n" +
                "导入根 / 禁止写入 Art：与【批量 FBX 导入】同一份 BatchFbxImportSettings，本区不改。\n" +
                "当前导入根：" + importRoot,
                MessageType.None);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("2 自动化设置", EditorStyles.miniBoldLabel);
            bool master = EditorGUILayout.ToggleLeft(
                "总自动化处理（导入期回调总闸）", ResourceProcessSwitches.MasterEnabled);
            if (master != ResourceProcessSwitches.MasterEnabled)
            {
                ResourceProcessSwitches.MasterEnabled = master;
            }

            if (!ResourceProcessSwitches.MasterEnabled)
            {
                EditorGUILayout.HelpBox(
                    "总闸已关：ImportAsset 仍会入库；配置导入根内的模型安全基线（剔灯剔相机 / OBJ 法线）仍会写入。\n" +
                    "用户设置自动 / 后处理自动不执行，资源总面板里的分项勾选此时无效。⑤ 手动总批量不受此闸影响。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "总闸已开。模型基线（剔灯剔相机 / OBJ 法线）此时必跑，不看下面的分项勾选；" +
                    "其余由【资源处理总面板】决定：贴图/模型谁开、是「设置自动」还是「后处理自动」。\n" +
                    "现状（只读）：贴图 设置" +
                    (ResourceProcessSwitches.TextureSettingsAuto ? "开" : "关") +
                    " / 后处理" +
                    (ResourceProcessSwitches.TexturePostProcessAuto ? "开" : "关") +
                    "；模型 设置" +
                    (ResourceProcessSwitches.ModelSettingsAuto ? "开" : "关") +
                    " / 后处理" +
                    (ResourceProcessSwitches.ModelPostProcessAuto ? "开" : "关") +
                    "。改分项请打开资源总面板。",
                    MessageType.None);
            }

            if (IsGltfSourcePath(ActiveSourcePath()))
            {
                EditorGUILayout.HelpBox(
                    "源是 .gltf（JSON + 旁路 .bin/贴图）。管线会整包入库，④ 按原子夹搬迁，不必先转 GLB。\n" +
                    "转成 GLB 仍可用（DCC / gltf-pipeline），不是必须。编辑器不会做 DCC 重导。",
                    MessageType.Info);
            }
        }
    }

    /// <summary>处理区：③→④→⑤ 连锁。</summary>
    private void DrawProcessZone()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("处理区", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            settings.runPrefab = EditorGUILayout.ToggleLeft("③ Prefab", settings.runPrefab);
            if (!settings.runPrefab)
            {
                settings.runFlatten = false;
                settings.runPostProcess = false;
            }

            using (new EditorGUI.DisabledScope(!settings.runPrefab))
            {
                settings.runFlatten = EditorGUILayout.ToggleLeft(
                    "④ 平铺到交付中间区（Art）", settings.runFlatten);
            }

            if (!settings.runFlatten)
            {
                settings.runPostProcess = false;
            }

            using (new EditorGUI.DisabledScope(!settings.runPrefab || !settings.runFlatten))
            {
                settings.runPostProcess = EditorGUILayout.ToggleLeft(
                    "⑤ 资源总批量（压图 / 材质 / 刷顶点色）", settings.runPostProcess);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.HelpBox(
                "须开前一步才能开后一步。④ 根路径写死 Assets/Art；详细分类、清夹和碰撞体来自管线平铺 SO，并在运行前冻结。",
                MessageType.None);

            flattenSettingsFoldout = EditorGUILayout.Foldout(
                flattenSettingsFoldout, "④ 管线平铺操作配置", true);
            if (flattenSettingsFoldout)
            {
                FlattenOperationSettingsGui.Draw(
                    flattenSettings, FlattenSettingsScope.Pipeline, drawCategories: true);
            }

            EditorGUILayout.Space(4f);
            if (sourceBindings.Count > 1)
            {
                EditorGUILayout.HelpBox(
                    "多行时请在导入区表内改各行 ID2。运行时每行用自己的 ID2 入库并建 Prefab。",
                    MessageType.Info);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                materialId = EditorGUILayout.TextField("materialId / ID2（可选）", materialId);
                if (EditorGUI.EndChangeCheck())
                {
                    SyncRowZeroFromLegacyFields();
                }

                EditorGUILayout.HelpBox(
                    "选源时自动填：父目录仅一个内核文件 → 三层名；同夹还有其它内核文件或三层不足（Warning）→ 三层+文件全名。手填覆盖。③ 用该 Id。",
                    MessageType.None);
            }
        }
    }

    /// <summary>输出区：⑥。</summary>
    private void DrawOutputZone()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("输出区", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            settings.runAb = EditorGUILayout.ToggleLeft("⑥ 导出", settings.runAb);
            settings.quiet = EditorGUILayout.ToggleLeft("Quiet（无确认框）", settings.quiet);
            EditorGUILayout.HelpBox(
                "⑥ 只决定这次跑不跑导出。打 AB / 是否 UP / 交付根与 AB 根都在导出 SO。\n" +
                "开④+⑥时打的是平铺返回的 Art Prefab。Quiet 只禁弹窗，不是 -quit。",
                MessageType.None);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.Space(4f);
            DrawExportSettingsSummary();
        }
    }

    private void DrawExportSettingsSummary()
    {
        if (exportSettings == null)
        {
            exportSettings = RetinarExportSettings.GetOrCreateAsset();
        }

        if (GUILayout.Button("打开导出 SO（产物 / 路径）", GUILayout.Height(26f)))
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
            products += " + UnityPackage";
        }

        EditorGUILayout.LabelField(
            "交付根: " + exportSettings.deliverableRoot +
            "  |  AB根: " + exportSettings.assetBundleRoot,
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField("产物: " + products, EditorStyles.miniLabel);
    }

    private void DrawBindingsTable()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "源与 ID2（" + sourceBindings.Count + " 行）",
            EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "接口：PipelineSourceAccept.SendToOrchestration / PipelineWindow.AcceptBindings。\n" +
            "建议 ID2：父目录磁盘上仅一个内核文件 → 三层夹名；还有其它 .fbx/.glb/.gltf/.obj 或三层 Warning → 三层 + 文件全名。手填覆盖。\n" +
            "路径只读，避免误改；换源请拖入 / 浏览 / 批量重新输出。ID2 可改。行索引由 Runner 承担。",
            MessageType.None);

        if (sourceBindings.Count > 1)
        {
            EditorGUILayout.HelpBox(
                "表内 " + sourceBindings.Count + " 行，运行将逐行入库 / 建 Prefab / 按该份 ctx 平铺。",
                MessageType.Info);
        }

        if (sourceBindings.Count == 0)
        {
            EditorGUILayout.LabelField("尚无绑定。拖入文件、浏览，或从批量面板「输出到编排」。", EditorStyles.miniLabel);
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

        row.ConvertZUpToYUp = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "OBJ 轴向修正（内容节点 −90°X）",
                "OBJ 格式没有 up-axis 字段，Unity 一律当 Y-up 读，Max 的 Z-up 导出会竖立。\n" +
                "勾上则由④在空壳的内容节点上叠 −90°X，与 Unity 对 FBX 的既有行为一致。\n" +
                "管线不自动判定：导出器都带 Flip YZ 勾选，署名看不出实际轴向，猜反了 AB 里也看不出来。"),
            row.ConvertZUpToYUp);

        string hint;
        if (axisHints.TryGetValue(path, out hint))
        {
            EditorGUILayout.LabelField(
                "  导出器「" + hint + "」默认 Z-up，多半需要勾选",
                EditorStyles.miniLabel);
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
        if (bindings != null)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                PipelineSourceBinding src = bindings[i];
                if (src == null || string.IsNullOrWhiteSpace(src.SourcePath))
                {
                    continue;
                }

                sourceBindings.Add(src.CloneWith(null));
            }
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
        opt.RunImport = true;
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
