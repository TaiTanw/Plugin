using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 40_Api — ⑥ AB 构建（Options：输出根 / 是否 UP；与直通共用）
// =====================================================================================

/// <summary>插件 1 · AB / 可选 UP 对外接口。</summary>
public static class RetinarAbApi
{
    private static readonly string[] ApprovedRuntimePrefixes =
    {
        "Assets/Retinar/Scripts/",
        "Assets/Retinar/XLua/",
        "Assets/Retinar/Plugins/",
        "Assets/RetinarRuntime/",
    };

    /// <summary>仅双端 AB（默认 Options）。</summary>
    public static RetinarAbBuildResult BuildAbOnly(IList<string> prefabPaths)
    {
        return Build(prefabPaths, RetinarAbBuildOptions.CreateDefaultAbOnly());
    }

    /// <summary>按 Options 打 AB，可选 UnityPackage；不改 Prefab、不跑门禁。</summary>
    public static RetinarAbBuildResult Build(IList<string> prefabPaths, RetinarAbBuildOptions options)
    {
        var result = new RetinarAbBuildResult();
        if (options == null)
        {
            options = RetinarAbBuildOptions.CreateDefaultAbOnly();
        }

        if (prefabPaths == null || prefabPaths.Count == 0)
        {
            result.FailLines.Add("Prefab 路径列表为空");
            return result;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            result.FailLines.Add("播放模式下不可打 AB");
            return result;
        }

        string deliverableRoot = options.NormalizedDeliverableRoot;
        string abRoot = options.NormalizedAssetBundleRoot;

        RetinarEditorUtil.EnsureDiskDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), deliverableRoot));
        RetinarEditorUtil.EnsureDiskDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), abRoot));

        for (int i = 0; i < prefabPaths.Count; i++)
        {
            string prefabPath = (prefabPaths[i] ?? string.Empty).Replace("\\", "/");
            if (string.IsNullOrEmpty(prefabPath))
            {
                continue;
            }

            string assetName = RetinarEditorUtil.MakeSafeName(
                Path.GetFileNameWithoutExtension(prefabPath));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                result.FailLines.Add(prefabPath + " — 无法加载 Prefab");
                continue;
            }

            string bundleFileName = RetinarEditorUtil.BuildBundleFileName(assetName);
            if (!BuildAndCopyAssetBundles(
                    prefabPath, assetName, bundleFileName, result.FailLines, options))
            {
                continue;
            }

            if (options.ExportUnityPackage)
            {
                List<string> dropped;
                if (!ExportUnityPackageForPrefab(
                        prefabPath, assetName, deliverableRoot, result.FailLines, out dropped))
                {
                    continue;
                }

                if (dropped.Count > 0)
                {
                    Debug.LogWarning("[Retinar][Ab] " + assetName +
                        "：UnityPackage 未收录 " + dropped.Count + " 条本包外依赖（不阻断）");
                }
            }

            result.OkNames.Add(assetName);
            result.BuiltBundleFiles.Add(bundleFileName);
            Debug.Log("[Retinar][Ab] 完成: " + assetName + " ← " + prefabPath +
                      (options.ExportUnityPackage ? " (+UP)" : " (AB only)"));
        }

        return result;
    }

    /// <summary>打双端 AB，并按 Options 拷到交付目录。</summary>
    public static bool BuildAndCopyAssetBundles(
        string prefabPath,
        string assetName,
        string bundleFileName,
        List<string> failLines,
        RetinarAbBuildOptions options = null)
    {
        if (failLines == null)
        {
            failLines = new List<string>();
        }

        if (options == null)
        {
            options = RetinarAbBuildOptions.CreateDefaultAbOnly();
        }

        string abRoot = options.NormalizedAssetBundleRoot;
        string deliverableRoot = options.NormalizedDeliverableRoot;

        var build = new AssetBundleBuild
        {
            assetBundleName = assetName.ToLowerInvariant(),
            assetBundleVariant = RetinarPaths.AssetBundleVariant,
            assetNames = new[] { prefabPath }
        };
        AssetBundleBuild[] builds = { build };

        BuildTarget[] targets = { BuildTarget.Android, BuildTarget.iOS };
        foreach (BuildTarget target in targets)
        {
            string platformFolder = RetinarEditorUtil.ToPlatformFolder(target);
            string outputPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                abRoot,
                platformFolder);
            RetinarEditorUtil.EnsureDiskDirectory(outputPath);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outputPath,
                builds,
                BuildAssetBundleOptions.ChunkBasedCompression,
                target);

            if (manifest == null)
            {
                failLines.Add(assetName + " — " + platformFolder + " BuildAssetBundles 返回 null");
                return false;
            }

            string builtPath = Path.Combine(outputPath, bundleFileName);
            if (!File.Exists(builtPath))
            {
                string alt = Path.Combine(outputPath, assetName.ToLowerInvariant());
                if (File.Exists(alt))
                {
                    File.Copy(alt, builtPath, true);
                    if (File.Exists(alt + ".manifest"))
                    {
                        File.Copy(alt + ".manifest", builtPath + ".manifest", true);
                    }
                }
            }

            if (!File.Exists(builtPath))
            {
                failLines.Add(assetName + " — 未找到 AB 文件: " + builtPath);
                return false;
            }

            if (options.CopyAbToDeliverables)
            {
                RetinarDeliverableIo.CopyBuiltBundleToDeliverables(
                    assetName, bundleFileName, platformFolder, abRoot, deliverableRoot);
            }
        }

        return true;
    }

    private static bool ExportUnityPackageForPrefab(
        string prefabPath,
        string assetName,
        string deliverableRoot,
        List<string> failLines,
        out List<string> dropped)
    {
        dropped = new List<string>();
        string[] packageAssets = CollectPackageAssetPaths(prefabPath, dropped);
        if (packageAssets.Length == 0)
        {
            failLines.Add(assetName + " — UnityPackage 依赖列表为空");
            return false;
        }

        string outputPath = RetinarDeliverableIo.GetUnityPackageOutputPath(assetName, deliverableRoot);
        try
        {
            RetinarDeliverableIo.ExportUnityPackage(packageAssets, outputPath);
        }
        catch (System.Exception ex)
        {
            failLines.Add(assetName + " — ExportPackage 异常: " + ex.Message);
            return false;
        }

        if (!File.Exists(outputPath))
        {
            failLines.Add(assetName + " — UnityPackage 未生成: " + outputPath);
            return false;
        }

        return true;
    }

    private static string[] CollectPackageAssetPaths(string prefabPath, List<string> dropped)
    {
        prefabPath = prefabPath.Replace("\\", "/");
        string artFolderPrefix = TryGetArtAssetFolderPrefix(prefabPath);

        List<string> deps = AssetDatabase.GetDependencies(prefabPath, true)
            .Select(p => p.Replace("\\", "/"))
            .Where(p => p.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrEmpty(artFolderPrefix))
        {
            return deps.ToArray();
        }

        var included = new List<string>();
        for (int i = 0; i < deps.Count; i++)
        {
            string path = deps[i];
            if (path.Equals(artFolderPrefix, System.StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(artFolderPrefix + "/", System.StringComparison.OrdinalIgnoreCase) ||
                IsApprovedRuntimeDependency(path))
            {
                included.Add(path);
                continue;
            }

            dropped.Add(path);
        }

        return included.ToArray();
    }

    private static bool IsApprovedRuntimeDependency(string assetPath)
    {
        for (int i = 0; i < ApprovedRuntimePrefixes.Length; i++)
        {
            if (assetPath.StartsWith(ApprovedRuntimePrefixes[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string TryGetArtAssetFolderPrefix(string assetPath)
    {
        string prefix = RetinarPaths.ArtRoot + "/";
        if (!assetPath.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string relative = assetPath.Substring(prefix.Length);
        int slash = relative.IndexOf('/');
        if (slash <= 0)
        {
            return null;
        }

        return RetinarPaths.ArtRoot + "/" + relative.Substring(0, slash);
    }
}

/// <summary>Build 结果。</summary>
public sealed class RetinarAbBuildResult
{
    public readonly List<string> OkNames = new List<string>();
    public readonly List<string> BuiltBundleFiles = new List<string>();
    public readonly List<string> FailLines = new List<string>();

    public bool Ok
    {
        get { return FailLines.Count == 0 && OkNames.Count > 0; }
    }

    public bool PartialOk
    {
        get { return OkNames.Count > 0; }
    }
}
