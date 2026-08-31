using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Pipeline — 总步骤开关（进版本库的 SO；与 L1 资源面板 EditorPrefs 分层）
// =====================================================================================

/// <summary>
/// 流程编排步骤开关。② 等总调度开关放本 SO；
/// 「具体哪些设置自动/后处理自动」仍由资源处理总面板 Prefs + L3 SO 决定。
/// </summary>
public class PipelineStepSettings : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Plugin/Pipeline/ConfigData/PipelineStepSettings.asset";

    [Header("总步骤（流程编排）")]
    [Tooltip("② 导入：工程外路径拷入 Import 区并 ImportAsset；已在 Assets 内则复用。")]
    public bool runImport = true;

    [Tooltip("③ 自动化 Prefab。处理区第一步；关掉则④⑤一并关。")]
    public bool runPrefab = true;

    [Tooltip("④ 平铺到 Art。须先开③；关掉则⑤一并关。根路径写死 Assets/Art。")]
    public bool runFlatten = true;

    [Tooltip("⑤ 资源总批量。须先开④（因而也须开③）。")]
    public bool runPostProcess = true;

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

        options.RunImport = runImport;
        options.RunPrefab = runPrefab;
        options.RunFlatten = runPrefab && runFlatten;
        options.RunPostProcess = runPrefab && runFlatten && runPostProcess;
        options.RunAb = runAb;
        options.Quiet = quiet;

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
