using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L1 主面板共用批量路径（贴图/模型总批量同一份）。本机 EditorPrefs，不进版本库。
// L2「使用主面板批量路径」只读本 Store；不再维护贴图/模型两套列表。
// =====================================================================================
public static class ResourceBatchFolderStore
{
    private const string MasterFoldersKey = "TOol.BatchFolders.Master";
    private const string LegacyTextureFoldersKey = "TOol.BatchFolders.Texture";
    private const string LegacyModelFoldersKey = "TOol.BatchFolders.Model";
    private const string MergedToMasterKey = "TOol.BatchFolders.MergedToMaster";
    private const string ArtDefaultSeededKey = "TOol.BatchFolders.ArtDefaultSeeded";
    private const string DefaultArtFolder = "Assets/Art";

    private static List<string> masterFolders;

    public static List<string> GetMasterFolders()
    {
        return new List<string>(LoadMaster());
    }

    public static void SetMasterFolders(IList<string> folders)
    {
        Save(ref masterFolders, MasterFoldersKey, folders);
    }

    /// <summary>兼容旧调用：已与主路径合并，读写同一份。</summary>
    public static List<string> GetTextureFolders()
    {
        return GetMasterFolders();
    }

    public static List<string> GetModelFolders()
    {
        return GetMasterFolders();
    }

    public static void SetTextureFolders(IList<string> folders)
    {
        SetMasterFolders(folders);
    }

    public static void SetModelFolders(IList<string> folders)
    {
        SetMasterFolders(folders);
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

    public static List<string> GetValidMasterFolders()
    {
        return GetValidFolders(GetMasterFolders());
    }

    /// <summary>供 L2 标题显示，例如「Assets/Art | Assets/Incoming（共 2）」。</summary>
    public static string FormatMasterPathsTitle(int maxShow = 2)
    {
        List<string> valid = GetValidMasterFolders();
        if (valid.Count == 0)
        {
            return "（主面板尚未配置有效路径）";
        }

        var builder = new StringBuilder();
        int show = Mathf.Min(maxShow, valid.Count);
        for (int i = 0; i < show; i++)
        {
            if (i > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(valid[i]);
        }

        if (valid.Count > show)
        {
            builder.Append("（共 ").Append(valid.Count).Append("）");
        }

        return builder.ToString();
    }

    private static List<string> LoadMaster()
    {
        if (masterFolders != null)
        {
            return masterFolders;
        }

        EnsureMergedFromLegacy();

        masterFolders = new List<string>();
        string raw = EditorPrefs.GetString(MasterFoldersKey, string.Empty);
        if (!string.IsNullOrEmpty(raw))
        {
            string[] parts = raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string path = Normalize(parts[i]);
                if (!string.IsNullOrEmpty(path) && !masterFolders.Contains(path))
                {
                    masterFolders.Add(path);
                }
            }
        }

        MaybeSeedDefaultArtFolder(ref masterFolders, MasterFoldersKey);
        return masterFolders;
    }

    private static void EnsureMergedFromLegacy()
    {
        if (EditorPrefs.GetBool(MergedToMasterKey, false))
        {
            return;
        }

        var merged = new List<string>();
        AppendLegacyRaw(merged, EditorPrefs.GetString(LegacyTextureFoldersKey, string.Empty));
        AppendLegacyRaw(merged, EditorPrefs.GetString(LegacyModelFoldersKey, string.Empty));
        AppendLegacyRaw(merged, EditorPrefs.GetString(MasterFoldersKey, string.Empty));

        if (merged.Count == 0 && AssetDatabase.IsValidFolder(DefaultArtFolder))
        {
            merged.Add(DefaultArtFolder);
        }

        Save(ref masterFolders, MasterFoldersKey, merged);
        EditorPrefs.SetBool(MergedToMasterKey, true);
        EditorPrefs.SetBool(ArtDefaultSeededKey, true);
    }

    private static void AppendLegacyRaw(List<string> target, string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        string[] parts = raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string path = Normalize(parts[i]);
            if (!string.IsNullOrEmpty(path) && !target.Contains(path))
            {
                target.Add(path);
            }
        }
    }

    private static void MaybeSeedDefaultArtFolder(ref List<string> cache, string key)
    {
        if (!AssetDatabase.IsValidFolder(DefaultArtFolder))
        {
            return;
        }

        if (cache.Count == 0)
        {
            cache.Add(DefaultArtFolder);
            Save(ref cache, key, cache);
            EditorPrefs.SetBool(ArtDefaultSeededKey, true);
            return;
        }

        if (EditorPrefs.GetBool(ArtDefaultSeededKey, false))
        {
            return;
        }

        if (!cache.Contains(DefaultArtFolder))
        {
            cache.Insert(0, DefaultArtFolder);
            Save(ref cache, key, cache);
        }

        EditorPrefs.SetBool(ArtDefaultSeededKey, true);
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
