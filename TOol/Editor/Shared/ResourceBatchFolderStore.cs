using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 子面板「依据文件路径批量」的文件夹列表，以及总面板批量执行的唯一起点。
// 与子面板当前范围下拉（选中 / 单文件夹）无关；本机 EditorPrefs，不进版本库。
// =====================================================================================
public static class ResourceBatchFolderStore
{
    private const string TextureFoldersKey = "TOol.BatchFolders.Texture";
    private const string ModelFoldersKey = "TOol.BatchFolders.Model";

    private static List<string> textureFolders;
    private static List<string> modelFolders;

    public static List<string> GetTextureFolders()
    {
        return new List<string>(Load(ref textureFolders, TextureFoldersKey));
    }

    public static List<string> GetModelFolders()
    {
        return new List<string>(Load(ref modelFolders, ModelFoldersKey));
    }

    public static void SetTextureFolders(IList<string> folders)
    {
        Save(ref textureFolders, TextureFoldersKey, folders);
    }

    public static void SetModelFolders(IList<string> folders)
    {
        Save(ref modelFolders, ModelFoldersKey, folders);
    }

    /// <summary>过滤掉空路径、非文件夹；不修改存储。</summary>
    public static List<string> GetValidFolders(IList<string> folders)
    {
        var result = new List<string>();
        if (folders == null)
        {
            return result;
        }

        for (int i = 0; i < folders.Count; i++)
        {
            string path = Normalize(folders[i]);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogWarning("[批量路径] 跳过无效文件夹: " + path);
                continue;
            }

            if (!result.Contains(path))
            {
                result.Add(path);
            }
        }

        return result;
    }

    private static List<string> Load(ref List<string> cache, string key)
    {
        if (cache != null)
        {
            return cache;
        }

        cache = new List<string>();
        string raw = EditorPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(raw))
        {
            return cache;
        }

        string[] parts = raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string path = Normalize(parts[i]);
            if (!string.IsNullOrEmpty(path) && !cache.Contains(path))
            {
                cache.Add(path);
            }
        }

        return cache;
    }

    private static void Save(ref List<string> cache, string key, IList<string> folders)
    {
        cache = new List<string>();
        if (folders != null)
        {
            for (int i = 0; i < folders.Count; i++)
            {
                string path = Normalize(folders[i]);
                if (!string.IsNullOrEmpty(path) && !cache.Contains(path))
                {
                    cache.Add(path);
                }
            }
        }

        var builder = new StringBuilder();
        for (int i = 0; i < cache.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append(cache[i]);
        }

        EditorPrefs.SetString(key, builder.ToString());
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return path.Replace("\\", "/").Trim();
    }
}
