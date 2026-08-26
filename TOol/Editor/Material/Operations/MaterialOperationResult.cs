public enum MaterialOperationStatus
{
    Skipped,
    Changed,
    Failed
}

public struct MaterialOperationResult
{
    public readonly MaterialOperationStatus Status;
    public readonly string Message;

    private MaterialOperationResult(MaterialOperationStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public static MaterialOperationResult Skipped(string message)
    {
        return new MaterialOperationResult(MaterialOperationStatus.Skipped, message);
    }

    public static MaterialOperationResult Changed(string message)
    {
        return new MaterialOperationResult(MaterialOperationStatus.Changed, message);
    }

    public static MaterialOperationResult Failed(string message)
    {
        return new MaterialOperationResult(MaterialOperationStatus.Failed, message);
    }
}
