#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>步骤 6–13：调度走 Run(plan)；不进 FlattenPaths；公开层不再转发七步。</summary>
public class FlattenSmokePathTests
{
    [Test]
    public void PipelineRunner_DoesNotEnterSafeZoneOrFlattenPaths()
    {
        string text = ReadPluginFile("Pipeline/Editor/PipelineRunner.cs");

        Assert.That(text, Does.Contain("ToolFlattenApi.Run"));
        Assert.That(text, Does.Contain("ToolFlattenApi.FromContext"));
        Assert.That(text, Does.Not.Contain("FlattenPaths"));
        Assert.That(text, Does.Not.Contain("CreateNormalizedPrefab"));
        Assert.That(text, Does.Not.Contain("SafeZone"));
    }

    [Test]
    public void RunPlan_UsesPackagedFlattenNotSafeZonePrefab()
    {
        string text = ReadPluginFile("TOol/Editor/Generated/Flatten/Service/FlattenBuildService.cs");

        Assert.That(text, Does.Contain("TryBeginPackagedFlatten"));
        Assert.That(text, Does.Not.Contain("CreateNormalizedPrefab"));
        Assert.That(text, Does.Not.Contain("FlattenSourcePaths"));
        Assert.That(text, Does.Not.Contain("FlattenPaths"));
    }

    [Test]
    public void ManualOrchestration_DoesNotCallFlattenPaths()
    {
        string text = ReadPluginFile("Pipeline/Editor/Flatten/Orchestration/ManualFlattenOrchestration.cs");

        Assert.That(text, Does.Contain("ToolFlattenApi.Run"));
        Assert.That(text, Does.Contain("ToolPrefabApi.BuildPrefabs"));
        Assert.That(text, Does.Contain("ConfirmSplitWithRelativeUris"));
        Assert.That(text, Does.Not.Contain("FlattenPaths"));
        Assert.That(text, Does.Not.Contain("CreateNormalizedPrefab"));
        Assert.That(text, Does.Not.Contain("PipelineJobContext.Build"));
    }

    [Test]
    public void FlattenKernel_DoesNotWriteImporterBundleNames()
    {
        string text = ReadPluginFile("TOol/Editor/Generated/Flatten/Service/RetinarBatchModelBuilder.cs");

        Assert.That(text, Does.Not.Contain(".assetBundleName"));
        Assert.That(text, Does.Not.Contain(".assetBundleVariant"));
        Assert.That(text, Does.Not.Contain("ClearDuplicateBundleNames"));
        Assert.That(text, Does.Not.Contain("ClearBundleName"));
    }

    [Test]
    public void FinishHeal_DoesNotOwnExtract()
    {
        string healFile = ReadPluginFile("TOol/Editor/Generated/Flatten/Service/RetinarBatchModelBuilder.AssetResolution.cs");
        string kernel = ReadPluginFile("TOol/Editor/Generated/Flatten/Service/RetinarBatchModelBuilder.cs");

        Assert.That(healFile, Does.Contain("自愈不 Extract"));
        Assert.That(healFile, Does.Not.Contain("ExtractAndBindPackagedModelTextures(asset."));
        Assert.That(kernel, Does.Contain("本趟唯一 Extract 口"));
        Assert.That(kernel, Does.Contain("ExtractAndBindPackagedModelTextures"));
    }

    [Test]
    public void ManualPrompt_HasCancelButtonAndAbortsOnFalse()
    {
        string prompt = ReadPluginFile("Pipeline/Editor/Flatten/Orchestration/FlattenManualPrompt.cs");
        Assert.That(prompt, Does.Contain("仍要平铺"));
        Assert.That(prompt, Does.Contain("取消"));
        Assert.That(prompt, Does.Contain("DisplayDialog"));
        Assert.That(prompt, Does.Not.Contain("WarnRelativeUriThenContinue"));
    }

    [Test]
    public void Step12_LegacyEntryChainAndPrivateHelpersAreRemoved()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        string[] removed = { "FlattenSourcePaths", "CreateNormalizedPrefab", "CreatePackagedAdjustedPrefab",
            "FlattenSelectedToArt", "OpenDeliverablesFolder", "RemapCopiedAssets", "CollectSourceAssets",
            "CopySourceTexturesToUnityArtFolder", "CopyModelToUnityArtFolder", "SetupAnimationController",
            "ApplyMaterialCopies", "CreateOrUpdateMaterialCopy", "ToGeneratedAsset" };
        foreach (string name in removed)
            Assert.That(typeof(RetinarBatchModelBuilder).GetMethod(name, flags), Is.Null, name);
        Assert.That(typeof(ToolFlattenApi).GetMethod("FlattenPaths", flags), Is.Null);
        Assert.That(typeof(FlattenSmokePathTests).Assembly.GetType("RetinarFlattenApi"), Is.Null);
        foreach (string name in new[] { "SafeZonePadding", "SafeZoneCenter", "SafeZoneSize", "EmissionIntensity", "EmissionColor", "AssetBundleVariant" })
            Assert.That(typeof(RetinarBatchModelBuilder).GetField(name, flags), Is.Null, name);
    }

    [Test]
    public void Step13_DuplicateSevenStepPublicOrchestrationRemoved()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static;
        string[] removed = { "TryBegin", "SplitDependencies", "RelocateAtomic", "ApplyImportAndExtract",
            "Remap", "CopyRendererMaterials", "TryFinish" };
        foreach (string name in removed)
        {
            Assert.That(typeof(ToolFlattenApi).GetMethod(name, flags), Is.Null, "ToolFlattenApi." + name);
            Assert.That(typeof(FlattenBuildService).GetMethod(name, flags), Is.Null, "FlattenBuildService." + name);
        }

        Assert.That(typeof(ToolFlattenApi).GetMethod("FlattenSelectedToArt", flags), Is.Null);
        Assert.That(typeof(ToolFlattenApi).GetMethod("ShouldRelocateAtomic", flags), Is.Null);
        Assert.That(typeof(ToolFlattenApi).GetMethod("ShouldApplyArtModelImporter", flags), Is.Null);
        Assert.That(typeof(ToolFlattenApi).GetMethod("HasMissingSidecars", flags), Is.Null);
        Assert.That(typeof(FlattenSmokePathTests).Assembly.GetType("RetinarFlattenApi"), Is.Null);

        Assert.That(typeof(ToolFlattenApi).GetMethod("Run", flags), Is.Not.Null);
        Assert.That(typeof(ToolFlattenApi).GetMethod("FromContext", flags), Is.Not.Null);

        string service = ReadPluginFile("TOol/Editor/Generated/Flatten/Service/FlattenBuildService.cs");
        Assert.That(service, Does.Contain("TryBeginPackagedFlatten"));
        Assert.That(service, Does.Contain("FlattenRelocateAtomic"));
        Assert.That(service, Does.Contain("FlattenSplitDependencies"));
        Assert.That(service, Does.Contain("FlattenApplyImportAndExtract"));
        Assert.That(service, Does.Contain("FlattenRemap"));
        Assert.That(service, Does.Contain("FlattenCopyRendererMaterials"));
        Assert.That(service, Does.Contain("TryFinishPackagedFlatten"));
        Assert.That(service, Does.Contain("FromContext"));
        Assert.That(ReadPluginFile("Pipeline/Editor/PipelineRunner.cs"), Does.Contain("ToolFlattenApi.FromContext"));
        Assert.That(ReadPluginFile("Pipeline/Editor/Flatten/Orchestration/ManualFlattenOrchestration.cs"),
            Does.Contain("ToolFlattenApi.Run"));
    }

    [Test]
    public void Step14_QualityCodesDoNotReturnFromFlatten()
    {
        string runner = ReadPluginFile("Pipeline/Editor/PipelineRunner.cs");
        Assert.That(runner, Does.Contain("PipelineFlattenQuality.Apply"));
        Assert.That(runner, Does.Contain("CanEscalateToPostProcess"));
        Assert.That(runner, Does.Contain("FlattenFailed"));
        Assert.That(PipelineErrorCodes.FlattenLeftoverFbm, Is.EqualTo(41));
        Assert.That(PipelineErrorCodes.FlattenTextureIdentity, Is.EqualTo(42));
    }

    [Test]
    public void Step12_SharedGeometryAnimationAndTextureHelpersRemain()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        string[] kept = { "ValidateFlattenSelectedToArt", "TryBeginPackagedFlatten", "FlattenSplitDependencies",
            "FlattenRelocateAtomic", "FlattenApplyImportAndExtract", "FlattenRemap", "FlattenCopyRendererMaterials",
            "TryFinishPackagedFlatten", "TryGetRendererBounds", "AddOrUpdateBoxCollider", "WrapIncomingPrefabInEmptyShell",
            "NormalizePreparedPrefabAnimations", "TryHealExternalDependencies", "BindKnownTexture" };
        foreach (string name in kept)
            Assert.That(typeof(RetinarBatchModelBuilder).GetMethod(name, flags), Is.Not.Null, name);
        Assert.That(ReadPluginFile("RetinarBatchBuilder_Share/Assets/Retinar/Editor/01_RetinarMenu.cs"),
            Does.Contain("RetinarEditorUtil.OpenDeliverablesFolder"));
    }

    static string ReadPluginFile(string relativeUnderPlugin)
    {
        string path = Path.Combine(Application.dataPath, "Plugin", relativeUnderPlugin.Replace('/', Path.DirectorySeparatorChar));
        Assert.That(File.Exists(path), Is.True, path);
        return File.ReadAllText(path);
    }
}
#endif
