using System.Collections.Generic;

// =====================================================================================
// Pipeline — 总步骤配置（流程级；勿与 L1 ResourceProcess Prefs 混用）
// =====================================================================================

/// <summary>
/// 一次运行的选项。步骤开关默认来自 <see cref="PipelineStepSettings"/>（SO）；
/// 平铺详细配置从 SO 冻结到 <see cref="FlattenPolicy"/>；运行中不再读 EditorPrefs。
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

    /// <summary>⑥ 是否导出（步骤开关）。产物种类/路径在 <see cref="AbBuildOptions"/> / 导出 SO。</summary>
    public bool RunAb = true;

    /// <summary>本次从导出 SO 快照的是否打 UP；⑥ 执行以 <see cref="AbBuildOptions"/> 为准。</summary>
    public bool ExportUnityPackage;

    /// <summary>⑥ 详细出包选项（输出根等）。</summary>
    public RetinarAbBuildOptions AbBuildOptions;

    /// <summary>禁止 DisplayDialog（编排默认 true）。</summary>
    public bool Quiet = true;

    /// <summary>④开始前从管线平铺 SO 冻结的本趟配置。</summary>
    public FlattenOperationPolicy FlattenPolicy =
        FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Pipeline);

    /// <summary>CLI/任务 Id；非空时 1 入库夹名与 ③ Prefab 名都用它（D10-1）。</summary>
    public string MaterialId;

    /// <summary>
    /// 多源 + 各自 ID2。Runner 主输入；空则用 SourcePath+MaterialId 合成一行。
    /// </summary>
    public List<PipelineSourceBinding> SourceBindings;

    /// <summary>1 完成后的工程内模型路径（也可预填已导入模型）。</summary>
    public List<string> ModelPaths = new List<string>();

    /// <summary>
    /// 已有 Prefab。RunPrefab=false 且本列表非空时 ⑥ 直接用。
    /// </summary>
    public List<string> PrefabPaths = new List<string>();

    /// <summary>⑤ 扫描文件夹；null 则用 L1 Store。</summary>
    public List<string> PostProcessFolderPaths;

    /// <summary>
    /// 第一份 2.5 ctx（兼容旧读取）。完整列表见 <see cref="JobContexts"/>。
    /// </summary>
    public PipelineJobContext JobContext;

    /// <summary>每份入库模型一份 2.5 ctx，与 ModelPaths 下标对齐。④ 按行映射闸。</summary>
    public List<PipelineJobContext> JobContexts;

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
