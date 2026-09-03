using System.Collections.Generic;
using System.IO;

// =====================================================================================
// Pipeline — materialId / ID2 命名（面板建议；Runner 空 Id 时补缺省）
// 识别：内核后缀。区分：父目录磁盘上内核文件是否多于 1。不读文件体，无文件夹 ctx。
// =====================================================================================

/// <summary>
/// 缺省 ID2：三层夹名；父目录还有其它内核格式文件、或三层 Warning 时追加文件全名。
/// </summary>
public static class PipelineMaterialId
{
    /// <summary>
    /// 由源路径建议默认 ID2。先看父目录磁盘上内核文件个数，再决定三层还是三层+文件全名。
    /// </summary>
    public static string SuggestDefault(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        bool fallback;
        string warning;
        string three = ThreeLayerName(sourcePath, out fallback, out warning);
        if (fallback || ParentHasMultipleKernelModels(sourcePath))
        {
            string fullName = Path.GetFileName(sourcePath.Trim().Replace("\\", "/"));
            return BatchFbxImportService.SanitizeFolderName(three + "_" + fullName);
        }

        return three;
    }

    /// <summary>
    /// D10 旧接口：列表内消歧用 stem。编排缺省 ID2 请用 <see cref="SuggestBindingsForSelection"/>。
    /// sharedMaterialId 非空时作为共用基名，再按需加 stem。
    /// </summary>
    public static List<PipelineSourceBinding> BuildSourceBindings(
        IList<string> sourcePaths,
        string sharedMaterialId = null)
    {
        var result = new List<PipelineSourceBinding>();
        if (sourcePaths == null || sourcePaths.Count == 0)
        {
            return result;
        }

        var normalized = new List<string>();
        for (int i = 0; i < sourcePaths.Count; i++)
        {
            string p = (sourcePaths[i] ?? string.Empty).Replace("\\", "/").Trim();
            if (!string.IsNullOrEmpty(p) && !normalized.Contains(p))
            {
                normalized.Add(p);
            }
        }

        if (normalized.Count == 0)
        {
            return result;
        }

        var parentCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < normalized.Count; i++)
        {
            string parent = ParentKey(normalized[i]);
            int count;
            parentCounts.TryGetValue(parent, out count);
            parentCounts[parent] = count + 1;
        }

        bool useShared = !string.IsNullOrWhiteSpace(sharedMaterialId);
        string sharedBase = useShared
            ? BatchFbxImportService.SanitizeFolderName(sharedMaterialId.Trim())
            : null;

        var usedIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < normalized.Count; i++)
        {
            string source = normalized[i];
            string parent = ParentKey(source);
            bool needsStem = parentCounts[parent] > 1 || (useShared && normalized.Count > 1);
            string stem = Path.GetFileNameWithoutExtension(source);

            string id;
            if (useShared)
            {
                id = needsStem && !string.IsNullOrEmpty(stem)
                    ? BatchFbxImportService.SanitizeFolderName(sharedBase + "_" + stem)
                    : sharedBase;
            }
            else
            {
                id = SuggestDefault(source);
            }

            id = EnsureUniqueId(id, usedIds, stem);
            result.Add(new PipelineSourceBinding(source, id));
        }

        return result;
    }

    /// <summary>
    /// 批量「输出到编排」用的缺省 ID2。每条各自 <see cref="SuggestDefault"/>（扫该文件父目录磁盘）。
    /// </summary>
    public static List<PipelineSourceBinding> SuggestBindingsForSelection(IList<string> sourcePaths)
    {
        var result = new List<PipelineSourceBinding>();
        if (sourcePaths == null || sourcePaths.Count == 0)
        {
            return result;
        }

        var normalized = new List<string>();
        for (int i = 0; i < sourcePaths.Count; i++)
        {
            string p = (sourcePaths[i] ?? string.Empty).Replace("\\", "/").Trim();
            if (!string.IsNullOrEmpty(p) && !normalized.Contains(p))
            {
                normalized.Add(p);
            }
        }

        var usedIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < normalized.Count; i++)
        {
            string source = normalized[i];
            string id = SuggestDefault(source);
            string stem = Path.GetFileNameWithoutExtension(source);
            id = EnsureUniqueId(id, usedIds, stem);
            result.Add(new PipelineSourceBinding(source, id));
        }

        return result;
    }

    /// <summary>
    /// 父目录（非递归）里内核格式文件是否多于 1。
    /// 只数 <see cref="ToolImportApi"/> 白名单后缀；不读文件体、不数 .bin/贴图。
    /// </summary>
    public static bool ParentHasMultipleKernelModels(string sourcePath)
    {
        return CountKernelModelsInParent(sourcePath) > 1;
    }

    /// <summary>父目录非递归内核文件个数；扫失败当 0。</summary>
    public static int CountKernelModelsInParent(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return 0;
        }

        string disk = ToDiskPath(sourcePath.Trim());
        string parent = Path.GetDirectoryName(disk);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            return 0;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(parent);
        }
        catch (System.Exception)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < files.Length; i++)
        {
            if (ToolImportApi.IsSupportedExtension(Path.GetExtension(files[i])))
            {
                count++;
            }
        }

        return count;
    }

    private static string ThreeLayerName(string sourcePath, out bool fallback, out string warning)
    {
        string forResolve = sourcePath ?? string.Empty;
        forResolve = forResolve.Replace("\\", "/");
        if (forResolve.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            string full = AssetPathUtility.ToFullPath(forResolve);
            if (!string.IsNullOrEmpty(full))
            {
                forResolve = full;
            }
        }

        return BatchFbxImportService.ResolveFolderName(forResolve, out fallback, out warning);
    }

    private static string ParentKey(string path)
    {
        string dir = Path.GetDirectoryName(path.Replace("\\", "/"));
        return string.IsNullOrEmpty(dir) ? string.Empty : dir.Replace("\\", "/").TrimEnd('/');
    }

    private static string ToDiskPath(string sourcePath)
    {
        string p = (sourcePath ?? string.Empty).Replace("\\", "/");
        if (p.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            string full = AssetPathUtility.ToFullPath(p);
            if (!string.IsNullOrEmpty(full))
            {
                return full.Replace("\\", "/");
            }
        }

        try
        {
            return Path.GetFullPath(p).Replace("\\", "/");
        }
        catch (System.Exception)
        {
            return p;
        }
    }

    private static string EnsureUniqueId(string id, HashSet<string> used, string stem)
    {
        string candidate = string.IsNullOrEmpty(id) ? "unnamed" : id;
        if (used.Add(candidate))
        {
            return candidate;
        }

        string safeStem = BatchFbxImportService.SanitizeFolderName(
            string.IsNullOrEmpty(stem) ? "item" : stem);
        int suffix = 2;
        while (!used.Add(candidate + "_" + safeStem + "_" + suffix))
        {
            suffix++;
        }

        return candidate + "_" + safeStem + "_" + suffix;
    }
}

/// <summary>一条源路径及其 ID2（materialId）。面板可改 Id；Runner 按行调度。</summary>
public sealed class PipelineSourceBinding
{
    public string SourcePath;
    public string MaterialId;

    /// <summary>
    /// 人给的输入，不是观测结果。OBJ 格式没有 up-axis 字段，Unity 只能当 Y-up 读；
    /// 源若是 Max 的 Z-up 导出就会竖立。勾上则由④在空壳的内容节点上叠 −90°X，
    /// 与 Unity 对 FBX（头里有 up-axis）的既有行为对齐。
    /// 不自动判定：猜反的代价是交付一架躺着的模型且 AB 里看不出来。
    /// </summary>
    public bool ConvertZUpToYUp;

    public PipelineSourceBinding()
    {
        SourcePath = string.Empty;
        MaterialId = string.Empty;
    }

    public PipelineSourceBinding(string sourcePath, string materialId)
        : this(sourcePath, materialId, false)
    {
    }

    public PipelineSourceBinding(string sourcePath, string materialId, bool convertZUpToYUp)
    {
        SourcePath = sourcePath ?? string.Empty;
        MaterialId = materialId ?? string.Empty;
        ConvertZUpToYUp = convertZUpToYUp;
    }

    /// <summary>
    /// 整行复制，可覆写路径。绑定行在面板与 Runner 之间被重建三次（ApplyBindings /
    /// CopyBindings / NormalizeBindings），逐字段手抄过一次就漏过一次开关——新增字段
    /// 只改这里，别再回到手抄。
    /// </summary>
    public PipelineSourceBinding CloneWith(string sourcePath)
    {
        return new PipelineSourceBinding(
            sourcePath ?? SourcePath,
            MaterialId,
            ConvertZUpToYUp);
    }
}
