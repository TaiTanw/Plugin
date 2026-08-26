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

    [Tooltip("③ 自动化 Prefab。")]
    public bool runPrefab = true;

    [Tooltip("④ 平铺到 Art（Converter 默认开；可关）。")]
    public bool runFlatten = true;

    [Tooltip("⑤ 资源总批量（Converter 默认开；对应「按批量路径执行全部」口；须先开④）。")]
    public bool runPostProcess = true;

    [Tooltip("⑥ 仅双端 AB。")]
    public bool runAb = true;

    [Tooltip("⑥ 额外打 UnityPackage（读/写与 RetinarExportSettings.exportUnityPackage 同步）。")]
    public bool exportUnityPackage;

    [Header("导入后与 L1 的衔接")]
    [Tooltip("导入成功后把模型所在夹写入资源处理总面板批量路径（EditorPrefs），便于日后开⑤。")]
    public bool syncImportFolderToResourcePanel = true;

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
        options.RunFlatten = runFlatten;
        options.RunPostProcess = runFlatten && runPostProcess;
        options.RunAb = runAb;
        options.ExportUnityPackage = exportUnityPackage;
        options.Quiet = quiet;
        options.SyncImportFolderToResourcePanel = syncImportFolderToResourcePanel;

        RetinarExportSettings export = RetinarExportSettings.Current;
        if (export != null)
        {
            // 步骤勾选与导出 SO 双向：以步骤 SO 为准写回 export 的 UP 开关
            export.exportUnityPackage = exportUnityPackage;
            options.AbBuildOptions = RetinarAbBuildOptions.FromExportSettings(
                export,
                exportUnityPackageOverride: exportUnityPackage,
                quietOverride: quiet);
        }
        else
        {
            options.AbBuildOptions = RetinarAbBuildOptions.CreateDefaultAbOnly();
            options.AbBuildOptions.ExportUnityPackage = exportUnityPackage;
            options.AbBuildOptions.Quiet = quiet;
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
