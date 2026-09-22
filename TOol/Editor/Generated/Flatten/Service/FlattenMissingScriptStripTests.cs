#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class FlattenMissingScriptStripTests
{
    string root;
    string artRoot;
    string sourcePrefab;
    string keepMarker;

    [SetUp]
    public void SetUp()
    {
        root = "Assets/__MissStrip_" + Guid.NewGuid().ToString("N");
        artRoot = root + "/Art";
        FlattenLayout.EnsureFolder(root + "/Incoming");
        FlattenLayout.EnsureFolder(artRoot);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            sourcePrefab = root + "/Incoming/Cube.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, sourcePrefab);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        InjectMissingScript(sourcePrefab);
        AssetDatabase.ImportAsset(sourcePrefab, ImportAssetOptions.ForceUpdate);
        Assert.That(CountMissingScripts(sourcePrefab), Is.GreaterThan(0));

        FlattenLayout.EnsureFolder(artRoot + "/Cube");
        keepMarker = artRoot + "/Cube/keep.txt";
        File.WriteAllText(AssetPathUtility.ToFullPath(keepMarker), "keep");
        AssetDatabase.ImportAsset(keepMarker);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(root) && root.StartsWith("Assets/__MissStrip_", StringComparison.Ordinal))
        {
            AssetDatabase.DeleteAsset(root);
        }
    }

    [Test]
    public void Begin_WithoutStrip_FailsAndLeavesSource()
    {
        LogAssert.Expect(LogType.Warning, new Regex("无独立模型文件依赖"));
        LogAssert.Expect(LogType.Error, new Regex("Missing Script"));

        var options = new RetinarFlattenOptions
        {
            ArtRoot = artRoot,
            ClearDestinationArtFolder = true,
            StripMissingScripts = false,
            SkipDependencySplit = true
        };

        RetinarFlattenWork work;
        string error;
        bool ok = RetinarBatchModelBuilder.TryBeginPackagedFlatten(
            sourcePrefab, options, out work, out error);

        Assert.That(ok, Is.False);
        Assert.That(work, Is.Null);
        Assert.That(error, Does.Contain("Missing Script"));
        Assert.That(CountMissingScripts(sourcePrefab), Is.GreaterThan(0));
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(keepMarker)), Is.True);
    }

    [Test]
    public void Begin_WithStrip_WritesArtCopyAndLeavesSource()
    {
        LogAssert.Expect(LogType.Warning, new Regex("无独立模型文件依赖"));
        LogAssert.Expect(LogType.Warning, new Regex("已剥 Missing Script"));

        var options = new RetinarFlattenOptions
        {
            ArtRoot = artRoot,
            ClearDestinationArtFolder = true,
            StripMissingScripts = true,
            SkipDependencySplit = true
        };

        RetinarFlattenWork work;
        string error;
        bool ok = RetinarBatchModelBuilder.TryBeginPackagedFlatten(
            sourcePrefab, options, out work, out error);

        Assert.That(ok, Is.True, error);
        Assert.That(work, Is.Not.Null);
        Assert.That(work.PrefabPath, Does.StartWith(artRoot + "/"));
        Assert.That(CountMissingScripts(sourcePrefab), Is.GreaterThan(0));
        Assert.That(CountMissingScripts(work.PrefabPath), Is.EqualTo(0));
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(keepMarker)), Is.False);
    }

    static int CountMissingScripts(string prefabPath)
    {
        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            return FlattenReferenceAudit.ListMissingScripts(instance).Count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    static void InjectMissingScript(string prefabPath)
    {
        string full = Path.GetFullPath(prefabPath);
        string text = File.ReadAllText(full);
        Match go = Regex.Match(text, @"--- !u!1 &(-?\d+)");
        Assert.That(go.Success, Is.True, "prefab YAML missing GameObject");
        string goId = go.Groups[1].Value;
        int layer = text.IndexOf("\n  m_Layer:", StringComparison.Ordinal);
        Assert.That(layer, Is.GreaterThan(0), "prefab YAML missing m_Layer");
        text = text.Insert(layer, "\n  - component: {fileID: 88001122}");
        text += "\n--- !u!114 &88001122\n" +
                "MonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: " + goId + "}\n" +
                "  m_Enabled: 1\n" +
                "  m_EditorHideFlags: 0\n" +
                "  m_Script: {fileID: 11500000, guid: 0123456789abcdef0123456789abcdef, type: 3}\n" +
                "  m_Name: \n" +
                "  m_EditorClassIdentifier: \n";
        File.WriteAllText(full, text);
    }
}
#endif
