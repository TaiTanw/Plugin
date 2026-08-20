using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 20_Package — 成品直达：最净打包（仅 AB + UnityPackage）
//
// 不做：SafeZone / 碰撞体 / Extract / 动画改名 / EnsureStandardAssetFolders / 改 Prefab。
// 不写：00_runtime / 01_source / 06_docs。
// 产物：Deliverables/<名>/02_unity + 03_assetbundles/{Android,iOS}
// =====================================================================================

/// <summary>
/// 选中成品 Prefab 直接打 AB 与 UnityPackage，不触碰 Art 目录结构与 Prefab 内容。
/// </summary>
public static class RetinarDirectPackage
{
    /// <summary>菜单：成品直达 / 选中预制体直通打包。</summary>
    public static void PackageSelectedPrefabsDirect()
    {
        if (RetinarEditorUtil.StopIfEditorIsPlaying())
        {
            return;
        }

        List<string> prefabPaths = CollectSelectedPrefabPaths();
        if (prefabPaths.Count == 0)
        {
            RetinarEditorUtil.ShowDialogDeferred(
                "Retinar 成品直达",
                "请在 Project 中选中一个或多个 Prefab（可多选）。\n\n" +
                "本入口不会平铺、不会改 Art、不会加碰撞体。\n" +
                "未整理的外部 FBX/散落资源请走「批量汇总 > 平铺到 Art」。",
                "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Retinar 成品直达",
                "将对 " + prefabPaths.Count + " 个预制体做最净打包：\n" +
                "· 仅输出 AB（Android/iOS）与 UnityPackage\n" +
                "· 不修改 Prefab / 不在 Assets 下生成结构文件夹\n\n是否继续？",
                "打包",
                "取消"))
        {
            return;
        }

        RetinarEditorUtil.EnsureDiskDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), RetinarPaths.DeliverableRoot));
        RetinarEditorUtil.EnsureDiskDirectory(
            Path.Combine(Directory.GetCurrentDirectory(), RetinarPaths.AssetBundleRoot));

        var okNames = new List<string>();
        var failLines = new List<string>();

        try
        {
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string prefabPath = prefabPaths[i];
                string assetName = RetinarEditorUtil.MakeSafeName(
                    Path.GetFileNameWithoutExtension(prefabPath));
                EditorUtility.DisplayProgressBar(
                    "Retinar 成品直达",
                    "打包: " + prefabPath,
                    (float)i / prefabPaths.Count);

                if (PackageOnePrefab(prefabPath, assetName, failLines))
                {
                    okNames.Add(assetName);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string failText = failLines.Count == 0
            ? string.Empty
            : "\n\n失败 " + failLines.Count + " 项：\n" + string.Join("\n", failLines.ToArray());

        RetinarEditorUtil.ShowDialogDeferred(
            "Retinar 成品直达",
            "完成。成功 " + okNames.Count + " / " + prefabPaths.Count + "。\n\n" +
            "交付目录: " + RetinarEditorUtil.GetDeliverablesAbsolutePath() +
            "\n（仅 02_unity + 03_assetbundles）" +
            failText,
            "OK");
    }

    public static bool ValidatePackageSelectedPrefabsDirect()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool PackageOnePrefab(string prefabPath, string assetName, List<string> failLines)
    {
        prefabPath = prefabPath.Replace("\\", "/");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            failLines.Add(prefabPath + " — 无法加载 Prefab");
            return false;
        }

        string bundleFileName = RetinarEditorUtil.BuildBundleFileName(assetName);
        //打包AB并将工程文件拷贝到交付文件
        if (!BuildAndCopyAssetBundles(prefabPath, assetName, bundleFileName, failLines))
        {
            return false;
        }
        //打包Pack
        if (!ExportUnityPackageForPrefab(prefabPath, assetName, failLines))
        {
            return false;
        }

        Debug.Log("[Retinar] 成品直达完成: " + assetName + " ← " + prefabPath);
        return true;
    }

    /// <summary>
    /// 用 AssetBundleBuild[] 显式指定单个 Prefab，避免依赖/污染 Importer 上的 bundle 名，
    /// 也避免工程内同名 bundle 把其它 Art 目录打进同一包。
    /// </summary>
    private static bool BuildAndCopyAssetBundles(
        string prefabPath,
        string assetName,
        string bundleFileName,
        List<string> failLines)
    {
        var build = new AssetBundleBuild//构建包结构信息
        {
            assetBundleName = assetName.ToLowerInvariant(),
            assetBundleVariant = RetinarPaths.AssetBundleVariant,
            assetNames = new[] { prefabPath }
        };
        AssetBundleBuild[] builds = { build };

        BuildTarget[] targets = { BuildTarget.Android, BuildTarget.iOS };//构建包平台信息
        foreach (BuildTarget target in targets)
        {
            string platformFolder = RetinarEditorUtil.ToPlatformFolder(target);
            string outputPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                RetinarPaths.AssetBundleRoot,
                platformFolder);
            RetinarEditorUtil.EnsureDiskDirectory(outputPath);


            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(//========正式打包，并返回打包结果信息（资源包清单)
                outputPath,
                builds,
                BuildAssetBundleOptions.None,
                target);

            if (manifest == null)
            {
                failLines.Add(assetName + " — " + platformFolder + " BuildAssetBundles 返回 null");
                return false;
            }

            string builtPath = Path.Combine(outputPath, bundleFileName);
            if (!File.Exists(builtPath))
            {
                // 部分 Unity 版本输出名为 assetBundleName 无 variant 拼接差异，再试一次无 variant 名
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

    private static bool ExportUnityPackageForPrefab(
        string prefabPath,
        string assetName,
        List<string> failLines)
    {
        string[] packageAssets = CollectPackageAssetPaths(prefabPath);
        if (packageAssets.Length == 0)
        {
            failLines.Add(assetName + " — UnityPackage 依赖列表为空");
            return false;
        }

        string outputPath = RetinarDeliverableIo.GetUnityPackageOutputPath(assetName);
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

    /// <summary>
    /// Prefab 在 Art/&lt;名&gt;/ 下时只收该资产夹（与历史 ExportUnityPackages 一致）；
    /// 否则收 Assets/ 下全部依赖（排除 Packages/）。
    /// </summary>
    private static string[] CollectPackageAssetPaths(string prefabPath)
    {
        prefabPath = prefabPath.Replace("\\", "/");
        string artFolderPrefix = TryGetArtAssetFolderPrefix(prefabPath);

        IEnumerable<string> deps = AssetDatabase.GetDependencies(prefabPath, true)
            .Select(p => p.Replace("\\", "/"));

        if (!string.IsNullOrEmpty(artFolderPrefix))
        {
            return deps
                .Where(p =>
                    p.Equals(artFolderPrefix, System.StringComparison.OrdinalIgnoreCase) ||
                    p.StartsWith(artFolderPrefix + "/", System.StringComparison.OrdinalIgnoreCase))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return deps
            .Where(p => p.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) &&
                        !p.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase))
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>若路径为 Assets/Art/&lt;名&gt;/... 则返回 Assets/Art/&lt;名&gt;。</summary>
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
    /// <summary>
    /// 得到当前选中物体并解析路径(确认是预设体）
    /// </summary>
    /// <returns></returns>
    private static List<string> CollectSelectedPrefabPaths()
    {
        var list = new List<string>();
        Object[] objects = Selection.objects;
        if (objects == null)
        {
            return list;
        }

        foreach (Object obj in objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            path = path.Replace("\\", "/");
            if (Path.GetExtension(path).ToLowerInvariant() != ".prefab")
            {
                continue;
            }

            if (!list.Contains(path))
            {
                list.Add(path);
            }
        }

        return list;
    }
}
