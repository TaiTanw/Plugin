// =====================================================================================
// 旧兼容壳。新代码应从 FlattenOperationSettings SO 冻结到 RetinarFlattenOptions。
// =====================================================================================

/// <summary>旧 API 兼容；仅代理人工 SO，不再读写 EditorPrefs。</summary>
public static class FlattenPostProcessSettings
{
    /// <summary>默认关。仅供仓外旧调用；管线不会读取本属性。</summary>
    public static bool AddBoxCollider
    {
        get
        {
            FlattenOperationSettings settings = FlattenOperationSettings.LoadManualOrDefaults();
            return settings != null && settings.AddBoxCollider;
        }
        set
        {
            FlattenOperationSettings settings = FlattenOperationSettings.GetOrCreateManualAsset();
            settings.AddBoxCollider = value;
            UnityEditor.EditorUtility.SetDirty(settings);
        }
    }
}
