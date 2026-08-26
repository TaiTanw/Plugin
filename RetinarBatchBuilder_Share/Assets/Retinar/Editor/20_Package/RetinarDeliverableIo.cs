using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 20_Package — 交付目录 I/O（AB 拷贝、UnityPackage 写出）
//
// 直通打包与 BuildAbOnly 共用；支持自定义 DeliverableRoot / AssetBundleRoot。
// =====================================================================================

/// <summary>
/// 把已构建的 AB / 导出的 UnityPackage 落到
/// <c>Deliverables/&lt;资产名&gt;/02_unity</c> 与 <c>03_assetbundles</c>。
/// </summary>
public static class RetinarDeliverableIo
{
    public static string GetAssetDeliverableRoot(string assetName, string deliverableRoot = null)
    {
        string root = string.IsNullOrWhiteSpace(deliverableRoot)
            ? RetinarPaths.DeliverableRoot
            : deliverableRoot.Trim().Replace("\\", "/").TrimEnd('/');
        return Path.Combine(Directory.GetCurrentDirectory(), root, assetName);
    }

    public static string GetUnityPackageOutputPath(string assetName, string deliverableRoot = null)
    {
        string dir = Path.Combine(
            GetAssetDeliverableRoot(assetName, deliverableRoot),
            RetinarPaths.DeliverableUnityFolder);
        RetinarEditorUtil.EnsureDiskDirectory(dir);
        return Path.Combine(dir, assetName + ".unitypackage");
    }

    public static string GetAssetBundleDeliverableDir(
        string assetName,
        string platformFolder,
        string deliverableRoot = null)
    {
        string dir = Path.Combine(
            GetAssetDeliverableRoot(assetName, deliverableRoot),
            RetinarPaths.DeliverableAssetBundlesFolder,
            platformFolder);
        RetinarEditorUtil.EnsureDiskDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 从工程根 AB 构建目录拷贝到 Deliverables。
    /// </summary>
    public static void CopyBuiltBundleToDeliverables(
        string assetName,
        string bundleFileName,
        string platformFolder,
        string assetBundleRoot = null,
        string deliverableRoot = null)
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string abRoot = string.IsNullOrWhiteSpace(assetBundleRoot)
            ? RetinarPaths.AssetBundleRoot
            : assetBundleRoot.Trim().Replace("\\", "/").TrimEnd('/');
        string sourceDir = Path.Combine(projectRoot, abRoot, platformFolder);
        string bundleSource = Path.Combine(sourceDir, bundleFileName);
        string manifestSource = bundleSource + ".manifest";
        string targetDir = GetAssetBundleDeliverableDir(assetName, platformFolder, deliverableRoot);

        CopyFileIfExists(bundleSource, Path.Combine(targetDir, bundleFileName));
        CopyFileIfExists(manifestSource, Path.Combine(targetDir, bundleFileName + ".manifest"));
    }

    public static void CopyFileIfExists(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning("[Retinar] 交付拷贝：源文件不存在 " + sourcePath);
            return;
        }

        string targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            RetinarEditorUtil.EnsureDiskDirectory(targetDir);
        }

        File.Copy(sourcePath, targetPath, true);
    }

    /// <summary>写出 UnityPackage（调用方已算好 asset 路径列表）。</summary>
    public static void ExportUnityPackage(string[] assetPaths, string outputPath)
    {
        if (assetPaths == null || assetPaths.Length == 0)
        {
            Debug.LogError("[Retinar] ExportUnityPackage：资产列表为空 → " + outputPath);
            return;
        }

        AssetDatabase.ExportPackage(assetPaths, outputPath, ExportPackageOptions.Default);
    }
}
