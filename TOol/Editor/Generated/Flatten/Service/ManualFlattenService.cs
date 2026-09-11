using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 人工平铺中间层：选中模型/Prefab，显式选择 B 或 B′；同一趟只用一份 SO 快照。
// =====================================================================================

public enum ManualFlattenMode
{
    SplitDependencies = 0,
    RelocateAtomic = 1
}

public sealed class ManualFlattenResult
{
    public int Requested;
    public int Succeeded;
    public readonly List<string> Outputs = new List<string>();
    public readonly List<string> Errors = new List<string>();

    public string Summary
    {
        get
        {
            return "成功 " + Succeeded + " / " + Requested +
                   (Errors.Count > 0 ? "，失败 " + Errors.Count + "（见 Console）" : string.Empty);
        }
    }
}

public static class ManualFlattenService
{
    private static readonly string[] SupportedModelExtensions =
    {
        ".fbx", ".obj", ".glb", ".gltf"
    };

    public static ManualFlattenResult RunSelection(
        FlattenOperationSettings settings,
        ManualFlattenMode mode)
    {
        var result = new ManualFlattenResult();
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            AddError(result, "脚本编译或 Play Mode 中，人工平铺已拒绝。", null);
            return result;
        }

        FlattenOperationPolicy policy = settings != null
            ? settings.CreatePolicy()
            : FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);

        List<string> selectedPaths = CollectSelectedPaths();
        result.Requested = selectedPaths.Count;
        if (selectedPaths.Count == 0)
        {
            AddError(
                result,
                mode == ManualFlattenMode.RelocateAtomic
                    ? "请选择 .gltf 或只依赖一个外部 URI glTF 包的 Prefab。"
                    : "请选择 Prefab 或 .fbx/.obj/.glb/.gltf 模型。",
                null);
            return result;
        }

        for (int i = 0; i < selectedPaths.Count; i++)
        {
            string output;
            string error;
            if (TryRunOne(selectedPaths[i], policy, mode, out output, out error))
            {
                result.Succeeded++;
                if (!string.IsNullOrEmpty(output))
                {
                    result.Outputs.Add(output);
                }
            }
            else
            {
                AddError(result, error, selectedPaths[i]);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ManualFlatten] " + mode + "：" + result.Summary +
                  "；快照=" + policy.ToLogString());
        return result;
    }

    private static bool TryRunOne(
        string sourcePath,
        FlattenOperationPolicy policy,
        ManualFlattenMode mode,
        out string output,
        out string error)
    {
        output = null;
        error = null;
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension == ".prefab")
        {
            return TryRunPrefab(sourcePath, policy, mode, out output, out error);
        }

        if (!IsSupportedModelExtension(extension))
        {
            error = "不支持的人工平铺输入: " + sourcePath;
            return false;
        }

        PipelineJobContext ctx = PipelineJobContext.Build(sourcePath);
        if (!ValidateBranch(ctx, sourcePath, mode, out error))
        {
            return false;
        }

        // 保持旧人工 FBX SafeZone 行为；其它模型先走③生成 Prefab，再进入同一④相位。
        if (extension == ".fbx" && mode == ManualFlattenMode.SplitDependencies)
        {
            RetinarFlattenOptions options = FlattenBuildService.CreateOptions(
                ctx, ToolFlattenRequest.ForManual(policy));
            List<string> outputs;
            int count = RetinarFlattenApi.FlattenPaths(
                new[] { sourcePath }, true, options, out outputs);
            if (count != 1 || outputs == null || outputs.Count != 1)
            {
                error = "FBX 普通平铺失败: " + sourcePath;
                return false;
            }

            output = outputs[0];
            return true;
        }

        List<string> prefabPaths = ToolPrefabApi.BuildPrefabs(new[] { sourcePath });
        if (prefabPaths == null || prefabPaths.Count != 1)
        {
            error = "无法为模型建立人工平铺 Prefab: " + sourcePath;
            return false;
        }

        return TryRunPhase(prefabPaths[0], ctx, policy, mode, out output, out error);
    }

    private static bool TryRunPrefab(
        string prefabPath,
        FlattenOperationPolicy policy,
        ManualFlattenMode mode,
        out string output,
        out string error)
    {
        output = null;
        error = null;
        List<PipelineJobContext> contexts = BuildModelContexts(prefabPath);

        if (mode == ManualFlattenMode.RelocateAtomic)
        {
            if (contexts.Count != 1 ||
                !string.Equals(
                    contexts[0].SourceExtension, ".gltf", StringComparison.OrdinalIgnoreCase))
            {
                error = "原子迁移要求 Prefab 恰好依赖一个 .gltf 主包: " + prefabPath;
                return false;
            }

            if (!ValidateBranch(contexts[0], prefabPath, mode, out error))
            {
                return false;
            }

            return TryRunPhase(prefabPath, contexts[0], policy, mode, out output, out error);
        }

        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].HasExternalUris)
            {
                error = "普通平铺会拆坏相对 URI；请改用“原子迁移”: " + prefabPath;
                return false;
            }
        }

        // 单模型时保留 Importer 事实；多模型/纯 Renderer Prefab 沿用旧菜单的完整 E 行为。
        PipelineJobContext context = contexts.Count == 1 ? contexts[0] : null;
        return TryRunPhase(prefabPath, context, policy, mode, out output, out error);
    }

    private static bool TryRunPhase(
        string prefabPath,
        PipelineJobContext ctx,
        FlattenOperationPolicy policy,
        ManualFlattenMode mode,
        out string output,
        out string error)
    {
        output = null;
        error = null;
        if (!ValidateBranch(ctx, prefabPath, mode, out error))
        {
            return false;
        }

        ToolFlattenRequest request = ToolFlattenRequest.ForManual(policy);
        RetinarFlattenWork work;
        if (!ToolFlattenApi.TryBegin(prefabPath, ctx, request, out work))
        {
            error = "平铺 Begin 失败: " + prefabPath;
            return false;
        }

        bool copied = mode == ManualFlattenMode.RelocateAtomic
            ? ToolFlattenApi.RelocateAtomic(work)
            : ToolFlattenApi.SplitDependencies(work);
        if (!copied)
        {
            error = (mode == ManualFlattenMode.RelocateAtomic ? "原子迁移" : "普通平铺") +
                    "复制阶段失败: " + prefabPath;
            return false;
        }

        ToolFlattenApi.ApplyImportAndExtract(work, ctx);
        ToolFlattenApi.Remap(work);
        ToolFlattenApi.CopyRendererMaterials(work);
        if (!ToolFlattenApi.TryFinish(work) || string.IsNullOrEmpty(work.PrefabPath))
        {
            error = "平铺 Finish 失败: " + prefabPath;
            return false;
        }

        output = work.PrefabPath;
        return true;
    }

    internal static bool ValidateBranch(
        PipelineJobContext ctx,
        string sourcePath,
        ManualFlattenMode mode,
        out string error)
    {
        error = null;
        if (mode == ManualFlattenMode.RelocateAtomic)
        {
            if (ctx == null || !ctx.HasExternalUris)
            {
                error = "原子迁移只接受已声明外部相对 URI 的 glTF 包: " + sourcePath;
                return false;
            }

            if (ToolFlattenApi.HasMissingSidecars(ctx))
            {
                error = "glTF 缺必需伴生 × " + ctx.MissingUris.Count + ": " + sourcePath;
                return false;
            }

            return true;
        }

        if (ctx != null && ctx.HasExternalUris)
        {
            error = "普通平铺会拆坏相对 URI；请改用“原子迁移”: " + sourcePath;
            return false;
        }

        return true;
    }

    private static List<PipelineJobContext> BuildModelContexts(string prefabPath)
    {
        var result = new List<PipelineJobContext>();
        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        for (int i = 0; i < dependencies.Length; i++)
        {
            string path = dependencies[i].Replace("\\", "/");
            if (IsSupportedModelExtension(Path.GetExtension(path).ToLowerInvariant()))
            {
                result.Add(PipelineJobContext.Build(path));
            }
        }

        return result;
    }

    private static List<string> CollectSelectedPaths()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        UnityEngine.Object[] selected = Selection.objects;
        for (int i = 0; selected != null && i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]).Replace("\\", "/");
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if ((extension == ".prefab" || IsSupportedModelExtension(extension)) && seen.Add(path))
            {
                result.Add(path);
            }
        }

        return result;
    }

    private static bool IsSupportedModelExtension(string extension)
    {
        for (int i = 0; i < SupportedModelExtensions.Length; i++)
        {
            if (string.Equals(
                    extension, SupportedModelExtensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddError(ManualFlattenResult result, string error, string sourcePath)
    {
        string line = string.IsNullOrEmpty(sourcePath) ? error : sourcePath + "：" + error;
        result.Errors.Add(line);
        Debug.LogError("[ManualFlatten] " + line);
    }
}
