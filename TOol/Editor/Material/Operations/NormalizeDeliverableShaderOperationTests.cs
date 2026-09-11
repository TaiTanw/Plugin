#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class NormalizeDeliverableShaderOperationTests
{
    private Material material;

    [SetUp]
    public void SetUp()
    {
        Shader standard = Shader.Find("Standard");
        Assert.That(standard, Is.Not.Null, "测试需要 Unity 内置 Standard Shader");
        material = new Material(standard);
    }

    [TearDown]
    public void TearDown()
    {
        if (material != null)
        {
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void CaptureSurface_DoesNotTreatColorAlphaAsTransparency()
    {
        material.color = new Color(1f, 0.6f, 0.1f, 0.1f);

        MaterialSurfaceSnapshot captured =
            NormalizeDeliverableShaderOperation.CaptureSurface(material);

        Assert.That(captured.Mode, Is.EqualTo(MaterialSurfaceMode.Opaque));
    }

    [Test]
    public void ApplyTargetSurface_ConfiguresOpaqueStandardState()
    {
        NormalizeDeliverableShaderOperation.ApplyTargetSurface(
            material,
            new MaterialSurfaceSnapshot(MaterialSurfaceMode.Opaque, 0.5f));

        Assert.That(material.GetFloat("_Mode"), Is.EqualTo(0f));
        Assert.That(material.GetInt("_SrcBlend"), Is.EqualTo((int)BlendMode.One));
        Assert.That(material.GetInt("_DstBlend"), Is.EqualTo((int)BlendMode.Zero));
        Assert.That(material.GetInt("_ZWrite"), Is.EqualTo(1));
        Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
        Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.False);
        Assert.That(material.IsKeywordEnabled("_ALPHABLEND_ON"), Is.False);
    }

    [Test]
    public void ApplyTargetSurface_ConfiguresCutoutAndPreservesCutoff()
    {
        material.EnableKeyword("_BUILTIN_AlphaClip");

        NormalizeDeliverableShaderOperation.ApplyTargetSurface(
            material,
            new MaterialSurfaceSnapshot(MaterialSurfaceMode.Cutout, 0.37f));

        Assert.That(material.GetFloat("_Mode"), Is.EqualTo(1f));
        Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.37f).Within(0.0001f));
        Assert.That(material.GetInt("_SrcBlend"), Is.EqualTo((int)BlendMode.One));
        Assert.That(material.GetInt("_DstBlend"), Is.EqualTo((int)BlendMode.Zero));
        Assert.That(material.GetInt("_ZWrite"), Is.EqualTo(1));
        Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.AlphaTest));
        Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
        Assert.That(material.IsKeywordEnabled("_BUILTIN_AlphaClip"), Is.False);
    }

    [Test]
    public void ApplyTargetSurface_ConfiguresGltfBlendAsStandardFade()
    {
        material.EnableKeyword("_BUILTIN_ALPHABLEND_ON");
        material.EnableKeyword("_BUILTIN_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");

        NormalizeDeliverableShaderOperation.ApplyTargetSurface(
            material,
            new MaterialSurfaceSnapshot(MaterialSurfaceMode.Blend, 0.5f));

        Assert.That(material.GetFloat("_Mode"), Is.EqualTo(2f));
        Assert.That(material.GetInt("_SrcBlend"), Is.EqualTo((int)BlendMode.SrcAlpha));
        Assert.That(material.GetInt("_DstBlend"), Is.EqualTo((int)BlendMode.OneMinusSrcAlpha));
        Assert.That(material.GetInt("_ZWrite"), Is.EqualTo(0));
        Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
        Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.False);
        Assert.That(material.IsKeywordEnabled("_ALPHABLEND_ON"), Is.True);
        Assert.That(material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"), Is.False);
        Assert.That(material.IsKeywordEnabled("_BUILTIN_ALPHABLEND_ON"), Is.False);
        Assert.That(material.IsKeywordEnabled("_BUILTIN_SURFACE_TYPE_TRANSPARENT"), Is.False);
        Assert.That(material.IsKeywordEnabled("_DISABLE_SSR_TRANSPARENT"), Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CaptureSurface_RoundTripsAppliedStandardState(int modeValue)
    {
        MaterialSurfaceMode mode = (MaterialSurfaceMode)modeValue;
        NormalizeDeliverableShaderOperation.ApplyTargetSurface(
            material,
            new MaterialSurfaceSnapshot(mode, 0.42f));

        MaterialSurfaceSnapshot captured =
            NormalizeDeliverableShaderOperation.CaptureSurface(material);

        Assert.That(captured.Mode, Is.EqualTo(mode));
        if (mode == MaterialSurfaceMode.Cutout)
        {
            Assert.That(captured.Cutoff, Is.EqualTo(0.42f).Within(0.0001f));
        }
    }
}
#endif
