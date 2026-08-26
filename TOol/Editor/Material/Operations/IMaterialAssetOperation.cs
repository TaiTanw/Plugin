// =====================================================================================
// 材质后处理扩展点。新增 Op：实现本接口 + 无参构造，放进 Material/Operations。
// =====================================================================================

public interface IMaterialAssetOperation
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    int Order { get; }

    AssetOperationEvaluation Evaluate(string assetPath, MaterialProcessSettings settings);

    bool CanProcess(string assetPath, MaterialProcessSettings settings);

    MaterialOperationResult Execute(MaterialOperationContext context);
}
