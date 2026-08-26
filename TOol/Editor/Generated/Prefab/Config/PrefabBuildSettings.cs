using UnityEngine;

// =====================================================================================
// Generated / Prefab / Config
// 中文：③ 预设体制作 — 配置（路径根、是否强制 Unpack 等）。
// 层级：L-配置。不执行保存 Prefab。
// =====================================================================================

/// <summary>
/// 预设体制作配置（常量起步；后续可改为 ScriptableObject）。
/// </summary>
public static class PrefabBuildSettings
{
    /// <summary>默认把自动生成的 Prefab 放到此 Assets 根下（可改）。</summary>
    public const string DefaultPrefabRoot = "Assets/IncomingPrefab";

    /// <summary>
    /// 为 true 时：若源是嵌套 PrefabInstance / 指向 .glb，保存前应 Unpack 完全。
    /// 避免平铺阶段长期依赖「套壳指 GLB」。
    /// </summary>
    public const bool PreferUnpackCompletely = true;
}
