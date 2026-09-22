using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 平铺拷贝分类：替代 GetPreparedPrefabDependencyFolder。
// Packages/ 与非 Assets 资源不拷；无人认领的 Assets 文件进 Unknown/（提示不阻断）。
// 实际落点见 ResolveDestAssetPath：.fbm 父夹永远套一层，禁止按文件名压平。
// =====================================================================================

/// <summary>按注册表把依赖路径解析成 Art/&lt;名&gt;/ 下的相对目录。</summary>
public static class FlattenCopyRunner
{
    public static string ResolveRelativeFolder(string assetPath)
    {
        return ResolveRelativeFolder(assetPath, FlattenCategorySettings.Load());
    }

    public static string ResolveRelativeFolder(string assetPath, FlattenOperationPolicy operationPolicy)
    {
        return ResolveRelativeFolder(
            assetPath,
            operationPolicy == null ? null : operationPolicy.Categories);
    }

    public static string ResolveRelativeFolder(string assetPath, FlattenCategorySettings settings)
    {
        string path = (assetPath ?? string.Empty).Replace("\\", "/");
        if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        settings = settings ?? FlattenCategorySettings.CreateDefaults();
        IList<IFlattenCategoryProcessor> processors = FlattenCategoryRegistry.All;
        for (int i = 0; i < processors.Count; i++)
        {
            IFlattenCategoryProcessor processor = processors[i];
            if (processor.Id == UnknownFlattenProcessor.ProcessorId)
            {
                continue;
            }

            if (!settings.IsEnabled(processor.Id))
            {
                continue;
            }

            if (!processor.Matches(path, settings))
            {
                continue;
            }

            return processor.ResolveRelativeFolder(path);
        }

        return UnknownFlattenProcessor.ProcessorId;
    }

    /// <summary>
    /// 分类夹下的实际落点：.fbm 父夹永远再套一层；非 .fbm 仅在分类夹根文件已被占用、
    /// 且来源父夹名不是分类叶名时套一层，避免 Texture/ 重跑把同文件再套进 Texture/Texture/。
    /// </summary>
    public static string ResolveDestAssetPath(
        string assetFolder,
        string sourcePath,
        FlattenOperationPolicy operationPolicy)
    {
        return ResolveDestAssetPath(
            assetFolder,
            sourcePath,
            operationPolicy == null ? null : operationPolicy.Categories);
    }

    public static string ResolveDestAssetPath(
        string assetFolder,
        string sourcePath,
        FlattenCategorySettings settings)
    {
        string relativeFolder = ResolveRelativeFolder(sourcePath, settings);
        if (string.IsNullOrEmpty(relativeFolder) || string.IsNullOrEmpty(assetFolder))
        {
            return null;
        }

        string normalizedSource = (sourcePath ?? string.Empty).Replace("\\", "/");
        string fileName = Path.GetFileName(normalizedSource);
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        string unit = assetFolder.Replace("\\", "/").TrimEnd('/');
        string flatDest = CombineAssetPath(CombineAssetPath(unit, relativeFolder), fileName);
        bool occupied = AssetDatabase.LoadMainAssetAtPath(flatDest) != null;
        string relativeDest = AppendSourceDisambiguation(relativeFolder, normalizedSource, occupied);
        return string.IsNullOrEmpty(relativeDest) ? null : CombineAssetPath(unit, relativeDest);
    }

    public static string AppendSourceDisambiguation(
        string relativeFolder,
        string sourcePath,
        bool flatDestOccupied)
    {
        string folder = (relativeFolder ?? string.Empty).Replace("\\", "/").Trim('/');
        string path = (sourcePath ?? string.Empty).Replace("\\", "/");
        string fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        string parent = ImmediateParentName(path);
        if (IsEmbeddedMediaFolderName(parent))
        {
            return folder + "/" + parent + "/" + fileName;
        }

        string leaf = LastSegment(folder);
        if (flatDestOccupied &&
            IsSafeNestedFolderName(parent) &&
            !parent.Equals(leaf, StringComparison.OrdinalIgnoreCase))
        {
            return folder + "/" + parent + "/" + fileName;
        }

        return folder + "/" + fileName;
    }

    static string CombineAssetPath(string left, string right)
    {
        string a = (left ?? string.Empty).Replace("\\", "/").TrimEnd('/');
        string b = (right ?? string.Empty).Replace("\\", "/").Trim('/');
        if (string.IsNullOrEmpty(a))
        {
            return b;
        }

        return string.IsNullOrEmpty(b) ? a : a + "/" + b;
    }

    static string ImmediateParentName(string assetPath)
    {
        string directory = Path.GetDirectoryName((assetPath ?? string.Empty).Replace("\\", "/"));
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        return LastSegment(directory.Replace("\\", "/"));
    }

    static string LastSegment(string path)
    {
        string normalized = (path ?? string.Empty).Replace("\\", "/").Trim('/');
        int slash = normalized.LastIndexOf('/');
        return slash < 0 ? normalized : normalized.Substring(slash + 1);
    }

    static bool IsEmbeddedMediaFolderName(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               name.EndsWith(".fbm", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSafeNestedFolderName(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "." || name == "..")
        {
            return false;
        }

        return name.IndexOf('/') < 0 && name.IndexOf('\\') < 0 && name.IndexOf(':') < 0;
    }

    public static void LogUnknownIfAny(string assetFolder, string assetName)
    {
        List<string> unknownPaths = CollectUnknownAssetPaths(assetFolder);
        if (unknownPaths.Count == 0)
        {
            return;
        }

        Debug.LogWarning("[Retinar] " + assetName + "：平铺后仍有 " + unknownPaths.Count +
            " 个未归类文件（包不完整，已继续平铺，请人工整理后可删 Unknown）：\n" +
            string.Join("\n", unknownPaths.ToArray()));
    }

    public static List<string> CollectUnknownAssetPaths(string assetFolder)
    {
        var unknownPaths = new List<string>();
        CollectFilesUnder(FlattenLayout.RootUnknownFolder(assetFolder), unknownPaths);
        CollectFilesUnder(FlattenLayout.ImageUnknownFolder(assetFolder), unknownPaths);
        return unknownPaths;
    }

    private static void CollectFilesUnder(string assetFolder, List<string> result)
    {
        if (string.IsNullOrEmpty(assetFolder) || !AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Object", new[] { assetFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            {
                continue;
            }

            if (!result.Contains(path))
            {
                result.Add(path);
            }
        }
    }
}
