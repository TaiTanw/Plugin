using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   这个窗口只做 GUI 与用户意图收集，一行实际处理逻辑都不写。
//   点下"执行"之后，它把（操作列表 + 目标列表 + 配置）交给 TextureOperationRunner，
//   剩下的进度条、异常兜底、结果汇总全在 Runner 里，和导入自动执行走的是同一条路。
//
// 这个窗口就是你要的那个"具体操作由自己控制"的入口，同时也是扩展点的展示面板：
//   任何实现了 ITextureAssetOperation 的类都会自动出现在下面的操作列表里，
//   不需要在这个文件里加任何代码。所以以后新增"贴图相关的批量处理"需求，
//   一律新建一个操作类，不要再新开菜单项或新写 AssetPostprocessor。
// =====================================================================================
public class TextureToolWindow : EditorWindow
{
    private TextureProcessSettings settings;
    private Editor settingsInspector;

    private TextureTargetCollector.Scope scope = TextureTargetCollector.Scope.Selection;
    private DefaultAsset targetFolder;

    // 范围收集要走 AssetDatabase.FindAssets，选"整个工程"时可能扫上万个资产。
    // OnGUI 每秒会被调用很多次，绝不能在里面直接收集，否则窗口一打开编辑器就卡住。
    // 这里缓存结果，只在范围变化、选中变化或用户点刷新时重新收集。
    private List<string> cachedTargets = new List<string>();
    private bool targetsDirty = true;

    // 手动执行时勾了哪些操作。只是窗口的临时状态，不需要持久化。
    private readonly Dictionary<string, bool> manualSelection = new Dictionary<string, bool>();

    private TextureOperationRunSummary lastSummary;
    private Vector2 resultScroll;

    [MenuItem("Tools/贴图处理工具")]
    public static void ShowWindow()
    {
        GetWindow<TextureToolWindow>("贴图处理工具").minSize = new Vector2(520f, 480f);
    }

    private void OnEnable()
    {
        settings = TextureProcessSettings.GetOrCreateAsset();
        targetsDirty = true;
    }

    /// <summary>Project 面板里换了选中对象，"当前选中"这个范围的结果就变了。</summary>
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
        if (settingsInspector != null)
        {
            DestroyImmediate(settingsInspector);
            settingsInspector = null;
        }
    }

    /// <summary>
    /// 总体流程：开关 → 配置 → 处理范围 → 操作列表 → 执行 → 结果。
    /// 每一段的具体绘制都下放到对应的 Draw 方法里。
    /// </summary>
    private void OnGUI()
    {
        if (settings == null)
        {
            settings = TextureProcessSettings.GetOrCreateAsset();
        }

        DrawSwitchSection();
        DrawSettingsSection();
        List<string> targets = DrawTargetSection();
        DrawOperationSection(targets);
        DrawResultSection();
    }

    // ---------------------------------------------------------------------------
    // 1) 总开关
    // ---------------------------------------------------------------------------

    private void DrawSwitchSection()
    {
        EditorGUILayout.LabelField("导入期总开关", EditorStyles.boldLabel);
        bool newValue = EditorGUILayout.ToggleLeft(AssetProcessSwitch.DisplayName, AssetProcessSwitch.IsEnabled);
        if (newValue != AssetProcessSwitch.IsEnabled)
        {
            AssetProcessSwitch.IsEnabled = newValue;
        }

        EditorGUILayout.HelpBox(
            AssetProcessSwitch.IsEnabled
                ? "已开启：FBX / 贴图导入时会按下面的配置自动处理。这个状态存在 EditorPrefs 里，脚本重编译和重启编辑器都不会丢。"
                : "已关闭：导入时完全不介入，行为等同于没装这套脚本。窗口里的手动执行不受这个开关影响。",
            AssetProcessSwitch.IsEnabled ? MessageType.Info : MessageType.None);

        EditorGUILayout.Space();
    }

    // ---------------------------------------------------------------------------
    // 2) 配置资产
    // ---------------------------------------------------------------------------

    private void DrawSettingsSection()
    {
        EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(settings, typeof(TextureProcessSettings), false);
            if (GUILayout.Button("在 Project 中定位", GUILayout.Width(120f)))
            {
                EditorGUIUtility.PingObject(settings);
            }
        }

        // 直接内嵌配置资产的 Inspector，而不是在这里逐个字段手写 GUI。
        // 这样以后往 TextureProcessSettings 里加字段，窗口自动就能编辑，不用改这个文件。
        if (settingsInspector == null || settingsInspector.target != settings)
        {
            if (settingsInspector != null)
            {
                DestroyImmediate(settingsInspector);
            }

            settingsInspector = Editor.CreateEditor(settings);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            settingsInspector.OnInspectorGUI();
        }

        EditorGUILayout.Space();
    }

    // ---------------------------------------------------------------------------
    // 3) 处理范围
    // ---------------------------------------------------------------------------

    private List<string> DrawTargetSection()
    {
        EditorGUILayout.LabelField("处理范围", EditorStyles.boldLabel);

        var newScope = (TextureTargetCollector.Scope)EditorGUILayout.EnumPopup("范围", scope);
        if (newScope != scope)
        {
            scope = newScope;
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

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("命中贴图数量", cachedTargets.Count.ToString());
            if (GUILayout.Button("重新扫描", GUILayout.Width(90f)))
            {
                targetsDirty = true;
            }
        }

        if (targetsDirty)
        {
            string folderPath = targetFolder == null ? null : AssetDatabase.GetAssetPath(targetFolder);
            cachedTargets = TextureTargetCollector.Collect(scope, folderPath);
            targetsDirty = false;
        }

        EditorGUILayout.Space();
        return cachedTargets;
    }

    // ---------------------------------------------------------------------------
    // 4) 操作列表与执行
    // ---------------------------------------------------------------------------

    private void DrawOperationSection(List<string> targets)
    {
        EditorGUILayout.LabelField("可用操作", EditorStyles.boldLabel);
        IList<ITextureAssetOperation> operations = TextureOperationRegistry.All;
        if (operations.Count == 0)
        {
            EditorGUILayout.HelpBox("工程里没有找到任何 ITextureAssetOperation 实现。", MessageType.Warning);
            return;
        }

        foreach (ITextureAssetOperation operation in operations)
        {
            DrawOperationRow(operation, targets);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(targets.Count == 0))
        {
            if (GUILayout.Button("执行勾选的操作", GUILayout.Height(28f)))
            {
                RunOperations(CollectManuallySelectedOperations(operations), targets);
            }
        }

        EditorGUILayout.Space();
    }

    private void DrawOperationRow(ITextureAssetOperation operation, List<string> targets)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = IsManuallySelected(operation.Id);
                bool newSelected = EditorGUILayout.ToggleLeft(operation.DisplayName, selected);
                if (newSelected != selected)
                {
                    manualSelection[operation.Id] = newSelected;
                }

                if (GUILayout.Button("只执行这一个", GUILayout.Width(110f)))
                {
                    RunOperations(new List<ITextureAssetOperation> { operation }, targets);
                }
            }

            EditorGUILayout.LabelField("Id: " + operation.Id, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(operation.Description, EditorStyles.wordWrappedMiniLabel);
            DrawImportAutoToggle(operation);
        }
    }

    private void DrawImportAutoToggle(ITextureAssetOperation operation)
    {
        bool isAuto = settings.importAutoOperationIds != null && settings.importAutoOperationIds.Contains(operation.Id);
        bool newIsAuto = EditorGUILayout.ToggleLeft("导入时自动执行", isAuto);
        if (newIsAuto == isAuto)
        {
            return;
        }

        Undo.RecordObject(settings, "修改导入时自动执行的操作");
        if (settings.importAutoOperationIds == null)
        {
            settings.importAutoOperationIds = new List<string>();
        }

        if (newIsAuto)
        {
            settings.importAutoOperationIds.Add(operation.Id);
        }
        else
        {
            settings.importAutoOperationIds.Remove(operation.Id);
        }

        // 立刻落盘。这项配置会被导入回调读到，如果只标 dirty 不保存，
        // 编辑器崩溃或强制重启后勾选就丢了，又变成"以为在处理其实没处理"。
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private bool IsManuallySelected(string operationId)
    {
        bool selected;
        return manualSelection.TryGetValue(operationId, out selected) && selected;
    }

    private List<ITextureAssetOperation> CollectManuallySelectedOperations(IList<ITextureAssetOperation> operations)
    {
        var result = new List<ITextureAssetOperation>();
        foreach (ITextureAssetOperation operation in operations)
        {
            if (IsManuallySelected(operation.Id))
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

        lastSummary = TextureOperationRunner.Run(operations, targets, settings, false);

        // 执行过后文件体积、甚至资产列表（TGA 转 PNG）都变了，缓存必须失效重扫，
        // 否则界面上还显示着旧的命中数量，容易让人以为没生效。
        targetsDirty = true;
        Repaint();
    }

    // ---------------------------------------------------------------------------
    // 5) 结果
    // ---------------------------------------------------------------------------

    private void DrawResultSection()
    {
        EditorGUILayout.LabelField("上次执行结果", EditorStyles.boldLabel);
        if (lastSummary == null)
        {
            EditorGUILayout.HelpBox("还没有在这个窗口里执行过操作。", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField(
            "改动 " + lastSummary.ChangedCount + " 项，跳过 " + lastSummary.SkippedCount +
            " 项，失败 " + lastSummary.FailedCount + " 项" +
            (lastSummary.Canceled ? "（中途取消）" : string.Empty));

        using (var scrollView = new EditorGUILayout.ScrollViewScope(resultScroll, GUILayout.MinHeight(120f)))
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
        }
    }
}
