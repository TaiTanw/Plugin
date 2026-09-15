#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class FlattenTextureIdentityTests
{
    string root;
    string unit;
    string source;
    string sourcePrefab;

    [SetUp]
    public void SetUp()
    {
        root = "Assets/__D24TextureIdentity_" + Guid.NewGuid().ToString("N");
        unit = root + "/Art/Unit";
        FlattenLayout.EnsureFolder(unit);
        source = MakeTexture(root + "/Incoming/hull.png", Color.red);
        var material = new Material(Shader.Find("Standard"));
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture>(source);
        AssetDatabase.CreateAsset(material, root + "/source.mat");
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            go.GetComponent<Renderer>().sharedMaterial = material;
            sourcePrefab = root + "/source.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, sourcePrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(root) && root.StartsWith("Assets/__D24TextureIdentity_", StringComparison.Ordinal))
            AssetDatabase.DeleteAsset(root);
    }

    [Test]
    public void ExactCopy_TracksGuidAfterMove_RejectsSiblingSameName()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        string copy = unit + "/hull.png";
        Assert.That(AssetDatabase.CopyAsset(source, copy), Is.True);
        identity.RegisterCopies(new Dictionary<string, string> { { source, copy } });
        string moved = unit + "/moved.png";
        Assert.That(AssetDatabase.MoveAsset(copy, moved), Is.Empty);
        Assert.That(identity.ResolveExact(source), Is.EqualTo(moved));
        Assert.That(identity.ResolveExact(moved), Is.EqualTo(moved));
        Assert.That(identity.ResolveExact(root + "/Art/Sibling/hull.png"), Is.Null);
        Assert.That(identity.IsInUnit(unit + "Extra/hull.png"), Is.False);
    }

    [Test]
    public void OldSameNameFile_IsNotTrustedByLocation()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        string copy = MakeTexture(unit + "/hull.png", Color.blue);
        identity.RegisterCopies(new Dictionary<string, string> { { source, copy } });
        Assert.That(identity.ResolveExact(source), Is.Null);
        Assert.That(identity.IsTrustedLocal(copy), Is.False);
        Assert.That(identity.Warnings.Count, Is.EqualTo(1));
    }

    [Test]
    public void ExtractEvidence_OnlyTrustsNewOrChangedFiles()
    {
        var identity = new FlattenTextureIdentity(unit);
        string old = MakeTexture(unit + "/old.png", Color.red);
        var before = identity.SnapshotExtractFolder(unit);
        string added = MakeTexture(unit + "/new.png", Color.green);
        identity.RegisterExtracted("missing-model.fbx", unit, before);
        Assert.That(identity.IsTrustedLocal(old), Is.False);
        Assert.That(identity.IsTrustedLocal(added), Is.True);
    }

    [Test]
    public void MaterialFallback_DoesNotCopySiblingOrClearReference_OnlyWarns()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        string sibling = MakeTexture(root + "/Art/Sibling/hull.png", Color.blue);
        Material material = new Material(Shader.Find("Standard"));
        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(sibling);
        material.mainTexture = texture;
        AssetDatabase.CreateAsset(material, unit + "/delivery.mat");
        var bind = typeof(RetinarBatchModelBuilder).GetMethod("BindKnownTexture", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(bind, Is.Not.Null);
        Assert.That((bool)bind.Invoke(null, new object[] { material, "_MainTex", texture, identity }), Is.False);
        Assert.That(material.mainTexture, Is.SameAs(texture));
        Assert.That(File.Exists(FlattenLayout.TextureFolder(unit) + "/hull.png"), Is.False);
        Assert.That(identity.Warnings.Count, Is.EqualTo(1));
        var row = FlattenRowResult.Succeeded(sourcePrefab, unit + "/delivery.prefab");
        var facts = typeof(FlattenBuildService).GetMethod("WithFacts", BindingFlags.Static | BindingFlags.NonPublic);
        facts.Invoke(null, new object[] { row, new RetinarFlattenWork
        {
            SourcePath = sourcePrefab, PrefabPath = sourcePrefab, AssetFolder = unit, TextureIdentity = identity
        } });
        Assert.That(row.TextureIdentityWarnings.Count, Is.EqualTo(1));
        Assert.That(row.Ok, Is.True);
        Assert.That(row.FailedStep, Is.EqualTo(FlattenStep.None));
    }

    [Test]
    public void MaterialFallback_DoesNotModifySourceMaterial()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(root + "/source.mat");
        var bind = typeof(RetinarBatchModelBuilder).GetMethod("BindKnownTexture", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That((bool)bind.Invoke(null, new object[] { material, "_MainTex", material.mainTexture, identity }), Is.False);
        Assert.That(identity.Warnings, Is.Empty);
        Assert.That(AssetDatabase.GetAssetPath(material.mainTexture), Is.EqualTo(source));
    }

    [Test]
    public void SplitRemapAndMaterialCopy_UseKnownCopy_NotSibling()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        string prefabFolder = FlattenLayout.PrefabFolder(unit);
        FlattenLayout.EnsureFolder(prefabFolder);
        string prefab = prefabFolder + "/delivery.prefab";
        Assert.That(AssetDatabase.CopyAsset(sourcePrefab, prefab), Is.True);
        var work = new RetinarFlattenWork
        {
            SourcePath = sourcePrefab, PrefabPath = prefab, AssetFolder = unit, AssetName = "delivery",
            TextureIdentity = identity, Options = new RetinarFlattenOptions()
        };
        Assert.That(RetinarBatchModelBuilder.FlattenSplitDependencies(work), Is.True);
        RetinarBatchModelBuilder.FlattenRemap(work);
        RetinarBatchModelBuilder.FlattenCopyRendererMaterials(work);
        GameObject result = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        Material delivery = result.GetComponent<Renderer>().sharedMaterial;
        Assert.That(identity.IsTrustedLocal(AssetDatabase.GetAssetPath(delivery.mainTexture)), Is.True);
        string sibling = MakeTexture(root + "/Art/Sibling/hull.png", Color.blue);
        Texture wrong = AssetDatabase.LoadAssetAtPath<Texture>(sibling);
        delivery.mainTexture = wrong;
        EditorUtility.SetDirty(delivery);
        AssetDatabase.SaveAssets();
        // 收尾贴图整理经过同一绑定口；不再补拷，也不按同名换成本单元的红图。
        var remap = typeof(RetinarBatchModelBuilder).GetMethod("RemapAllArtMaterialsToLocalTextures", BindingFlags.Static | BindingFlags.NonPublic);
        remap.Invoke(null, new object[] { unit, identity });
        Assert.That(delivery.mainTexture, Is.SameAs(wrong));
        Assert.That(identity.Warnings.Count, Is.GreaterThan(0));
        Assert.That(AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Material>(root + "/source.mat").mainTexture), Is.EqualTo(source));
    }

    [Test]
    public void ModelImporter_RejectsUnknownSiblingWithoutClearingExistingRemap()
    {
        string model = unit + "/probe.obj";
        File.WriteAllText(model, "o probe\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(model);
        string sibling = MakeTexture(root + "/Art/Sibling/hull.png", Color.blue);
        Texture wrong = AssetDatabase.LoadAssetAtPath<Texture>(sibling);
        var key = new AssetImporter.SourceAssetIdentifier(typeof(Texture2D), "hull.png");
        importer.AddRemap(key, wrong);
        var identity = new FlattenTextureIdentity(unit);
        var remap = typeof(RetinarBatchModelBuilder).GetMethod("RemapKnownModelTextures", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That((int)remap.Invoke(null, new object[] { importer, identity }), Is.EqualTo(0));
        Assert.That(importer.GetExternalObjectMap()[key], Is.SameAs(wrong));
        Assert.That(File.Exists(FlattenLayout.TextureFolder(unit) + "/hull.png"), Is.False);
        Assert.That(identity.Warnings.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ModelImporter_RebindsOnlyItsOriginalCopy_WhenSiblingHasSameName()
    {
        string sourceModel = root + "/Incoming/probe.obj";
        File.WriteAllText(root + "/Incoming/probe.mtl", "newmtl hull\nKd 1 1 1\nmap_Kd hull.png\n");
        File.WriteAllText(sourceModel, "mtllib probe.mtl\no probe\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl hull\nf 1 2 3\n");
        AssetDatabase.ImportAsset(sourceModel, ImportAssetOptions.ForceSynchronousImport);
        var modelRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourceModel));
        string modelPrefab = root + "/model.prefab";
        try { PrefabUtility.SaveAsPrefabAsset(modelRoot, modelPrefab); }
        finally { UnityEngine.Object.DestroyImmediate(modelRoot); }
        var identity = FlattenTextureIdentity.Capture(modelPrefab, unit);
        Assert.That(identity.IsOriginalSource(source), Is.True, "OBJ 样例必须真的引用 Incoming/hull.png");
        string model = unit + "/probe.obj";
        string texture = unit + "/hull.png";
        Assert.That(AssetDatabase.CopyAsset(sourceModel, model), Is.True);
        Assert.That(AssetDatabase.CopyAsset(source, texture), Is.True);
        identity.RegisterCopies(new Dictionary<string, string> { { sourceModel, model }, { source, texture } });
        Assert.That(identity.ResolveForModel(model, "hull.png"), Is.EqualTo(texture));
        string sibling = MakeTexture(root + "/Art/Sibling/hull.png", Color.blue);
        var importer = (ModelImporter)AssetImporter.GetAtPath(model);
        var key = new AssetImporter.SourceAssetIdentifier(typeof(Texture2D), "hull.png");
        importer.AddRemap(key, AssetDatabase.LoadAssetAtPath<Texture>(sibling));
        var remap = typeof(RetinarBatchModelBuilder).GetMethod("RemapKnownModelTextures", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That((int)remap.Invoke(null, new object[] { importer, identity }), Is.GreaterThan(0));
        Assert.That(AssetDatabase.GetAssetPath(importer.GetExternalObjectMap()[key]), Is.EqualTo(texture));
        Assert.That(identity.ResolveExact(sibling), Is.Null);
    }

    [Test]
    public void FilenameResolution_RejectsAmbiguityAndDifferentExtension()
    {
        string[] candidates = { "Assets/Art/Unit/hull.png", "Assets/Art/Unit/hull.jpg" };
        Assert.That(FlattenTextureIdentity.SelectUniqueCandidate("hull", candidates), Is.Null);
        Assert.That(FlattenTextureIdentity.SelectUniqueCandidate("hull.jpg", candidates), Is.EqualTo(candidates[1]));
        Assert.That(FlattenTextureIdentity.SelectUniqueCandidate("hull.tga", candidates), Is.Null);
        Assert.That(FlattenTextureIdentity.SelectUniqueCandidate("hull.png", new[] { "a/hull.png", "b/hull.png" }), Is.Null);
    }

    static string MakeTexture(string path, Color color)
    {
        FlattenLayout.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        var image = new Texture2D(1, 1);
        try
        {
            image.SetPixel(0, 0, color);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return path;
    }
}
#endif
