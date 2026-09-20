using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 人工端路径：选择器「执行导入」根、③ Prefab 根、④ 交付根、入库预警。
// 编排三根读 PipelineStepSettings，不读本资产。交付文件名仍以 Prefab 名为准。
// =====================================================================================
public class BatchFbxImportSettings : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Plugin/TOol/ConfigData/BatchFbxImportSettings.asset";

    [Header("手动端路径")]
    [Tooltip("外部模型拷入的工程内根路径。只服务批量选择器「执行导入」。编排 [1] 读步骤 SO 工作区导入根，不读本字段。")]
    public string importRootPath = "Assets/Incoming";

    [Tooltip("人工③ Prefab 落盘根；人工④若选中模型会先③，也写这里。编排③读步骤 SO，不读本字段。")]
    public string prefabRootPath = "Assets/IncomingPrefab";

    [Tooltip("人工④ 平铺写出根。编排④读步骤 SO，不读本字段。导入期跳过 Art 仍钉死 Assets/Art，不跟本字段。")]
    public string artRootPath = "Assets/Art";

    [Header("交付区警报")]
    [Tooltip("导入根与生成目标路径不得落在这些前缀下（默认 Assets/Art/）。面板阶段即警报并禁用执行。" +
             "注意：本列表只服务「批量 FBX 入库」禁止写入交付区；" +
             "与贴图/模型 SO 的 excludedPathPrefixes（跳过自动设置/后处理）是另一份配置，默认值相同但不共享。" +
             "改交付根时请与 Texture/Model ProcessSettings 的排除前缀对照。")]
    public List<string> deliveryAlertPathPrefixes = new List<string> { "Assets/Art/" };

    private static BatchFbxImportSettings assetInstance;
    private static BatchFbxImportSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public string NormalizedImportRoot
    {
        get { return NormalizeAssetsRoot(importRootPath, "Assets/Incoming"); }
    }

    public string NormalizedPrefabRoot
    {
        get { return NormalizeAssetsRoot(prefabRootPath, PrefabBuildSettings.DefaultPrefabRoot); }
    }

    public string NormalizedArtRoot
    {
        get { return NormalizeAssetsRoot(artRootPath, FlattenBuildSettings.ArtRoot); }
    }

    /// <summary>
    /// 路径是否就是导入根或位于其下。按目录边界判断，避免
    /// Assets/IncomingElse 被误认为 Assets/Incoming 的子路径。
    /// </summary>
    public bool ContainsAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        string root = NormalizedImportRoot;
        string path = assetPath.Trim().Replace("\\", "/").TrimEnd('/');
        return path.Equals(root, System.StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(root + "/", System.StringComparison.OrdinalIgnoreCase);
    }

    public bool IsDeliveryAlertPath(string assetPath)
    {
        return ResourceExcludeUtility.IsExcludedPath(assetPath, deliveryAlertPathPrefixes);
    }

    /// <summary>导入根本身是否误指交付区（或为空）。</summary>
    public bool TryValidateImportRoot(out string error)
    {
        return TryValidateNonDeliveryRoot(NormalizedImportRoot, "导入根", "Assets/Incoming", out error);
    }

    public bool TryValidatePrefabRoot(out string error)
    {
        return TryValidateNonDeliveryRoot(
            NormalizedPrefabRoot, "Prefab 根", PrefabBuildSettings.DefaultPrefabRoot, out error);
    }

    public bool TryValidateArtRoot(out string error)
    {
        string root = NormalizedArtRoot;
        if (!IsAssetsFolderPath(root))
        {
            error = "交付根必须是 Assets/ 下的路径，例如 " + FlattenBuildSettings.ArtRoot + "。";
            return false;
        }

        error = null;
        return true;
    }

    public bool TryValidateManualPaths(out string error)
    {
        if (!TryValidateImportRoot(out error) ||
            !TryValidatePrefabRoot(out error) ||
            !TryValidateArtRoot(out error))
        {
            return false;
        }

        string art = NormalizedArtRoot;
        if (IsUnder(NormalizedImportRoot, art))
        {
            error = "导入根不能落在交付根下: " + NormalizedImportRoot + " ⊂ " + art;
            return false;
        }

        if (IsUnder(NormalizedPrefabRoot, art))
        {
            error = "Prefab 根不能落在交付根下: " + NormalizedPrefabRoot + " ⊂ " + art;
            return false;
        }

        error = null;
        return true;
    }

    private bool TryValidateNonDeliveryRoot(
        string root,
        string label,
        string example,
        out string error)
    {
        if (!IsAssetsFolderPath(root))
        {
            error = label + "必须是 Assets/ 下的路径，例如 " + example + "。";
            return false;
        }

        if (IsDeliveryAlertPath(root) || IsDeliveryAlertPath(root + "/"))
        {
            error = label + "落在交付区警报路径下（默认 Assets/Art/），禁止执行。请改到导入区。";
            return false;
        }

        error = null;
        return true;
    }

    private static string NormalizeAssetsRoot(string path, string fallback)
    {
        string raw = string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
        return raw.Replace("\\", "/").TrimEnd('/');
    }

    private static bool IsAssetsFolderPath(string root)
    {
        return !string.IsNullOrEmpty(root) &&
               root.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnder(string inner, string outer)
    {
        if (string.IsNullOrEmpty(inner) || string.IsNullOrEmpty(outer))
        {
            return false;
        }

        return inner.Equals(outer, System.StringComparison.OrdinalIgnoreCase) ||
               inner.StartsWith(outer + "/", System.StringComparison.OrdinalIgnoreCase);
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
                    "打开「批量选择器」面板会自动创建 " + DefaultAssetPath);
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
