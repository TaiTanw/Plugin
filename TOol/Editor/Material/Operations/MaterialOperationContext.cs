using System;

public struct MaterialOperationContext
{
    public readonly string AssetPath;
    public readonly MaterialProcessSettings Settings;
    public readonly Action<string, float> ReportSubProgress;

    public MaterialOperationContext(
        string assetPath,
        MaterialProcessSettings settings,
        Action<string, float> reportSubProgress)
    {
        AssetPath = assetPath;
        Settings = settings;
        ReportSubProgress = reportSubProgress;
    }
}
