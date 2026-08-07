// =====================================================================================
// 模型后处理扩展点。新增操作：实现本接口 + 无参构造，放进 Model/Operations。
// Evaluate 与扫描 / Runner 共用；importRoot 供 OnPostprocessModel 时从层级取 Mesh。
// =====================================================================================
using UnityEngine;

public interface IModelAssetOperation
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    int Order { get; }

    /// <summary>
    /// 统一评估。importRoot 非空时（导入回调）可从层级收集 Mesh；手动/delayCall 传 null。
    /// </summary>
    AssetOperationEvaluation Evaluate(
        string assetPath,
        ModelProcessSettings settings,
        GameObject importRoot);

    /// <summary>等价于 Evaluate(path, settings, null).NeedsWork。</summary>
    bool CanProcess(string assetPath, ModelProcessSettings settings);

    ModelOperationResult Execute(ModelOperationContext context);
}
