#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public class PipelineCliPackTests
{
    [Test]
    public void TryParseArgs_AcceptsUnityPackage_ModelTableUnchanged()
    {
        bool ok = PipelineCli.TryParseArgs(
            new[] { "Unity.exe", "-source", @"D:\art\hero.unitypackage", "-materialId", "PackA" },
            out string source,
            out string materialId,
            out string error);

        Assert.That(ok, Is.True, error);
        Assert.That(error, Is.Null);
        Assert.That(source.Replace("\\", "/"), Does.EndWith("hero.unitypackage"));
        Assert.That(materialId, Is.EqualTo("PackA"));
        Assert.That(
            ToolImportApi.GetSupportedModelExtensions(),
            Is.EqualTo(new[] { ".fbx", ".glb", ".gltf", ".obj" }));
        Assert.That(ToolImportApi.IsSupportedModelPath(source), Is.False);
        Assert.That(ToolImportApi.IsUnityPackagePath(source), Is.True);
    }
}
#endif
