using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 20_Package — 成品直达：最净打包（仅 AB + UnityPackage）
//
// 不做：SafeZone / 碰撞体 / Extract / 动画改名 / EnsureStandardAssetFolders / 改 Prefab。
// 不写：00_runtime / 01_source / 06_docs。
// 产物：Deliverables/<名>/02_unity + 03_assetbundles/{Android,iOS}
// 后续会撤掉直通，改为面板设置输出文件夹后统一走规范化。本期只报告未收录依赖。
// =====================================================================================

/// <summary>
/// 选中成品 Prefab 直接打 AB 与 UnityPackage，不触碰 Art 目录结构与 Prefab 内容。
/// </summary>
public static class RetinarDirectPackage
{
    private static readonly string[] ApprovedRuntimePrefixes =
    {
        "Assets/Retinar/Scripts/",
        "Assets/Retinar/XLua/",
        "Assets/Retinar/Plugins/",
        "Assets/RetinarRuntime/",
    };

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
                "· 不修改 Prefab / 不在 Assets 下生成结构文件夹\n" +
                "· 本包外依赖会写入报告，但不阻断（后续改为规范化）\n\n是否继续？",
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
        var droppedSections = new List<string>();

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

                List<string> dropped;
                if (PackageOnePrefab(prefabPath, assetName, failLines, out dropped))
                {
                    okNames.Add(assetName);
                    if (dropped.Count > 0)
                    {
                        droppedSections.Add(assetName + "（" + dropped.Count + "）\n  " +
                            string.Join("\n  ", dropped.ToArray()));
                    }
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

        string droppedText = string.Empty;
        if (droppedSections.Count > 0)
        {
            string droppedReportPath = WriteDroppedDependencyReport(droppedSections);
            droppedText = "\n\nUnityPackage 未收录本包外依赖 " + droppedSections.Count +
                " 个资产（不阻断；导入其它工程后动画/材质可能 Missing）：\n" +
                PreviewLines(string.Join("\n", droppedSections.ToArray()), 8);
            if (!string.IsNullOrEmpty(droppedReportPath))
            {
                droppedText += "\n完整报告: " + droppedReportPath;
            }
        }

        RetinarEditorUtil.ShowDialogDeferred(
            "Retinar 成品直达",
            "完成。成功 " + okNames.Count + " / " + prefabPaths.Count + "。\n\n" +
            "交付目录: " + RetinarEditorUtil.GetDeliverablesAbsolutePath() +
            "\n（仅 02_unity + 03_assetbundles）" +
            failText +
            droppedText,
            "OK");
    }

    public static bool ValidatePackageSelectedPrefabsDirect()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool PackageOnePrefab(
        string prefabPath,
        string assetName,
        List<string> failLines,
        out List<string> dropped)
    {
        dropped = new List<string>();
        prefabPath = prefabPath.Replace("\\", "/");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            failLines.Add(prefabPath + " — 无法加载 Prefab");
            return false;
        }

        string bundleFileName = RetinarEditorUtil.BuildBundleFileName(assetName);
        if (!RetinarAbApi.BuildAndCopyAssetBundles(prefabPath, assetName, bundleFileName, failLines))
        {
            return false;
        }

        if (!ExportUnityPackageForPrefab(prefabPath, assetName, failLines, dropped))
        {
            return false;
        }

        if (dropped.Count > 0)
        {
            Debug.LogWarning("[Retinar] 成品直达 " + assetName +
                "：UnityPackage 未收录 " + dropped.Count + " 条本包外依赖（不阻断）：\n" +
                string.Join("\n", dropped.ToArray()));
        }
        else
        {
            Debug.Log("[Retinar] 成品直达完成: " + assetName + " ← " + prefabPath);
        }

        return true;
    }

    // BuildAndCopyAssetBundles 已迁至 RetinarAbApi（编排 BuildAbOnly 与直通共用）

    private static bool ExportUnityPackageForPrefab(
        string prefabPath,
        string assetName,
        List<string> failLines,
        List<string> dropped)
    {
        string[] packageAssets = CollectPackageAssetPaths(prefabPath, dropped);
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
    /// Prefab 在 Art/&lt;名&gt;/ 下时只收该资产夹；本包外依赖写入 dropped，不打进 UnityPackage。
    /// 否则收 Assets/ 下全部依赖（排除 Packages/）。
    /// </summary>
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

    private static string WriteDroppedDependencyReport(List<string> sections)
    {
        string outputDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            RetinarPaths.DeliverableRoot,
            "_diagnostics");
        RetinarEditorUtil.EnsureDiskDirectory(outputDir);
        string reportPath = Path.Combine(outputDir, "direct_package_dropped_deps.txt");
        var lines = new List<string>
        {
            "Retinar 成品直达 — UnityPackage 未收录的本包外依赖",
            "Generated: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "",
            "说明：直通只把 Art/<名>/ 打进 UnityPackage，下列路径被丢掉。本次不阻断。",
            "导入其它工程后，动画换材质等引用会变成 Missing。后续将撤掉直通，改为面板设置输出后统一走规范化。",
            "处理建议：先「平铺到 Art」把依赖收进本包，再导出。",
            ""
        };
        lines.AddRange(sections);
        File.WriteAllLines(reportPath, lines.ToArray(), new UTF8Encoding(false));
        return Path.GetFullPath(reportPath);
    }

    private static string PreviewLines(string text, int maxLines)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length <= maxLines)
        {
            return text;
        }

        var preview = new List<string>();
        for (int i = 0; i < maxLines; i++)
        {
            preview.Add(lines[i]);
        }

        preview.Add("…另有 " + (lines.Length - maxLines) + " 行，见完整报告");
        return string.Join("\n", preview.ToArray());
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
