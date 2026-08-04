using System;

public struct ModelOperationContext
{
    public readonly string AssetPath;
    public readonly ModelProcessSettings Settings;
    public readonly bool TriggeredByImport;
    public readonly Action<float, string> ReportSubProgress;

    public ModelOperationContext(
        string assetPath,
        ModelProcessSettings settings,
        bool triggeredByImport,
        Action<float, string> reportSubProgress)
    {
        AssetPath = assetPath;
        Settings = settings;
        TriggeredByImport = triggeredByImport;
        ReportSubProgress = reportSubProgress ?? ((ratio, detail) => { });
    }
}
