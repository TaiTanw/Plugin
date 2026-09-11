using System.Collections.Generic;

// =====================================================================================
// 40_Api — ④ 单份工作单。能力方法之间只传这个对象，不传 PipelineJobContext。
// =====================================================================================

/// <summary>
/// 一份源 Prefab 在平铺过程中的可变状态。B / B′ 交出的源→副本表也在这里。
/// </summary>
public sealed class RetinarFlattenWork
{
    public string SourcePath;
    public string SourceModelPath;
    public string AssetName;
    public string AssetFolder;
    public string PrefabPath;
    public Dictionary<string, string> CopiedDependencies;
    public RetinarFlattenOptions Options;
}
