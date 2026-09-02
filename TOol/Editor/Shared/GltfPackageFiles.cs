using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// =====================================================================================
// Shared — glTF 外 URI 扫描。② 伴生拷与 ctx.Build 共用。
// 换解析器：只改 Scan() 内部，或新增 ScanXxx 再由 Scan 转调。
// =====================================================================================

/// <summary>一次 .gltf 文件的外 URI 扫描结果（磁盘路径）。</summary>
public sealed class GltfExternalScan
{
    public bool FileOk;
    public bool HasExternalUris;
    public readonly List<string> SidecarFullPaths = new List<string>();
    public readonly List<string> MissingUris = new List<string>();
    public readonly List<string> Notes = new List<string>();
}

/// <summary>
/// ② 结束、③ 开始前的 glTF 事实探测。当前实现：JSON 文本里的 <c>"uri"</c> 扫描。
/// </summary>
public static class GltfPackageFiles
{
    static readonly Regex QuotedUri = new Regex(
        "\"uri\"\\s*:\\s*\"([^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// 入口。以后若要按 buffers[]/images[] 解析，在本方法内改调用即可。
    /// </summary>
    public static GltfExternalScan Scan(string gltfFullPath)
    {
        return ScanQuotedUris(gltfFullPath);
    }

    /// <summary>现网探测：正则扫 quoted uri。漏检风险见 d23 报告第 5 条。</summary>
    public static GltfExternalScan ScanQuotedUris(string gltfFullPath)
    {
        var scan = new GltfExternalScan();
        if (string.IsNullOrEmpty(gltfFullPath) || !File.Exists(gltfFullPath))
        {
            scan.Notes.Add("gltf 磁盘文件不存在: " + gltfFullPath);
            scan.HasExternalUris = true;
            return scan;
        }

        string json;
        try
        {
            json = File.ReadAllText(gltfFullPath);
        }
        catch (Exception ex)
        {
            scan.Notes.Add("gltf JSON 读失败: " + ex.Message);
            scan.HasExternalUris = true;
            return scan;
        }

        if (string.IsNullOrEmpty(json) || json.TrimStart().Length == 0)
        {
            scan.Notes.Add("gltf 零字节或空 JSON");
            scan.HasExternalUris = true;
            return scan;
        }

        scan.FileOk = true;
        string dir = Path.GetDirectoryName(gltfFullPath);
        MatchCollection matches = QuotedUri.Matches(json);
        bool anyRelative = false;
        for (int i = 0; i < matches.Count; i++)
        {
            string uri = matches[i].Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(uri) ||
                uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            anyRelative = true;
            string sidecarFull;
            if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                sidecarFull = uri.Substring("file:".Length).TrimStart('/');
                if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) && uri.Length > 8)
                {
                    sidecarFull = uri.Substring(8).Replace('/', Path.DirectorySeparatorChar);
                }
            }
            else
            {
                sidecarFull = Path.GetFullPath(Path.Combine(dir ?? string.Empty, uri.Replace("\\", "/")));
            }

            sidecarFull = sidecarFull.Replace("\\", "/");
            if (File.Exists(sidecarFull))
            {
                AddUnique(scan.SidecarFullPaths, sidecarFull);
            }
            else
            {
                scan.MissingUris.Add(uri);
            }
        }

        scan.HasExternalUris = anyRelative || scan.SidecarFullPaths.Count > 0;
        if (anyRelative && scan.SidecarFullPaths.Count == 0)
        {
            scan.Notes.Add("声明了外 URI 但 Sidecar 列表空");
        }

        return scan;
    }

    /// <summary>相对 gltf 所在目录的相对路径；不在该树下则只返回文件名。</summary>
    public static string MakeRelativeToGltfDir(string gltfFullPath, string sidecarFullPath)
    {
        string root = Path.GetFullPath(Path.GetDirectoryName(gltfFullPath) ?? string.Empty)
            .Replace("\\", "/").TrimEnd('/');
        string path = Path.GetFullPath(sidecarFullPath ?? string.Empty).Replace("\\", "/");
        if (path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(root.Length + 1);
        }

        return Path.GetFileName(path);
    }

    static void AddUnique(List<string> list, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(path);
    }
}
