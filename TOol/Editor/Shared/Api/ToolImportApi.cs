using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Shared / Api — ② 导入窄口（单文件优先；批量仍走 BatchFbxImportService）
// =====================================================================================

/// <summary>插件 2 · 导入对外接口。</summary>
public static class ToolImportApi
{
    private static readonly string[] SupportedModelExtensions =
    {
        ".fbx", ".glb", ".gltf", ".obj"
    };

    /// <summary>
    /// 单文件导入：工程外则拷入 Import 区并 ImportAsset；已在 Assets 则原样返回。
    /// 设置自动依赖 Unity 导入回调，本方法不另调设置逻辑。
    /// </summary>
    /// <param name="sourcePath">磁盘绝对路径或 Assets/…</param>
    /// <param name="assetModelPath">成功时的工程内模型路径</param>
    /// <param name="message">说明字符串</param>
    /// <returns>是否得到可用的工程内模型路径</returns>
    public static bool ImportSingleModel(string sourcePath, out string assetModelPath, out string message)
    {
        assetModelPath = null;
        message = null;

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            message = "源路径为空";
            return false;
        }

        string normalized = sourcePath.Replace("\\", "/").Trim();
        if (IsSupportedModelPath(normalized) == false &&
            IsSupportedModelPath(normalized.ToLowerInvariant()) == false)
        {
            // still allow if extension ok case-insensitive
        }

        string ext = Path.GetExtension(normalized);
        if (!IsSupportedExtension(ext))
        {
            message = "不支持的扩展名（需 .fbx/.glb/.gltf/.obj）: " + ext;
            return false;
        }

        if (TryAsExistingAssetPath(normalized, out assetModelPath))
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetModelPath) == null)
            {
                message = "Assets 路径无法加载: " + assetModelPath;
                assetModelPath = null;
                return false;
            }

            message = "已在工程内，跳过拷贝: " + assetModelPath;
            return true;
        }

        string fullDisk = Path.GetFullPath(normalized).Replace("\\", "/");
        if (!File.Exists(fullDisk))
        {
            message = "源文件不存在: " + fullDisk;
            return false;
        }

        BatchFbxImportSettings settings = BatchFbxImportSettings.GetOrCreateAsset();
        if (!settings.TryValidateImportRoot(out string rootError))
        {
            message = rootError;
            return false;
        }

        bool fallback;
        string warning;
        string folderName = BatchFbxImportService.ResolveFolderName(fullDisk, out fallback, out warning);
        string targetFolder = settings.NormalizedImportRoot + "/" + folderName;
        string fileName = Path.GetFileName(fullDisk);
        string targetAsset = targetFolder + "/" + fileName;

        if (settings.IsDeliveryAlertPath(targetFolder) ||
            settings.IsDeliveryAlertPath(targetFolder + "/"))
        {
            message = "目标落在交付区警报路径: " + targetFolder;
            return false;
        }

        // 已存在则复用（单文件编排，不强制 Conflict 失败）
        if (AssetDatabase.LoadMainAssetAtPath(targetAsset) != null ||
            File.Exists(AssetPathUtility.ToFullPath(targetAsset)))
        {
            assetModelPath = targetAsset;
            message = "目标已存在，复用: " + targetAsset +
                      (string.IsNullOrEmpty(warning) ? string.Empty : "（" + warning + "）");
            if (AssetDatabase.LoadMainAssetAtPath(targetAsset) == null)
            {
                AssetDatabase.ImportAsset(targetAsset, ImportAssetOptions.ForceUpdate);
            }

            return AssetDatabase.LoadMainAssetAtPath(targetAsset) != null;
        }

        EnsureAssetFolder(settings.NormalizedImportRoot);
        EnsureAssetFolder(targetFolder);

        string destFull = AssetPathUtility.ToFullPath(targetAsset);
        if (string.IsNullOrEmpty(destFull))
        {
            message = "无法解析目标磁盘路径: " + targetAsset;
            return false;
        }

        string destDir = Path.GetDirectoryName(destFull);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        try
        {
            File.Copy(fullDisk, destFull, false);
        }
        catch (Exception ex)
        {
            message = "拷贝失败: " + ex.Message;
            return false;
        }

        AssetDatabase.ImportAsset(targetAsset, ImportAssetOptions.ForceUpdate);
        assetModelPath = targetAsset;

        if (AssetDatabase.LoadMainAssetAtPath(targetAsset) == null)
        {
            message = "ImportAsset 后无法加载（GLB 需宿主已装 UnityGLTF）: " + targetAsset;
            assetModelPath = null;
            return false;
        }

        message = "已导入: " + targetAsset +
                  (string.IsNullOrEmpty(warning) ? string.Empty : "（" + warning + "）") +
                  (fallback ? " [夹名回退]" : string.Empty);
        return true;
    }

    /// <summary>按已收集条目执行批量（人工批量面板）。</summary>
    public static BatchFbxImportService.BatchResult ExecuteBatch(
        System.Collections.Generic.IList<BatchFbxImportService.ImportItem> items,
        BatchFbxImportSettings settings = null)
    {
        if (settings == null)
        {
            settings = BatchFbxImportSettings.GetOrCreateAsset();
        }

        return BatchFbxImportService.ExecuteBatch(items, settings);
    }

    public static bool IsSupportedExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext))
        {
            return false;
        }

        ext = ext.ToLowerInvariant();
        for (int i = 0; i < SupportedModelExtensions.Length; i++)
        {
            if (ext == SupportedModelExtensions[i])
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupportedModelPath(string path)
    {
        return IsSupportedExtension(Path.GetExtension(path ?? string.Empty));
    }

    private static bool TryAsExistingAssetPath(string path, out string assetPath)
    {
        assetPath = null;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string p = path.Replace("\\", "/");
        if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            assetPath = p;
            return true;
        }

        string dataPath = Application.dataPath.Replace("\\", "/");
        string full = path;
        try
        {
            full = Path.GetFullPath(path).Replace("\\", "/");
        }
        catch
        {
            return false;
        }

        if (full.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase) ||
            full.Equals(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            assetPath = "Assets" + full.Substring(dataPath.Length);
            return true;
        }

        return false;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Replace("\\", "/").Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            return;
        }

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
