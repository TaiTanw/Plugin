using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 批量 FBX 导入面板配置。只描述「导入到哪」与「交付区警报路径」，
// 不参与交付命名（交付名仍以人工改好的 Prefab 名为准）。
// =====================================================================================
public class BatchFbxImportSettings : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Plugin/TOol/ConfigData/BatchFbxImportSettings.asset";

    [Header("导入区")]
    [Tooltip("外部 FBX 拷入的工程内根路径。必须是 Assets/ 下路径，且不得落在下方交付区警报前缀内。")]
    public string importRootPath = "Assets/Incoming";

    [Header("交付区警报")]
    [Tooltip("导入根与生成目标路径不得落在这些前缀下（默认 Assets/Art/）。面板阶段即警报并禁用执行。")]
    public List<string> deliveryAlertPathPrefixes = new List<string> { "Assets/Art/" };

    private static BatchFbxImportSettings assetInstance;
    private static BatchFbxImportSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public string NormalizedImportRoot
    {
        get
        {
            string path = string.IsNullOrWhiteSpace(importRootPath) ? "Assets/Incoming" : importRootPath.Trim();
            return path.Replace("\\", "/").TrimEnd('/');
        }
    }

    public bool IsDeliveryAlertPath(string assetPath)
    {
        return ResourceExcludeUtility.IsExcludedPath(assetPath, deliveryAlertPathPrefixes);
    }

    /// <summary>导入根本身是否误指交付区（或为空）。</summary>
    public bool TryValidateImportRoot(out string error)
    {
        string root = NormalizedImportRoot;
        if (string.IsNullOrEmpty(root) ||
            !root.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            error = "导入根必须是 Assets/ 下的路径，例如 Assets/Incoming。";
            return false;
        }

        if (IsDeliveryAlertPath(root) || IsDeliveryAlertPath(root + "/"))
        {
            error = "导入根落在交付区警报路径下（默认 Assets/Art/），禁止执行。请改到导入区。";
            return false;
        }

        error = null;
        return true;
    }

    public static BatchFbxImportSettings Current
    {
        get
        {
            BatchFbxImportSettings found = FindExistingAsset();
            if (found != null)
            {
                return found;
            }

            if (fallbackInstance == null)
            {
                fallbackInstance = CreateInstance<BatchFbxImportSettings>();
            }

            if (!fallbackWarningLogged)
            {
                fallbackWarningLogged = true;
                Debug.LogWarning("[BatchFbxImportSettings] 工程里还没有配置资产，本次使用内存默认值。" +
                    "打开「批量FBX导入」面板会自动创建 " + DefaultAssetPath);
            }

            return fallbackInstance;
        }
    }

    public static BatchFbxImportSettings GetOrCreateAsset()
    {
        BatchFbxImportSettings found = FindExistingAsset();
        if (found != null)
        {
            return found;
        }

        EnsureAssetFolder(Path.GetDirectoryName(DefaultAssetPath).Replace("\\", "/"));
        var created = CreateInstance<BatchFbxImportSettings>();
        AssetDatabase.CreateAsset(created, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        assetInstance = created;
        fallbackWarningLogged = false;
        Debug.Log("[BatchFbxImportSettings] 已创建配置资产: " + DefaultAssetPath);
        return created;
    }

    private static BatchFbxImportSettings FindExistingAsset()
    {
        if (assetInstance != null)
        {
            return assetInstance;
        }

        assetInstance = AssetDatabase.LoadAssetAtPath<BatchFbxImportSettings>(DefaultAssetPath);
        if (assetInstance != null)
        {
            return assetInstance;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:BatchFbxImportSettings"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            assetInstance = AssetDatabase.LoadAssetAtPath<BatchFbxImportSettings>(path);
            if (assetInstance != null)
            {
                return assetInstance;
            }
        }

        return null;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
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
