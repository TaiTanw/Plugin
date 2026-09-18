using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Pipeline — 总步骤开关（进版本库的 SO；与 L1 资源面板 EditorPrefs 分层）
// =====================================================================================

/// <summary>
/// 流程编排步骤开关。② 等总调度开关放本 SO。
/// ⑤ 纳入三类也在本资产；不读资源处理总面板 Prefs。
/// 导入基线无开关；可选 External / 贴图 Importer 在编排 ProcessSettings。
/// Incoming / IncomingPrefab / Art 只写本资产；内核靠 Options 传入，不 Load 本 SO。
/// </summary>
public class PipelineStepSettings : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Plugin/Pipeline/ConfigData/PipelineStepSettings.asset";

    [Header("工作区根（编排真源；面板外工具自管）")]
    [Tooltip("[1] 工程外文件拷入此根。不读批量选择器 BatchFbxImportSettings。")]
    public string importRootPath = PipelineWorkspace.DefaultImportRoot;

    [Tooltip("[③] 独立 Prefab 落盘根。不读 PrefabBuildSettings。")]
    public string prefabRootPath = PipelineWorkspace.DefaultPrefabRoot;

    [Tooltip("[④] 平铺写出根；[⑤] 扫单元、[⑥] UP 切 Art 前缀也用它。不读 FlattenBuildSettings / RetinarPaths。")]
    public string artRootPath = PipelineWorkspace.DefaultArtRoot;

    [Header("总步骤（流程编排）")]
    [Tooltip("③ 自动化 Prefab。处理区第一步；关掉则④⑤一并关。")]
    public bool runPrefab = true;

    [Tooltip("④ 平铺到交付根。须先开③；关掉则⑤一并关。写出根见 artRootPath。")]
    public bool runFlatten = true;

    [Tooltip("⑤ 资源总批量。须先开④（因而也须开③）。")]
    public bool runPostProcess = true;

    [Header("⑤ 纳入（编排 SO；不读资源面板 Prefs）")]
    [Tooltip("⑤ 是否跑贴图主批量。默认开。")]
    public bool postProcessIncludeTexture = true;

    [Tooltip("⑤ 是否跑材质主批量（交付 Shader）。默认开。")]
    public bool postProcessIncludeMaterial = true;

    [Tooltip("⑤ 是否跑模型主批量。默认开。")]
    public bool postProcessIncludeModel = true;

    [Tooltip("⑥ 是否导出。产物种类与路径读 RetinarExportSettings，本开关不选文件类型。")]
    public bool runAb = true;

    [Tooltip("禁止确认弹窗。")]
    public bool quiet = true;

    private static PipelineStepSettings assetInstance;
    private static PipelineStepSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public static PipelineStepSettings Current
    {
        get
        {
            PipelineStepSettings found = FindExistingAsset();
            if (found != null)
            {
                return found;
            }

            if (fallbackInstance == null)
            {
                fallbackInstance = CreateInstance<PipelineStepSettings>();
            }

            if (!fallbackWarningLogged)
            {
                fallbackWarningLogged = true;
                Debug.LogWarning("[PipelineStepSettings] 尚无配置资产，使用内存默认。" +
                    "打开自动化管线总面板会创建 " + DefaultAssetPath);
            }

            return fallbackInstance;
        }
    }

    public static PipelineStepSettings GetOrCreateAsset()
    {
        PipelineStepSettings found = FindExistingAsset();
        if (found != null)
        {
            return found;
        }

        string dir = Path.GetDirectoryName(DefaultAssetPath).Replace("\\", "/");
        EnsureAssetFolder(dir);
        var created = CreateInstance<PipelineStepSettings>();
        AssetDatabase.CreateAsset(created, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        assetInstance = created;
        return created;
    }

    public void ApplyTo(PipelineOptions options)
    {
        if (options == null)
        {
            return;
        }

        // Import is mandatory for the user-facing orchestration entries and stays
        // at PipelineOptions' default. Direct API callers may still override it.
        options.RunPrefab = runPrefab;
        options.RunFlatten = runPrefab && runFlatten;
        options.RunPostProcess = runPrefab && runFlatten && runPostProcess;
        options.PostProcessIncludeTexture = postProcessIncludeTexture;
        options.PostProcessIncludeMaterial = postProcessIncludeMaterial;
        options.PostProcessIncludeModel = postProcessIncludeModel;
        options.RunAb = runAb;
        options.Quiet = quiet;
        options.ImportRoot = PipelineWorkspace.Normalize(
            importRootPath, PipelineWorkspace.DefaultImportRoot);
        options.PrefabRoot = PipelineWorkspace.Normalize(
            prefabRootPath, PipelineWorkspace.DefaultPrefabRoot);
        options.ArtRoot = PipelineWorkspace.Normalize(
            artRootPath, PipelineWorkspace.DefaultArtRoot);

        FlattenOperationSettings flatten = FlattenOperationSettings.LoadPipelineOrDefaults();
        options.FlattenPolicy = flatten != null
            ? flatten.CreatePolicy()
            : FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Pipeline);

        RetinarExportSettings export = RetinarExportSettings.Current;
        if (export != null)
        {
            options.ExportUnityPackage = export.exportUnityPackage;
            options.AbBuildOptions = RetinarAbBuildOptions.FromExportSettings(
                export,
                quietOverride: quiet);
        }
        else
        {
            options.AbBuildOptions = RetinarAbBuildOptions.CreateDefaultAbOnly();
            options.AbBuildOptions.Quiet = quiet;
            options.ExportUnityPackage = options.AbBuildOptions.ExportUnityPackage;
        }

        if (options.AbBuildOptions != null)
        {
            options.AbBuildOptions.ArtRoot = options.ArtRoot;
        }
    }

    private static PipelineStepSettings FindExistingAsset()
    {
        if (assetInstance != null)
        {
            return assetInstance;
        }

        assetInstance = AssetDatabase.LoadAssetAtPath<PipelineStepSettings>(DefaultAssetPath);
        if (assetInstance != null)
        {
            return assetInstance;
        }

        string[] guids = AssetDatabase.FindAssets("t:PipelineStepSettings");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            assetInstance = AssetDatabase.LoadAssetAtPath<PipelineStepSettings>(path);
        }

        return assetInstance;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
