using System.Collections.Generic;

// =====================================================================================
// Pipeline — 批量面板把「筛选完成」的路径+ID2 交给编排。不入库。
// =====================================================================================

/// <summary>
/// 编排接受批量选择结果的入口。
/// 列表非空即可；Conflict 只约束批量「执行导入」，不拦本事件。
/// </summary>
public static class PipelineSourceAccept
{
    /// <summary>打开编排面板并投递。筛选完成，尚未 1 入库。</summary>
    public static void SendToOrchestration(IList<PipelineSourceBinding> bindings)
    {
        PipelineWindow.AcceptBindings(bindings);
    }
}
