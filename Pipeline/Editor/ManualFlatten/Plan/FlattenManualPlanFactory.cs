using System;
using System.Collections.Generic;
using System.IO;

// =====================================================================================
// ManualFlatten / Plan — 人工 FlattenPlan。按钮给 Branch；sidecar 只来自 Scan。禁止 ctx。
// =====================================================================================

/// <summary>把 Scan 事实 + 人给的政策写成 Run(plan) 入参。</summary>
public static class FlattenManualPlanFactory
{
    /// <summary>
    /// RelocateAtomic：必须有外 URI；缺伴生则失败（与 Run 闸相同）。
    /// Split：不因外 URI 失败；相对 URI 确认由 <see cref="RelativeUriWarningIfSplit"/> 给出。
    /// </summary>
    public static bool TryValidate(
        FlattenBranch branch,
        FlattenSidecarFacts facts,
        string sourcePath,
        out string error)
    {
        error = null;
        if (branch == FlattenBranch.RelocateAtomic)
        {
            if (facts == null || !facts.HasExternalUris)
            {
                error = "原子迁移只接受已声明外部相对 URI 的 glTF 包: " + sourcePath;
                return false;
            }

            if (facts.MissingUris != null && facts.MissingUris.Count > 0)
            {
                error = "glTF 缺必需伴生 × " + facts.MissingUris.Count + ": " + sourcePath;
                return false;
            }

            return true;
        }

        return true;
    }

    /// <summary>普通平铺且 Scan 到相对 URI 时的确认文案；无需提示则 null。</summary>
    public static string RelativeUriWarningIfSplit(
        FlattenBranch branch,
        FlattenSidecarFacts facts,
        string sourcePath)
    {
        if (branch != FlattenBranch.SplitDependencies ||
            facts == null ||
            !facts.HasExternalUris)
        {
            return null;
        }

        return "该 glTF 含相对 URI（.bin/外图）。普通平铺可能拆坏相对路径。" +
               "点「仍要平铺」继续（人责），点取消或关闭则中止: " +
               sourcePath;
    }

    public static FlattenPlan Create(
        string prefabPath,
        FlattenBranch branch,
        FlattenOperationPolicy policy,
        FlattenSidecarFacts facts,
        bool applyArtModelImporter)
    {
        policy = policy ?? FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual);
        var plan = new FlattenPlan();
        plan.SourcePrefabPath = prefabPath;
        plan.Branch = branch;
        plan.ApplyArtModelImporter = applyArtModelImporter;
        plan.OperationPolicy = policy;
        plan.ClearDestinationArtFolder = policy.ClearDestinationArtFolder;
        plan.ConvertZUpToYUp = false;

        if (branch == FlattenBranch.RelocateAtomic && facts != null)
        {
            plan.PrimaryAssetPath = facts.GltfAssetPath;
            if (facts.SidecarAssetPaths != null && facts.SidecarAssetPaths.Count > 0)
            {
                plan.SidecarPaths = new List<string>(facts.SidecarAssetPaths);
            }

            if (facts.MissingUris != null && facts.MissingUris.Count > 0)
            {
                plan.MissingUris = new List<string>(facts.MissingUris);
            }
        }

        return plan;
    }

    /// <summary>按 Prefab 依赖推断是否跑 E：单 gltf/glb 跳过；其余与旧菜单 ctx=null 一样跑。</summary>
    public static bool InferApplyArtModelImporter(IList<string> modelPaths)
    {
        if (modelPaths == null || modelPaths.Count != 1)
        {
            return true;
        }

        string ext = Path.GetExtension(modelPaths[0]);
        return !string.Equals(ext, ".gltf", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(ext, ".glb", StringComparison.OrdinalIgnoreCase);
    }
}
