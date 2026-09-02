using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

// =====================================================================================
// Pipeline — 导入事实（文件归类）。不是步骤开关。不引用平铺 API。
// =====================================================================================

/// <summary>② 成功后观测到的包形态。编排用它收窄④，但本类型不知道平铺怎么跑。</summary>
public sealed class PipelineJobContext
{
    public string PrimaryAssetPath;
    public string SourceExtension = string.Empty;
    public PipelineImporterKind ImporterKind;
    public bool HasExternalUris;
    public readonly List<string> SidecarPaths = new List<string>();
    public bool MainAssetOk;
    public PipelineMaterialForm MaterialForm;
    public readonly List<string> Warnings = new List<string>();

    /// <summary>对工程内主文件同步观测。失败只写 Warnings，不抛。</summary>
    public static PipelineJobContext Build(string primaryAssetPath)
    {
        var ctx = new PipelineJobContext();
        ctx.PrimaryAssetPath = (primaryAssetPath ?? string.Empty).Replace("\\", "/");
        if (string.IsNullOrEmpty(ctx.PrimaryAssetPath))
        {
            ctx.Warnings.Add("主路径为空");
            return ctx;
        }

        ctx.SourceExtension = Path.GetExtension(ctx.PrimaryAssetPath);
        string ext = (ctx.SourceExtension ?? string.Empty).ToLowerInvariant();

        UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(ctx.PrimaryAssetPath);
        ctx.MainAssetOk = main is GameObject;

        AssetImporter importer = AssetImporter.GetAtPath(ctx.PrimaryAssetPath);
        if (importer is ModelImporter)
        {
            ctx.ImporterKind = PipelineImporterKind.ModelImporter;
        }
        else if (importer is ScriptedImporter)
        {
            ctx.ImporterKind = PipelineImporterKind.ScriptedImporter;
        }
        else
        {
            ctx.ImporterKind = PipelineImporterKind.Unknown;
            if (importer == null)
            {
                ctx.Warnings.Add("无 Importer: " + ctx.PrimaryAssetPath);
            }
        }

        if (!ctx.MainAssetOk)
        {
            ctx.Warnings.Add("主资产不是 GameObject: " + ctx.PrimaryAssetPath);
        }

        ctx.MaterialForm = DetectMaterialForm(ctx.PrimaryAssetPath);

        if (ext == ".fbx" || ext == ".obj")
        {
            ctx.HasExternalUris = false;
        }
        else if (ext == ".glb")
        {
            ctx.HasExternalUris = false;
        }
        else if (ext == ".gltf")
        {
            PipelineGltfUriProbe.Apply(ctx);
        }
        else
        {
            ctx.Warnings.Add("未单独探测的扩展名: " + ext);
        }

        return ctx;
    }

    public string ToLogString()
    {
        var sb = new StringBuilder();
        sb.Append("[Pipeline] ctx");
        sb.Append(" path=").Append(PrimaryAssetPath);
        sb.Append(" ext=").Append(SourceExtension);
        sb.Append(" importer=").Append(ImporterKind);
        sb.Append(" hasExternalUris=").Append(HasExternalUris);
        sb.Append(" mainOk=").Append(MainAssetOk);
        sb.Append(" mat=").Append(MaterialForm);
        sb.Append(" sidecars=").Append(SidecarPaths.Count);
        sb.Append(" warnings=").Append(Warnings.Count);
        if (SidecarPaths.Count > 0)
        {
            sb.AppendLine();
            for (int i = 0; i < SidecarPaths.Count; i++)
            {
                sb.Append("  sidecar: ").AppendLine(SidecarPaths[i]);
            }
        }

        for (int i = 0; i < Warnings.Count; i++)
        {
            sb.Append("  warn: ").AppendLine(Warnings[i]);
        }

        return sb.ToString().TrimEnd();
    }

    static PipelineMaterialForm DetectMaterialForm(string primaryPath)
    {
        string[] deps = AssetDatabase.GetDependencies(primaryPath, true);
        if (deps == null || deps.Length == 0)
        {
            return PipelineMaterialForm.Unknown;
        }

        for (int i = 0; i < deps.Length; i++)
        {
            string dep = (deps[i] ?? string.Empty).Replace("\\", "/");
            if (dep.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                return PipelineMaterialForm.HasStandaloneMat;
            }
        }

        return PipelineMaterialForm.SubAssetOnly;
    }
}

public enum PipelineImporterKind
{
    Unknown = 0,
    ModelImporter = 1,
    ScriptedImporter = 2
}

public enum PipelineMaterialForm
{
    Unknown = 0,
    SubAssetOnly = 1,
    HasStandaloneMat = 2
}
