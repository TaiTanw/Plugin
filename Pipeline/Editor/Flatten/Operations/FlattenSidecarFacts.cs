using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

// =====================================================================================
// Flatten / Operations — glTF 轻扫描事实。不是 PipelineJobContext（不 Load 主资产/Importer/材质/轴向）。
// =====================================================================================

/// <summary>一次 Scan 的操作数据：相对 URI、伴生 Assets 路径、缺件。</summary>
public sealed class FlattenSidecarFacts
{
    public string GltfAssetPath;
    public bool HasExternalUris;
    public readonly List<string> SidecarAssetPaths = new List<string>();
    public readonly List<string> MissingUris = new List<string>();

    /// <summary>对工程内 .gltf 做 <see cref="GltfPackageFiles.Scan"/>。非 gltf 返回空事实。</summary>
    public static FlattenSidecarFacts FromGltfAsset(string gltfAssetPath)
    {
        var facts = new FlattenSidecarFacts();
        facts.GltfAssetPath = (gltfAssetPath ?? string.Empty).Replace("\\", "/");
        string ext = Path.GetExtension(facts.GltfAssetPath);
        if (!string.Equals(ext, ".gltf", StringComparison.OrdinalIgnoreCase))
        {
            return facts;
        }

        string full = AssetPathUtility.ToFullPath(facts.GltfAssetPath);
        GltfExternalScan scan = GltfPackageFiles.Scan(full);
        facts.HasExternalUris = scan.HasExternalUris;
        for (int i = 0; i < scan.SidecarFullPaths.Count; i++)
        {
            string asset = AssetPathUtility.ToAssetPath(scan.SidecarFullPaths[i]);
            AddUnique(facts.SidecarAssetPaths, string.IsNullOrEmpty(asset)
                ? scan.SidecarFullPaths[i]
                : asset);
        }

        for (int i = 0; i < scan.MissingUris.Count; i++)
        {
            AddUnique(facts.MissingUris, scan.MissingUris[i]);
        }

        return facts;
    }

    /// <summary>Prefab 依赖里的 .gltf（GetDependencies，不 Build ctx）。</summary>
    public static List<string> ListGltfDependencies(string prefabPath)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(prefabPath))
        {
            return result;
        }

        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        for (int i = 0; i < dependencies.Length; i++)
        {
            string path = dependencies[i].Replace("\\", "/");
            if (string.Equals(Path.GetExtension(path), ".gltf", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(path);
            }
        }

        return result;
    }

    /// <summary>Prefab 依赖里的内核模型。</summary>
    public static List<string> ListModelDependencies(string prefabPath)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(prefabPath))
        {
            return result;
        }

        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        for (int i = 0; i < dependencies.Length; i++)
        {
            string path = dependencies[i].Replace("\\", "/");
            string ext = Path.GetExtension(path);
            if (IsKernelModelExtension(ext))
            {
                result.Add(path);
            }
        }

        return result;
    }

    /// <summary>若干 glTF 的 Scan 合并：任一有外 URI 则 HasExternalUris。</summary>
    public static FlattenSidecarFacts MergeGltfScans(IList<string> gltfAssetPaths)
    {
        var merged = new FlattenSidecarFacts();
        if (gltfAssetPaths == null)
        {
            return merged;
        }

        for (int i = 0; i < gltfAssetPaths.Count; i++)
        {
            FlattenSidecarFacts one = FromGltfAsset(gltfAssetPaths[i]);
            if (one.HasExternalUris)
            {
                merged.HasExternalUris = true;
            }

            if (string.IsNullOrEmpty(merged.GltfAssetPath))
            {
                merged.GltfAssetPath = one.GltfAssetPath;
            }

            for (int s = 0; s < one.SidecarAssetPaths.Count; s++)
            {
                AddUnique(merged.SidecarAssetPaths, one.SidecarAssetPaths[s]);
            }

            for (int m = 0; m < one.MissingUris.Count; m++)
            {
                AddUnique(merged.MissingUris, one.MissingUris[m]);
            }
        }

        return merged;
    }

    public static bool IsKernelModelExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".obj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase);
    }

    static void AddUnique(List<string> list, string path)
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
