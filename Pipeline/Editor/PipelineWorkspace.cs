using System.Collections.Generic;

// =====================================================================================
// 编排工作区三根：Incoming / IncomingPrefab / Art。只服务管线步骤。
// 内核不得 Load 本类；由 PipelineOptions 传入。批量选择器 / 人工平铺 / 资源面板自管。
// =====================================================================================
public static class PipelineWorkspace
{
    public const string DefaultImportRoot = "Assets/Incoming";
    public const string DefaultPrefabRoot = "Assets/IncomingPrefab";
    public const string DefaultArtRoot = "Assets/Art";

    public static string Normalize(string path, string fallback)
    {
        string p = string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
        return p.Replace("\\", "/").TrimEnd('/');
    }

    public static void ApplyDefaults(PipelineOptions options)
    {
        if (options == null)
        {
            return;
        }

        options.ImportRoot = Normalize(options.ImportRoot, DefaultImportRoot);
        options.PrefabRoot = Normalize(options.PrefabRoot, DefaultPrefabRoot);
        options.ArtRoot = Normalize(options.ArtRoot, DefaultArtRoot);
    }

    public static bool TryValidate(PipelineOptions options, out string error)
    {
        error = null;
        if (options == null)
        {
            error = "PipelineOptions 为 null";
            return false;
        }

        ApplyDefaults(options);
        if (!IsAssetsRoot(options.ImportRoot, "导入根 Incoming", out error) ||
            !IsAssetsRoot(options.PrefabRoot, "Prefab 根 IncomingPrefab", out error) ||
            !IsAssetsRoot(options.ArtRoot, "交付根 Art", out error))
        {
            return false;
        }

        if (IsUnder(options.ImportRoot, options.ArtRoot))
        {
            error = "导入根不能落在交付根下: " + options.ImportRoot + " ⊂ " + options.ArtRoot;
            return false;
        }

        if (IsUnder(options.PrefabRoot, options.ArtRoot))
        {
            error = "Prefab 根不能落在交付根下: " + options.PrefabRoot + " ⊂ " + options.ArtRoot;
            return false;
        }

        return true;
    }

    public static bool IsUnder(string path, string root)
    {
        return ResourceExcludeUtility.IsUnderRoot(path, root);
    }

    /// <summary>从交付 Prefab 路径切出 Art/&lt;单元&gt;，供⑤扫描。</summary>
    public static List<string> CollectUnitFolders(IList<string> assetPaths, string root)
    {
        var folders = new List<string>();
        if (assetPaths == null)
        {
            return folders;
        }

        string art = Normalize(root, DefaultArtRoot);
        string prefix = art + "/";
        for (int i = 0; i < assetPaths.Count; i++)
        {
            string path = (assetPaths[i] ?? string.Empty).Replace("\\", "/");
            if (!path.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relative = path.Substring(prefix.Length);
            int slash = relative.IndexOf('/');
            if (slash <= 0)
            {
                continue;
            }

            string unit = art + "/" + relative.Substring(0, slash);
            if (!folders.Contains(unit))
            {
                folders.Add(unit);
            }
        }

        return folders;
    }

    private static bool IsAssetsRoot(string path, string label, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(path) ||
            !path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            error = label + "必须是 Assets/ 下路径，当前: " + path;
            return false;
        }

        return true;
    }
}
