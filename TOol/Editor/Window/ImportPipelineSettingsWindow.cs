using UnityEditor;
using UnityEngine;

// =====================================================================================
// [2] 全局导入设置：Unity AssetPostprocessor（导入当下），不是管线⑤。
// ⑤ 在④平铺之后主动总批量，triggeredByImport=false。
// =====================================================================================

public sealed class ImportPipelineSettingsWindow : EditorWindow
{
    private const string PrefFoldAdvanced = "TOol.ImportPipeline.Fold.Advanced";

    private ImportPipelineSettings hookSettings;
    private ModelProcessSettings modelSettings;
    private TextureProcessSettings textureSettings;
    private SerializedObject hookSerialized;
    private SerializedObject modelSerialized;
    private SerializedObject textureSerialized;
    private Vector2 scroll;
    private bool foldAdvanced;

    [MenuItem("Tools/全局导入设置（2）", false, 25)]
    public static void ShowWindow()
    {
        GetWindow<ImportPipelineSettingsWindow>("全局导入设置 [2]").minSize = new Vector2(480f, 560f);
    }

    private void OnEnable()
    {
        BindAssets();
        foldAdvanced = EditorPrefs.GetBool(PrefFoldAdvanced, false);
    }

    private void OnDisable()
    {
        EditorPrefs.SetBool(PrefFoldAdvanced, foldAdvanced);
        hookSerialized = null;
        modelSerialized = null;
        textureSerialized = null;
    }

    private void BindAssets()
    {
        hookSettings = ImportPipelineSettings.GetOrCreateAsset();
        modelSettings = ModelProcessSettings.GetOrCreatePipelineAsset();
        textureSettings = TextureProcessSettings.GetOrCreatePipelineAsset();
        hookSerialized = null;
        modelSerialized = null;
        textureSerialized = null;
    }

    private void OnGUI()
    {
        if (hookSettings == null || modelSettings == null || textureSettings == null)
        {
            BindAssets();
        }

        using (var scope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;

            EditorGUILayout.LabelField("全局导入设置 [2]", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "本页管 Unity 导入回调（AssetPostprocessor），在资源进工程当下执行。\n" +
                "不是管线⑤：⑤ 在④平铺之后主动跑总批量（刷白 / 压图 / 材质），" +
                "triggeredByImport=false，用来保证交付处理成功。",
                MessageType.Info);

            DrawPreprocessSection();
            DrawAdvancedAbandoned();
        }
    }

    private void DrawPreprocessSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Unity 导入管线 · OnPreprocess（导入当下写 Importer）",
            EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "模型：OnPreprocessModel（ModelImporter）。\n" +
                "贴图：OnPreprocessTexture（TextureImporter）。\n" +
                "总开关关则整段不跑。开则按下列分项写 Importer。Art 硬跳过，不读选择器导入根。",
                MessageType.None);

            ScriptableObjectSettingsGui.DrawOnly(
                hookSettings, ref hookSerialized, "importHooksEnabled");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("模型（编排 ModelProcessSettings）", EditorStyles.miniBoldLabel);
            SettingsAssetPathGui.DrawPinned(modelSettings);
            ScriptableObjectSettingsGui.DrawOnly(
                modelSettings,
                ref modelSerialized,
                "modelStripLightsAndCameras",
                "modelCalculateNormalsForObj",
                "modelUseExternalMaterials");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("贴图（编排 TextureProcessSettings）", EditorStyles.miniBoldLabel);
            SettingsAssetPathGui.DrawPinned(textureSettings);
            ScriptableObjectSettingsGui.DrawOnly(
                textureSettings,
                ref textureSerialized,
                "applyImporterSettingsOnImport",
                "textureDisableReadWrite");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "选中 SO 只展示位置，改动请通过本面板。",
                    EditorStyles.miniLabel);
                if (GUILayout.Button("钩子 SO", GUILayout.Width(72f), GUILayout.Height(22f)))
                {
                    Ping(hookSettings);
                }

                if (GUILayout.Button("模型 SO", GUILayout.Width(72f), GUILayout.Height(22f)))
                {
                    Ping(modelSettings);
                }

                if (GUILayout.Button("贴图 SO", GUILayout.Width(72f), GUILayout.Height(22f)))
                {
                    Ping(textureSettings);
                }
            }
        }
    }

    private void DrawAdvancedAbandoned()
    {
        EditorGUILayout.Space(10f);
        bool open = EditorGUILayout.Foldout(
            foldAdvanced, "高级 · 自动化处理（导入后 delayCall，半废弃）", true, EditorStyles.foldoutHeader);
        if (open != foldAdvanced)
        {
            foldAdvanced = open;
            EditorPrefs.SetBool(PrefFoldAdvanced, open);
        }

        if (!foldAdvanced)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "Unity 导入管线 · OnPostprocessAllAssets / OnPostprocessModel 之后 delayCall 跑 Op。\n" +
                "风险：导入栈里改资产不保证成功（嵌套导入、冲顶点色、时序）。" +
                "现网已把刷白/压图迁到④之后的⑤总批量，本层不复活入队。\n" +
                "下列开关只是从旧 Prefs 迁到本页；Processor 入口仍是空钩子，勾了也不会在导入时执行。\n" +
                "「不介入目录」曾给自动流跳过路径；OnPreprocess 已不再读，⑤总批量也不读。改了不影响现网导入钩子。",
                MessageType.Warning);

            DrawPrefToggle("总闸 MasterEnabled（只闸 delayCall 有效性，不闸 OnPreprocess）",
                ResourceProcessSwitches.MasterEnabled,
                v => ResourceProcessSwitches.MasterEnabled = v);
            DrawPrefToggle("贴图 · 设置自动 Prefs（已不驱动 OnPreprocess，改由上方 SO）",
                ResourceProcessSwitches.TextureSettingsAuto,
                v => ResourceProcessSwitches.TextureSettingsAuto = v);
            DrawPrefToggle("贴图 · 后处理自动 Prefs（调度器仍查，但无入队）",
                ResourceProcessSwitches.TexturePostProcessAuto,
                v => ResourceProcessSwitches.TexturePostProcessAuto = v);
            DrawPrefToggle("模型 · 设置自动 Prefs（已不驱动 OnPreprocess，改由上方 SO）",
                ResourceProcessSwitches.ModelSettingsAuto,
                v => ResourceProcessSwitches.ModelSettingsAuto = v);
            DrawPrefToggle("模型 · 后处理自动 Prefs（调度器仍查，但无入队）",
                ResourceProcessSwitches.ModelPostProcessAuto,
                v => ResourceProcessSwitches.ModelPostProcessAuto = v);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("不介入目录（残留，不驱动钩子）", EditorStyles.miniBoldLabel);
            ScriptableObjectSettingsGui.DrawOnly(
                modelSettings, ref modelSerialized, "excludedPathPrefixes");
            ScriptableObjectSettingsGui.DrawOnly(
                textureSettings, ref textureSerialized, "excludedPathPrefixes");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("若复活才会用的 Op 勾选（当前导入不跑）", EditorStyles.miniBoldLabel);
            ScriptableObjectSettingsGui.DrawOnly(
                modelSettings, ref modelSerialized, "importAutoOperationIds");
            ScriptableObjectSettingsGui.DrawOnly(
                textureSettings, ref textureSerialized, "importAutoOperationIds");
        }
    }

    private static void DrawPrefToggle(string label, bool value, System.Action<bool> set)
    {
        bool next = EditorGUILayout.ToggleLeft(label, value);
        if (next != value && set != null)
        {
            set(next);
        }
    }

    private static void Ping(Object asset)
    {
        if (asset == null)
        {
            return;
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
