// =====================================================================================
// Op 统一评估结果：扫描 dry-run 与 Runner 筛工作项共用，避免 CanProcess / Execute 两套口径。
// =====================================================================================
public enum AssetOperationEligibility
{
    /// <summary>类型/扩展名等不适用（扫描与执行都忽略）。</summary>
    NotApplicable = 0,

    /// <summary>适用但当前无需改（已达标、被策略跳过如 .fbm 等）。</summary>
    Skip = 1,

    /// <summary>适用且需要执行（扫描命中；Runner 纳入待处理）。</summary>
    NeedsWork = 2
}

public struct AssetOperationEvaluation
{
    public AssetOperationEligibility Eligibility;
    public string Reason;

    public bool NeedsWork
    {
        get { return Eligibility == AssetOperationEligibility.NeedsWork; }
    }

    public bool IsApplicable
    {
        get { return Eligibility != AssetOperationEligibility.NotApplicable; }
    }

    public static AssetOperationEvaluation NotApplicable(string reason)
    {
        return new AssetOperationEvaluation
        {
            Eligibility = AssetOperationEligibility.NotApplicable,
            Reason = reason ?? string.Empty
        };
    }

    public static AssetOperationEvaluation Skip(string reason)
    {
        return new AssetOperationEvaluation
        {
            Eligibility = AssetOperationEligibility.Skip,
            Reason = reason ?? string.Empty
        };
    }

    public static AssetOperationEvaluation NeedsWorkResult(string reason)
    {
        return new AssetOperationEvaluation
        {
            Eligibility = AssetOperationEligibility.NeedsWork,
            Reason = reason ?? string.Empty
        };
    }
}
