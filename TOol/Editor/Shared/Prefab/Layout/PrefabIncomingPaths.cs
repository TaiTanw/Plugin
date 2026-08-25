using System.IO;

// =====================================================================================
// Shared / Prefab / Layout
// 中文：③ 预设体制作 — 专用夹路径约定（只拼路径，不写盘）。
// 层级：L-配置/路径。对齐 Retinar 侧 FlattenLayout「只回答路径」的风格。
// =====================================================================================

/// <summary>
/// 自动 Prefab 落盘路径。
/// </summary>
public static class PrefabIncomingPaths
{
    /// <summary>专用根目录（来自 PrefabBuildSettings）。</summary>
    public static string PrefabRoot
    {
        get { return PrefabBuildSettings.DefaultPrefabRoot.Replace("\\", "/").TrimEnd('/'); }
    }

    /// <summary>
    /// 由源模型资产路径推导目标 Prefab 路径，例如
    /// Assets/Incoming/Foo/Bar.glb → Assets/IncomingPrefab/Bar.prefab
    /// </summary>
    public static string PrefabPathForSourceModel(string sourceModelAssetPath)
    {
        string name = Path.GetFileNameWithoutExtension(sourceModelAssetPath ?? string.Empty);
        if (string.IsNullOrEmpty(name))
        {
            name = "Unnamed";
        }

        return PrefabRoot + "/" + name + ".prefab";
    }
}
