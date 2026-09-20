#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;

public class PipelineFlattenQualityTests
{
    [Test]
    public void LeftoverFbm_Sets41_KeepsRowOk()
    {
        var result = new PipelineResult();
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");

        PipelineFlattenQuality.Apply(result, row, 1);

        Assert.That(row.Ok, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenLeftoverFbm));
        Assert.That(result.Ok, Is.False);
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("leftover .fbm"));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("⑤⑥继续"));
    }

    [Test]
    public void TextureIdentity_Sets42_KeepsRowOk()
    {
        var result = new PipelineResult();
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.TextureIdentityWarnings.Add("slot MainTex still points outside");

        PipelineFlattenQuality.Apply(result, row, 2);

        Assert.That(row.Ok, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenTextureIdentity));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("贴图身份"));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("槽位引用未清"));
    }

    [Test]
    public void LeftoverWinsOverIdentityOnSameRow()
    {
        var result = new PipelineResult();
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");
        row.TextureIdentityWarnings.Add("identity");

        PipelineFlattenQuality.Apply(result, row, 1);

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenLeftoverFbm));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("identity"));
    }

    [Test]
    public void EarlierIdentityCode_IsPreservedWhenLeftoverArrives()
    {
        var result = new PipelineResult();
        result.Fail(PipelineErrorCodes.FlattenTextureIdentity, "prior identity");
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");

        PipelineFlattenQuality.Apply(result, row, 2);

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenTextureIdentity));
        Assert.That(string.Join("\n", result.Messages.ToArray()), Does.Contain("leftover .fbm"));
    }

    [Test]
    public void HardFlatten40_NotOverwrittenByQuality()
    {
        var result = new PipelineResult();
        result.Fail(PipelineErrorCodes.FlattenFailed, "missing uris");
        var row = FlattenRowResult.Succeeded("Assets/IncomingPrefab/a.prefab", "Assets/Art/a/a.prefab");
        row.LeftoverExternalFbm.Add("Assets/Incoming/x.fbm/tex.png");
        row.TextureIdentityWarnings.Add("identity");

        PipelineFlattenQuality.Apply(result, row, 1);

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenFailed));
    }

    [Test]
    public void QualityCodes_CanEscalateTo50()
    {
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.Ok), Is.True);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.FlattenLeftoverFbm), Is.True);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.FlattenTextureIdentity), Is.True);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.FlattenFailed), Is.False);
        Assert.That(PipelineFlattenQuality.CanEscalateToPostProcess(PipelineErrorCodes.ImportFailed), Is.False);
    }

    [Test]
    public void LaterPhaseFailures_DoNotOverwriteFirstFailureCode()
    {
        var result = new PipelineResult();

        result.Fail(PipelineErrorCodes.FlattenLeftoverFbm, "first");
        result.Fail(PipelineErrorCodes.PostProcessFailed, "later post-process");
        result.Fail(PipelineErrorCodes.AbFailed, "later asset bundle");

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.FlattenLeftoverFbm));
        Assert.That(result.Messages, Has.Count.EqualTo(3));
    }
}


public sealed class PipelineStepSettingsTests
{
    [Test]
    public void FromSettings_KeepsMandatoryImportEnabled()
    {
        PipelineStepSettings settings = UnityEngine.ScriptableObject.CreateInstance<PipelineStepSettings>();
        try
        {
            PipelineOptions options = PipelineOptions.FromSettings(settings, "Assets/model.fbx");

            Assert.That(options.RunImport, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void ApplyTo_DoesNotOverrideDirectApiImportChoice()
    {
        PipelineStepSettings settings = UnityEngine.ScriptableObject.CreateInstance<PipelineStepSettings>();
        try
        {
            var options = new PipelineOptions { RunImport = false };

            settings.ApplyTo(options);

            Assert.That(options.RunImport, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void FromSettings_DefaultsPostProcessIncludesAllTypes()
    {
        PipelineStepSettings settings = UnityEngine.ScriptableObject.CreateInstance<PipelineStepSettings>();
        try
        {
            PipelineOptions options = PipelineOptions.FromSettings(settings, "Assets/model.fbx");

            Assert.That(options.PostProcessIncludeTexture, Is.True);
            Assert.That(options.PostProcessIncludeMaterial, Is.True);
            Assert.That(options.PostProcessIncludeModel, Is.True);
            Assert.That(options.CleanupImportRootsAfterRun, Is.False);
            Assert.That(options.CleanupArtAfterRun, Is.False);
            Assert.That(options.ImportRoot, Is.EqualTo(PipelineWorkspace.DefaultImportRoot));
            Assert.That(options.PrefabRoot, Is.EqualTo(PipelineWorkspace.DefaultPrefabRoot));
            Assert.That(options.ArtRoot, Is.EqualTo(PipelineWorkspace.DefaultArtRoot));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void ApplyTo_CopiesPostProcessIncludeFromStepSettings()
    {
        PipelineStepSettings settings = UnityEngine.ScriptableObject.CreateInstance<PipelineStepSettings>();
        try
        {
            settings.postProcessIncludeTexture = false;
            settings.postProcessIncludeMaterial = true;
            settings.postProcessIncludeModel = false;

            PipelineOptions options = PipelineOptions.FromSettings(settings, "Assets/model.fbx");

            Assert.That(options.PostProcessIncludeTexture, Is.False);
            Assert.That(options.PostProcessIncludeMaterial, Is.True);
            Assert.That(options.PostProcessIncludeModel, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void ApplyTo_CopiesWorkspaceRoots()
    {
        PipelineStepSettings settings = UnityEngine.ScriptableObject.CreateInstance<PipelineStepSettings>();
        try
        {
            settings.importRootPath = "Assets/InA";
            settings.prefabRootPath = "Assets/PfA";
            settings.artRootPath = "Assets/OutA";

            PipelineOptions options = PipelineOptions.FromSettings(settings, "Assets/model.fbx");

            Assert.That(options.ImportRoot, Is.EqualTo("Assets/InA"));
            Assert.That(options.PrefabRoot, Is.EqualTo("Assets/PfA"));
            Assert.That(options.ArtRoot, Is.EqualTo("Assets/OutA"));
            Assert.That(options.AbBuildOptions, Is.Not.Null);
            Assert.That(options.AbBuildOptions.ArtRoot, Is.EqualTo("Assets/OutA"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void ApplyTo_CopiesCleanupFlags()
    {
        PipelineStepSettings settings = UnityEngine.ScriptableObject.CreateInstance<PipelineStepSettings>();
        try
        {
            settings.cleanupImportRootsAfterRun = true;
            settings.cleanupArtAfterRun = true;

            PipelineOptions options = PipelineOptions.FromSettings(settings, "Assets/model.fbx");

            Assert.That(options.CleanupImportRootsAfterRun, Is.True);
            Assert.That(options.CleanupArtAfterRun, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void CollectUnitFolders_UsesGivenArtRoot()
    {
        var folders = PipelineWorkspace.CollectUnitFolders(
            new[] { "Assets/Delivery/Unit/Prefab/a.prefab", "Assets/Art/Other/x.prefab" },
            "Assets/Delivery");

        Assert.That(folders, Is.EquivalentTo(new[] { "Assets/Delivery/Unit" }));
    }

    [Test]
    public void TryValidate_RejectsImportUnderArt()
    {
        var options = new PipelineOptions();
        options.ImportRoot = "Assets/Art/Incoming";
        options.PrefabRoot = "Assets/IncomingPrefab";
        options.ArtRoot = "Assets/Art";

        Assert.That(PipelineWorkspace.TryValidate(options, out string error), Is.False);
        Assert.That(error, Does.Contain("导入根"));
    }

    [Test]
    public void TryClearRootContents_RejectsPluginAndAssets()
    {
        Assert.That(
            PipelineWorkspace.TryClearRootContents("Assets/Plugin", out _, out string pluginError),
            Is.False);
        Assert.That(pluginError, Does.Contain("Plugin"));

        Assert.That(
            PipelineWorkspace.TryClearRootContents("Assets", out _, out string assetsError),
            Is.False);
        Assert.That(assetsError, Does.Contain("Assets"));
    }

    [Test]
    public void TryClearRootContents_DeletesChildrenKeepsRoot()
    {
        const string root = "Assets/_PipelineWorkspaceClearTest";
        const string child = root + "/unit";
        try
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "_PipelineWorkspaceClearTest");
            }

            if (!AssetDatabase.IsValidFolder(child))
            {
                AssetDatabase.CreateFolder(root, "unit");
            }

            AssetDatabase.Refresh();

            int deleted;
            string error;
            Assert.That(PipelineWorkspace.TryClearRootContents(root, out deleted, out error), Is.True);
            AssetDatabase.Refresh();
            Assert.That(error, Is.Null);
            Assert.That(deleted, Is.GreaterThan(0));
            Assert.That(AssetDatabase.IsValidFolder(root), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(child), Is.False);
        }
        finally
        {
            if (AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.DeleteAsset(root);
            }

            AssetDatabase.Refresh();
        }
    }
}
#endif
