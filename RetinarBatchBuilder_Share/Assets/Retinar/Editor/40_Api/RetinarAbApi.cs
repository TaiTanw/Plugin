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

    /// <summary>Project 选中的 .prefab（不含文件夹）。</summary>
    public static List<string> CollectSelectedPrefabAssetPaths()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        UnityEngine.Object[] selected = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
        if (selected == null)
        {
            return paths;
        }

        for (int i = 0; i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]);
            if (string.IsNullOrEmpty(path) ||
                !path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string n = path.Replace("\\", "/");
            if (seen.Add(n))
            {
                paths.Add(n);
            }
        }

        return paths;
    }

    /// <summary>文件夹下全部 .prefab（含子夹）。不读管线 / 导出 SO。</summary>
    public static List<string> CollectPrefabAssetPathsUnderFolder(string folder)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            return paths;
        }

        string root = folder.Replace("\\", "/").TrimEnd('/');
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
        for (int g = 0; g < guids.Length; g++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[g]).Replace("\\", "/");
            if (string.IsNullOrEmpty(p) ||
                !p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seen.Add(p))
            {
                paths.Add(p);
            }
        }

        paths.Sort(System.StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    /// <summary>按导出 SO 打选中 Prefab（人工菜单）。不读管线步骤 SO。</summary>
    public static RetinarAbBuildResult BuildFromSelection()
    {
        List<string> paths = CollectSelectedPrefabAssetPaths();
        RetinarAbBuildOptions options = RetinarAbBuildOptions.FromExportSettings(
            RetinarExportSettings.Current,
            quietOverride: false);
        var result = new RetinarAbBuildResult();
        if (paths.Count == 0)
        {
            result.FailLines.Add("未选中 Project 内 .prefab");
            NotifyManual(result, options, "选中导出");
            return result;
        }

        result = Build(paths, options);
        NotifyManual(result, options, "选中导出");
        return result;
    }

    /// <summary>
    /// 弹框选 Assets 下文件夹，扫描其中全部 Prefab。输出总父夹名 = 预设体文件名。
    /// 不读管线步骤 SO。
    /// </summary>
    public static RetinarAbBuildResult BuildFromPickedFolder()
    {
        RetinarAbBuildOptions options = RetinarAbBuildOptions.FromExportSettings(
            RetinarExportSettings.Current,
            quietOverride: false);
        var result = new RetinarAbBuildResult();

        if (!EditorUtility.DisplayDialog(
            "批量导出文件夹预设体",
            "将选择工程 Assets 下的一个文件夹，并扫描其中全部 .prefab（含子夹）。\n" +
            "每个预设体的文件名（不含扩展名）作为输出总父夹名：Deliverables/<预设体名>/，" +
            "AB 文件名为 <预设体名>_android/_ios.assetbundle。",
            "选择文件夹",
            "取消"))
        {
            result.FailLines.Add("已取消");
            return result;
        }

        string folder;
        string pickError;
        if (!TryPickAssetsFolder(out folder, out pickError))
        {
            result.FailLines.Add(pickError);
            if (!string.Equals(pickError, "已取消", System.StringComparison.Ordinal))
            {
                NotifyManual(result, options, "文件夹批量导出");
            }

            return result;
        }

        List<string> paths = CollectPrefabAssetPathsUnderFolder(folder);
        options.ArtRoot = folder;

        if (paths.Count == 0)
        {
            result.FailLines.Add("文件夹下没有 .prefab: " + folder);
            NotifyManual(result, options, "文件夹批量导出");
            return result;
        }

        string dup = FindDuplicatePrefabStems(paths);
        if (dup != null)
        {
            result.FailLines.Add(dup);
            NotifyManual(result, options, "文件夹批量导出");
            return result;
        }

        string preview = folder + "\n共 " + paths.Count + " 个 Prefab。" +
                         "\n输出总父夹 = 预设体文件名（Deliverables/<名>/）。";
        int show = paths.Count < 8 ? paths.Count : 8;
        for (int i = 0; i < show; i++)
        {
            preview += "\n  " + Path.GetFileNameWithoutExtension(paths[i]);
        }

        if (paths.Count > show)
        {
            preview += "\n  …";
        }

        if (!options.Quiet &&
            !EditorUtility.DisplayDialog("确认批量导出", preview, "导出", "取消"))
        {
            result.FailLines.Add("已取消");
            return result;
        }

        result = Build(paths, options);
        NotifyManual(result, options, "文件夹批量导出");
        return result;
    }

    private static bool TryPickAssetsFolder(out string assetFolder, out string error)
    {
        assetFolder = null;
        error = null;
        string dataAbs = Path.GetFullPath(Application.dataPath)
            .TrimEnd(Path.DirectorySeparatorChar, '/');
        string start = dataAbs;
        string artAbs = Path.Combine(dataAbs, "Art");
        if (Directory.Exists(artAbs))
        {
            start = artAbs;
        }

        string picked = EditorUtility.OpenFolderPanel("选择要扫描的文件夹", start, "");
        if (string.IsNullOrEmpty(picked))
        {
            error = "已取消";
            return false;
        }

        string full = Path.GetFullPath(picked)
            .TrimEnd(Path.DirectorySeparatorChar, '/');
        if (string.Equals(full, dataAbs, System.StringComparison.OrdinalIgnoreCase))
        {
            assetFolder = "Assets";
            return true;
        }

        string prefix = dataAbs + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
        {
            error = "请选择本工程 Assets 下的文件夹: " + picked;
            return false;
        }

        string rel = full.Substring(dataAbs.Length).Replace("\\", "/").Trim('/');
        assetFolder = "Assets/" + rel;
        if (!AssetDatabase.IsValidFolder(assetFolder))
        {
            error = "不是工程内资产文件夹: " + assetFolder;
            return false;
        }

        return true;
    }

    private static string FindDuplicatePrefabStems(IList<string> paths)
    {
        var first = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < paths.Count; i++)
        {
            string stem = RetinarEditorUtil.MakeSafeName(
                Path.GetFileNameWithoutExtension(paths[i]));
            string existing;
            if (first.TryGetValue(stem, out existing))
            {
                return "预设体文件名重复，无法作为输出总父夹: " + stem +
                       "\n  " + existing + "\n  " + paths[i];
            }

            first.Add(stem, paths[i]);
        }

        return null;
    }

    private static void NotifyManual(
        RetinarAbBuildResult result,
        RetinarAbBuildOptions options,
        string logLabel)
    {
        string body = FormatManualSummary(result);
        Debug.Log("[Retinar][Ab] " + logLabel + "\n" + body);
        if (options != null && options.Quiet)
        {
            return;
        }

        EditorUtility.DisplayDialog(
            result != null && result.Ok ? "导出完成" : "导出未完成",
            body,
            "OK");
    }

    private static string FormatManualSummary(RetinarAbBuildResult result)
    {
        if (result == null)
        {
            return "无结果";
        }

        return "成功 " + result.OkNames.Count +
               " 失败 " + result.FailLines.Count +
               (result.FailLines.Count > 0
                   ? "\n" + string.Join("\n", result.FailLines.ToArray())
                   : string.Empty);
    }

    static string FormatExtraSuffix(RetinarAbBuildOptions options)
    {
        if (options == null)
        {
            return " (AB only)";
        }

        string suffix = options.ExportUnityPackage ? " +UP" : string.Empty;
        if (options.ExportSourceModels)
        {
            suffix += " +Model";
        }

        if (options.ExportAssetInfo)
        {
            suffix += " +Info";
        }

        return suffix.Length == 0 ? " (AB only)" : " (" + suffix.TrimStart() + ")";
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

            List<string> builtFiles;
            if (!BuildAndCopyAssetBundles(prefabPath, assetName, result.FailLines, options, out builtFiles))
            {
                continue;
            }

            if (builtFiles != null)
            {
                result.BuiltBundleFiles.AddRange(builtFiles);
            }

            RetinarOptionalDeliverables.WriteAfterAb(
                prefabPath, assetName, deliverableRoot, options);

            if (options.ExportUnityPackage)
            {
                List<string> dropped;
                if (!ExportUnityPackageForPrefab(
                        prefabPath, assetName, deliverableRoot, result.FailLines, out dropped, options))
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
            Debug.Log("[Retinar][Ab] 完成: " + assetName + " ← " + prefabPath +
                      FormatExtraSuffix(options));
        }

        return result;
    }

    /// <summary>打双端 AB，产物平铺在 AB 根与 03_assetbundles，文件名带 _android / _ios。</summary>
    public static bool BuildAndCopyAssetBundles(
        string prefabPath,
        string assetName,
        List<string> failLines,
        RetinarAbBuildOptions options,
        out List<string> builtFiles)
    {
        builtFiles = new List<string>();
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
        string projectRoot = Directory.GetCurrentDirectory();
        string productDir = Path.Combine(projectRoot, abRoot);
        RetinarEditorUtil.EnsureDiskDirectory(productDir);

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
            string suffix = RetinarEditorUtil.ToPlatformFileSuffix(target);
            string staging = Path.Combine(projectRoot, "Library", "RetinarAbBuild", suffix);
            RetinarEditorUtil.EnsureDiskDirectory(staging);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                staging,
                builds,
                BuildAssetBundleOptions.ChunkBasedCompression,
                target);

            if (manifest == null)
            {
                failLines.Add(assetName + " — " + suffix + " BuildAssetBundles 返回 null");
                return false;
            }

            string unityName = RetinarEditorUtil.BuildUnityBundleFileName(assetName);
            string builtPath = Path.Combine(staging, unityName);
            if (!File.Exists(builtPath))
            {
                string alt = Path.Combine(staging, assetName.ToLowerInvariant());
                if (File.Exists(alt))
                {
                    builtPath = alt;
                }
            }

            if (!File.Exists(builtPath))
            {
                failLines.Add(assetName + " — 未找到 AB 文件: " + Path.Combine(staging, unityName));
                return false;
            }

            string productName = RetinarEditorUtil.BuildBundleFileName(assetName, target);
            string productPath = Path.Combine(productDir, productName);
            File.Copy(builtPath, productPath, true);
            if (File.Exists(builtPath + ".manifest"))
            {
                File.Copy(builtPath + ".manifest", productPath + ".manifest", true);
            }

            builtFiles.Add(productName);

            if (options.CopyAbToDeliverables)
            {
                RetinarDeliverableIo.CopyBuiltBundleToDeliverables(
                    assetName, productName, productPath, deliverableRoot);
            }
        }

        return true;
    }

    private static bool ExportUnityPackageForPrefab(
        string prefabPath,
        string assetName,
        string deliverableRoot,
        List<string> failLines,
        out List<string> dropped,
        RetinarAbBuildOptions options)
    {
        dropped = new List<string>();
        string[] packageAssets = CollectPackageAssetPaths(
            prefabPath, dropped, options != null ? options.NormalizedArtRoot : RetinarPaths.ArtRoot);
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

    private static string[] CollectPackageAssetPaths(string prefabPath, List<string> dropped, string artRoot)
    {
        prefabPath = prefabPath.Replace("\\", "/");
        string artFolderPrefix = TryGetArtAssetFolderPrefix(prefabPath, artRoot);

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

    private static string TryGetArtAssetFolderPrefix(string assetPath, string artRoot)
    {
        string root = string.IsNullOrWhiteSpace(artRoot)
            ? RetinarPaths.ArtRoot
            : artRoot.Replace("\\", "/").TrimEnd('/');
        string prefix = root + "/";
        if (!assetPath.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string relative = assetPath.Substring(prefix.Length);
        int slash = relative.IndexOf('/');
        if (slash <= 0)
        {
            return root;
        }

        string first = relative.Substring(0, slash);
        if (string.Equals(first, "Prefab", System.StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return root + "/" + first;
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
