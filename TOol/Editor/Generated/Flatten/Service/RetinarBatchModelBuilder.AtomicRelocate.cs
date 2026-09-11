using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// ④ B′ 原子搬迁：Art/<名>/<名>/ 保持相对 URI。不改 CopyAdjustedPrefabDependencies。
// =====================================================================================

public static partial class RetinarBatchModelBuilder
{
    /// <summary>
    /// 主文件 + 伴生拷到 <c>Art/&lt;名&gt;/&lt;名&gt;/</c>，返回旧→新路径表给 D。
    /// </summary>
    private static Dictionary<string, string> RelocateAtomicPackage(
        string assetFolder,
        string assetName,
        RetinarFlattenOptions flattenOptions,
        string fallbackModelPath)
    {
        var copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        flattenOptions = flattenOptions ?? RetinarFlattenOptions.Default;

        if (flattenOptions.MissingUris != null && flattenOptions.MissingUris.Count > 0)
        {
            for (int i = 0; i < flattenOptions.MissingUris.Count; i++)
            {
                Debug.LogError("[Retinar] B′ 缺必需伴生: " + flattenOptions.MissingUris[i]);
            }

            Debug.LogError("[Retinar] B′ 已拒绝：缺必需伴生 × " + flattenOptions.MissingUris.Count);
            return null;
        }

        string primary = (flattenOptions.PrimaryAssetPath ?? string.Empty).Replace("\\", "/");
        if (string.IsNullOrEmpty(primary))
        {
            primary = (fallbackModelPath ?? string.Empty).Replace("\\", "/");
        }

        if (string.IsNullOrEmpty(primary))
        {
            Debug.LogWarning("[Retinar] B′ 无主文件路径，原子搬迁跳过");
            return copied;
        }

        string destRoot = FlattenLayout.Combine(assetFolder, assetName);
        FlattenLayout.EnsureFolder(destRoot);

        string primaryFull = ResolveFullPath(primary);
        if (string.IsNullOrEmpty(primaryFull) || !File.Exists(primaryFull))
        {
            Debug.LogWarning("[Retinar] B′ 主文件磁盘不存在: " + primary);
            return copied;
        }

        var sources = new List<string>();
        if (flattenOptions.SidecarPaths != null)
        {
            for (int i = 0; i < flattenOptions.SidecarPaths.Count; i++)
            {
                AddUniquePath(sources, (flattenOptions.SidecarPaths[i] ?? string.Empty).Replace("\\", "/"));
            }
        }

        AddUniquePath(sources, primary);

        bool allCopied = true;
        int copiedFileCount = 0;
        for (int i = 0; i < sources.Count; i++)
        {
            string src = sources[i];
            string srcFull = ResolveFullPath(src);
            if (string.IsNullOrEmpty(srcFull) || !File.Exists(srcFull))
            {
                Debug.LogError("[Retinar] B′ 必需输入在执行时已缺失: " + src);
                allCopied = false;
                continue;
            }

            string rel = GltfPackageFiles.MakeRelativeToGltfDir(primaryFull, srcFull);
            string destAsset = destRoot + "/" + rel.Replace("\\", "/");
            destAsset = destAsset.Replace("\\", "/");
            if (!destAsset.StartsWith(destRoot + "/", StringComparison.OrdinalIgnoreCase) &&
                !destAsset.Equals(destRoot, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[Retinar] B′ 拒绝跳出原子夹: " + src + " → " + destAsset);
                allCopied = false;
                continue;
            }
            string destFolder = Path.GetDirectoryName(destAsset);
            if (!string.IsNullOrEmpty(destFolder))
            {
                FlattenLayout.EnsureFolder(destFolder.Replace("\\", "/"));
            }

            string copiedPath = CopyPackageFileToArt(src, srcFull, destAsset);
            string copiedFull = ResolveFullPath(copiedPath);
            if (string.IsNullOrEmpty(copiedPath) ||
                !copiedPath.Equals(destAsset, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(copiedFull) ||
                !File.Exists(copiedFull))
            {
                Debug.LogError("[Retinar] B′ 未生成必需目标: " + src + " → " + destAsset);
                allCopied = false;
                continue;
            }

            copiedFileCount++;
            if (!copiedPath.Equals(src, StringComparison.OrdinalIgnoreCase))
            {
                copied[NormalizeAssetOrFull(src)] = copiedPath;
            }
        }

        AssetDatabase.Refresh();
        if (!allCopied || copiedFileCount != sources.Count)
        {
            Debug.LogError("[Retinar] B′ 原子搬迁不完整：成功 " + copiedFileCount +
                           " / 必需 " + sources.Count + " → " + destRoot);
            return null;
        }

        Debug.Log("[Retinar] B′ 原子搬迁 " + copiedFileCount + " 条 → " + destRoot);
        return copied;
    }

    static string CopyPackageFileToArt(string sourceHint, string sourceFull, string destAsset)
    {
        destAsset = destAsset.Replace("\\", "/");
        string destFull = AssetPathToFullPath(destAsset);
        if (string.IsNullOrEmpty(destFull))
        {
            Debug.LogWarning("[Retinar] B′ 无法解析目标: " + destAsset);
            return sourceHint;
        }

        if (sourceHint.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sourceHint) != null)
        {
            return CopyAssetToExactPath(sourceHint, destAsset);
        }

        string destDir = Path.GetDirectoryName(destFull);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        try
        {
            if (!File.Exists(destFull))
            {
                File.Copy(sourceFull, destFull, false);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Retinar] B′ File.Copy 失败: " + sourceFull + " → " + destFull + " " + ex.Message);
            return sourceHint;
        }

        AssetDatabase.ImportAsset(destAsset, ImportAssetOptions.ForceUpdate);
        return destAsset;
    }

    static string ResolveFullPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return AssetPathToFullPath(path);
        }

        try
        {
            return Path.GetFullPath(path).Replace("\\", "/");
        }
        catch
        {
            return path.Replace("\\", "/");
        }
    }

    static string NormalizeAssetOrFull(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        path = path.Replace("\\", "/");
        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        string asset = FullPathToAssetPath(path);
        return string.IsNullOrEmpty(asset) ? path : asset;
    }

    static void AddUniquePath(List<string> list, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(path);
    }
}
