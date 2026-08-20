using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 平铺拷贝分类：替代 GetPreparedPrefabDependencyFolder。
// Packages/ 与非 Assets 资源不拷；无人认领的 Assets 文件进 Unknown/（提示不阻断）。
// =====================================================================================

/// <summary>按注册表把依赖路径解析成 Art/&lt;名&gt;/ 下的相对目录。</summary>
public static class FlattenCopyRunner
{
    public static string ResolveRelativeFolder(string assetPath)
    {
        string path = (assetPath ?? string.Empty).Replace("\\", "/");
        if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        FlattenCategorySettings settings = FlattenCategorySettings.Load();
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
