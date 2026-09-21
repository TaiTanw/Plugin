using System;
using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// Shared / UnityPackage — 信封内根 Prefab。子 Prefab 仍留在信封，不单独开一批。
// =====================================================================================

/// <summary>pack 展开：只收没被同信封其它 Prefab 依赖的根。</summary>
public static class UnityPackageRootPrefabs
{
    /// <summary>
    /// 列出 <paramref name="envelopeAssetFolder"/> 下的根 Prefab（Assets 路径）。
    /// 夹无效或没有 Prefab 时返回空列表。
    /// </summary>
    public static List<string> Collect(string envelopeAssetFolder)
    {
        var roots = new List<string>();
        if (string.IsNullOrWhiteSpace(envelopeAssetFolder))
        {
            return roots;
        }

        string folder = envelopeAssetFolder.Replace("\\", "/").TrimEnd('/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return roots;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        var prefabs = new List<string>();
        var inEnvelope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (guids != null)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = (AssetDatabase.GUIDToAssetPath(guids[i]) ?? string.Empty).Replace("\\", "/");
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (inEnvelope.Add(path))
                {
                    prefabs.Add(path);
                }
            }
        }

        var nested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < prefabs.Count; i++)
        {
            string prefab = prefabs[i];
            string[] deps = AssetDatabase.GetDependencies(prefab, true);
            if (deps == null)
            {
                continue;
            }

            for (int d = 0; d < deps.Length; d++)
            {
                string dep = (deps[d] ?? string.Empty).Replace("\\", "/");
                if (string.IsNullOrEmpty(dep) ||
                    dep.Equals(prefab, StringComparison.OrdinalIgnoreCase) ||
                    !dep.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (inEnvelope.Contains(dep))
                {
                    nested.Add(dep);
                }
            }
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (!nested.Contains(prefabs[i]))
            {
                roots.Add(prefabs[i]);
            }
        }

        roots.Sort(StringComparer.OrdinalIgnoreCase);
        return roots;
    }
}
