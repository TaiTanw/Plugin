using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// =====================================================================================
// Shared / UnityPackage — Assets 前预处理。不解 ImportPackage，不读管线 SO。
// =====================================================================================

/// <summary>解到信封目录的结果（磁盘路径，尚未 Refresh）。</summary>
public sealed class UnityPackageExtractResult
{
    public bool Ok;
    public string Error;
    public int WrittenFiles;
    public int DroppedDangerous;
    public int DroppedScenes;
    public int DroppedOther;
    public readonly List<string> WrittenRelativePaths = new List<string>();
}

/// <summary>
/// 把 .unitypackage 解到指定磁盘根：去掉 Assets/ 前缀，删除脚本/dll 等危险项。
/// destRoot 由调用方决定（管线导入根/ID2）；本类不判断人工根。
/// </summary>
public static class UnityPackagePreprocess
{
    static readonly string[] DangerousExtensions =
    {
        ".cs", ".dll", ".asmdef", ".js", ".so", ".bundle", ".dylib", ".winmd", ".exe"
    };

    public static bool IsUnityPackagePath(string path)
    {
        return string.Equals(
            Path.GetExtension(path ?? string.Empty),
            ".unitypackage",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解到 destRoot。夹不存在则创建。不调 AssetDatabase。
    /// </summary>
    public static bool TryExtractToFolder(
        string packagePath,
        string destRoot,
        out UnityPackageExtractResult result)
    {
        result = new UnityPackageExtractResult();
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            result.Error = "unitypackage 不存在: " + packagePath;
            return false;
        }

        if (string.IsNullOrWhiteSpace(destRoot))
        {
            result.Error = "解包目标根为空";
            return false;
        }

        destRoot = destRoot.Replace("\\", "/").TrimEnd('/');
        List<UnityPackageTarEntry> entries;
        try
        {
            entries = UnityPackageTar.Read(packagePath);
        }
        catch (Exception ex)
        {
            result.Error = "无法读取 unitypackage: " + ex.Message;
            return false;
        }

        Dictionary<string, AssetGroup> groups = GroupByGuid(entries);
        foreach (KeyValuePair<string, AssetGroup> pair in groups)
        {
            AssetGroup g = pair.Value;
            if (string.IsNullOrWhiteSpace(g.Pathname))
            {
                result.DroppedOther++;
                continue;
            }

            string relative;
            string skipReason;
            if (!TryMakeEnvelopeRelative(g.Pathname, out relative, out skipReason))
            {
                if (skipReason == "scene")
                {
                    result.DroppedScenes++;
                }
                else if (skipReason == "dangerous")
                {
                    result.DroppedDangerous++;
                }
                else
                {
                    result.DroppedOther++;
                }

                continue;
            }

            if (!TryWriteGroup(destRoot, relative, g, out string writeError))
            {
                result.Error = writeError;
                return false;
            }

            result.WrittenFiles++;
            result.WrittenRelativePaths.Add(relative);
        }

        result.Ok = true;
        return true;
    }

    static Dictionary<string, AssetGroup> GroupByGuid(List<UnityPackageTarEntry> entries)
    {
        var groups = new Dictionary<string, AssetGroup>(StringComparer.OrdinalIgnoreCase);
        if (entries == null)
        {
            return groups;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            UnityPackageTarEntry e = entries[i];
            if (e == null || string.IsNullOrEmpty(e.Name))
            {
                continue;
            }

            string name = e.Name.Replace("\\", "/").TrimStart('/');
            int slash = name.IndexOf('/');
            string guid;
            string leaf;
            if (slash <= 0)
            {
                guid = "_";
                leaf = name;
            }
            else
            {
                guid = name.Substring(0, slash);
                leaf = name.Substring(slash + 1);
            }

            AssetGroup g;
            if (!groups.TryGetValue(guid, out g))
            {
                g = new AssetGroup();
                groups[guid] = g;
            }

            if (string.Equals(leaf, "pathname", StringComparison.OrdinalIgnoreCase))
            {
                g.Pathname = Encoding.UTF8.GetString(e.Data ?? Array.Empty<byte>()).Trim();
            }
            else if (string.Equals(leaf, "asset", StringComparison.OrdinalIgnoreCase))
            {
                g.Asset = e.Data ?? Array.Empty<byte>();
            }
            else if (string.Equals(leaf, "asset.meta", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(leaf, "meta", StringComparison.OrdinalIgnoreCase))
            {
                g.Meta = e.Data ?? Array.Empty<byte>();
            }
        }

        return groups;
    }

    static bool TryMakeEnvelopeRelative(string pathname, out string relative, out string skipReason)
    {
        relative = null;
        skipReason = null;
        string p = (pathname ?? string.Empty).Replace("\\", "/").Trim().Trim('"');
        if (p.Length >= 1 && p[0] == '\uFEFF')
        {
            p = p.Substring(1).Trim();
        }

        if (string.IsNullOrEmpty(p))
        {
            skipReason = "empty";
            return false;
        }

        if (p.IndexOf("..", StringComparison.Ordinal) >= 0)
        {
            skipReason = "traversal";
            return false;
        }

        if (p.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("ProjectSettings/", StringComparison.OrdinalIgnoreCase))
        {
            skipReason = "packages";
            return false;
        }

        if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            p = p.Substring("Assets/".Length);
        }

        p = p.Trim('/');
        if (string.IsNullOrEmpty(p))
        {
            skipReason = "empty";
            return false;
        }

        string ext = Path.GetExtension(p);
        if (string.Equals(ext, ".unity", StringComparison.OrdinalIgnoreCase))
        {
            skipReason = "scene";
            return false;
        }

        if (IsDangerousExtension(ext))
        {
            skipReason = "dangerous";
            return false;
        }

        relative = p;
        return true;
    }

    static bool IsDangerousExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext))
        {
            return false;
        }

        for (int i = 0; i < DangerousExtensions.Length; i++)
        {
            if (string.Equals(ext, DangerousExtensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static bool TryWriteGroup(string destRoot, string relative, AssetGroup g, out string error)
    {
        error = null;
        bool folder = IsFolderMeta(g.Meta) || (g.Asset == null && string.IsNullOrEmpty(Path.GetExtension(relative)));
        string dest = destRoot + "/" + relative;
        string destFull = dest.Replace('/', Path.DirectorySeparatorChar);
        try
        {
            if (folder)
            {
                Directory.CreateDirectory(destFull);
                if (g.Meta != null && g.Meta.Length > 0)
                {
                    File.WriteAllBytes(destFull + ".meta", g.Meta);
                }

                return true;
            }

            string parent = Path.GetDirectoryName(destFull);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllBytes(destFull, g.Asset ?? Array.Empty<byte>());
            if (g.Meta != null && g.Meta.Length > 0)
            {
                File.WriteAllBytes(destFull + ".meta", g.Meta);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = "写入失败 " + dest + ": " + ex.Message;
            return false;
        }
    }

    static bool IsFolderMeta(byte[] meta)
    {
        if (meta == null || meta.Length == 0)
        {
            return false;
        }

        string text = Encoding.UTF8.GetString(meta);
        return text.IndexOf("folderAsset: yes", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    sealed class AssetGroup
    {
        public string Pathname;
        public byte[] Asset;
        public byte[] Meta;
    }
}
