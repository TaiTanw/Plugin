using System.IO;
using UnityEngine;

// =====================================================================================
// Generated / Prefab / Layout
// 中文：③ 预设体制作 — 专用夹路径约定（只拼路径，不写盘）。
// 层级：L-配置/路径。命名对齐 BatchFbxImportService.ResolveFolderName（向上三层）。
// =====================================================================================

/// <summary>
/// 自动 Prefab 落盘路径。
/// </summary>
public static class PrefabIncomingPaths
{
    /// <summary>人工 Prefab 根（选择器 SO）；编排③传入 override，不读此处。</summary>
    public static string PrefabRoot
    {
        get { return BatchFbxImportSettings.Current.NormalizedPrefabRoot; }
    }

    /// <summary>
    /// 由源模型资产路径推导目标 Prefab 路径。
    /// 缺省名 = ResolveFolderName（向上三层 _ 拼接）；materialId 非空时优先用 materialId。
    /// </summary>
    /// <param name="sourceModelAssetPath">Assets 下模型路径，或磁盘绝对路径</param>
    /// <param name="materialId">CLI/任务 Id；非空则覆盖三层名</param>
    /// <param name="nameDisambiguator">同名冲突时追加的 stem（可为 null）</param>
    /// <param name="prefabRoot">非空覆盖落盘根。</param>
    /// <param name="importRoot">非空覆盖 Incoming 夹名识别。</param>
    public static string PrefabPathForSourceModel(
        string sourceModelAssetPath,
        string materialId = null,
        string nameDisambiguator = null,
        string prefabRoot = null,
        string importRoot = null)
    {
        string baseName = ResolvePrefabBaseName(sourceModelAssetPath, materialId, importRoot);
        if (!string.IsNullOrEmpty(nameDisambiguator))
        {
            string stem = BatchFbxImportService.SanitizeFolderName(nameDisambiguator);
            if (!string.IsNullOrEmpty(stem) &&
                !baseName.EndsWith("_" + stem, System.StringComparison.OrdinalIgnoreCase))
            {
                baseName = BatchFbxImportService.SanitizeFolderName(baseName + "_" + stem);
            }
        }

        string root = string.IsNullOrWhiteSpace(prefabRoot)
            ? PrefabRoot
            : prefabRoot.Replace("\\", "/").TrimEnd('/');
        return root + "/" + baseName + ".prefab";
    }

    /// <summary>解析 Prefab 主文件名（无扩展名、无根路径）。</summary>
    public static string ResolvePrefabBaseName(
        string sourceModelAssetPath,
        string materialId = null,
        string importRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(materialId))
        {
            return BatchFbxImportService.SanitizeFolderName(materialId.Trim());
        }

        string pathForResolve = sourceModelAssetPath ?? string.Empty;
        pathForResolve = pathForResolve.Replace("\\", "/");

        // 已在 Import 区：夹名在导入时已是「三层名」，勿再对 Assets/Incoming/… 向上取三层
        // （否则会得到 Assets_Incoming_xxx）。
        string importFolderName;
        if (TryGetIncomingImportFolderName(pathForResolve, out importFolderName, importRoot))
        {
            return importFolderName;
        }

        // Assets/ 路径先换成磁盘路径，供 ResolveFolderName 取「向上三层」。
        if (pathForResolve.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            string full = AssetPathUtility.ToFullPath(pathForResolve);
            if (!string.IsNullOrEmpty(full))
            {
                pathForResolve = full;
            }
        }

        bool fallback;
        string warning;
        string folderName = BatchFbxImportService.ResolveFolderName(pathForResolve, out fallback, out warning);
        if (!string.IsNullOrEmpty(warning))
        {
            Debug.LogWarning("[TOol][Prefab] 命名：" + warning + " ← " + sourceModelAssetPath);
        }

        if (string.IsNullOrEmpty(folderName) || folderName == "unnamed_fbx")
        {
            string stem = Path.GetFileNameWithoutExtension(sourceModelAssetPath ?? string.Empty);
            folderName = BatchFbxImportService.SanitizeFolderName(
                string.IsNullOrEmpty(stem) ? "Unnamed" : stem);
        }

        return folderName;
    }

    /// <summary>
    /// Assets/{ImportRoot}/{夹名}/文件 → 返回夹名（导入时已按三层规则命名）。
    /// </summary>
    private static bool TryGetIncomingImportFolderName(
        string assetPath,
        out string folderName,
        string importRoot = null)
    {
        folderName = null;
        if (string.IsNullOrEmpty(assetPath) ||
            !assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string root = string.IsNullOrWhiteSpace(importRoot)
            ? BatchFbxImportSettings.Current.NormalizedImportRoot
            : importRoot.Replace("\\", "/").TrimEnd('/');
        string prefix = root.TrimEnd('/') + "/";
        if (!assetPath.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = assetPath.Substring(prefix.Length);
        int slash = rest.IndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        folderName = BatchFbxImportService.SanitizeFolderName(rest.Substring(0, slash));
        return !string.IsNullOrEmpty(folderName);
    }
}
