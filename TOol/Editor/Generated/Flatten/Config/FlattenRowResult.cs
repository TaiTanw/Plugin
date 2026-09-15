// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 一行 Run(plan) 的结果。步骤 1 只记成功/失败步/路径；拷贝计数、残留 .fbm 是步骤 9。
// =====================================================================================

/// <summary>Run(plan) 停在哪一步。None 表示跑完 Finish。</summary>
public enum FlattenStep
{
    None = 0,
    MissingSidecars = 1,
    Begin = 2,
    SplitDependencies = 3,
    RelocateAtomic = 4,
    ApplyImportAndExtract = 5,
    Remap = 6,
    CopyRendererMaterials = 7,
    Finish = 8
}

/// <summary>一行平铺结果。编排读本对象，不解析日志。</summary>
public sealed class FlattenRowResult
{
    public bool Ok;
    public FlattenStep FailedStep = FlattenStep.None;
    public string Message;
    public string SourcePrefabPath;
    public string ArtPrefabPath;

    public static FlattenRowResult Succeeded(string sourcePrefabPath, string artPrefabPath)
    {
        return new FlattenRowResult
        {
            Ok = true,
            FailedStep = FlattenStep.None,
            SourcePrefabPath = sourcePrefabPath,
            ArtPrefabPath = artPrefabPath
        };
    }

    public static FlattenRowResult Failed(
        FlattenStep step,
        string message,
        string sourcePrefabPath = null,
        string artPrefabPath = null)
    {
        return new FlattenRowResult
        {
            Ok = false,
            FailedStep = step,
            Message = message,
            SourcePrefabPath = sourcePrefabPath,
            ArtPrefabPath = artPrefabPath
        };
    }
}
