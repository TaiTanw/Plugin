using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Pipeline — D3 自动化管线总面板（单文件；步骤开关走 SO）
// =====================================================================================

/// <summary>
/// 流程编排人机入口。导入区 1 入库无开关、2 总闸=MasterEnabled；分项自动在资源处理总面板。
/// </summary>
public class PipelineWindow : EditorWindow
{
    private PipelineStepSettings settings;
    private RetinarExportSettings exportSettings;
    private string sourcePath = string.Empty;
    private string materialId = string.Empty;
    /// <summary>上次已为 materialId 同步过的源路径；源变化时才重写默认 Id。</summary>
    private string materialIdSyncedForSource = string.Empty;
    private string lastResultText = string.Empty;
    private Vector2 scroll;
    private Vector2 resultScroll;

    [MenuItem("Tools/自动化管线总面板", false, 40)]
    public static void ShowWindow()
    {
        GetWindow<PipelineWindow>("自动化管线").minSize = new Vector2(480f, 560f);
    }

    private void OnEnable()
    {
        settings = PipelineStepSettings.GetOrCreateAsset();
        exportSettings = RetinarExportSettings.GetOrCreateAsset();
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = PipelineStepSettings.GetOrCreateAsset();
        }

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scrollScope.scrollPosition;

            EditorGUILayout.LabelField("自动化管线（单文件）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "三区：导入（1 入库 / 2 总自动化）→ 处理（③④⑤）→ 输出（⑥）。\n" +
                "处理区须开前一步才能开后一步：③开才能④，④开才能⑤。\n" +
                "导入区 2 只开总闸（与资源总面板「总开关」同一 Prefs）；" +
                "哪些资源、设置自动/后处理自动仍在资源总面板。",
                MessageType.Info);

            DrawImportZone();
            DrawProcessZone();
            DrawOutputZone();

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(sourcePath)))
            {
                if (GUILayout.Button("运行管线", GUILayout.Height(36f)))
                {
                    RunPipeline();
                }
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
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
        GUI.Box(drop, string.IsNullOrEmpty(sourcePath)
            ? "拖放模型文件到此处"
            : Path.GetFileName(sourcePath));
        HandleDrag(drop);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        string edited = EditorGUILayout.TextField(sourcePath);
        if (EditorGUI.EndChangeCheck())
        {
            SetSourcePath(edited);
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
    }

    /// <summary>导入区：1 入库（无勾选）+ 2 总自动化处理（MasterEnabled）。</summary>
    private void DrawImportZone()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("导入区", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSourceSection();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("1 入库（导入器，无开关）", EditorStyles.miniBoldLabel);
            BatchFbxImportSettings importSettings = BatchFbxImportSettings.Current;
            string importRoot = importSettings != null
                ? importSettings.NormalizedImportRoot
                : "Assets/Incoming";
            EditorGUILayout.HelpBox(
                "工程外文件始终拷入导入根并 ImportAsset（已在 Assets 内则复用、不拷）。\n" +
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
                    "总闸已关：ImportAsset 仍会入库，但设置自动 / 后处理自动回调里直接 return，不改 Importer、不跑导入期 Op。\n" +
                    "资源总面板里的分项勾选此时无效。⑤ 手动总批量不受此闸影响。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "总闸已开。随后由【资源处理总面板】决定：贴图/模型谁开、是「设置自动」还是「后处理自动」。\n" +
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

            if (IsGltfSourcePath(sourcePath))
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
                "须开前一步才能开后一步。④ 根路径写死 Assets/Art；开④+⑤时扫本次 Art 单元（D17）。",
                MessageType.None);

            EditorGUILayout.Space(4f);
            materialId = EditorGUILayout.TextField("materialId（可选）", materialId);
            EditorGUILayout.HelpBox(
                "选源时自动填三层名；③ 用该 Id；④ 通常跟 Prefab 名。留空则③按三层规则算名。",
                MessageType.None);
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

        string candidate = null;
        if (DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                string p = DragAndDrop.paths[i];
                if (!string.IsNullOrEmpty(p) && ToolImportApi.IsSupportedExtension(Path.GetExtension(p)))
                {
                    candidate = p.Replace("\\", "/");
                    break;
                }
            }
        }

        if (candidate == null && DragAndDrop.objectReferences != null)
        {
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                Object obj = DragAndDrop.objectReferences[i];
                string ap = AssetDatabase.GetAssetPath(obj);
                if (ToolImportApi.IsSupportedExtension(Path.GetExtension(ap)))
                {
                    candidate = ap.Replace("\\", "/");
                    break;
                }
            }
        }

        if (candidate == null)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            return;
        }

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            SetSourcePath(candidate);
            evt.Use();
            Repaint();
        }
    }

    private static bool IsGltfSourcePath(string path)
    {
        return !string.IsNullOrEmpty(path) &&
               string.Equals(Path.GetExtension(path), ".gltf", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>D9：改源路径时同步默认 materialId；清空源时清 Id。</summary>
    private void SetSourcePath(string path)
    {
        string normalized = (path ?? string.Empty).Replace("\\", "/").Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            sourcePath = string.Empty;
            materialId = string.Empty;
            materialIdSyncedForSource = string.Empty;
            return;
        }

        bool sourceChanged = !string.Equals(
            materialIdSyncedForSource, normalized, System.StringComparison.OrdinalIgnoreCase);
        sourcePath = normalized;
        if (sourceChanged)
        {
            materialId = PipelineMaterialId.SuggestDefault(normalized);
            materialIdSyncedForSource = normalized;
        }
    }

    private void RunPipeline()
    {
        AssetDatabase.SaveAssets();
        PipelineOptions opt = PipelineOptions.FromSettings(settings, sourcePath);
        // 导入区 1 无勾选：本面板跑管线时始终入库（工程外拷入；已在 Assets 则复用）。
        opt.RunImport = true;
        if (!string.IsNullOrWhiteSpace(materialId))
        {
            opt.MaterialId = materialId.Trim();
        }

        // D10 预备：单文件也写成一条绑定，便于日后统一消费；Runner 暂不读 SourceBindings。
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            opt.SourceBindings = PipelineMaterialId.BuildSourceBindings(
                new[] { sourcePath },
                string.IsNullOrWhiteSpace(materialId) ? null : materialId.Trim());
        }

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
}
