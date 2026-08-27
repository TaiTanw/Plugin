using System.Collections.Generic;
using System.IO;
using UnityEngine;

// =====================================================================================
// Pipeline — materialId 命名（中间层；面板 D9 / 批量 D10 预备）
// =====================================================================================

/// <summary>
/// 单源默认 Id，以及多源消歧绑定（D10 预备；编排 Runner 尚未消费多绑定列表）。
/// </summary>
public static class PipelineMaterialId
{
    /// <summary>
    /// 由源路径建议默认 materialId（= Prefab 三层夹名规则，与 ③ 缺省命名一致）。
    /// </summary>
    public static string SuggestDefault(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        return PrefabIncomingPaths.ResolvePrefabBaseName(sourcePath.Trim().Replace("\\", "/"), null);
    }

    /// <summary>
    /// D10 预备：多文件/多夹 → 每条源对应一个 MaterialId。
    /// 同父目录下多个模型：在基名后追加文件 stem，避免撞名（对齐 PrefabBuild 多源消歧）。
    /// sharedMaterialId 非空时作为共用基名，再按需加 stem；为空则每条各自 SuggestDefault。
    /// <para>正式多选 UI / Runner 消费暂缓；本方法供后续接线。</para>
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
                if (needsStem && !string.IsNullOrEmpty(stem) &&
                    !id.EndsWith("_" + BatchFbxImportService.SanitizeFolderName(stem),
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    id = BatchFbxImportService.SanitizeFolderName(id + "_" + stem);
                }
            }

            id = EnsureUniqueId(id, usedIds, stem);
            result.Add(new PipelineSourceBinding(source, id));
        }

        return result;
    }

    private static string ParentKey(string path)
    {
        string dir = Path.GetDirectoryName(path.Replace("\\", "/"));
        return string.IsNullOrEmpty(dir) ? string.Empty : dir.Replace("\\", "/").TrimEnd('/');
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

/// <summary>D10 预备：一条源路径及其 materialId（编排尚未批量消费）。</summary>
public sealed class PipelineSourceBinding
{
    public readonly string SourcePath;
    public readonly string MaterialId;

    public PipelineSourceBinding(string sourcePath, string materialId)
    {
        SourcePath = sourcePath ?? string.Empty;
        MaterialId = materialId ?? string.Empty;
    }
}
