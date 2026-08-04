using System.Collections.Generic;

public class ModelOperationRunSummary
{
    public int ChangedCount;
    public int SkippedCount;
    public int FailedCount;
    public bool Canceled;
    public readonly List<string> ChangedLines = new List<string>();
    public readonly List<string> FailedLines = new List<string>();

    public bool HasAnythingToReport
    {
        get { return ChangedCount > 0 || FailedCount > 0 || Canceled || SkippedCount > 0; }
    }
}
