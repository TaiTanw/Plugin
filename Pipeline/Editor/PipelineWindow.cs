using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Pipeline — D3 自动化管线总面板（单文件；步骤开关走 SO）
// =====================================================================================

/// <summary>
/// 流程编排人机入口。与「资源处理总面板」分工：本窗管总步骤；资源窗管具体自动设置 Prefs。
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
        if (settings != null && exportSettings != null)
        {
            settings.exportUnityPackage = exportSettings.exportUnityPackage;
        }
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
                "拖入一个 .fbx / .glb / .gltf / .obj（工程外或 Assets 内）。\n" +
                "步骤开关保存在 PipelineStepSettings（SO）。\n" +
                "「设置自动」仍由【资源处理总面板】EditorPrefs 控制，导入时靠 Unity 回调，本面板不另调（导入自动流默认不碰 Art）。\n" +
                "④⑤ Converter 默认开，可关；⑤ = 代跑资源总面板「执行全部」同一内核，不是导入钩子。开④时扫本次 Art 单元。",
                MessageType.Info);

            DrawSourceSection();
            DrawStep2Section();
            DrawOtherStepsSection();

            EditorGUILayout.Space(8f);
            materialId = EditorGUILayout.TextField("materialId（可选，可改）", materialId);
            EditorGUILayout.HelpBox(
                "选源/拖入/浏览时自动填入默认名（三层夹名规则）；点「清除」时一并清空。\n" +
                "可手改业务 Id。留空跑管线时 ③ 仍按三层规则算名（内核保留空判断）。\n" +
                "填写：③ Prefab 用该 Id；④ 通常跟 Prefab 名；⑥ 现网 AB 名仍跟资产名。\n" +
                "开④+⑥时：⑥自动改打平铺返回的交付中间区 Prefab。",
                MessageType.None);

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
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("输入（单文件）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
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
    }

    /// <summary>② 独立一层（SO）。</summary>
    private void DrawStep2Section()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("步骤② 导入（总调度 SO）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            settings.runImport = EditorGUILayout.ToggleLeft(
                "启用② 导入（工程外拷入 Import 区）", settings.runImport);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("高级操作", EditorStyles.miniBoldLabel);
            settings.syncImportFolderToResourcePanel = EditorGUILayout.ToggleLeft(
                "②.2 导入后写入资源总面板批量路径", settings.syncImportFolderToResourcePanel);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.HelpBox(
                "②.2 为高级操作：只把「模型所在夹」（多为 Import 区）追加进资源总面板 L1 路径，" +
                "方便日后手动批量；不开关设置自动，也不等于⑤会扫到 Art 单元。\n" +
                "「贴图/模型 · 设置自动」请在资源处理总面板配置；本步主开关只负责是否导入。",
                MessageType.None);
        }
    }

    private void DrawOtherStepsSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("其它总步骤（SO）", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            settings.runPrefab = EditorGUILayout.ToggleLeft("③ Prefab（默认开）", settings.runPrefab);
            settings.runFlatten = EditorGUILayout.ToggleLeft(
                "④ 平铺到交付中间区（Art）", settings.runFlatten);
            if (!settings.runFlatten && settings.runPostProcess)
            {
                settings.runPostProcess = false;
            }

            using (new EditorGUI.DisabledScope(!settings.runFlatten))
            {
                settings.runPostProcess = EditorGUILayout.ToggleLeft(
                    "⑤ 资源总批量（须先开④；对中间区做压图/材质等）", settings.runPostProcess);
            }

            EditorGUILayout.HelpBox(
                "④ 把 Prefab 依赖收敛进交付中间区，供⑤处理与⑥出包。\n" +
                "中间区根路径当前写死为 Assets/Art（RetinarPaths.ArtRoot / 平铺代码 const），" +
                "本面板与 PipelineStepSettings 均不可改；单元为 Art/<名>/。\n" +
                "开④+⑤时自动扫本次 Art 单元（含 Model/），不再只靠 L1 的 Import 夹。",
                MessageType.None);

            if (!settings.runFlatten)
            {
                EditorGUILayout.HelpBox(
                    "⑤ 依赖④：未平铺到交付中间区时总批量通常无意义，故锁定关闭。",
                    MessageType.None);
            }

            settings.runAb = EditorGUILayout.ToggleLeft("⑥ 双端 AB（默认开）", settings.runAb);
            using (new EditorGUI.DisabledScope(!settings.runAb))
            {
                settings.exportUnityPackage = EditorGUILayout.ToggleLeft(
                    "⑥ 附带 UnityPackage", settings.exportUnityPackage);
            }

            settings.quiet = EditorGUILayout.ToggleLeft("Quiet（无确认框）", settings.quiet);
            EditorGUILayout.HelpBox(
                "Quiet 只禁止弹窗，不会退出编辑器（退出是 Unity 命令行 -quit）。\n" +
                "默认勾选：跑完只写面板「上次结果」和 Console。\n" +
                "取消勾选：本次结束会弹出结果框（④进度条仍可能出现）。",
                MessageType.None);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
                if (exportSettings != null)
                {
                    exportSettings.exportUnityPackage = settings.exportUnityPackage;
                    EditorUtility.SetDirty(exportSettings);
                }
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("导出路径设置…（交付根 / AB 根）", GUILayout.Height(26f)))
            {
                if (exportSettings == null)
                {
                    exportSettings = RetinarExportSettings.GetOrCreateAsset();
                }

                Selection.activeObject = exportSettings;
                EditorGUIUtility.PingObject(exportSettings);
            }

            if (exportSettings != null)
            {
                EditorGUILayout.LabelField(
                    "交付根: " + exportSettings.deliverableRoot +
                    "  |  AB根: " + exportSettings.assetBundleRoot,
                    EditorStyles.miniLabel);
            }
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
