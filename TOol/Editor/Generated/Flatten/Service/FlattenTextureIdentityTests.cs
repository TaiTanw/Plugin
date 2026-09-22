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
        identity.TrustMatchingUnitTextures();
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
        Assert.That(
            FlattenTextureIdentity.SelectUniqueCandidate(
                "hull.png",
                new[] { "a/Child.fbm/hull.png", "b/hull.png" }),
            Is.EqualTo("a/Child.fbm/hull.png"));
    }

    [Test]
    public void TwoSameNameSources_RegisterSeparateDests_BothTrusted()
    {
        FlattenCategorySettings categories = FlattenCategorySettings.CreateDefaults();
        string textureSource = MakeTexture(root + "/Incoming/Texture/body.png", Color.red);
        string fbmSource = MakeTexture(root + "/Incoming/Child.fbm/body.png", Color.green);
        var matA = new Material(Shader.Find("Standard"));
        matA.mainTexture = AssetDatabase.LoadAssetAtPath<Texture>(textureSource);
        AssetDatabase.CreateAsset(matA, root + "/bodyA.mat");
        var matB = new Material(Shader.Find("Standard"));
        matB.mainTexture = AssetDatabase.LoadAssetAtPath<Texture>(fbmSource);
        AssetDatabase.CreateAsset(matB, root + "/bodyB.mat");
        var go = new GameObject("twoBody");
        go.AddComponent<MeshRenderer>().sharedMaterial = matA;
        var child = new GameObject("child");
        child.transform.SetParent(go.transform);
        child.AddComponent<MeshRenderer>().sharedMaterial = matB;
        string prefab = root + "/twoBody.prefab";
        try
        {
            PrefabUtility.SaveAsPrefabAsset(go, prefab);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        var identity = FlattenTextureIdentity.Capture(prefab, unit);
        Assert.That(identity.IsOriginalSource(textureSource), Is.True);
        Assert.That(identity.IsOriginalSource(fbmSource), Is.True);

        string destA = FlattenCopyRunner.ResolveDestAssetPath(unit, textureSource, categories);
        string destB = FlattenCopyRunner.ResolveDestAssetPath(unit, fbmSource, categories);
        destA = destA.Replace("\\", "/");
        destB = destB.Replace("\\", "/");
        Assert.That(destA, Is.EqualTo(unit + "/image/Texture/body.png"));
        Assert.That(destB, Is.EqualTo(unit + "/image/Texture/Child.fbm/body.png"));

        FlattenLayout.EnsureFolder(Path.GetDirectoryName(destA).Replace("\\", "/"));
        FlattenLayout.EnsureFolder(Path.GetDirectoryName(destB).Replace("\\", "/"));
        Assert.That(AssetDatabase.CopyAsset(textureSource, destA), Is.True);
        Assert.That(AssetDatabase.CopyAsset(fbmSource, destB), Is.True);
        identity.RegisterCopies(new Dictionary<string, string>
        {
            { textureSource, destA },
            { fbmSource, destB }
        });
        Assert.That(identity.ResolveExact(textureSource), Is.EqualTo(destA));
        Assert.That(identity.ResolveExact(fbmSource), Is.EqualTo(destB));
        identity.TrustMatchingUnitTextures();
        Assert.That(identity.ResolveExact(textureSource), Is.EqualTo(destA));
        Assert.That(identity.ResolveExact(fbmSource), Is.EqualTo(destB));
        Assert.That(identity.Warnings, Is.Empty);
    }

    [Test]
    public void TrustMatchingUnitTextures_AcceptsCompanionDest_NotInCopiedTable()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        string nested = unit + "/image/Texture/Child.fbm/hull.png";
        FlattenLayout.EnsureFolder(Path.GetDirectoryName(nested).Replace("\\", "/"));
        File.Copy(source, nested);
        AssetDatabase.ImportAsset(nested, ImportAssetOptions.ForceSynchronousImport);
        nested = nested.Replace("\\", "/");
        Assert.That(identity.IsTrustedLocal(nested), Is.False);
        Assert.That(identity.ResolveExact(source), Is.Null);

        identity.TrustMatchingUnitTextures();
        Assert.That(identity.IsTrustedLocal(nested), Is.True);
        Assert.That(identity.ResolveExact(nested), Is.EqualTo(nested));
        Assert.That(identity.ResolveExact(source), Is.Null, "不得把 copies[来源] 改成套层别名");
        Assert.That(identity.Warnings, Is.Empty);

        Material material = new Material(Shader.Find("Standard"));
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture>(nested);
        AssetDatabase.CreateAsset(material, unit + "/delivery.mat");
        var bind = typeof(RetinarBatchModelBuilder).GetMethod(
            "BindKnownTexture", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That((bool)bind.Invoke(null, new object[] { material, "_MainTex", material.mainTexture, identity }),
            Is.False);
        Assert.That(material.mainTexture, Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture>(nested)));
        Assert.That(identity.Warnings, Is.Empty);
    }

    [Test]
    public void BeginFingerprint_RemainsFrozen_WhenSourceBytesChange()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        string originalCopy = unit + "/original.png";
        Assert.That(AssetDatabase.CopyAsset(source, originalCopy), Is.True);
        string replacement = MakeTexture(root + "/replacement.png", Color.blue);
        File.Copy(replacement, source, true);
        AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceSynchronousImport);
        string changedCopy = unit + "/changed.png";
        Assert.That(AssetDatabase.CopyAsset(source, changedCopy), Is.True);

        identity.TrustMatchingUnitTextures();
        Assert.That(identity.IsTrustedLocal(originalCopy), Is.True);
        Assert.That(identity.IsTrustedLocal(changedCopy), Is.False);
        identity.RegisterCopies(new Dictionary<string, string> { { source, changedCopy } });
        Assert.That(identity.ResolveExact(source), Is.Null);
        Assert.That(identity.Warnings.Count, Is.EqualTo(1));
    }

    [Test]
    public void AutoImportEvidence_RejectsUnregisteredModelAndOutsideUnit()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        Assert.That(identity.SnapshotModelImport(root + "/unknown.fbx", unit + "/unknown.fbx"), Is.Null);
        string added = MakeTexture(unit + "/unknown.fbm/new.png", Color.green);
        identity.RegisterModelImport(root + "/unknown.fbx", unit + "/unknown.fbx",
            new Dictionary<string, string>());
        Assert.That(identity.IsTrustedLocal(added), Is.False);
        Assert.That(identity.ResolveExact(added), Is.Null);
    }

    [Test]
    public void CompanionDestFbm_DifferentBytesFromCapture_IsTrustedAfterRegisterExtracted()
    {
        var identity = FlattenTextureIdentity.Capture(sourcePrefab, unit);
        string destFolder = FlattenTextureIdentity.DestFbmFolder(unit + "/Model/Character.fbx", FlattenLayout.TextureFolder(unit));
        string nested = MakeTexture(destFolder + "/zhengtai_d_2.png", Color.blue);
        identity.TrustMatchingUnitTextures();
        Assert.That(identity.IsTrustedLocal(nested), Is.False, "指纹对不上 Begin 来源时 TrustMatching 不得收下");
        identity.RegisterExtracted(unit + "/Model/Character.fbx", destFolder, new Dictionary<string, string>());
        Assert.That(identity.IsTrustedLocal(nested), Is.True);
        Assert.That(identity.ResolveExact(source), Is.Null, "不得改 copies[来源]");
        Assert.That(identity.Warnings, Is.Empty);

        Material material = new Material(Shader.Find("Standard"));
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture>(nested);
        AssetDatabase.CreateAsset(material, unit + "/delivery.mat");
        var bind = typeof(RetinarBatchModelBuilder).GetMethod(
            "BindKnownTexture", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That((bool)bind.Invoke(null, new object[] { material, "_MainTex", material.mainTexture, identity }),
            Is.False);
        Assert.That(identity.Warnings, Is.Empty);
    }

    [Test]
    public void ModelImporter_BindsInUnitFbmDependency_WhenLedgerHasAnotherSameName()
    {
        string used = MakeTexture(unit + "/Child.fbm/face.png", Color.red);
        string extra = MakeTexture(unit + "/Other.fbm/face.png", Color.blue);
        string model = unit + "/probe.obj";
        File.WriteAllText(unit + "/probe.mtl", "newmtl face\nKd 1 1 1\nmap_Kd Child.fbm/face.png\n");
        File.WriteAllText(model, "mtllib probe.mtl\no probe\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl face\nf 1 2 3\n");
        AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceSynchronousImport);

        string[] deps = AssetDatabase.GetDependencies(model, true);
        Assert.That(deps, Does.Contain(used));
        Assert.That(deps, Does.Not.Contain(extra));

        var identity = new FlattenTextureIdentity(unit);
        identity.RegisterExtracted(model, unit + "/Child.fbm", new Dictionary<string, string>());
        identity.RegisterExtracted(model, unit + "/Other.fbm", new Dictionary<string, string>());
        Assert.That(identity.ResolveForModel(model, "face.png"), Is.Null);

        var importer = (ModelImporter)AssetImporter.GetAtPath(model);
        var key = new AssetImporter.SourceAssetIdentifier(typeof(Texture2D), "face.png");
        importer.AddRemap(key, AssetDatabase.LoadAssetAtPath<Texture>(extra));
        var remap = typeof(RetinarBatchModelBuilder).GetMethod(
            "RemapKnownModelTextures", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That((int)remap.Invoke(null, new object[] { importer, identity }), Is.GreaterThan(0));
        Assert.That(AssetDatabase.GetAssetPath(importer.GetExternalObjectMap()[key]), Is.EqualTo(used));
        Assert.That(identity.Warnings, Is.Empty);
    }

    [Test]
    public void ModelImporter_DoesNotGuess_WhenTwoInUnitFbmDependenciesShareAName()
    {
        string faceA = MakeTexture(unit + "/A.fbm/face.png", Color.red);
        string faceB = MakeTexture(unit + "/B.fbm/face.png", Color.blue);
        string model = unit + "/two.obj";
        File.WriteAllText(unit + "/two.mtl",
            "newmtl a\nKd 1 0 0\nmap_Kd A.fbm/face.png\nnewmtl b\nKd 0 0 1\nmap_Kd B.fbm/face.png\n");
        File.WriteAllText(model,
            "mtllib two.mtl\no two\nv 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nusemtl a\nf 1 2 3\nusemtl b\nf 1 3 4\n");
        AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceSynchronousImport);

        string[] deps = AssetDatabase.GetDependencies(model, true);
        Assert.That(deps, Does.Contain(faceA));
        Assert.That(deps, Does.Contain(faceB));

        var identity = new FlattenTextureIdentity(unit);
        Texture kept = AssetDatabase.LoadAssetAtPath<Texture>(faceA);
        var importer = (ModelImporter)AssetImporter.GetAtPath(model);
        var key = new AssetImporter.SourceAssetIdentifier(typeof(Texture2D), "face.png");
        importer.AddRemap(key, kept);
        var remap = typeof(RetinarBatchModelBuilder).GetMethod(
            "RemapKnownModelTextures", BindingFlags.Static | BindingFlags.NonPublic);
        remap.Invoke(null, new object[] { importer, identity });
        Assert.That(importer.GetExternalObjectMap()[key], Is.SameAs(kept));
        Assert.That(identity.Warnings.Count, Is.GreaterThan(0));
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
