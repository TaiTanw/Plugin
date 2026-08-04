public enum ModelOperationStatus
{
    Skipped,
    Changed,
    Failed
}

public struct ModelOperationResult
{
    public readonly ModelOperationStatus Status;
    public readonly string Message;

    private ModelOperationResult(ModelOperationStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public static ModelOperationResult Skipped(string message)
    {
        return new ModelOperationResult(ModelOperationStatus.Skipped, message);
    }

    public static ModelOperationResult Changed(string message)
    {
        return new ModelOperationResult(ModelOperationStatus.Changed, message);
    }

    public static ModelOperationResult Failed(string message)
    {
        return new ModelOperationResult(ModelOperationStatus.Failed, message);
    }
}
