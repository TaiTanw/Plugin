#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PipelineGltfUriProbeTests
{
    private const string TestRootAssetPath = "Assets/__D26_2_GltfUriProbeTests";

    private static string TestRootFullPath
    {
        get { return Path.Combine(Application.dataPath, "__D26_2_GltfUriProbeTests"); }
    }

    [SetUp]
    public void SetUp()
    {
        DeleteTestFiles();
        Directory.CreateDirectory(TestRootFullPath);
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTestFiles();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    [Test]
    public void Apply_MissingUriWritesTypedFactAndKeepsDisplayWarning()
    {
        string gltfAssetPath = TestRootAssetPath + "/missing.gltf";
        WriteText(
            "missing.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"missing.bin\",\"byteLength\":4}]}");

        var ctx = new PipelineJobContext { PrimaryAssetPath = gltfAssetPath };
        PipelineGltfUriProbe.Apply(ctx);

        Assert.That(ctx.HasExternalUris, Is.True);
        Assert.That(ctx.MissingUris, Is.EquivalentTo(new[] { "missing.bin" }));
        Assert.That(ctx.Warnings, Does.Contain("缺伴生: missing.bin"));
        Assert.That(FlattenBuildService.HasMissingSidecars(ctx), Is.True);

        RetinarFlattenOptions options = FlattenBuildService.CreateOptions(ctx, null);
        Assert.That(options.SkipDependencySplit, Is.True);
        Assert.That(options.MissingUris, Is.EquivalentTo(ctx.MissingUris));
        Assert.That(options.MissingUris, Is.Not.SameAs(ctx.MissingUris));
    }

    [Test]
    public void Apply_AllDeclaredSidecarsExist_LeavesTypedMissingListEmpty()
    {
        string gltfAssetPath = TestRootAssetPath + "/complete.gltf";
        WriteBytes("data/model.bin", new byte[] { 1, 2, 3, 4 });
        WriteBytes("textures/albedo.png", new byte[] { 5, 6, 7, 8 });
        WriteText(
            "complete.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"data/model.bin\",\"byteLength\":4}]," +
            "\"images\":[{\"uri\":\"textures/albedo.png\"}]}");

        var ctx = new PipelineJobContext { PrimaryAssetPath = gltfAssetPath };
        PipelineGltfUriProbe.Apply(ctx);

        Assert.That(ctx.HasExternalUris, Is.True);
        Assert.That(ctx.MissingUris, Is.Empty);
        Assert.That(ctx.SidecarPaths, Is.EquivalentTo(new[]
        {
            TestRootAssetPath + "/data/model.bin",
            TestRootAssetPath + "/textures/albedo.png"
        }));
        Assert.That(ctx.Warnings.Exists(w => w.StartsWith("缺伴生:")), Is.False);
        Assert.That(FlattenBuildService.HasMissingSidecars(ctx), Is.False);
    }

    [Test]
    public void MissingGate_UsesTypedListAndNeverWarningText()
    {
        var warningOnly = new PipelineJobContext { HasExternalUris = true };
        warningOnly.Warnings.Add("缺伴生: warning-only.bin");

        Assert.That(FlattenBuildService.HasMissingSidecars(warningOnly), Is.False);
        Assert.That(
            FlattenBuildService.CreateOptions(warningOnly, null).MissingUris,
            Is.Empty);

        var typedOnly = new PipelineJobContext { HasExternalUris = true };
        typedOnly.MissingUris.Add("typed-only.bin");

        Assert.That(FlattenBuildService.HasMissingSidecars(typedOnly), Is.True);
        Assert.That(
            FlattenBuildService.CreateOptions(typedOnly, null).MissingUris,
            Is.EquivalentTo(new[] { "typed-only.bin" }));
    }

    private static void WriteText(string relativePath, string contents)
    {
        string fullPath = Path.Combine(TestRootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, contents);
    }

    private static void WriteBytes(string relativePath, byte[] contents)
    {
        string fullPath = Path.Combine(TestRootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, contents);
    }

    private static void DeleteTestFiles()
    {
        if (Directory.Exists(TestRootFullPath))
        {
            Directory.Delete(TestRootFullPath, true);
        }

        string metaPath = TestRootFullPath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }
}
#endif
