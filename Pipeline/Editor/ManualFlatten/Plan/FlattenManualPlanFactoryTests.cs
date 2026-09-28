#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class FlattenManualPlanFactoryTests
{
    private const string TestRootAssetPath = "Assets/__FlattenManualPlanTests";

    private static string TestRootFullPath
    {
        get { return Path.Combine(Application.dataPath, "__FlattenManualPlanTests"); }
    }

    [SetUp]
    public void SetUp()
    {
        FlattenManualPrompt.Confirm = null;
        DeleteTestFiles();
        Directory.CreateDirectory(TestRootFullPath);
    }

    [TearDown]
    public void TearDown()
    {
        FlattenManualPrompt.Confirm = null;
        DeleteTestFiles();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    [Test]
    public void Split_ExternalUris_DoesNotReject_GivesWarning()
    {
        string gltf = WriteGltfWithMissingBin();
        FlattenSidecarFacts facts = FlattenSidecarFacts.FromGltfAsset(gltf);

        Assert.That(facts.HasExternalUris, Is.True);
        Assert.That(
            FlattenManualPlanFactory.TryValidate(
                FlattenBranch.SplitDependencies, facts, gltf, out string error),
            Is.True);
        Assert.That(error, Is.Null);

        string warn = FlattenManualPlanFactory.RelativeUriWarningIfSplit(
            FlattenBranch.SplitDependencies, facts, gltf);
        Assert.That(warn, Does.Contain("相对 URI"));

        FlattenPlan plan = FlattenManualPlanFactory.Create(
            "Assets/IncomingPrefab/probe.prefab",
            FlattenBranch.SplitDependencies,
            FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual),
            facts,
            false);
        Assert.That(plan.MissingUris, Is.Empty);
        Assert.That(plan.Branch, Is.EqualTo(FlattenBranch.SplitDependencies));
    }

    [Test]
    public void Atomic_MissingBin_FailsValidate()
    {
        string gltf = WriteGltfWithMissingBin();
        FlattenSidecarFacts facts = FlattenSidecarFacts.FromGltfAsset(gltf);

        Assert.That(
            FlattenManualPlanFactory.TryValidate(
                FlattenBranch.RelocateAtomic, facts, gltf, out string error),
            Is.False);
        Assert.That(error, Does.Contain("缺必需伴生"));
    }

    [Test]
    public void Atomic_EmbeddedDataUri_Rejected()
    {
        string gltfAssetPath = TestRootAssetPath + "/embedded.gltf";
        WriteText(
            "embedded.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"data:application/octet-stream;base64,AAAA\",\"byteLength\":4}]}");

        FlattenSidecarFacts facts = FlattenSidecarFacts.FromGltfAsset(gltfAssetPath);
        Assert.That(facts.HasExternalUris, Is.False);
        Assert.That(
            FlattenManualPlanFactory.TryValidate(
                FlattenBranch.RelocateAtomic, facts, gltfAssetPath, out _),
            Is.False);
    }

    [Test]
    public void Atomic_CompleteSidecar_CreatesRelocatePlan()
    {
        WriteBytes("data.bin", new byte[] { 1, 2, 3, 4 });
        string gltfAssetPath = TestRootAssetPath + "/complete.gltf";
        WriteText(
            "complete.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"data.bin\",\"byteLength\":4}]}");

        FlattenSidecarFacts facts = FlattenSidecarFacts.FromGltfAsset(gltfAssetPath);
        Assert.That(facts.HasExternalUris, Is.True);
        Assert.That(facts.MissingUris, Is.Empty);
        Assert.That(
            FlattenManualPlanFactory.TryValidate(
                FlattenBranch.RelocateAtomic, facts, gltfAssetPath, out _),
            Is.True);

        FlattenPlan plan = FlattenManualPlanFactory.Create(
            "Assets/IncomingPrefab/probe.prefab",
            FlattenBranch.RelocateAtomic,
            FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual),
            facts,
            false);
        Assert.That(plan.Branch, Is.EqualTo(FlattenBranch.RelocateAtomic));
        Assert.That(plan.PrimaryAssetPath, Is.EqualTo(gltfAssetPath));
        Assert.That(plan.SidecarPaths, Does.Contain(TestRootAssetPath + "/data.bin"));
    }

    [Test]
    public void MixedModels_AssignsRelativeUriStrategyOnlyToGltf()
    {
        WriteBytes("data.bin", new byte[] { 1, 2, 3, 4 });
        string gltf = TestRootAssetPath + "/complete.gltf";
        WriteText("complete.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"data.bin\",\"byteLength\":4}]}");
        string fbx = TestRootAssetPath + "/other.fbx";

        List<FlattenModelUnit> units = FlattenBuildService.BuildModelUnitsFromPaths(
            new[] { fbx, gltf, gltf });

        Assert.That(units, Has.Count.EqualTo(2));
        Assert.That(units.Find(x => x.ModelPath == fbx).Strategy,
            Is.EqualTo(FlattenModelCopyStrategy.Categorized));
        FlattenModelUnit gltfUnit = units.Find(x => x.ModelPath == gltf);
        Assert.That(gltfUnit.Strategy, Is.EqualTo(FlattenModelCopyStrategy.PreserveRelativeFiles));
        Assert.That(gltfUnit.SidecarPaths, Does.Contain(TestRootAssetPath + "/data.bin"));
        Assert.That(gltfUnit.MissingReferences, Is.Empty);
    }

    [Test]
    public void RelativeImageWithoutBin_StillPreservesModelPackage()
    {
        WriteBytes("albedo.png", new byte[] { 1, 2, 3, 4 });
        string gltf = TestRootAssetPath + "/image-only.gltf";
        WriteText("image-only.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"images\":[{\"uri\":\"albedo.png\"}]}");

        List<FlattenModelUnit> units = FlattenBuildService.BuildModelUnitsFromPaths(new[] { gltf });

        Assert.That(units, Has.Count.EqualTo(1));
        Assert.That(units[0].Strategy, Is.EqualTo(FlattenModelCopyStrategy.PreserveRelativeFiles));
        Assert.That(units[0].SidecarPaths, Does.Contain(TestRootAssetPath + "/albedo.png"));
    }

    [Test]
    public void EmbeddedGltf_UsesCategorizedStrategy()
    {
        string gltf = TestRootAssetPath + "/embedded.gltf";
        WriteText("embedded.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"data:application/octet-stream;base64,AQID\",\"byteLength\":3}]}");

        List<FlattenModelUnit> units = FlattenBuildService.BuildModelUnitsFromPaths(new[] { gltf });

        Assert.That(units, Has.Count.EqualTo(1));
        Assert.That(units[0].Strategy, Is.EqualTo(FlattenModelCopyStrategy.Categorized));
        Assert.That(units[0].SidecarPaths, Is.Empty);
    }

    [Test]
    public void RelativeFileOutsideModelDirectory_FailsBeforeArtIsCleared()
    {
        WriteBytes("data.bin", new byte[] { 1, 2, 3, 4 });
        string gltf = TestRootAssetPath + "/nested/outer.gltf";
        WriteText("nested/outer.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"../data.bin\",\"byteLength\":4}]}");

        List<FlattenModelUnit> units = FlattenBuildService.BuildModelUnitsFromPaths(new[] { gltf });

        Assert.That(units[0].Strategy, Is.EqualTo(FlattenModelCopyStrategy.PreserveRelativeFiles));
        Assert.That(units[0].MissingReferences, Has.Count.EqualTo(1));
        Assert.That(units[0].MissingReferences[0], Does.Contain("目录外"));
    }

    [Test]
    public void Prompt_Ok_Continues()
    {
        string captured = null;
        FlattenManualPrompt.Confirm = msg =>
        {
            captured = msg;
            return true;
        };
        Assert.That(FlattenManualPrompt.ConfirmSplitWithRelativeUris("warn-text"), Is.True);
        Assert.That(captured, Is.EqualTo("warn-text"));
    }

    [Test]
    public void Prompt_Cancel_Stops()
    {
        FlattenManualPrompt.Confirm = _ => false;
        Assert.That(FlattenManualPrompt.ConfirmSplitWithRelativeUris("warn-text"), Is.False);
    }

    [Test]
    public void Prompt_EmptyMessage_ContinuesWithoutCallback()
    {
        bool called = false;
        FlattenManualPrompt.Confirm = _ =>
        {
            called = true;
            return false;
        };
        Assert.That(FlattenManualPrompt.ConfirmSplitWithRelativeUris(null), Is.True);
        Assert.That(FlattenManualPrompt.ConfirmSplitWithRelativeUris(string.Empty), Is.True);
        Assert.That(called, Is.False);
    }

    [Test]
    public void Run_SplitPlanWithMissingUrisEmpty_DoesNotUseCtx()
    {
        FlattenPlan plan = FlattenManualPlanFactory.Create(
            "Assets/__FlattenPlanProbe/missing.prefab",
            FlattenBranch.SplitDependencies,
            FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual),
            null,
            false);
        LogAssert.Expect(LogType.Warning, new Regex("Begin|no renderers|Renderer"));
        FlattenRowResult row = ToolFlattenApi.Run(plan);
        Assert.That(row.FailedStep, Is.EqualTo(FlattenStep.Begin));
    }

    private static string WriteGltfWithMissingBin()
    {
        string gltfAssetPath = TestRootAssetPath + "/missing.gltf";
        WriteText(
            "missing.gltf",
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"buffers\":[{\"uri\":\"missing.bin\",\"byteLength\":4}]}");
        return gltfAssetPath;
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
