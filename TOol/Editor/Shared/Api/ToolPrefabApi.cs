using System.Collections.Generic;

// =====================================================================================
// Shared / Api — ③ Prefab 窄口
// =====================================================================================

/// <summary>插件 2 · 自动化 Prefab 对外接口。</summary>
public static class ToolPrefabApi
{
    /// <summary>
    /// 已导入模型 → 独立 Prefab 写盘（空根则选择器 SO 人工 Prefab 根）。
    /// </summary>
    /// <param name="sourceModelPaths">Assets 下 .fbx/.glb/.gltf/.obj</param>
    /// <param name="materialId">非空覆盖三层命名</param>
    /// <returns>成功写出的 Prefab 路径</returns>
    /// <param name="prefabRoot">非空则写此根（编排传入）；空则选择器 SO 人工 Prefab 根。</param>
    /// <param name="importRoot">非空则按此 Incoming 取夹名；空则读批量选择器导入根。</param>
    public static List<string> BuildPrefabs(
        IList<string> sourceModelPaths,
        string materialId = null,
        string prefabRoot = null,
        string importRoot = null)
    {
        return PrefabBuildService.BuildPrefabsFromModels(
            sourceModelPaths, materialId, prefabRoot, importRoot);
    }

    /// <summary>Prefab 落盘根目录。</summary>
    public static string PrefabRoot
    {
        get { return PrefabIncomingPaths.PrefabRoot; }
    }
}
