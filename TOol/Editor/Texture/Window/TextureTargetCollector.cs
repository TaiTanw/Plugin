using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：只负责"这次要处理哪些贴图资产"，返回资产路径列表。
// =====================================================================================
public static class TextureTargetCollector
{
    public enum Scope
    {
        /// <summary>Project 面板当前选中；选中文件夹时递归其下贴图。</summary>
        Selection,

        /// <summary>窗口临时指定的单个文件夹。</summary>
        Folder,

        /// <summary>只读使用主面板共用批量路径（多文件夹，各根递归）。</summary>
        BatchByPath
    }

    /// <summary>中文标签，供 EnumPopup 以外的下拉使用。</summary>
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
            return string.IsNullOrEmpty(folderAssetPath)
                ? new List<string>()
                : CollectFromFolders(new[] { folderAssetPath });
        }

        List<string> valid = ResourceBatchFolderStore.GetValidFolders(
            batchFolders ?? ResourceBatchFolderStore.GetMasterFolders());
        return valid.Count == 0 ? new List<string>() : CollectFromFolders(valid.ToArray());
    }

    /// <summary>总面板：始终按主面板批量路径收集。</summary>
    public static List<string> CollectFromBatchFolders()
    {
        return Collect(Scope.BatchByPath, null, ResourceBatchFolderStore.GetMasterFolders());
    }

    private static List<string> CollectFromSelection()
    {
        var result = new List<string>();
        var folders = new List<string>();

        foreach (Object selected in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                folders.Add(path);
            }
            else if (TextureCodecRegistry.IsSupported(path))
            {
                result.Add(path);
            }
        }

        if (folders.Count > 0)
        {
            foreach (string path in CollectFromFolders(folders.ToArray()))
            {
                if (!result.Contains(path))
                {
                    result.Add(path);
                }
            }
        }

        return result;
    }

    private static List<string> CollectFromFolders(string[] folderAssetPaths)
    {
        var result = new List<string>();
        if (folderAssetPaths == null || folderAssetPaths.Length == 0)
        {
            return result;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folderAssetPaths))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (TextureCodecRegistry.IsSupported(path) && !result.Contains(path))
            {
                result.Add(path);
            }
        }

        result.Sort();
        return result;
    }
}
