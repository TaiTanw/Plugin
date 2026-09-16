// =====================================================================================
// Pipeline — 错误码（与 CLI 退出码对齐草案）
// =====================================================================================

/// <summary>流程编排统一错误码。</summary>
public static class PipelineErrorCodes
{
    public const int Ok = 0;
    public const int BadArgs = 10;
    public const int ImportFailed = 20;
    public const int PrefabFailed = 30;
    public const int FlattenFailed = 40;
    /// <summary>④ leftover 外部 .fbm；报错但不停⑤⑥。</summary>
    public const int FlattenLeftoverFbm = 41;
    /// <summary>④ 贴图身份警告；报错但不停⑤⑥；槽位引用不清。</summary>
    public const int FlattenTextureIdentity = 42;
    public const int PostProcessFailed = 50;
    public const int AbFailed = 60;
    public const int LicenseOrEnv = 70;
    public const int Other = 80;
}
