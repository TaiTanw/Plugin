using System.Collections.Generic;

public class MaterialOperationRunSummary
{
    public int ChangedCount;
    public int SkippedCount;
    public int FailedCount;
    public bool Canceled;
    public readonly List<string> ChangedLines = new List<string>();
    public readonly List<string> FailedLines = new List<string>();
    public readonly List<string> SkippedLines = new List<string>();
}
