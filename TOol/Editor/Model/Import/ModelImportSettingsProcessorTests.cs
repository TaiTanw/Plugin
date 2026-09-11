#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class ModelImportSettingsProcessorTests
{
    [TestCase("Assets/Incoming/unit/model.fbx", true)]
    [TestCase("assets/incoming/UNIT/MODEL.FBX", true)]
    [TestCase("Assets\\Incoming\\unit\\model.fbx", true)]
    [TestCase("Assets/IncomingElse/unit/model.fbx", false)]
    [TestCase("Assets/Incoming.prefab", false)]
    [TestCase("Assets/Art/model.fbx", false)]
    public void ContainsAssetPath_UsesDirectoryBoundary(string assetPath, bool expected)
    {
        BatchFbxImportSettings settings = ScriptableObject.CreateInstance<BatchFbxImportSettings>();
        try
        {
            settings.importRootPath = "Assets/Incoming";
            Assert.That(settings.ContainsAssetPath(assetPath), Is.EqualTo(expected));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void ContainsAssetPath_NormalizesConfiguredRoot()
    {
        BatchFbxImportSettings settings = ScriptableObject.CreateInstance<BatchFbxImportSettings>();
        try
        {
            settings.importRootPath = " Assets\\CustomIncoming\\ ";
            Assert.That(
                settings.ContainsAssetPath("Assets/CustomIncoming/unit/model.obj"),
                Is.True);
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [TestCase(false, false, false, false)]
    [TestCase(false, false, true, true)]
    [TestCase(false, true, false, false)]
    [TestCase(false, true, true, true)]
    [TestCase(true, false, false, true)]
    [TestCase(true, false, true, true)]
    [TestCase(true, true, false, false)]
    [TestCase(true, true, true, true)]
    public void Baseline_ImportRootBypassesMasterAndExcludeOnlyInsideRoot(
        bool masterEnabled,
        bool excluded,
        bool isInImportRoot,
        bool expected)
    {
        Assert.That(
            ModelImportSettingsProcessor.ShouldApplyIncomingBaseline(
                masterEnabled, excluded, isInImportRoot),
            Is.EqualTo(expected));
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public void Policy_RequiresMasterAndModelSettingsAuto(
        bool masterEnabled,
        bool modelSettingsAuto,
        bool expected)
    {
        Assert.That(
            ModelImportSettingsProcessor.ShouldApplyIncomingPolicy(
                masterEnabled,
                modelSettingsAuto),
            Is.EqualTo(expected));
    }
}
#endif
