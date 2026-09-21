#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

public class UnityPackagePreprocessTests
{
    string root;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "UnityPackagePreprocessTests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void IsUnityPackagePath_OnlyUnityPackage_ModelTableUnchanged()
    {
        Assert.That(UnityPackagePreprocess.IsUnityPackagePath(@"D:\a\b.unitypackage"), Is.True);
        Assert.That(UnityPackagePreprocess.IsUnityPackagePath("x.fbx"), Is.False);

        string[] ext = ToolImportApi.GetSupportedModelExtensions();
        Assert.That(ext, Is.EqualTo(new[] { ".fbx", ".glb", ".gltf", ".obj" }));
    }

    [Test]
    public void Extract_StripsScriptsAndDlls_KeepsPrefabMetaGuid_StripsAssetsPrefix()
    {
        string pack = Path.Combine(root, "sample.unitypackage");
        const string prefabGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string csGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string dllGuid = "cccccccccccccccccccccccccccccccc";
        UnityPackageTar.Write(pack, new List<UnityPackageTarEntry>
        {
            Entry(prefabGuid, "pathname", "Assets/Models/Hero.prefab\n"),
            Entry(prefabGuid, "asset", "%YAML 1.1\n--- !u!1 &1\n"),
            Entry(prefabGuid, "asset.meta", "fileFormatVersion: 2\nguid: " + prefabGuid + "\n"),
            Entry(csGuid, "pathname", "Assets/Models/Evil.cs\n"),
            Entry(csGuid, "asset", "class Evil {}\n"),
            Entry(csGuid, "asset.meta", "fileFormatVersion: 2\nguid: " + csGuid + "\n"),
            Entry(dllGuid, "pathname", "Assets/Plugins/hack.dll\n"),
            Entry(dllGuid, "asset", new byte[] { 0x4d, 0x5a }),
            Entry(dllGuid, "asset.meta", "fileFormatVersion: 2\nguid: " + dllGuid + "\n")
        });

        string dest = Path.Combine(root, "envelope");
        UnityPackageExtractResult result;
        Assert.That(UnityPackagePreprocess.TryExtractToFolder(pack, dest, out result), Is.True, result.Error);
        Assert.That(result.Ok, Is.True);
        Assert.That(result.DroppedDangerous, Is.EqualTo(2));
        Assert.That(result.WrittenFiles, Is.EqualTo(1));

        string prefab = Path.Combine(dest, "Models", "Hero.prefab");
        string meta = prefab + ".meta";
        Assert.That(File.Exists(prefab), Is.True);
        Assert.That(File.Exists(meta), Is.True);
        Assert.That(File.ReadAllText(meta), Does.Contain("guid: " + prefabGuid));
        Assert.That(Directory.Exists(Path.Combine(dest, "Assets")), Is.False);
        Assert.That(File.Exists(Path.Combine(dest, "Models", "Evil.cs")), Is.False);
        Assert.That(File.Exists(Path.Combine(dest, "Plugins", "hack.dll")), Is.False);
        Assert.That(File.Exists(Path.Combine(dest, "Models", "Evil.cs.meta")), Is.False);
    }

    [Test]
    public void Extract_SkipsScene_DoesNotFail()
    {
        string pack = Path.Combine(root, "scene.unitypackage");
        const string sceneGuid = "dddddddddddddddddddddddddddddddd";
        const string prefabGuid = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        UnityPackageTar.Write(pack, new List<UnityPackageTarEntry>
        {
            Entry(sceneGuid, "pathname", "Assets/Scenes/Demo.unity"),
            Entry(sceneGuid, "asset", "%YAML 1.1\n"),
            Entry(sceneGuid, "asset.meta", "guid: " + sceneGuid + "\n"),
            Entry(prefabGuid, "pathname", "Assets/Box.prefab"),
            Entry(prefabGuid, "asset", "%YAML 1.1\n"),
            Entry(prefabGuid, "asset.meta", "guid: " + prefabGuid + "\n")
        });

        string dest = Path.Combine(root, "out");
        UnityPackageExtractResult result;
        Assert.That(UnityPackagePreprocess.TryExtractToFolder(pack, dest, out result), Is.True);
        Assert.That(result.DroppedScenes, Is.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(dest, "Box.prefab")), Is.True);
        Assert.That(File.Exists(Path.Combine(dest, "Scenes", "Demo.unity")), Is.False);
    }

    [Test]
    public void Extract_RejectsMissingPackage()
    {
        UnityPackageExtractResult result;
        Assert.That(
            UnityPackagePreprocess.TryExtractToFolder(Path.Combine(root, "nope.unitypackage"), root, out result),
            Is.False);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Error, Does.Contain("不存在"));
    }

    static UnityPackageTarEntry Entry(string guid, string leaf, string text)
    {
        return Entry(guid, leaf, Encoding.UTF8.GetBytes(text));
    }

    static UnityPackageTarEntry Entry(string guid, string leaf, byte[] data)
    {
        return new UnityPackageTarEntry
        {
            Name = guid + "/" + leaf,
            Data = data
        };
    }
}
#endif
