#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;

public class UnityPackageImportApiTests
{
    string temp;
    string importRoot;

    [SetUp]
    public void SetUp()
    {
        temp = Path.Combine(Path.GetTempPath(), "UnityPackageImportApiTests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(temp);
        importRoot = "Assets/__UnityPackageImportTests_" + Guid.NewGuid().ToString("N");
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(importRoot) &&
            importRoot.StartsWith("Assets/__UnityPackageImportTests_", StringComparison.Ordinal) &&
            AssetDatabase.IsValidFolder(importRoot))
        {
            AssetDatabase.DeleteAsset(importRoot);
        }

        if (!string.IsNullOrEmpty(temp) && Directory.Exists(temp))
        {
            Directory.Delete(temp, true);
        }
    }

    [Test]
    public void ModelTable_StillExcludesUnityPackage()
    {
        Assert.That(
            ToolImportApi.GetSupportedModelExtensions(),
            Is.EqualTo(new[] { ".fbx", ".glb", ".gltf", ".obj" }));
        Assert.That(ToolImportApi.IsSupportedModelPath("a.unitypackage"), Is.False);
        Assert.That(ToolImportApi.IsUnityPackagePath("a.unitypackage"), Is.True);
    }

    [Test]
    public void ImportSingleModel_PackWithoutImportRoot_Fails()
    {
        string pack = WriteSamplePack("no-root.unitypackage", includeScript: false);
        string path;
        string message;
        Assert.That(ToolImportApi.ImportSingleModel(pack, "PackA", out path, out message), Is.False);
        Assert.That(message, Does.Contain("导入根"));
        Assert.That(path, Is.Null);
    }

    [Test]
    public void ImportSingleModel_Pack_WritesEnvelopeUnderId2_StripsScript_OneRefreshLoadsPrefab()
    {
        string pack = WriteSamplePack("sample.unitypackage", includeScript: true);
        string envelope;
        string message;
        Assert.That(
            ToolImportApi.ImportSingleModel(
                pack, "PackA", out envelope, out message, importRoot, "Assets/Art"),
            Is.True,
            message);
        Assert.That(envelope, Is.EqualTo(importRoot + "/PackA"));
        Assert.That(AssetDatabase.IsValidFolder(envelope), Is.True);

        string prefab = envelope + "/Models/Hero.prefab";
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(prefab)), Is.True);
        Assert.That(AssetDatabase.LoadMainAssetAtPath(prefab), Is.Not.Null);
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(envelope + "/Models/Evil.cs")), Is.False);
        Assert.That(AssetDatabase.IsValidFolder(envelope + "/Assets"), Is.False);
        Assert.That(message, Does.Contain("丢弃脚本/dll 1"));
    }

    [Test]
    public void ImportSingleModel_Pack_ClearsPreviousEnvelope()
    {
        string pack = WriteSamplePack("again.unitypackage", includeScript: false);
        string envelope;
        string message;
        Assert.That(
            ToolImportApi.ImportSingleModel(pack, "PackA", out envelope, out message, importRoot, "Assets/Art"),
            Is.True,
            message);

        string leftover = envelope + "/leftover.txt";
        File.WriteAllText(AssetPathUtility.ToFullPath(leftover), "stale");
        AssetDatabase.Refresh();
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(leftover)), Is.True);

        Assert.That(
            ToolImportApi.ImportSingleModel(pack, "PackA", out envelope, out message, importRoot, "Assets/Art"),
            Is.True,
            message);
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(leftover)), Is.False);
        Assert.That(AssetDatabase.LoadMainAssetAtPath(envelope + "/Models/Hero.prefab"), Is.Not.Null);
    }

    string WriteSamplePack(string fileName, bool includeScript)
    {
        string pack = Path.Combine(temp, fileName);
        const string prefabGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string csGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var entries = new List<UnityPackageTarEntry>
        {
            Entry(prefabGuid, "pathname", "Assets/Models/Hero.prefab\n"),
            Entry(prefabGuid, "asset", MinimalPrefabYaml("Hero")),
            Entry(prefabGuid, "asset.meta", "fileFormatVersion: 2\nguid: " + prefabGuid + "\n")
        };
        if (includeScript)
        {
            entries.Add(Entry(csGuid, "pathname", "Assets/Models/Evil.cs\n"));
            entries.Add(Entry(csGuid, "asset", "class Evil {}\n"));
            entries.Add(Entry(csGuid, "asset.meta", "fileFormatVersion: 2\nguid: " + csGuid + "\n"));
        }

        UnityPackageTar.Write(pack, entries);
        return pack;
    }

    static UnityPackageTarEntry Entry(string guid, string leaf, string text)
    {
        return new UnityPackageTarEntry
        {
            Name = guid + "/" + leaf,
            Data = Encoding.UTF8.GetBytes(text)
        };
    }

    static string MinimalPrefabYaml(string name)
    {
        return
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" +
            "--- !u!1 &100000\nGameObject:\n  m_Name: " + name + "\n  m_TagString: Untagged\n" +
            "  m_Layer: 0\n  m_Component:\n  - component: {fileID: 400000}\n  m_IsActive: 1\n" +
            "--- !u!4 &400000\nTransform:\n  m_GameObject: {fileID: 100000}\n" +
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n" +
            "  m_LocalPosition: {x: 0, y: 0, z: 0}\n" +
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n";
    }
}
#endif
