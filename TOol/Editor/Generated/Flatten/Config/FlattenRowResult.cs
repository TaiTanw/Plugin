using System.Collections.Generic;

// =====================================================================================
// Generated / Flatten / Config
// 中文：④ 一行 Run(plan) 的结果。步骤 9–10：拷贝计数、Extract 次数、残留 .fbm、未绑贴图槽。不改失败闸。
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
    public int CopiedDependencyCount;
    public int ExtractTexturesCallCount;
    public readonly List<string> LeftoverExternalFbm = new List<string>();
    public readonly List<string> UnboundTextureSlots = new List<string>();
    /// <summary>步骤 11 身份警告；不改变 Ok / FailedStep，批量继续。</summary>
    public readonly List<string> TextureIdentityWarnings = new List<string>();

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
