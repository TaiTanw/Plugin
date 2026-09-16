// =====================================================================================
// Pipeline — ④ 质量码 41 leftover .fbm / 42 贴图身份。Fail 但不停后续步。
// =====================================================================================

/// <summary>
/// 内核 <see cref="FlattenRowResult.Ok"/> 仍为 true；编排在此记 4x 码。
/// 40 整趟停不走这里。50/60 可覆盖 41/42。
/// </summary>
public static class PipelineFlattenQuality
{
    public static bool IsQualityCode(int code)
    {
        return code == PipelineErrorCodes.FlattenLeftoverFbm
            || code == PipelineErrorCodes.FlattenTextureIdentity;
    }

    public static bool CanEscalateToPostProcess(int code)
    {
        return code == PipelineErrorCodes.Ok || IsQualityCode(code);
    }

    public static void Apply(PipelineResult result, FlattenRowResult row, int index)
    {
        if (result == null || row == null)
        {
            return;
        }

        if (row.LeftoverExternalFbm != null && row.LeftoverExternalFbm.Count > 0)
        {
            string msg = "[Pipeline] ④ [" + index + "] leftover .fbm × " +
                         row.LeftoverExternalFbm.Count + "（报错但不卡，⑤⑥继续）";
            for (int i = 0; i < row.LeftoverExternalFbm.Count; i++)
            {
                result.Info("  leftover: " + row.LeftoverExternalFbm[i]);
            }

            RecordQuality(
                result,
                PipelineErrorCodes.FlattenLeftoverFbm,
                msg);
        }

        if (row.TextureIdentityWarnings != null && row.TextureIdentityWarnings.Count > 0)
        {
            string msg = "[Pipeline] ④ [" + index + "] 贴图身份 × " +
                         row.TextureIdentityWarnings.Count +
                         "（报错但不卡，⑤⑥继续；槽位引用未清）";
            for (int i = 0; i < row.TextureIdentityWarnings.Count; i++)
            {
                result.Info("  identity: " + row.TextureIdentityWarnings[i]);
            }

            RecordQuality(
                result,
                PipelineErrorCodes.FlattenTextureIdentity,
                msg);
        }
    }

    static void RecordQuality(PipelineResult result, int code, string message)
    {
        int current = result.ExitCode;
        if (current == PipelineErrorCodes.Ok)
        {
            result.Fail(code, message);
            return;
        }

        if (code == PipelineErrorCodes.FlattenLeftoverFbm &&
            current == PipelineErrorCodes.FlattenTextureIdentity)
        {
            result.Fail(code, message);
            return;
        }

        result.Info(message);
    }
}
