#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

public class PipelinePreviewSourcesTests
{
    [Test]
    public void OpenFileFilter_IncludesUnityPackage_ModelTableUnchanged()
    {
        Assert.That(PipelinePreviewSources.OpenFileFilter, Does.Contain("unitypackage"));
        Assert.That(
            ToolImportApi.GetSupportedModelExtensions(),
            Is.EqualTo(new[] { ".fbx", ".glb", ".gltf", ".obj" }));
        Assert.That(ToolImportApi.IsSupportedModelPath("a.unitypackage"), Is.False);
        Assert.That(PipelinePreviewSources.IsAcceptedPath(@"D:\x\y.unitypackage"), Is.True);
        Assert.That(PipelinePreviewSources.IsAcceptedPath("a.fbx"), Is.True);
        Assert.That(PipelinePreviewSources.IsAcceptedPath("a.cs"), Is.False);
    }

    [Test]
    public void NormalizeTable_TwoPacks_KeepsFirstOnly()
    {
        var input = new List<PipelineSourceBinding>
        {
            new PipelineSourceBinding(@"D:\a.unitypackage", "A"),
            new PipelineSourceBinding(@"D:\b.unitypackage", "B")
        };

        List<PipelineSourceBinding> table = PipelinePreviewSources.NormalizeTable(input);

        Assert.That(table.Count, Is.EqualTo(1));
        Assert.That(table[0].SourcePath.Replace("\\", "/"), Does.EndWith("a.unitypackage"));
        Assert.That(table[0].MaterialId, Is.EqualTo("A"));
        Assert.That(PipelinePreviewSources.TableHasPack(table), Is.True);
    }

    [Test]
    public void NormalizeTable_PackAndModel_KeepsPackOnly()
    {
        var input = new List<PipelineSourceBinding>
        {
            new PipelineSourceBinding("hero.fbx", "Hero"),
            new PipelineSourceBinding("pack.unitypackage", "Pack")
        };

        List<PipelineSourceBinding> table = PipelinePreviewSources.NormalizeTable(input);

        Assert.That(table.Count, Is.EqualTo(1));
        Assert.That(table[0].SourcePath, Does.EndWith("pack.unitypackage"));
        Assert.That(table[0].MaterialId, Is.EqualTo("Pack"));
    }

    [Test]
    public void NormalizeTable_ModelsOnly_UnchangedCount()
    {
        var input = new List<PipelineSourceBinding>
        {
            new PipelineSourceBinding("a.fbx", "A"),
            new PipelineSourceBinding("b.obj", "B")
        };

        List<PipelineSourceBinding> table = PipelinePreviewSources.NormalizeTable(input);

        Assert.That(table.Count, Is.EqualTo(2));
        Assert.That(PipelinePreviewSources.TableHasPack(table), Is.False);
    }

    [Test]
    public void NormalizeTable_DropsUnsupported()
    {
        var input = new List<PipelineSourceBinding>
        {
            new PipelineSourceBinding("x.cs", "X"),
            new PipelineSourceBinding("y.fbx", "Y")
        };

        List<PipelineSourceBinding> table = PipelinePreviewSources.NormalizeTable(input);

        Assert.That(table.Count, Is.EqualTo(1));
        Assert.That(table[0].SourcePath, Does.EndWith("y.fbx"));
    }
}
#endif
