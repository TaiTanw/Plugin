using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

// =====================================================================================
// 编排工作区三根：Incoming / IncomingPrefab / Art。只服务管线步骤。
// 内核不得 Load 本类；由 PipelineOptions 传入。批量选择器 / 人工平铺 / 资源面板自管。
// =====================================================================================
public static class PipelineWorkspace
{
    public const string DefaultImportRoot = "Assets/Incoming";
    public const string DefaultPrefabRoot = "Assets/IncomingPrefab";
    public const string DefaultArtRoot = "Assets/Art";

    public static string Normalize(string path, string fallback)
    {
        string p = string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
        return p.Replace("\\", "/").TrimEnd('/');
    }

    public static void ApplyDefaults(PipelineOptions options)
    {
        if (options == null)
        {
            return;
        }

        options.ImportRoot = Normalize(options.ImportRoot, DefaultImportRoot);
        options.PrefabRoot = Normalize(options.PrefabRoot, DefaultPrefabRoot);
        options.ArtRoot = Normalize(options.ArtRoot, DefaultArtRoot);
    }

    public static bool TryValidate(PipelineOptions options, out string error)
    {
        error = null;
        if (options == null)
        {
            error = "PipelineOptions 为 null";
            return false;
        }

        ApplyDefaults(options);
        if (!IsAssetsRoot(options.ImportRoot, "导入根 Incoming", out error) ||
            !IsAssetsRoot(options.PrefabRoot, "Prefab 根 IncomingPrefab", out error) ||
            !IsAssetsRoot(options.ArtRoot, "交付根 Art", out error))
        {
            return false;
        }

        if (IsUnder(options.ImportRoot, options.ArtRoot))
        {
            error = "导入根不能落在交付根下: " + options.ImportRoot + " ⊂ " + options.ArtRoot;
            return false;
        }

        if (IsUnder(options.PrefabRoot, options.ArtRoot))
        {
            error = "Prefab 根不能落在交付根下: " + options.PrefabRoot + " ⊂ " + options.ArtRoot;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 挂起导入后执行清空。禁止在半截树里 Refresh（glTF 会按已删伴生重导刷屏）。
    /// </summary>
    public static void RunWipes(Action wipe)
    {
        if (wipe == null)
        {
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ReleaseCachedFileHandles();
        bool autoOff = false;
        bool editing = false;
        try
        {
            AssetDatabase.DisallowAutoRefresh();
            autoOff = true;
            AssetDatabase.StartAssetEditing();
            editing = true;
            wipe();
        }
        finally
        {
            if (editing)
            {
                AssetDatabase.StopAssetEditing();
            }

            if (autoOff)
            {
                AssetDatabase.AllowAutoRefresh();
            }

            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// 删除 <paramref name="assetRoot"/> 下全部直接子项，根夹本身留下。
    /// 拒绝 Assets、Plugin、非 Assets/ 路径。夹不存在视为已空。
    /// 本方法不 Refresh；本趟清空走 <see cref="RunWipes"/>。
    /// </summary>
    public static bool TryClearRootContents(string assetRoot, out int deletedCount, out string error)
    {
        deletedCount = 0;
        error = null;
        if (string.IsNullOrWhiteSpace(assetRoot))
        {
            error = "根路径为空";
            return false;
        }

        string root = Normalize(assetRoot, assetRoot);
        if (root.IndexOf("..", StringComparison.Ordinal) >= 0 ||
            !root.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            root.Equals("Assets", StringComparison.OrdinalIgnoreCase))
        {
            error = "拒绝清空非 Assets/ 子夹: " + assetRoot;
            return false;
        }

        if (IsUnder(root, "Assets/Plugin"))
        {
            error = "拒绝清空 Plugin: " + root;
            return false;
        }

        string full = AssetPathUtility.ToFullPath(root);
        if (string.IsNullOrEmpty(full) || !Directory.Exists(full))
        {
            return true;
        }

        string[] entries = Directory.GetFileSystemEntries(full);
        bool allOk = true;
        string firstFail = null;
        for (int i = 0; i < entries.Length; i++)
        {
            string disk = entries[i].Replace("\\", "/");
            if (disk.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string name = Path.GetFileName(disk);
            if (string.IsNullOrEmpty(name) || name == "." || name == "..")
            {
                continue;
            }

            string assetPath = root + "/" + name;
            if (TryDeleteTree(assetPath, disk))
            {
                deletedCount++;
                continue;
            }

            allOk = false;
            if (firstFail == null)
            {
                firstFail = assetPath;
            }
        }

        if (!allOk)
        {
            error = "未能删除: " + firstFail;
            return false;
        }

        return true;
    }

    private static bool TryDeleteTree(string assetPath, string diskPath)
    {
        if (string.IsNullOrEmpty(diskPath))
        {
            return false;
        }

        diskPath = diskPath.Replace("\\", "/");
        assetPath = (assetPath ?? string.Empty).Replace("\\", "/");

        if (Directory.Exists(diskPath))
        {
            string[] children = Directory.GetFileSystemEntries(diskPath);
            for (int i = 0; i < children.Length; i++)
            {
                string childDisk = children[i].Replace("\\", "/");
                if (childDisk.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string childName = Path.GetFileName(childDisk);
                if (string.IsNullOrEmpty(childName) || childName == "." || childName == "..")
                {
                    continue;
                }

                TryDeleteTree(assetPath + "/" + childName, childDisk);
            }
        }

        if (!File.Exists(diskPath) && !Directory.Exists(diskPath))
        {
            TryDeleteSidecarMeta(diskPath);
            return true;
        }

        if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.DeleteAsset(assetPath))
        {
            return true;
        }

        return TryDeleteOnDisk(diskPath);
    }

    private static bool TryDeleteOnDisk(string diskPath)
    {
        ClearReadOnlyRecursive(diskPath);
        FileUtil.DeleteFileOrDirectory(diskPath);
        TryDeleteSidecarMeta(diskPath);
        if (!File.Exists(diskPath) && !Directory.Exists(diskPath))
        {
            return true;
        }

        try
        {
            ClearReadOnlyRecursive(diskPath);
            if (Directory.Exists(diskPath))
            {
                Directory.Delete(diskPath, true);
            }
            else if (File.Exists(diskPath))
            {
                File.Delete(diskPath);
            }

            TryDeleteSidecarMeta(diskPath);
        }
        catch (Exception)
        {
            return !File.Exists(diskPath) && !Directory.Exists(diskPath);
        }

        return !File.Exists(diskPath) && !Directory.Exists(diskPath);
    }

    private static void TryDeleteSidecarMeta(string diskPath)
    {
        string meta = diskPath + ".meta";
        if (!File.Exists(meta))
        {
            return;
        }

        try
        {
            File.SetAttributes(meta, FileAttributes.Normal);
        }
        catch (Exception)
        {
        }

        FileUtil.DeleteFileOrDirectory(meta);
        if (File.Exists(meta))
        {
            try
            {
                File.Delete(meta);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void ClearReadOnlyRecursive(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                return;
            }

            if (!Directory.Exists(path))
            {
                return;
            }

            string[] children = Directory.GetFileSystemEntries(path);
            for (int i = 0; i < children.Length; i++)
            {
                ClearReadOnlyRecursive(children[i]);
            }

            File.SetAttributes(path, FileAttributes.Normal);
        }
        catch (Exception)
        {
        }
    }

    public static bool IsUnder(string path, string root)
    {
        return ResourceExcludeUtility.IsUnderRoot(path, root);
    }

    /// <summary>从交付 Prefab 路径切出 Art/&lt;单元&gt;，供⑤扫描。</summary>
    public static List<string> CollectUnitFolders(IList<string> assetPaths, string root)
    {
        var folders = new List<string>();
        if (assetPaths == null)
        {
            return folders;
        }

        string art = Normalize(root, DefaultArtRoot);
        string prefix = art + "/";
        for (int i = 0; i < assetPaths.Count; i++)
        {
            string path = (assetPaths[i] ?? string.Empty).Replace("\\", "/");
            if (!path.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relative = path.Substring(prefix.Length);
            int slash = relative.IndexOf('/');
            if (slash <= 0)
            {
                continue;
            }

            string unit = art + "/" + relative.Substring(0, slash);
            if (!folders.Contains(unit))
            {
                folders.Add(unit);
            }
        }

        return folders;
    }

    private static bool IsAssetsRoot(string path, string label, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(path) ||
            !path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            error = label + "必须是 Assets/ 下路径，当前: " + path;
            return false;
        }

        return true;
    }
}
