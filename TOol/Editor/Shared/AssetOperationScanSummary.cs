using System.Collections.Generic;

/// <summary>仅扫描（dry-run）汇总：不改文件，只收集 Evaluate.NeedsWork 命中项。</summary>
public class AssetOperationScanSummary
{
    public int NeedsWorkCount;
    public int SkippedCount;
    public int NotApplicableCount;
    public bool Canceled;

    public readonly List<string> NeedsWorkLines = new List<string>();
    public readonly List<string> SkippedLines = new List<string>();

    public bool HasNeedsWork
    {
        get { return NeedsWorkCount > 0; }
    }
}
