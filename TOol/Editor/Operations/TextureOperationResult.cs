// =====================================================================================
// 职责边界：
//   只是一个返回值载体。存在的意义是让每个操作都能明确表达三种结局，
//   而不是靠 bool 加日志去猜——"跳过"和"失败"必须分开，否则批量执行完
//   看到一堆 false 根本分不清是"这文件本来就不用处理"还是"处理坏了"。
// =====================================================================================
public enum TextureOperationStatus
{
    /// <summary>不需要处理，本来就符合要求。汇总时不算问题。</summary>
    Skipped,

    /// <summary>实际改动了文件。</summary>
    Changed,

    /// <summary>尝试处理但失败了。汇总时会被单独列出来，需要人工介入。</summary>
    Failed
}

public struct TextureOperationResult
{
    public readonly TextureOperationStatus Status;
    public readonly string Message;

    private TextureOperationResult(TextureOperationStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public static TextureOperationResult Skipped(string message)
    {
        return new TextureOperationResult(TextureOperationStatus.Skipped, message);
    }

    public static TextureOperationResult Changed(string message)
    {
        return new TextureOperationResult(TextureOperationStatus.Changed, message);
    }

    public static TextureOperationResult Failed(string message)
    {
        return new TextureOperationResult(TextureOperationStatus.Failed, message);
    }
}
