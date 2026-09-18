// =====================================================================================
// 薄转发。人工④调度在 Pipeline/Editor/ManualFlatten/Orchestration。
// =====================================================================================

/// <summary>兼容窗口/菜单调用。实现见 <see cref="ManualFlattenOrchestration"/>。</summary>
public static class ManualFlattenService
{
    public static ManualFlattenResult RunSelection(
        FlattenOperationSettings settings,
        ManualFlattenMode mode)
    {
        return ManualFlattenOrchestration.RunSelection(settings, mode);
    }
}
