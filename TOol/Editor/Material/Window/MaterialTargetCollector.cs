using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 收集本次要处理的 .mat（选中 / 指定夹 / 主面板批量路径）。
// =====================================================================================

/// <summary>材质目标收集。</summary>
public static class MaterialTargetCollector
{
    public enum Scope
    {
        Selection,
        Folder,
        BatchByPath
    }

    public static readonly string[] ScopeLabels =
    {
        "当前选中",
        "指定文件夹",
        "使用主面板批量路径"
    };

    public static List<string> Collect(Scope scope, string folderAssetPath, IList<string> batchFolders)
    {
        if (scope == Scope.Selection)
        {
            return CollectFromSelection();
        }

        if (scope == Scope.Folder)
        {
            if (string.IsNullOrEmpty(folderAssetPath) || !AssetDatabase.IsValidFolder(folderAssetPath))
            {
                return new List<string>();
            }

            return CollectFromFolders(new[] { folderAssetPath });
        }

        List<string> valid = ResourceBatchFolderStore.GetValidFolders(
            batchFolders ?? ResourceBatchFolderStore.GetMasterFolders());
        return valid.Count == 0 ? new List<string>() : CollectFromFolders(valid);
    }

    public static List<string> CollectFromBatchFolders()
    {
        return Collect(Scope.BatchByPath, null, ResourceBatchFolderStore.GetMasterFolders());
    }

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

    private static List<string> CollectFromSelection()
    {
        var result = new List<string>();
        var folders = new List<string>();

        Object[] selected = Selection.objects;
        if (selected == null)
        {
            return result;
        }

        for (int i = 0; i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                if (!folders.Contains(path))
                {
                    folders.Add(path);
                }
            }
            else if (path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!result.Contains(path))
                {
                    result.Add(path);
                }
            }
        }

        if (folders.Count > 0)
        {
            List<string> fromFolders = CollectFromFolders(folders);
            for (int i = 0; i < fromFolders.Count; i++)
            {
                if (!result.Contains(fromFolders[i]))
                {
                    result.Add(fromFolders[i]);
                }
            }
        }

        result.Sort();
        return result;
    }
}
