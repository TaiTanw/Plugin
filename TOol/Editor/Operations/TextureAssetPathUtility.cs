using System.IO;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   只做资产相对路径（Assets/xxx）和磁盘绝对路径之间的换算，以及文件体积读取。
//
// 为什么单独抽出来：
//   原来的实现直接用 Path.GetFullPath("Assets/xxx")，它依赖【进程当前工作目录】。
//   编辑器启动时 cwd 恰好是工程根目录，所以平时看起来是对的，但只要有任何代码
//   （包括第三方插件）调过 Directory.SetCurrentDirectory，路径就会算错，而且错得很隐蔽。
//   这里改成显式基于 Application.dataPath 拼接，不受 cwd 影响。
// =====================================================================================
public static class TextureAssetPathUtility
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
            // Packages/ 下的资产是只读的（来自包缓存或本地包），不应该被这套工具改写。
            return null;
        }

        // Application.dataPath 就是 "<工程根>/Assets"，去掉末尾的 "Assets" 得到工程根。
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        return projectRoot + "/" + assetPath;
    }

    /// <summary>
    /// 判断路径是否落在 Unity 的内嵌媒体抽取目录（&lt;FBX名&gt;.fbm）里。
    ///
    /// 为什么要单独识别它（这里踩过坑）：
    ///   .fbm 不是艺术家维护的目录，而是 Unity 从 FBX 二进制里抽取内嵌贴图后生成的缓存。
    ///   模型被重新导入时 Unity 会照着 FBX 里的原始数据重新抽取一遍，把这里的文件覆盖回去。
    ///   所以改写 .fbm 里的贴图有两个坏处：
    ///     1. 白做——下一次模型导入就被还原成原始大图；
    ///     2. 有害——期间的降分辨率是真的，缓存被还原前，引用它的材质会先掉一半清晰度。
    ///   真正该压的是"贴图作为独立资产存在"的地方（艺术家自己的贴图目录，或者打包工具
    ///   平铺到 Assets/Art/&lt;模型&gt;/Texture/ 之后的那一份）。
    /// </summary>
    public static bool IsInsideEmbeddedMediaFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string[] segments = assetPath.Replace("\\", "/").Split('/');

        // 最后一段是文件名，只看它前面的目录段。
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].EndsWith(".fbm", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>文件不存在时返回 -1，方便调用方和"体积为 0 的空文件"区分开。</summary>
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
