using System.Collections.Generic;
using System.IO;

// =====================================================================================
// Pipeline — [1] 预览收源规则。选择器仍走模型表；pack 只走浏览/路径框。
// =====================================================================================

/// <summary>预览表：模型可多行；pack 最多一条且独占该表。</summary>
public static class PipelinePreviewSources
{
    public const string OpenFileFilter = "fbx,glb,gltf,obj,unitypackage";

    public static bool IsAcceptedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string ext = Path.GetExtension(path);
        return ToolImportApi.IsSupportedExtension(ext) || ToolImportApi.IsUnityPackagePath(path);
    }

    public static bool IsPackBinding(PipelineSourceBinding row)
    {
        return row != null && ToolImportApi.IsUnityPackagePath(row.SourcePath);
    }

    public static bool TableHasPack(IList<PipelineSourceBinding> rows)
    {
        if (rows == null)
        {
            return false;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (IsPackBinding(rows[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 丢掉非模型非 pack。出现 pack 时只留第一条 pack（模型与其余 pack 都丢掉）。
    /// </summary>
    public static List<PipelineSourceBinding> NormalizeTable(IList<PipelineSourceBinding> bindings)
    {
        var models = new List<PipelineSourceBinding>();
        PipelineSourceBinding firstPack = null;
        if (bindings == null)
        {
            return models;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            PipelineSourceBinding src = bindings[i];
            if (src == null || string.IsNullOrWhiteSpace(src.SourcePath))
            {
                continue;
            }

            string path = src.SourcePath.Replace("\\", "/").Trim();
            if (ToolImportApi.IsUnityPackagePath(path))
            {
                if (firstPack == null)
                {
                    firstPack = src.CloneWith(path);
                }

                continue;
            }

            if (ToolImportApi.IsSupportedModelPath(path))
            {
                models.Add(src.CloneWith(path));
            }
        }

        if (firstPack != null)
        {
            return new List<PipelineSourceBinding> { firstPack };
        }

        return models;
    }
}
