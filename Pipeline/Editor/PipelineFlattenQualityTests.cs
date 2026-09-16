#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public class PipelineFlattenQualityTests
{
    [Test]
    public void LeftoverFbm_Sets41_KeepsRowOk()
    {
        var result = new PipelineResult();
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");

        PipelineFlattenQuality.Apply(result, row, 1);

        Assert.That(row.Ok, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenLeftoverFbm));
        Assert.That(result.Ok, Is.False);
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("leftover .fbm"));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("⑤⑥继续"));
    }

    [Test]
    public void TextureIdentity_Sets42_KeepsRowOk()
    {
        var result = new PipelineResult();
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.TextureIdentityWarnings.Add("slot MainTex still points outside");

        PipelineFlattenQuality.Apply(result, row, 2);

        Assert.That(row.Ok, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenTextureIdentity));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("贴图身份"));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("槽位引用未清"));
    }

    [Test]
    public void LeftoverWinsOverIdentityOnSameRow()
    {
        var result = new PipelineResult();
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");
        row.TextureIdentityWarnings.Add("identity");

        PipelineFlattenQuality.Apply(result, row, 1);

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenLeftoverFbm));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("identity"));
    }

    [Test]
    public void LeftoverOverlaysPriorIdentityCode()
    {
        var result = new PipelineResult();
        result.Fail(PipelineErrorCodes.FlattenTextureIdentity, "prior identity");
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");

        PipelineFlattenQuality.Apply(result, row, 2);

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenLeftoverFbm));
    }

    [Test]
    public void HardFlatten40_NotOverwrittenByQuality()
    {
        var result = new PipelineResult();
        result.Fail(PipelineErrorCodes.FlattenFailed, "missing uris");
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");
        row.TextureIdentityWarnings.Add("identity");

        PipelineFlattenQuality.Apply(result, row, 1);

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenFailed));
    }

    [Test]
    public void QualityCodes_CanEscalateTo50()
    {
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.Ok), Is.True);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.FlattenLeftoverFbm), Is.True);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.FlattenTextureIdentity), Is.True);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.FlattenFailed), Is.False);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.ImportFailed), Is.False);
    }
}
#endif
