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
    private string sourcePath = string.Empty;
    private string materialId = string.Empty;
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
                "「设置自动」仍由【资源处理总面板】EditorPrefs 控制，导入时靠 Unity 回调，本面板不另调。\n" +
                "⑤ 默认关闭；需要时再勾选（调用资源总批量口）。",
                MessageType.Info);

            DrawSourceSection();
            DrawStep2Section();
            DrawOtherStepsSection();

            EditorGUILayout.Space(8f);
            materialId = EditorGUILayout.TextField("materialId（可选）", materialId);
            EditorGUILayout.HelpBox(
                "留空：用导入夹名（三层规则）作 Prefab 名。\n" +
                "填写：③ Prefab 用该 Id；④ 通常跟 Prefab 名；⑥ 现网 AB 名仍跟资产名（契约1已退化）。\n" +
                "开④+⑥时：⑥自动改打平铺返回的 Art Prefab，无需手填 Art 路径。",
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
            sourcePath = EditorGUILayout.TextField(sourcePath);
            if (GUILayout.Button("浏览…", GUILayout.Width(64f)))
            {
                string picked = EditorUtility.OpenFilePanel(
                    "选择模型",
                    string.IsNullOrEmpty(sourcePath) ? "" : Path.GetDirectoryName(sourcePath),
                    "fbx,glb,gltf,obj");
                if (!string.IsNullOrEmpty(picked))
                {
                    sourcePath = picked.Replace("\\", "/");
                }
            }

            if (GUILayout.Button("清除", GUILayout.Width(48f)))
            {
                sourcePath = string.Empty;
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
            settings.syncImportFolderToResourcePanel = EditorGUILayout.ToggleLeft(
                "导入后写入资源总面板批量路径", settings.syncImportFolderToResourcePanel);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.HelpBox(
                "具体「贴图/模型 · 设置自动」开关请在资源处理总面板配置（EditorPrefs）。\n" +
                "本步只负责是否导入；导入触发后设置自动走 Unity 管线回调。",
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
            settings.runFlatten = EditorGUILayout.ToggleLeft("④ 平铺到 Art", settings.runFlatten);
            if (!settings.runFlatten && settings.runPostProcess)
            {
                settings.runPostProcess = false;
            }

            using (new EditorGUI.DisabledScope(!settings.runFlatten))
            {
                settings.runPostProcess = EditorGUILayout.ToggleLeft(
                    "⑤ 资源总批量（须先开④）", settings.runPostProcess);
            }

            if (!settings.runFlatten)
            {
                EditorGUILayout.HelpBox("⑤ 依赖④：未平铺到 Art 时总批量通常无意义，故锁定关闭。", MessageType.None);
            }

            settings.runAb = EditorGUILayout.ToggleLeft("⑥ 仅双端 AB（默认开）", settings.runAb);
            settings.quiet = EditorGUILayout.ToggleLeft("Quiet（无确认框）", settings.quiet);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
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
            sourcePath = candidate;
            evt.Use();
            Repaint();
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

        PipelineResult result = PipelineRunner.Run(opt);
        lastResultText = result.ToString();
        Repaint();
    }
}
