using System.Collections.Generic;
using System.Text;

// =====================================================================================
// Pipeline — 一次运行结果
// =====================================================================================

/// <summary>流程编排运行摘要。</summary>
public sealed class PipelineResult
{
    public int ExitCode = PipelineErrorCodes.Ok;
    public readonly List<string> PrefabOutputs = new List<string>();
    public readonly List<string> AbOutputs = new List<string>();
    public readonly List<string> Messages = new List<string>();

    public bool Ok
    {
        get { return ExitCode == PipelineErrorCodes.Ok; }
    }

    public void Fail(int code, string message)
    {
        ExitCode = code;
        if (!string.IsNullOrEmpty(message))
        {
            Messages.Add(message);
        }
    }

    public void Info(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Messages.Add(message);
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Pipeline] exit=" + ExitCode + (Ok ? " OK" : " FAIL"));
        for (int i = 0; i < Messages.Count; i++)
        {
            sb.AppendLine(Messages[i]);
        }

        return sb.ToString().TrimEnd();
    }
}
