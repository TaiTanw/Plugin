using System.Collections.Generic;

// =====================================================================================
// Shared / Api — ③ Prefab 窄口
// =====================================================================================

/// <summary>插件 2 · 自动化 Prefab 对外接口。</summary>
public static class ToolPrefabApi
{
    /// <summary>
    /// 已导入模型 → 独立 Prefab 写盘（默认 <c>Assets/IncomingPrefab/</c>）。
    /// </summary>
    /// <param name="sourceModelPaths">Assets 下 .fbx/.glb/.gltf/.obj</param>
    /// <param name="materialId">非空覆盖三层命名</param>
    /// <returns>成功写出的 Prefab 路径</returns>
    public static List<string> BuildPrefabs(IList<string> sourceModelPaths, string materialId = null)
    {
        return PrefabBuildService.BuildPrefabsFromModels(sourceModelPaths, materialId);
    }

    /// <summary>Prefab 落盘根目录。</summary>
    public static string PrefabRoot
    {
        get { return PrefabIncomingPaths.PrefabRoot; }
    }
}
