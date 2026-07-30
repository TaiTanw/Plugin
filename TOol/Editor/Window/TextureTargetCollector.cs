using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   只负责"这次要处理哪些贴图资产"，返回一串资产路径。
//   不判断该不该处理（那是各操作的 CanProcess），也不执行任何处理。
//
// 从窗口里拆出来的原因：窗口只该管 GUI。范围收集涉及 Selection、文件夹递归、
//   全工程搜索三套逻辑，混在 OnGUI 里会让"为什么这次少处理了一个文件"这种问题
//   很难定位。
// =====================================================================================
public static class TextureTargetCollector
{
    public enum Scope
    {
        /// <summary>Project 面板里当前选中的资产；选中文件夹时递归其下所有贴图。</summary>
        Selection,

        /// <summary>指定一个文件夹，递归其下所有贴图。</summary>
        Folder,

        /// <summary>整个工程的所有贴图。</summary>
        WholeProject
    }

    public static List<string> Collect(Scope scope, string folderAssetPath)
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

        return CollectFromFolders(new[] { "Assets" });
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

        // 用 t:Texture2D 过一遍，再按扩展名筛。只按扩展名遍历全工程文件会慢得多，
        // 而只信 t:Texture2D 又会漏掉导入失败的文件——两个条件一起用最稳。
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
