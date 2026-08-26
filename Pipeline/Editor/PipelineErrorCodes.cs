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
    public const int PostProcessFailed = 50;
    public const int AbFailed = 60;
    public const int LicenseOrEnv = 70;
    public const int Other = 80;
}
