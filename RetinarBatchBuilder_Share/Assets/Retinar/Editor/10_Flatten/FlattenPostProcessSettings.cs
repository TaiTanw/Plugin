using UnityEditor;

// =====================================================================================
// 平铺后处理开关。分类面板已去掉勾选；默认关（适配自动化只出 AB）。
// 若需加碰撞体：EditorPrefs 键 Retinar.Flatten.AddBoxCollider = true，或日后挂管线 Options。
// =====================================================================================

/// <summary>平铺后处理本机开关。</summary>
public static class FlattenPostProcessSettings
{
    private const string AddBoxColliderKey = "Retinar.Flatten.AddBoxCollider";

    /// <summary>默认关。为 true 时平铺加根 BoxCollider，规范化导出也会校验碰撞体。</summary>
    public static bool AddBoxCollider
    {
        get { return EditorPrefs.GetBool(AddBoxColliderKey, false); }
        set { EditorPrefs.SetBool(AddBoxColliderKey, value); }
    }
}
