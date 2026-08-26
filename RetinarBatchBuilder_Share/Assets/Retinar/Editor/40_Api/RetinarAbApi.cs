using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 40_Api — ⑥ BuildAbOnly（仅双端 AB；D1 最小口径）
//
// 已确认本阶段：Android + iOS AB；quiet；任意 Prefab 路径；不打 UnityPackage；无确认框。
// 压缩：ChunkBasedCompression（LZ4）。命名/main 契约仍见 d1-ab-only。
// =====================================================================================

/// <summary>插件 1 · AB 构建对外接口。</summary>
public static class RetinarAbApi
{
    /// <summary>
    /// 仅打 Android/iOS AssetBundle，并拷到 Deliverables/.../03_assetbundles。
    /// 不导出 UnityPackage、不改 Prefab、不弹确认框。
    /// </summary>
    public static RetinarAbBuildResult BuildAbOnly(IList<string> prefabPaths)
    {
        var result = new RetinarAbBuildResult();
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

        RetinarEditorUtil.EnsureDiskDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), RetinarPaths.DeliverableRoot));
        RetinarEditorUtil.EnsureDiskDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), RetinarPaths.AssetBundleRoot));

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
            if (BuildAndCopyAssetBundles(prefabPath, assetName, bundleFileName, result.FailLines))
            {
                result.OkNames.Add(assetName);
                result.BuiltBundleFiles.Add(bundleFileName);
                Debug.Log("[Retinar][AbOnly] 完成: " + assetName + " ← " + prefabPath);
            }
        }

        return result;
    }

    /// <summary>
    /// 用 AssetBundleBuild[] 显式指定单个 Prefab 打双端 AB，并拷到 Deliverables。
    /// </summary>
    public static bool BuildAndCopyAssetBundles(
        string prefabPath,
        string assetName,
        string bundleFileName,
        List<string> failLines)
    {
        if (failLines == null)
        {
            failLines = new List<string>();
        }

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
                RetinarPaths.AssetBundleRoot,
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

            RetinarDeliverableIo.CopyBuiltBundleToDeliverables(assetName, bundleFileName, platformFolder);
        }

        return true;
    }
}

/// <summary>BuildAbOnly 结果。</summary>
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
