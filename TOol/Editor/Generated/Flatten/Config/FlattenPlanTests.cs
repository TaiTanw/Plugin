#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

public class FlattenPlanTests
{
    [Test]
    public void Run_NullPlan_FailsAtBeginWithoutThrowing()
    {
        FlattenRowResult result = ToolFlattenApi.Run(null);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.FailedStep, Is.EqualTo(FlattenStep.Begin));
        Assert.That(result.ArtPrefabPath, Is.Null);
    }

    [Test]
    public void Run_FakePrefabPlan_CallsThroughAndFailsAtBegin()
    {
        var plan = new FlattenPlan
        {
            SourcePrefabPath = "Assets/__FlattenPlanProbe/missing.prefab",
            Branch = FlattenBranch.SplitDependencies,
            ApplyArtModelImporter = false,
            ClearDestinationArtFolder = false,
            ConvertZUpToYUp = false,
            OperationPolicy = FlattenOperationPolicy.CreateDefaults(FlattenSettingsScope.Manual)
        };

        FlattenRowResult result = ToolFlattenApi.Run(plan);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.FailedStep, Is.EqualTo(FlattenStep.Begin));
        Assert.That(result.SourcePrefabPath, Is.EqualTo(plan.SourcePrefabPath));
        Assert.That(result.Message, Does.Contain("Begin"));
    }

    [Test]
    public void Run_MissingUris_FailsBeforeBegin()
    {
        var plan = new FlattenPlan
        {
            SourcePrefabPath = "Assets/__FlattenPlanProbe/missing.prefab",
            Branch = FlattenBranch.RelocateAtomic,
            PrimaryAssetPath = "Assets/Incoming/probe.gltf",
            ApplyArtModelImporter = false
        };
        plan.MissingUris.Add("data.bin");

        FlattenRowResult result = ToolFlattenApi.Run(plan);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.FailedStep, Is.EqualTo(FlattenStep.MissingSidecars));
        Assert.That(result.Message, Does.Contain("缺 1"));
    }

    [Test]
    public void CreateOptionsFromPlan_MapsBranchWithoutCtx()
    {
        var policy = new FlattenOperationPolicy(
            "Assets/Plugin/Pipeline/ConfigData/Test.asset",
            FlattenSettingsScope.Pipeline,
            true,
            true,
            FlattenCategorySettings.CreateDefaults());
        var plan = new FlattenPlan
        {
            SourcePrefabPath = "Assets/IncomingPrefab/probe.prefab",
            Branch = FlattenBranch.RelocateAtomic,
            ApplyArtModelImporter = false,
            OperationPolicy = policy,
            ClearDestinationArtFolder = true,
            ConvertZUpToYUp = true,
            PrimaryAssetPath = "Assets/Incoming/probe.gltf",
            SidecarPaths = new List<string> { "Assets/Incoming/probe.bin" }
        };

        RetinarFlattenOptions options = FlattenBuildService.CreateOptionsFromPlan(plan);

        Assert.That(options.SkipDependencySplit, Is.True);
        Assert.That(options.PrimaryAssetPath, Is.EqualTo(plan.PrimaryAssetPath));
        Assert.That(options.SidecarPaths, Is.EquivalentTo(plan.SidecarPaths));
        Assert.That(options.SidecarPaths, Is.Not.SameAs(plan.SidecarPaths));
        Assert.That(options.OperationPolicy, Is.SameAs(policy));
        Assert.That(options.ClearDestinationArtFolder, Is.True);
        Assert.That(options.ConvertZUpToYUp, Is.True);
        Assert.That(options.AddBoxCollider, Is.True);
    }

    [Test]
    public void CreateOptionsFromPlan_SplitBranch_DoesNotSkipDependencySplit()
    {
        var plan = new FlattenPlan
        {
            SourcePrefabPath = "Assets/IncomingPrefab/probe.prefab",
            Branch = FlattenBranch.SplitDependencies
        };

        RetinarFlattenOptions options = FlattenBuildService.CreateOptionsFromPlan(plan);

        Assert.That(options.SkipDependencySplit, Is.False);
        Assert.That(options.SidecarPaths, Is.Empty);
    }

    [Test]
    public void FromContext_ThenRun_MissingUrisFailBeforeBegin()
    {
        var ctx = new PipelineJobContext
        {
            HasExternalUris = true,
            PrimaryAssetPath = "Assets/Incoming/probe.gltf",
            ImporterKind = PipelineImporterKind.ScriptedImporter
        };
        ctx.MissingUris.Add("data.bin");
        ctx.SidecarPaths.Add("Assets/Incoming/keep.bin");

        ToolFlattenRequest request = ToolFlattenRequest.ForPipeline();
        FlattenPlan plan = ToolFlattenApi.FromContext(
            ctx, request, "Assets/IncomingPrefab/probe.prefab");

        Assert.That(plan.Branch, Is.EqualTo(FlattenBranch.RelocateAtomic));
        Assert.That(plan.ApplyArtModelImporter, Is.False);
        Assert.That(plan.MissingUris, Is.EquivalentTo(new[] { "data.bin" }));
        Assert.That(plan.SidecarPaths, Is.EquivalentTo(new[] { "Assets/Incoming/keep.bin" }));
        Assert.That(plan.MissingUris, Is.Not.SameAs(ctx.MissingUris));

        FlattenRowResult row = ToolFlattenApi.Run(plan);
        Assert.That(row.Ok, Is.False);
        Assert.That(row.FailedStep, Is.EqualTo(FlattenStep.MissingSidecars));
        Assert.That(row.Message, Does.Contain("缺 1"));
    }

    [Test]
    public void FromContext_NullCtx_MatchesMenuCreateOptions()
    {
        ToolFlattenRequest request = ToolFlattenRequest.MenuDefault;
        FlattenPlan plan = ToolFlattenApi.FromContext(null, request, "Assets/IncomingPrefab/probe.prefab");
        RetinarFlattenOptions fromPlan = FlattenBuildService.CreateOptionsFromPlan(plan);
        RetinarFlattenOptions fromCtx = FlattenBuildService.CreateOptions(null, request);

        Assert.That(plan.Branch, Is.EqualTo(FlattenBranch.SplitDependencies));
        Assert.That(plan.ApplyArtModelImporter, Is.True);
        Assert.That(fromPlan.SkipDependencySplit, Is.EqualTo(fromCtx.SkipDependencySplit));
        Assert.That(fromPlan.ConvertZUpToYUp, Is.EqualTo(fromCtx.ConvertZUpToYUp));
        Assert.That(fromPlan.ClearDestinationArtFolder, Is.EqualTo(fromCtx.ClearDestinationArtFolder));
    }
}
#endif
