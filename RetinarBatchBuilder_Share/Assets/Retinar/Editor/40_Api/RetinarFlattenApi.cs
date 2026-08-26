using System.Collections.Generic;

// =====================================================================================
// 40_Api — ④ 平铺窄口
// =====================================================================================

/// <summary>插件 1 · 平铺到 Art 对外接口。</summary>
public static class RetinarFlattenApi
{
    /// <summary>
    /// 按路径平铺到 Art。quiet=true 时无 DisplayDialog（编排默认）。
    /// </summary>
    /// <returns>成功平铺条数</returns>
    public static int FlattenPaths(IList<string> sourcePaths, bool quiet = true)
    {
        List<string> artPrefabPaths;
        return FlattenPaths(sourcePaths, quiet, out artPrefabPaths);
    }

    /// <summary>
    /// 平铺并返回 Art 下交付 Prefab 路径（供⑥使用）。
    /// </summary>
    public static int FlattenPaths(IList<string> sourcePaths, bool quiet, out List<string> artPrefabPaths)
    {
        List<string> unknownLines;
        return RetinarBatchModelBuilder.FlattenSourcePaths(sourcePaths, quiet, out unknownLines, out artPrefabPaths);
    }

    /// <summary>菜单兼容：选中项平铺（可弹窗）。</summary>
    public static void FlattenSelectedToArt()
    {
        RetinarFlattenScheduler.FlattenSelectedToArt();
    }
}
