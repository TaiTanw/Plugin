using System;
using UnityEngine;

public struct ModelOperationContext
{
    public readonly string AssetPath;
    public readonly ModelProcessSettings Settings;
    public readonly bool TriggeredByImport;
    public readonly Action<float, string> ReportSubProgress;
    /// <summary>
    /// OnPostprocessModel 传入的导入根节点。此时 AssetDatabase.LoadAllAssetsAtPath 常仍为空，
    /// 必须从层级上的 MeshFilter / SkinnedMeshRenderer 取 Mesh。
    /// </summary>
    public readonly GameObject ImportRoot;

    public ModelOperationContext(
        string assetPath,
        ModelProcessSettings settings,
        bool triggeredByImport,
        Action<float, string> reportSubProgress)
        : this(assetPath, settings, triggeredByImport, reportSubProgress, null)
    {
    }

    public ModelOperationContext(
        string assetPath,
        ModelProcessSettings settings,
        bool triggeredByImport,
        Action<float, string> reportSubProgress,
        GameObject importRoot)
    {
        AssetPath = assetPath;
        Settings = settings;
        TriggeredByImport = triggeredByImport;
        ReportSubProgress = reportSubProgress ?? ((ratio, detail) => { });
        ImportRoot = importRoot;
    }
}
