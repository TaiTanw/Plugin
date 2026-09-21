#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class NormalizeDeliverableShaderOperationTests
{
    private Material material;
    private string createdUnit;
    private string createdOutside;

    [SetUp]
    public void SetUp()
    {
        Shader standard = Shader.Find("Standard");
        Assert.That(standard, Is.Not.Null, "测试需要 Unity 内置 Standard Shader");
        material = new Material(standard);
        createdUnit = null;
        createdOutside = null;
    }

    [TearDown]
    public void TearDown()
    {
        if (material != null)
        {
            Object.DestroyImmediate(material);
        }

        DeleteTestFolder(createdUnit);
        DeleteTestFolder(createdOutside);
        createdUnit = null;
        createdOutside = null;
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

    [Test]
    public void TryApplyFadeIfMainTexMarksTransparency_NullOrEmptyMainTex_ReturnsFalse()
    {
        Assert.That(
            NormalizeDeliverableShaderOperation.TryApplyFadeIfMainTexMarksTransparency(null),
            Is.False);
        Assert.That(
            NormalizeDeliverableShaderOperation.TryApplyFadeIfMainTexMarksTransparency(material),
            Is.False);
        Assert.That(material.GetFloat("_Mode"), Is.EqualTo(0f));
    }

    [Test]
    public void IsUnityBuiltInShader_Standard_IsTrue()
    {
        Assert.That(
            NormalizeDeliverableShaderOperation.IsUnityBuiltInShader(Shader.Find("Standard")),
            Is.True);
    }

    [Test]
    public void IsUnityBuiltInShader_SpritesDefault_IsTrue()
    {
        Shader sprites = Shader.Find("Sprites/Default");
        Assert.That(sprites, Is.Not.Null);
        Assert.That(NormalizeDeliverableShaderOperation.IsUnityBuiltInShader(sprites), Is.True);
    }

    [Test]
    public void Evaluate_ArtUnitCustomShader_Skips()
    {
        createdUnit = "Assets/Art/__NormShaderKeep";
        string matPath = WriteShaderAndMat(createdUnit, "Hidden/NormKeepTest/KeepMine");
        MaterialProcessSettings settings = ScriptableObject.CreateInstance<MaterialProcessSettings>();
        settings.EnsureMasterBatchDefaults();

        AssetOperationEvaluation eval =
            new NormalizeDeliverableShaderOperation().Evaluate(matPath, settings);

        Assert.That(eval.Eligibility, Is.EqualTo(AssetOperationEligibility.Skip));
        Assert.That(eval.Reason, Does.Contain("本单元 Art Shader"));
    }

    [Test]
    public void Evaluate_ShaderOutsideArtUnit_NeedsBake()
    {
        createdOutside = "Assets/__NormShaderOutside";
        createdUnit = "Assets/Art/__NormShaderBake";
        FlattenLayout.EnsureFolder(createdOutside);
        string shaderPath = createdOutside + "/NotInArt.shader";
        File.WriteAllText(
            AssetPathUtility.ToFullPath(shaderPath),
            TinyUnlitShader("Hidden/NormKeepTest/NotInArt"));
        AssetDatabase.ImportAsset(shaderPath);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        Assert.That(shader, Is.Not.Null);

        FlattenLayout.EnsureFolder(createdUnit + "/Material");
        string matPath = createdUnit + "/Material/BakeMe.mat";
        var mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();

        MaterialProcessSettings settings = ScriptableObject.CreateInstance<MaterialProcessSettings>();
        settings.EnsureMasterBatchDefaults();

        AssetOperationEvaluation eval =
            new NormalizeDeliverableShaderOperation().Evaluate(matPath, settings);

        Assert.That(eval.NeedsWork, Is.True);
        Assert.That(eval.Reason, Does.Contain("需烘焙"));
    }

    static string WriteShaderAndMat(string unitRoot, string shaderName)
    {
        FlattenLayout.EnsureFolder(unitRoot + "/Shader");
        FlattenLayout.EnsureFolder(unitRoot + "/Material");
        string shaderPath = unitRoot + "/Shader/KeepMine.shader";
        File.WriteAllText(
            AssetPathUtility.ToFullPath(shaderPath),
            TinyUnlitShader(shaderName));
        AssetDatabase.ImportAsset(shaderPath);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        Assert.That(shader, Is.Not.Null);
        string matPath = unitRoot + "/Material/KeepMine.mat";
        AssetDatabase.CreateAsset(new Material(shader), matPath);
        AssetDatabase.SaveAssets();
        return matPath;
    }

    static string TinyUnlitShader(string name)
    {
        return "Shader \"" + name + "\" {\n" +
               "Properties { _Color (\"Color\", Color) = (1,1,1,1) }\n" +
               "SubShader { Tags { \"RenderType\"=\"Opaque\" }\n" +
               "Pass { CGPROGRAM\n" +
               "#pragma vertex vert\n#pragma fragment frag\n#include \"UnityCG.cginc\"\n" +
               "fixed4 _Color;\n" +
               "float4 vert(float4 v:POSITION):SV_POSITION { return UnityObjectToClipPos(v); }\n" +
               "fixed4 frag():SV_Target { return _Color; }\n" +
               "ENDCG } } }\n";
    }

    static void DeleteTestFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || !AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        AssetDatabase.DeleteAsset(assetFolder);
    }
}
#endif
