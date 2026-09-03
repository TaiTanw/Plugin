using System;
using System.IO;
using UnityEngine;

// =====================================================================================
// Pipeline — OBJ 轴向提示。只嗅不判，不参与决策。
// =====================================================================================

/// <summary>
/// OBJ 文件里没有 up-axis 字段，Unity 一律按 Y-up 读。源若来自 Max 那套默认 Z-up 的
/// 导出器就会竖立。这里只读文件头的导出器署名注释，给人一条「大概率 Z-up」的提示。
///
/// 不能升级成自动判定：这些导出器都带 Flip YZ 之类的勾选，勾了就是 Y-up，署名一样。
/// 真正的开关是 <see cref="PipelineSourceBinding.ConvertZUpToYUp"/>，由人填。
/// </summary>
public static class PipelineObjAxisProbe
{
    /// <summary>顶点数据之前的头部注释区，足够覆盖各家导出器的署名行。</summary>
    private const int MaxHeaderLines = 16;

    private static readonly string[] ZUpExporterMarkers =
    {
        "3ds max",
        "guruware"
    };

    /// <summary>
    /// 命中返回文件头里那行导出器署名；未命中、非 .obj、读不到都返回 null。
    /// 失败一律静默：这只是提示，不该让任何一步失败。
    /// </summary>
    public static string SniffZUpExporter(string path)
    {
        if (string.IsNullOrEmpty(path) ||
            !path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string fullPath = ToFullPath(path);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            using (var reader = new StreamReader(fullPath))
            {
                for (int i = 0; i < MaxHeaderLines; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    string trimmed = line.Trim();
                    if (!trimmed.StartsWith("#", StringComparison.Ordinal))
                    {
                        // 注释区结束，后面是几十万行顶点，不再往下读。
                        break;
                    }

                    string lowered = trimmed.ToLowerInvariant();
                    for (int m = 0; m < ZUpExporterMarkers.Length; m++)
                    {
                        if (lowered.Contains(ZUpExporterMarkers[m]))
                        {
                            return trimmed.TrimStart('#').Trim();
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>Assets 相对路径与外部磁盘路径都要能读（绑定行在入库前就想显示提示）。</summary>
    private static string ToFullPath(string path)
    {
        string normalized = path.Replace("\\", "/");
        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        string projectRoot = Application.dataPath;
        if (projectRoot.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase))
        {
            projectRoot = projectRoot.Substring(0, projectRoot.Length - "/Assets".Length);
        }

        return projectRoot + "/" + normalized;
    }
}
