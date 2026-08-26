using System.Collections.Generic;

// =====================================================================================
// Pipeline — 总步骤配置（流程级；勿与 L1 ResourceProcess Prefs 混用）
// =====================================================================================

/// <summary>
/// 一次运行的选项。步骤开关默认来自 <see cref="PipelineStepSettings"/>（SO）；
/// 资源总面板的「设置自动」等仍用 EditorPrefs，本类不写入那些开关。
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>单文件源：工程外磁盘路径，或 Assets/ 下模型路径。</summary>
    public string SourcePath;

    /// <summary>② 导入（总调度 SO；默认见 PipelineStepSettings）。</summary>
    public bool RunImport = true;

    /// <summary>③ Prefab（默认开）。</summary>
    public bool RunPrefab = true;

    /// <summary>④ 平铺到 Art（默认关）。</summary>
    public bool RunFlatten;

    /// <summary>⑤ 资源后处理（默认关）。</summary>
    public bool RunPostProcess;

    /// <summary>⑥ 仅双端 AB（默认开）。</summary>
    public bool RunAb = true;

    /// <summary>禁止 DisplayDialog（编排默认 true）。</summary>
    public bool Quiet = true;

    /// <summary>导入后把所在夹写入 L1 批量路径（不改 L1 自动化 Prefs 开关）。</summary>
    public bool SyncImportFolderToResourcePanel = true;

    /// <summary>CLI/任务 Id；非空时覆盖 Prefab 三层命名。</summary>
    public string MaterialId;

    /// <summary>② 完成后的工程内模型路径（也可预填已导入模型）。</summary>
    public List<string> ModelPaths = new List<string>();

    /// <summary>
    /// 已有 Prefab。RunPrefab=false 且本列表非空时 ⑥ 直接用。
    /// </summary>
    public List<string> PrefabPaths = new List<string>();

    /// <summary>⑤ 扫描文件夹；null 则用 L1 Store。</summary>
    public List<string> PostProcessFolderPaths;

    /// <summary>从 SO 填充步骤开关，并带上单文件源。</summary>
    public static PipelineOptions FromSettings(PipelineStepSettings settings, string sourcePath = null)
    {
        var opt = new PipelineOptions();
        if (settings != null)
        {
            settings.ApplyTo(opt);
        }

        opt.SourcePath = sourcePath;
        return opt;
    }
}
