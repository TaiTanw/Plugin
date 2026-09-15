#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public class PipelineFlattenAlignmentTests
{
    [Test]
    public void Align_EqualCounts_PairsAll()
    {
        bool match = PipelineRunner.TryAlignFlattenRows(2, 2, out int paired, out string message);

        Assert.That(match, Is.True);
        Assert.That(paired, Is.EqualTo(2));
        Assert.That(message, Is.Null);
    }

    [Test]
    public void Align_TwoPrefabsOneContext_PairsFirstOnlyAndMismatch()
    {
        bool match = PipelineRunner.TryAlignFlattenRows(2, 1, out int paired, out string message);

        Assert.That(match, Is.False);
        Assert.That(paired, Is.EqualTo(1));
        Assert.That(message, Does.Contain("prefab=2"));
        Assert.That(message, Does.Contain("ctx=1"));
    }

    [Test]
    public void Align_EmptyContexts_DoesNotInventFirstJobContext()
    {
        bool match = PipelineRunner.TryAlignFlattenRows(3, 0, out int paired, out string message);

        Assert.That(match, Is.False);
        Assert.That(paired, Is.EqualTo(0));
        Assert.That(message, Does.Contain("prefab=3"));
        Assert.That(message, Does.Contain("ctx=0"));
    }
}
#endif
