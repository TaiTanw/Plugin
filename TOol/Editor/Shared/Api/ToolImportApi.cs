using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Shared / Api — 1 入库窄口（单文件优先；批量仍走 BatchFbxImportService）
// =====================================================================================

/// <summary>插件 2 · 导入对外接口。</summary>
public static class ToolImportApi
{
    private static readonly string[] SupportedModelExtensions =
    {
        ".fbx", ".glb", ".gltf", ".obj"
    };

    /// <summary>1 入库识别表。批量筛选只能是子集；CLI / 编排单文件认全表。</summary>
    public static string[] GetSupportedModelExtensions()
    {
        var copy = new string[SupportedModelExtensions.Length];
        for (int i = 0; i < SupportedModelExtensions.Length; i++)
        {
            copy[i] = SupportedModelExtensions[i];
        }

        return copy;
    }

    /// <summary>展示用：<c>.fbx / .glb / .gltf / .obj</c>。</summary>
    public static string FormatSupportedExtensionsDisplay()
    {
        return string.Join(" / ", SupportedModelExtensions);
    }

    /// <summary>
    /// 单文件导入：工程外则先清本趟 Incoming 单元夹再拷入并 ImportAsset；已在 Assets 则原样返回（不清夹）。
    /// Incoming 夹名：<paramref name="incomingFolderName"/> 非空则用它（ID2）；否则向上三层。
    /// </summary>
    public static bool ImportSingleModel(string sourcePath, out string assetModelPath, out string message)
    {
        return ImportSingleModel(sourcePath, null, out assetModelPath, out message);
    }

    /// <inheritdoc cref="ImportSingleModel(string,out string,out string)"/>
    public static bool ImportSingleModel(
        string sourcePath,
        string incomingFolderName,
        out string assetModelPath,
        out string message)
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
            message = "不支持的扩展名（需 " + FormatSupportedExtensionsDisplay() + "）: " + ext;
            return false;
        }

        if (string.Equals(ext, ".gltf", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log(
                "[1 入库] 源是 .gltf。将连同旁路 .bin/贴图整包入库；④ 有外 URI 时走原子搬迁。" +
                "不必先转 GLB（D22 封装仍可选，编辑器不做 DCC 重导）。");
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
        string folderName = ResolveIncomingFolderName(fullDisk, incomingFolderName, out fallback, out warning);
        string targetFolder = settings.NormalizedImportRoot + "/" + folderName;
        string fileName = Path.GetFileName(fullDisk);
        string targetAsset = targetFolder + "/" + fileName;

        if (settings.IsDeliveryAlertPath(targetFolder) ||
            settings.IsDeliveryAlertPath(targetFolder + "/"))
        {
            message = "目标落在交付区警报路径: " + targetFolder;
            return false;
        }

        // 管线单文件：只清本趟 Incoming/<单元夹>/，再拷。不扫整棵 Incoming。源已在 Assets 时上面已 return。
        if (!AssetUnitFolder.TryDeleteImmediateChildFolder(settings.NormalizedImportRoot, targetFolder))
        {
            message = "无法清空导入单元夹: " + targetFolder;
            return false;
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
            // .gltf：下面 CopyGltfSidecarsBeside 会跟拷相对 URI 伴生。禁止用 GLTFSceneExporter 当入库。
            File.Copy(fullDisk, destFull, false);
            CopyGltfSidecarsBeside(fullDisk, destFull);
            CopyObjSidecarsBeside(fullDisk, destFull);
            CopyFbxSidecarsBeside(fullDisk, destFull);
        }
        catch (Exception ex)
        {
            message = "拷贝失败: " + ex.Message;
            return false;
        }

        AssetDatabase.Refresh();
        assetModelPath = targetAsset;
        // Refresh 已导入成功则不要再 ForceUpdate：OBJ 会把「无法线」警告再打一遍（每 Mesh 一条）。
        if (AssetDatabase.LoadMainAssetAtPath(targetAsset) == null)
        {
            AssetDatabase.ImportAsset(targetAsset, ImportAssetOptions.ForceUpdate);
        }

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

    /// <summary>编排/批量收集：路径后缀是否在 1 入库白名单。</summary>
    public static bool IsSupportedModelPath(string path)
    {
        return IsSupportedExtension(Path.GetExtension(path ?? string.Empty));
    }

    static string ResolveIncomingFolderName(
        string fullDisk,
        string incomingFolderName,
        out bool fallback,
        out string warning)
    {
        if (!string.IsNullOrWhiteSpace(incomingFolderName))
        {
            fallback = false;
            warning = null;
            return BatchFbxImportService.SanitizeFolderName(incomingFolderName.Trim());
        }

        return BatchFbxImportService.ResolveFolderName(fullDisk, out fallback, out warning);
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

    /// <summary>
    /// .gltf 入库时把相对 URI 伴生拷到同一导入夹（保持相对路径），再让 Import 能找到 .bin/图。
    /// 批量「执行导入」拷主文件后也可调。
    /// </summary>
    public static void CopyGltfSidecarsBeside(string sourceGltfFull, string destGltfFull)
    {
        if (string.IsNullOrEmpty(sourceGltfFull) ||
            !sourceGltfFull.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GltfExternalScan scan = GltfPackageFiles.Scan(sourceGltfFull);
        string destRoot = Path.GetDirectoryName(destGltfFull);
        if (string.IsNullOrEmpty(destRoot))
        {
            return;
        }

        for (int i = 0; i < scan.SidecarFullPaths.Count; i++)
        {
            string srcFull = scan.SidecarFullPaths[i];
            string rel = GltfPackageFiles.MakeRelativeToGltfDir(sourceGltfFull, srcFull);
            string destFull = Path.GetFullPath(Path.Combine(destRoot, rel)).Replace("\\", "/");
            string destDir = Path.GetDirectoryName(destFull);
            try
            {
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (!File.Exists(destFull))
                {
                    File.Copy(srcFull, destFull, false);
                }

                string destAsset = FullPathUnderAssets(destFull);
                if (!string.IsNullOrEmpty(destAsset))
                {
                    AssetDatabase.ImportAsset(destAsset, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[1 入库] 伴生拷贝失败: " + srcFull + " → " + destFull + " " + ex.Message);
            }
        }

        if (scan.MissingUris.Count > 0)
        {
            Debug.LogWarning("[1 入库] gltf 缺伴生 × " + scan.MissingUris.Count +
                             "（ctx 会记 MissingUris + Warnings，④将失败）");
        }
    }

    /// <summary>
    /// .obj 入库时跟拷 mtllib 与 mtl 里的贴图。缺文件只 Warning，仍导入网格。
    /// </summary>
    public static void CopyObjSidecarsBeside(string sourceObjFull, string destObjFull)
    {
        if (string.IsNullOrEmpty(sourceObjFull) ||
            !sourceObjFull.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ObjExternalScan scan = ObjPackageFiles.Scan(sourceObjFull);
        string destRoot = Path.GetDirectoryName(destObjFull);
        if (string.IsNullOrEmpty(destRoot))
        {
            return;
        }

        for (int i = 0; i < scan.SidecarFullPaths.Count; i++)
        {
            string srcFull = scan.SidecarFullPaths[i];
            string rel = ObjPackageFiles.MakeRelativeToObjDir(sourceObjFull, srcFull);
            string destFull = Path.GetFullPath(Path.Combine(destRoot, rel)).Replace("\\", "/");
            string destDir = Path.GetDirectoryName(destFull);
            try
            {
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (!File.Exists(destFull))
                {
                    File.Copy(srcFull, destFull, false);
                }

                string destAsset = FullPathUnderAssets(destFull);
                if (!string.IsNullOrEmpty(destAsset))
                {
                    AssetDatabase.ImportAsset(destAsset, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[1 入库] obj 伴生拷贝失败: " + srcFull + " → " + destFull + " " + ex.Message);
            }
        }

        if (scan.SidecarFullPaths.Count > 0)
        {
            Debug.Log("[1 入库] obj 已跟拷伴生 × " + scan.SidecarFullPaths.Count);
        }

        if (scan.MissingUris.Count > 0)
        {
            Debug.LogWarning("[1 入库] obj 缺伴生 × " + scan.MissingUris.Count +
                             "（源目录没有 .mtl/贴图则 Unity 会白膜）");
        }
    }

    /// <summary>
    /// .fbx 入库时跟拷同目录/Texture/.fbm 及 FBX 写出的相对贴图。缺文件只 Warning，仍导入网格。
    /// 不 Extract、不改 Importer。内嵌贴图仍等④。
    /// </summary>
    public static void CopyFbxSidecarsBeside(string sourceFbxFull, string destFbxFull)
    {
        if (string.IsNullOrEmpty(sourceFbxFull) ||
            !sourceFbxFull.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FbxExternalScan scan = FbxPackageFiles.Scan(sourceFbxFull);
        string destRoot = Path.GetDirectoryName(destFbxFull);
        if (string.IsNullOrEmpty(destRoot))
        {
            return;
        }

        for (int i = 0; i < scan.SidecarFullPaths.Count; i++)
        {
            string srcFull = scan.SidecarFullPaths[i];
            string rel = FbxPackageFiles.MakeRelativeToFbxDir(sourceFbxFull, srcFull);
            string destFull = Path.GetFullPath(Path.Combine(destRoot, rel)).Replace("\\", "/");
            string destDir = Path.GetDirectoryName(destFull);
            try
            {
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                if (!File.Exists(destFull))
                {
                    File.Copy(srcFull, destFull, false);
                }

                string destAsset = FullPathUnderAssets(destFull);
                if (!string.IsNullOrEmpty(destAsset))
                {
                    AssetDatabase.ImportAsset(destAsset, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[1 入库] fbx 伴生拷贝失败: " + srcFull + " → " + destFull + " " + ex.Message);
            }
        }

        if (scan.SidecarFullPaths.Count > 0)
        {
            Debug.Log("[1 入库] fbx 已跟拷伴生 × " + scan.SidecarFullPaths.Count);
        }

        if (scan.MissingUris.Count > 0)
        {
            Debug.LogWarning("[1 入库] fbx 缺伴生 × " + scan.MissingUris.Count +
                             "（源旁没有独立贴图则 Unity 会白膜；内嵌图仍等④ Extract）");
        }
    }

    static string FullPathUnderAssets(string fullPath)
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
