using UnityEditor;

// =====================================================================================
// 平铺后处理开关（后续会做成调度层）。本期只有「是否加根 BoxCollider」。
// 业务验收门禁 / 输出勾选见 30_Business（SO 接口已留，本期不接线）。
// =====================================================================================

/// <summary>平铺后处理本机开关。</summary>
public static class FlattenPostProcessSettings
{
    private const string AddBoxColliderKey = "Retinar.Flatten.AddBoxCollider";

    /// <summary>默认开，保持旧 AR 交付行为。关掉则不加碰撞体，导出也不再要求有。</summary>
    public static bool AddBoxCollider
    {
        get { return EditorPrefs.GetBool(AddBoxColliderKey, true); }
        set { EditorPrefs.SetBool(AddBoxColliderKey, value); }
    }
}
