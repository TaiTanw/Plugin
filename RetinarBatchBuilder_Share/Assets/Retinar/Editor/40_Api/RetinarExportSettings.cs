using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 导出路径 / UP 开关（SO；管线与直通共用）
// =====================================================================================

/// <summary>
/// 交付输出根目录与是否打 UnityPackage。进版本库。
/// </summary>
public class RetinarExportSettings : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Plugin/RetinarBatchBuilder_Share/Assets/Retinar/ConfigData/RetinarExportSettings.asset";

    [Header("输出根（相对工程根）")]
    [Tooltip("交付物根目录，默认 Deliverables。")]
    public string deliverableRoot = RetinarPaths.DeliverableRoot;

    [Tooltip("BuildPipeline 临时 AB 输出根，默认 AssetBundles。")]
    public string assetBundleRoot = RetinarPaths.AssetBundleRoot;

    [Header("产物")]
    [Tooltip("⑥ 是否额外打 UnityPackage 到 Deliverables/<名>/02_unity。管线面板不另设勾选，只读本字段。")]
    public bool exportUnityPackage;

    [Tooltip("是否把 AB 拷到 Deliverables/<名>/03_assetbundles。")]
    public bool copyAbToDeliverables = true;

    private static RetinarExportSettings assetInstance;
    private static RetinarExportSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public static RetinarExportSettings Current
    {
        get
        {
            RetinarExportSettings found = FindExistingAsset();
            if (found != null)
            {
                return found;
            }

            if (fallbackInstance == null)
            {
                fallbackInstance = CreateInstance<RetinarExportSettings>();
            }

            if (!fallbackWarningLogged)
            {
                fallbackWarningLogged = true;
                Debug.LogWarning("[RetinarExportSettings] 尚无配置资产，使用内存默认。将创建 " +
                    DefaultAssetPath);
            }

            return fallbackInstance;
        }
    }

    public static RetinarExportSettings GetOrCreateAsset()
    {
        RetinarExportSettings found = FindExistingAsset();
        if (found != null)
        {
            return found;
        }

        string dir = Path.GetDirectoryName(DefaultAssetPath).Replace("\\", "/");
        EnsureAssetFolder(dir);
        var created = CreateInstance<RetinarExportSettings>();
        AssetDatabase.CreateAsset(created, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        assetInstance = created;
        return created;
    }

    private static RetinarExportSettings FindExistingAsset()
    {
        if (assetInstance != null)
        {
            return assetInstance;
        }

        assetInstance = AssetDatabase.LoadAssetAtPath<RetinarExportSettings>(DefaultAssetPath);
        if (assetInstance != null)
        {
            return assetInstance;
        }

        string[] guids = AssetDatabase.FindAssets("t:RetinarExportSettings");
        if (guids != null && guids.Length > 0)
        {
            assetInstance = AssetDatabase.LoadAssetAtPath<RetinarExportSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
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
