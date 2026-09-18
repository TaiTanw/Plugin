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

    [TestCase(false, false)]
    [TestCase(true, true)]
    public void Policy_FollowsExternalFlagOnly(bool modelUseExternalMaterials, bool expected)
    {
        Assert.That(
            ModelImportSettingsProcessor.ShouldApplyIncomingPolicy(modelUseExternalMaterials),
            Is.EqualTo(expected));
    }
}
#endif
