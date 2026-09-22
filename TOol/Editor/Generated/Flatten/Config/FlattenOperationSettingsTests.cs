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
        Assert.That(manual.CreatePolicy().StripMissingScripts, Is.False);
        Assert.That(pipeline.CreatePolicy().StripMissingScripts, Is.True);
        bool previous = pipeline.StripMissingScripts;
        try
        {
            pipeline.StripMissingScripts = false;
            Assert.That(pipeline.CreatePolicy().StripMissingScripts, Is.True);
        }
        finally
        {
            pipeline.StripMissingScripts = previous;
        }
    }

    [Test]
    public void CreatePolicy_FreezesValuesAndCategoryRules()
    {
        FlattenOperationSettings settings = ScriptableObject.CreateInstance<FlattenOperationSettings>();
        try
        {
            settings.ClearDestinationArtFolder = true;
            settings.AddBoxCollider = true;
            settings.StripMissingScripts = true;
            settings.SetCategory("Model", false, "fbx,obj");

            FlattenOperationPolicy snapshot = settings.CreatePolicy();
            settings.ClearDestinationArtFolder = false;
            settings.AddBoxCollider = false;
            settings.StripMissingScripts = false;
            settings.SetCategory("Model", true, "glb");

            Assert.That(snapshot.ClearDestinationArtFolder, Is.True);
            Assert.That(snapshot.AddBoxCollider, Is.True);
            Assert.That(snapshot.StripMissingScripts, Is.True);
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
            FlattenCategorySettings.CreateDefaults(),
            true);
        ToolFlattenRequest request = ToolFlattenRequest.ForPipeline(policy);
        request.ConvertZUpToYUp = true;

        RetinarFlattenOptions options = FlattenBuildService.CreateOptions(null, request);

        Assert.That(options.OperationPolicy, Is.SameAs(policy));
        Assert.That(options.ClearDestinationArtFolder, Is.True);
        Assert.That(options.AddBoxCollider, Is.True);
        Assert.That(options.StripMissingScripts, Is.True);
        Assert.That(options.ConvertZUpToYUp, Is.True);
    }
}
#endif
