#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public class FlattenCopyRunnerTests
{
    [Test]
    public void AppendSourceDisambiguation_NestsFbmParentAlways()
    {
        const string source = "Assets/Incoming/Child.fbm/body.png";
        Assert.That(
            FlattenCopyRunner.AppendSourceDisambiguation("image/Texture", source, false),
            Is.EqualTo("image/Texture/Child.fbm/body.png"));
        Assert.That(
            FlattenCopyRunner.AppendSourceDisambiguation("image/Texture", source, true),
            Is.EqualTo("image/Texture/Child.fbm/body.png"));
    }

    [Test]
    public void AppendSourceDisambiguation_KeepsCategoryFilename_WhenParentIsCategoryLeaf()
    {
        const string source = "Assets/Incoming/Texture/body.png";
        Assert.That(
            FlattenCopyRunner.AppendSourceDisambiguation("image/Texture", source, false),
            Is.EqualTo("image/Texture/body.png"));
        Assert.That(
            FlattenCopyRunner.AppendSourceDisambiguation("image/Texture", source, true),
            Is.EqualTo("image/Texture/body.png"));
    }

    [Test]
    public void AppendSourceDisambiguation_NestsNonFbmParent_OnlyWhenFlatDestOccupied()
    {
        const string source = "Assets/Incoming/牵引关系/Text-1.png";
        Assert.That(
            FlattenCopyRunner.AppendSourceDisambiguation("image/UI", source, false),
            Is.EqualTo("image/UI/Text-1.png"));
        Assert.That(
            FlattenCopyRunner.AppendSourceDisambiguation("image/UI", source, true),
            Is.EqualTo("image/UI/牵引关系/Text-1.png"));
    }
}
#endif
