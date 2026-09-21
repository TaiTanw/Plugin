using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

// =====================================================================================
// ManualFlatten — 选区展开。文件夹 = 原址 pack 根，只收根 Prefab。
// =====================================================================================

/// <summary>人工④选区：模型/Prefab 原样；夹走 <see cref="UnityPackageRootPrefabs"/>。</summary>
public static class ManualFlattenSources
{
    public static List<string> Expand(
        IList<string> selectedAssetPaths,
        out List<string> errors)
    {
        errors = new List<string>();
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (selectedAssetPaths == null)
        {
            return result;
        }

        for (int i = 0; i < selectedAssetPaths.Count; i++)
        {
            string path = (selectedAssetPaths[i] ?? string.Empty).Replace("\\", "/").Trim();
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (ToolImportApi.IsUnityPackagePath(path))
            {
                errors.Add("请选择解包后的根文件夹，不要选 .unitypackage 文件: " + path);
                continue;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                List<string> roots = UnityPackageRootPrefabs.Collect(path);
                if (roots == null || roots.Count == 0)
                {
                    errors.Add("信封内无根 Prefab: " + path);
                    continue;
                }

                for (int r = 0; r < roots.Count; r++)
                {
                    AddUnique(result, seen, roots[r]);
                }

                continue;
            }

            string ext = Path.GetExtension(path);
            if (string.Equals(ext, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                FlattenSidecarFacts.IsKernelModelExtension(ext))
            {
                AddUnique(result, seen, path);
            }
        }

        return result;
    }

    static void AddUnique(List<string> result, HashSet<string> seen, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string n = path.Replace("\\", "/");
        if (seen.Add(n))
        {
            result.Add(n);
        }
    }
}
