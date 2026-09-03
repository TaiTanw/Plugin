using System;
using System.Collections.Generic;
using System.IO;

// =====================================================================================
// Shared — Wavefront OBJ 伴生：mtllib + mtl 里的 map_* 。1 入库跟拷，与 gltf Scan 同类。
// 不读网格本体，不做轴向转换。
// =====================================================================================

/// <summary>.obj 旁路扫描结果。</summary>
public sealed class ObjExternalScan
{
    public readonly List<string> SidecarFullPaths = new List<string>();
    public readonly List<string> MissingUris = new List<string>();
}

/// <summary>从 .obj / .mtl 文本收集要跟拷的磁盘文件。</summary>
public static class ObjPackageFiles
{
    /// <summary>obj 同目录（及 mtl 相对路径）下的 .mtl 与贴图。</summary>
    public static ObjExternalScan Scan(string objFullPath)
    {
        var scan = new ObjExternalScan();
        if (string.IsNullOrEmpty(objFullPath) || !File.Exists(objFullPath))
        {
            return scan;
        }

        string objDir = Path.GetDirectoryName(objFullPath);
        var mtlFiles = new List<string>();
        try
        {
            using (var reader = new StreamReader(objFullPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (t.Length < 8 ||
                        !t.StartsWith("mtllib", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string rest = t.Substring(6).Trim();
                    if (string.IsNullOrEmpty(rest))
                    {
                        continue;
                    }

                    AddResolved(objDir, rest, mtlFiles, scan.MissingUris);
                }
            }
        }
        catch (Exception)
        {
            return scan;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < mtlFiles.Count; i++)
        {
            string mtl = mtlFiles[i];
            if (!seen.Add(mtl))
            {
                continue;
            }

            scan.SidecarFullPaths.Add(mtl);
            CollectMapsFromMtl(mtl, scan, seen);
        }

        return scan;
    }

    public static string MakeRelativeToObjDir(string objFullPath, string sidecarFullPath)
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(objFullPath ?? string.Empty));
        string path = Path.GetFullPath(sidecarFullPath ?? string.Empty).Replace("\\", "/");
        if (string.IsNullOrEmpty(root))
        {
            return Path.GetFileName(path);
        }

        root = Path.GetFullPath(root).Replace("\\", "/").TrimEnd('/') + "/";
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(root.Length);
        }

        return Path.GetFileName(path);
    }

    private static void CollectMapsFromMtl(string mtlFull, ObjExternalScan scan, HashSet<string> seen)
    {
        string mtlDir = Path.GetDirectoryName(mtlFull);
        string[] lines;
        try
        {
            lines = File.ReadAllLines(mtlFull);
        }
        catch (Exception)
        {
            return;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string t = (lines[i] ?? string.Empty).Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsMapKeyword(t))
            {
                continue;
            }

            string pathToken = LastPathToken(t);
            if (string.IsNullOrEmpty(pathToken))
            {
                continue;
            }

            AddResolved(mtlDir, pathToken, scan.SidecarFullPaths, scan.MissingUris, seen);
        }
    }

    private static bool IsMapKeyword(string line)
    {
        if (line.StartsWith("map_", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("bump", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("disp", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("norm", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("refl", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string LastPathToken(string line)
    {
        int q1 = line.IndexOf('"');
        if (q1 >= 0)
        {
            int q2 = line.IndexOf('"', q1 + 1);
            if (q2 > q1)
            {
                return line.Substring(q1 + 1, q2 - q1 - 1).Trim();
            }
        }

        string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        return parts[parts.Length - 1];
    }

    private static void AddResolved(
        string baseDir,
        string relative,
        List<string> into,
        List<string> missing)
    {
        AddResolved(baseDir, relative, into, missing, null);
    }

    private static void AddResolved(
        string baseDir,
        string relative,
        List<string> into,
        List<string> missing,
        HashSet<string> seen)
    {
        string n = (relative ?? string.Empty).Trim().Replace("\\", "/");
        if (string.IsNullOrEmpty(n))
        {
            return;
        }

        string full;
        try
        {
            full = Path.GetFullPath(Path.Combine(baseDir ?? string.Empty, n)).Replace("\\", "/");
        }
        catch (Exception)
        {
            missing.Add(relative);
            return;
        }

        if (!File.Exists(full))
        {
            missing.Add(n);
            return;
        }

        if (seen != null && !seen.Add(full))
        {
            return;
        }

        if (seen == null)
        {
            for (int i = 0; i < into.Count; i++)
            {
                if (string.Equals(into[i], full, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        into.Add(full);
    }
}
