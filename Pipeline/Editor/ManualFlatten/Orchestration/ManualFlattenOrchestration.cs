using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Flatten / Orchestration — 人工④调度。选中 → 组 plan → Run。不 Build ctx。
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

/// <summary>人工完整④。B / B′ 由按钮写入 plan.Branch。</summary>
public static class ManualFlattenOrchestration
{
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

        FlattenBranch branch = mode == ManualFlattenMode.RelocateAtomic
            ? FlattenBranch.RelocateAtomic
            : FlattenBranch.SplitDependencies;

        List<string> rawPaths = CollectRawSelectedAssetPaths();
        List<string> expandErrors;
        List<string> selectedPaths = ManualFlattenSources.Expand(rawPaths, out expandErrors);
        for (int e = 0; e < expandErrors.Count; e++)
        {
            AddError(result, expandErrors[e], null);
        }

        result.Requested = selectedPaths.Count;
        if (selectedPaths.Count == 0)
        {
            if (expandErrors.Count == 0)
            {
                AddError(
                    result,
                    branch == FlattenBranch.RelocateAtomic
                        ? "请选择 .gltf、只依赖一个外部 URI glTF 包的 Prefab，或解包后的根文件夹。"
                        : "请选择 Prefab、.fbx/.obj/.glb/.gltf，或解包后的根文件夹。",
                    null);
            }

            return result;
        }

        for (int i = 0; i < selectedPaths.Count; i++)
        {
            string output;
            string error;
            if (TryRunOne(selectedPaths[i], policy, branch, out output, out error))
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

    static bool TryRunOne(
        string sourcePath,
        FlattenOperationPolicy policy,
        FlattenBranch branch,
        out string output,
        out string error)
    {
        output = null;
        error = null;
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension == ".prefab")
        {
            return TryRunPrefab(sourcePath, policy, branch, out output, out error);
        }

        if (!FlattenSidecarFacts.IsKernelModelExtension(extension))
        {
            error = "不支持的人工平铺输入: " + sourcePath;
            return false;
        }

        FlattenSidecarFacts facts = FlattenSidecarFacts.FromGltfAsset(sourcePath);
        if (!FlattenManualPlanFactory.TryValidate(branch, facts, sourcePath, out error))
        {
            return false;
        }

        string warn = FlattenManualPlanFactory.RelativeUriWarningIfSplit(branch, facts, sourcePath);
        if (!FlattenManualPrompt.ConfirmSplitWithRelativeUris(warn))
        {
            error = FlattenManualPrompt.CancelledMessage;
            return false;
        }

        List<string> prefabPaths = ToolPrefabApi.BuildPrefabs(new[] { sourcePath });
        if (prefabPaths == null || prefabPaths.Count != 1)
        {
            error = "无法为模型建立人工平铺 Prefab: " + sourcePath;
            return false;
        }

        FlattenPlan plan = FlattenManualPlanFactory.Create(
            prefabPaths[0],
            branch,
            policy,
            facts.HasExternalUris || branch == FlattenBranch.RelocateAtomic ? facts : null,
            FlattenManualPlanFactory.InferApplyArtModelImporter(new[] { sourcePath }));
        return RunPlan(plan, out output, out error);
    }

    static bool TryRunPrefab(
        string prefabPath,
        FlattenOperationPolicy policy,
        FlattenBranch branch,
        out string output,
        out string error)
    {
        output = null;
        error = null;
        List<string> gltfs = FlattenSidecarFacts.ListGltfDependencies(prefabPath);
        List<string> models = FlattenSidecarFacts.ListModelDependencies(prefabPath);

        FlattenSidecarFacts facts;
        if (branch == FlattenBranch.RelocateAtomic)
        {
            if (gltfs.Count != 1)
            {
                error = "原子迁移要求 Prefab 恰好依赖一个 .gltf 主包: " + prefabPath;
                return false;
            }

            facts = FlattenSidecarFacts.FromGltfAsset(gltfs[0]);
        }
        else
        {
            facts = FlattenSidecarFacts.MergeGltfScans(gltfs);
        }

        if (!FlattenManualPlanFactory.TryValidate(branch, facts, prefabPath, out error))
        {
            return false;
        }

        string warn = FlattenManualPlanFactory.RelativeUriWarningIfSplit(branch, facts, prefabPath);
        if (!FlattenManualPrompt.ConfirmSplitWithRelativeUris(warn))
        {
            error = FlattenManualPrompt.CancelledMessage;
            return false;
        }

        FlattenPlan plan = FlattenManualPlanFactory.Create(
            prefabPath,
            branch,
            policy,
            branch == FlattenBranch.RelocateAtomic ? facts : null,
            FlattenManualPlanFactory.InferApplyArtModelImporter(models));
        return RunPlan(plan, out output, out error);
    }

    static bool RunPlan(FlattenPlan plan, out string output, out string error)
    {
        output = null;
        error = null;
        FlattenRowResult row = ToolFlattenApi.Run(plan);
        if (!row.Ok)
        {
            error = string.IsNullOrEmpty(row.Message)
                ? ("平铺失败: " + plan.SourcePrefabPath)
                : row.Message;
            return false;
        }

        output = row.ArtPrefabPath;
        return true;
    }

    static List<string> CollectRawSelectedAssetPaths()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        UnityEngine.Object[] selected = Selection.objects;
        for (int i = 0; selected != null && i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]).Replace("\\", "/");
            if (string.IsNullOrEmpty(path) || !seen.Add(path))
            {
                continue;
            }

            result.Add(path);
        }

        return result;
    }

    static void AddError(ManualFlattenResult result, string error, string sourcePath)
    {
        string line = string.IsNullOrEmpty(sourcePath) ? error : sourcePath + "：" + error;
        result.Errors.Add(line);
        if (error == FlattenManualPrompt.CancelledMessage)
        {
            Debug.Log("[ManualFlatten] " + line);
            return;
        }

        Debug.LogError("[ManualFlatten] " + line);
    }
}
