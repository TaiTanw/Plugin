// =====================================================================================
// 模型后处理扩展点。新增操作：实现本接口 + 无参构造，放进 Model/Operations。
// =====================================================================================
public interface IModelAssetOperation
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    int Order { get; }
    bool CanProcess(string assetPath, ModelProcessSettings settings);
    ModelOperationResult Execute(ModelOperationContext context);
}
