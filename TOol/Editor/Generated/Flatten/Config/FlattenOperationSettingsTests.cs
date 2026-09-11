#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class FlattenOperationSettingsTests
{
    [Test]
    public void DefaultAssets_AreSeparatedByFolderScope()
    {
        FlattenOperationSettings manual = FlattenOperationSettings.LoadManualOrDefaults();
        FlattenOperationSettings pipeline = FlattenOperationSettings.LoadPipelineOrDefaults();

        Assert.That(manual, Is.Not.Null);
        Assert.That(pipeline, Is.Not.Null);
        Assert.That(FlattenOperationSettings.GetScope(manual), Is.EqualTo(FlattenSettingsScope.Manual));
        Assert.That(FlattenOperationSettings.GetScope(pipeline), Is.EqualTo(FlattenSettingsScope.Pipeline));
        Assert.That(manual.ClearDestinationArtFolder, Is.False);
        Assert.That(pipeline.ClearDestinationArtFolder, Is.True);
    }

    [Test]
    public void CreatePolicy_FreezesValuesAndCategoryRules()
    {
        FlattenOperationSettings settings = ScriptableObject.CreateInstance<FlattenOperationSettings>();
        try
        {
            settings.ClearDestinationArtFolder = true;
            settings.AddBoxCollider = true;
            settings.SetCategory("Model", false, "fbx,obj");

            FlattenOperationPolicy snapshot = settings.CreatePolicy();
            settings.ClearDestinationArtFolder = false;
            settings.AddBoxCollider = false;
            settings.SetCategory("Model", true, "glb");

            Assert.That(snapshot.ClearDestinationArtFolder, Is.True);
            Assert.That(snapshot.AddBoxCollider, Is.True);
            Assert.That(snapshot.Categories.IsEnabled("Model"), Is.False);
            Assert.That(snapshot.Categories.GetSuffixes("Model", new[] { "fallback" }),
                Is.EquivalentTo(new[] { "fbx", "obj" }));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void ResolveRelativeFolder_UsesSuppliedSnapshot()
    {
        FlattenOperationSettings enabledSettings = ScriptableObject.CreateInstance<FlattenOperationSettings>();
        FlattenOperationSettings disabledSettings = ScriptableObject.CreateInstance<FlattenOperationSettings>();
        try
        {
            enabledSettings.SetCategory("Model", true, "fbx");
            disabledSettings.SetCategory("Model", false, "fbx");

            string enabled = FlattenCopyRunner.ResolveRelativeFolder(
                "Assets/Test/model.fbx", enabledSettings.CreatePolicy());
            string disabled = FlattenCopyRunner.ResolveRelativeFolder(
                "Assets/Test/model.fbx", disabledSettings.CreatePolicy());

            Assert.That(enabled, Is.EqualTo(ModelFlattenProcessor.ProcessorId));
            Assert.That(disabled, Is.EqualTo(UnknownFlattenProcessor.ProcessorId));
        }
        finally
        {
            Object.DestroyImmediate(enabledSettings);
            Object.DestroyImmediate(disabledSettings);
        }
    }

    [Test]
    public void CreateOptions_MapsOperationPolicyWithoutReadingPrefs()
    {
        var policy = new FlattenOperationPolicy(
            "Assets/Plugin/Pipeline/ConfigData/Test.asset",
            FlattenSettingsScope.Pipeline,
            true,
            true,
            FlattenCategorySettings.CreateDefaults());
        ToolFlattenRequest request = ToolFlattenRequest.ForPipeline(policy);
        request.ConvertZUpToYUp = true;

        RetinarFlattenOptions options = FlattenBuildService.CreateOptions(null, request);

        Assert.That(options.OperationPolicy, Is.SameAs(policy));
        Assert.That(options.ClearDestinationArtFolder, Is.True);
        Assert.That(options.AddBoxCollider, Is.True);
        Assert.That(options.ConvertZUpToYUp, Is.True);
    }

    [Test]
    public void ManualSplit_RejectsExternalUriPackage()
    {
        var ctx = new PipelineJobContext { HasExternalUris = true };

        bool accepted = ManualFlattenService.ValidateBranch(
            ctx, "Assets/Test/model.gltf", ManualFlattenMode.SplitDependencies, out string error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("原子迁移"));
    }

    [Test]
    public void ManualAtomic_RequiresExternalUrisAndCompleteSidecars()
    {
        var embedded = new PipelineJobContext { HasExternalUris = false };
        Assert.That(
            ManualFlattenService.ValidateBranch(
                embedded, "embedded.gltf", ManualFlattenMode.RelocateAtomic, out _),
            Is.False);

        var missing = new PipelineJobContext { HasExternalUris = true };
        missing.MissingUris.Add("data.bin");
        Assert.That(
            ManualFlattenService.ValidateBranch(
                missing, "missing.gltf", ManualFlattenMode.RelocateAtomic, out _),
            Is.False);

        var complete = new PipelineJobContext { HasExternalUris = true };
        Assert.That(
            ManualFlattenService.ValidateBranch(
                complete, "complete.gltf", ManualFlattenMode.RelocateAtomic, out _),
            Is.True);
    }
}
#endif
