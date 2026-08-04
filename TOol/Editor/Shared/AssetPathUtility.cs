using System.IO;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   Shared 层工具。只做资产相对路径（Assets/xxx）和磁盘绝对路径之间的换算、
//   文件体积读取、以及识别 Unity 的 .fbm 内嵌媒体缓存目录。
//   贴图 / 模型两侧共用，不要再各写一份。
// =====================================================================================
public static class AssetPathUtility
{
    /// <summary>把 "Assets/xxx/a.png" 换成磁盘绝对路径。不是 Assets/ 开头的返回 null。</summary>
    public static string ToFullPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        assetPath = assetPath.Replace("\\", "/");
        if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        return projectRoot + "/" + assetPath;
    }

    /// <summary>
    /// 判断路径是否落在 Unity 的内嵌媒体抽取目录（&lt;FBX名&gt;.fbm）里。
    /// .fbm 是缓存，权威数据仍在 FBX 里；自动改写它是白做且有害。
    /// </summary>
    public static bool IsInsideEmbeddedMediaFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string[] segments = assetPath.Replace("\\", "/").Split('/');
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].EndsWith(".fbm", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>文件不存在时返回 -1。</summary>
    public static long GetFileLength(string assetPath)
    {
        string fullPath = ToFullPath(assetPath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return -1;
        }

        return new FileInfo(fullPath).Length;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024L)
        {
            return bytes + " B";
        }

        if (bytes < 1024L * 1024L)
        {
            return (bytes / 1024f).ToString("F1") + " KB";
        }

        return (bytes / 1024f / 1024f).ToString("F2") + " MB";
    }
}
