using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>收集批量路径下的 .mat。</summary>
public static class MaterialTargetCollector
{
    public static List<string> CollectFromFolders(IList<string> folderAssetPaths)
    {
        var result = new List<string>();
        if (folderAssetPaths == null || folderAssetPaths.Count == 0)
        {
            return result;
        }

        var roots = new List<string>();
        for (int i = 0; i < folderAssetPaths.Count; i++)
        {
            string path = (folderAssetPaths[i] ?? string.Empty).Replace("\\", "/").TrimEnd('/');
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) && !roots.Contains(path))
            {
                roots.Add(path);
            }
        }

        if (roots.Count == 0)
        {
            return result;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", roots.ToArray());
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) ||
                !path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!result.Contains(path))
            {
                result.Add(path);
            }
        }

        result.Sort();
        return result;
    }

    public static List<string> CollectFromBatchFolders()
    {
        return CollectFromFolders(ResourceBatchFolderStore.GetValidMasterFolders());
    }
}
