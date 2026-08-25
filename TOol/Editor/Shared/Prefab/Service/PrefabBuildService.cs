using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Shared / Prefab / Service
// 中文：③ 预设体制作 — 执行层（收集已导入模型 → 生成独立 Prefab）。
// 层级：L-内核。不调用插件 1 平铺/导出。
//
// 后续填充：
//   - 从路径列表找 .fbx / .glb
//   - LoadMainAssetAsGameObject →（可选）Unpack → PrefabUtility.SaveAsPrefabAsset
//   - 确保 PrefabRoot 文件夹存在
// =====================================================================================

/// <summary>
/// 自动化预设体制作服务（骨架）。
/// </summary>
public static class PrefabBuildService
{
    /// <summary>
    /// 对给定工程内模型路径生成独立 Prefab。当前为占位：只校验路径并打日志，不写盘。
    /// </summary>
    /// <param name="sourceModelPaths">Assets 下 .fbx / .glb 等路径</param>
    /// <returns>计划生成的目标 Prefab 路径列表（占位阶段）</returns>
    public static List<string> BuildPrefabsFromModels(IList<string> sourceModelPaths)
    {
        var planned = new List<string>();
        if (sourceModelPaths == null)
        {
            return planned;
        }

        for (int i = 0; i < sourceModelPaths.Count; i++)
        {
            string source = (sourceModelPaths[i] ?? string.Empty).Replace("\\", "/");
            if (string.IsNullOrEmpty(source))
            {
                continue;
            }

            string target = PrefabIncomingPaths.PrefabPathForSourceModel(source);
            planned.Add(target);
            Debug.Log("[TOol][Prefab] 占位：将生成 " + target + " ← " + source +
                      "（尚未 SaveAsPrefabAsset，见 Shared/Prefab/README.md）");
        }

        return planned;
    }

    /// <summary>
    /// 确保专用 Prefab 根文件夹在 AssetDatabase 中存在。占位：仅拼路径，不 CreateFolder。
    /// </summary>
    public static string GetOrDescribePrefabRoot()
    {
        return PrefabIncomingPaths.PrefabRoot;
    }
}
