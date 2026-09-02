using System;
using UnityEngine;

// =====================================================================================
// Pipeline — ② 后 / ③ 前：把 GltfPackageFiles.Scan 填进 ctx。
// 扩展探测：改 GltfPackageFiles.Scan，或在 Apply 里加一步覆盖 HasExternalUris/Sidecar。
// =====================================================================================

/// <summary>JobContext.Build 的 glTF 探测入口。不引用平铺。</summary>
public static class PipelineGltfUriProbe
{
    public static void Apply(PipelineJobContext ctx)
    {
        if (ctx == null || string.IsNullOrEmpty(ctx.PrimaryAssetPath))
        {
            return;
        }

        string full = AssetPathToFullPath(ctx.PrimaryAssetPath);
        GltfExternalScan scan = GltfPackageFiles.Scan(full);
        ctx.HasExternalUris = scan.HasExternalUris;
        for (int i = 0; i < scan.SidecarFullPaths.Count; i++)
        {
            string asset = FullPathToAssetPath(scan.SidecarFullPaths[i]);
            AddUnique(ctx.SidecarPaths, string.IsNullOrEmpty(asset)
                ? scan.SidecarFullPaths[i]
                : asset);
        }

        for (int i = 0; i < scan.MissingUris.Count; i++)
        {
            ctx.Warnings.Add("缺伴生: " + scan.MissingUris[i]);
        }

        for (int i = 0; i < scan.Notes.Count; i++)
        {
            ctx.Warnings.Add(scan.Notes[i]);
        }
    }

    static void AddUnique(System.Collections.Generic.List<string> list, string path)
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

    static string AssetPathToFullPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) ||
            !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string data = Application.dataPath.Replace("\\", "/");
        return data + assetPath.Substring("Assets".Length);
    }

    static string FullPathToAssetPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return null;
        }

        string full = fullPath.Replace("\\", "/");
        string data = Application.dataPath.Replace("\\", "/");
        if (full.StartsWith(data + "/", StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + full.Substring(data.Length);
        }

        return null;
    }
}
