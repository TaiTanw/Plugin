#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class UnityPackageExpandTests
{
    string temp;
    string assetsRoot;

    [SetUp]
    public void SetUp()
    {
        temp = Path.Combine(Path.GetTempPath(), "UnityPackageExpandTests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(temp);
        assetsRoot = "Assets/__UnityPackageExpand_" + Guid.NewGuid().ToString("N");
        EnsureFolder(assetsRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(assetsRoot) &&
            assetsRoot.StartsWith("Assets/__UnityPackageExpand_", StringComparison.Ordinal) &&
            AssetDatabase.IsValidFolder(assetsRoot))
        {
            AssetDatabase.DeleteAsset(assetsRoot);
        }

        if (!string.IsNullOrEmpty(temp) && Directory.Exists(temp))
        {
            Directory.Delete(temp, true);
        }
    }

    [Test]
    public void Collect_NestedPrefab_OnlyParentIsRoot()
    {
        string child;
        string parent;
        CreateNestedPair(assetsRoot, out parent, out child);

        List<string> roots = UnityPackageRootPrefabs.Collect(assetsRoot);

        Assert.That(roots, Is.EqualTo(new[] { parent }));
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(child)), Is.True);
    }

    [Test]
    public void Collect_TwoIndependent_BothRoots()
    {
        string a = SaveEmptyPrefab(assetsRoot + "/Alpha.prefab", "Alpha");
        string b = SaveEmptyPrefab(assetsRoot + "/Beta.prefab", "Beta");

        List<string> roots = UnityPackageRootPrefabs.Collect(assetsRoot);

        Assert.That(roots, Is.EquivalentTo(new[] { a, b }));
    }

    [Test]
    public void Collect_EmptyFolder_ReturnsEmpty()
    {
        Assert.That(UnityPackageRootPrefabs.Collect(assetsRoot), Is.Empty);
        Assert.That(UnityPackageRootPrefabs.Collect("Assets/__no_such_folder"), Is.Empty);
    }

    [Test]
    public void PrefabApi_AlreadyPrefab_PassThrough_IgnoresPackId2Name()
    {
        string prefab = SaveEmptyPrefab(assetsRoot + "/Hero.prefab", "Hero");
        string prefabRoot = assetsRoot + "/IncomingPrefab";
        EnsureFolder(prefabRoot);

        List<string> written = ToolPrefabApi.BuildPrefabs(
            new[] { prefab }, "PackA", prefabRoot, assetsRoot + "/Incoming");

        Assert.That(written, Is.EqualTo(new[] { prefab }));
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabRoot + "/PackA.prefab"), Is.Null);
    }

    [Test]
    public void Runner_PackWithoutRootPrefab_Exits20()
    {
        string pack = Path.Combine(temp, "empty.unitypackage");
        UnityPackageTar.Write(pack, new List<UnityPackageTarEntry>
        {
            Entry("ffffffffffffffffffffffffffffffff", "pathname", "Assets/note.txt\n"),
            Entry("ffffffffffffffffffffffffffffffff", "asset", "no prefab here\n"),
            Entry("ffffffffffffffffffffffffffffffff", "asset.meta",
                "fileFormatVersion: 2\nguid: ffffffffffffffffffffffffffffffff\n")
        });

        PipelineOptions opt = MakeIsolatedOptions(pack, "PackA");
        LogAssert.Expect(LogType.Error, new Regex(@"\[Pipeline\] exit=20 FAIL"));
        PipelineResult result = PipelineRunner.Run(opt);

        Assert.That(result.ExitCode, Is.EqualTo(PipelineErrorCodes.ImportFailed));
        Assert.That(result.ToString(), Does.Contain("无根 Prefab"));
    }

    [Test]
    public void Runner_Pack_ExpandsRootOnly_PrefabPassThroughNotPackId2()
    {
        string child;
        string parent;
        CreateNestedPair(assetsRoot + "/src", out parent, out child);
        string pack = Path.Combine(temp, "nested.unitypackage");
        AssetDatabase.ExportPackage(
            new[] { parent, child },
            pack,
            ExportPackageOptions.Default);
        // 源还在工程里时，解包副本会撞 GUID，父子依赖断掉，两个都会被当成根。
        AssetDatabase.DeleteAsset(assetsRoot + "/src");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        PipelineOptions opt = MakeIsolatedOptions(pack, "PackA");
        PipelineResult result = PipelineRunner.Run(opt);

        Assert.That(result.Ok, Is.True, result.ToString());
        Assert.That(opt.ModelPaths.Count, Is.EqualTo(1));
        Assert.That(Path.GetFileName(opt.ModelPaths[0]), Is.EqualTo("Parent.prefab"));
        Assert.That(opt.SourceBindings[0].MaterialId, Is.EqualTo("Parent"));
        Assert.That(result.PrefabOutputs, Is.EqualTo(opt.ModelPaths));
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(opt.PrefabRoot + "/PackA.prefab"), Is.Null);

        string[] envelopePrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { opt.ImportRoot + "/PackA" });
        Assert.That(envelopePrefabs.Length, Is.EqualTo(2), "子 Prefab 仍留在信封");
    }

    PipelineOptions MakeIsolatedOptions(string packPath, string packId2)
    {
        string incoming = assetsRoot + "/Incoming";
        string prefabRoot = assetsRoot + "/IncomingPrefab";
        string art = assetsRoot + "/Art";
        EnsureFolder(incoming);
        EnsureFolder(prefabRoot);
        EnsureFolder(art);
        return new PipelineOptions
        {
            SourceBindings = new List<PipelineSourceBinding>
            {
                new PipelineSourceBinding(packPath.Replace("\\", "/"), packId2)
            },
            ImportRoot = incoming,
            PrefabRoot = prefabRoot,
            ArtRoot = art,
            RunImport = true,
            RunPrefab = true,
            RunFlatten = false,
            RunPostProcess = false,
            RunAb = false,
            Quiet = true,
            CleanupImportRootsAfterRun = false,
            CleanupArtAfterRun = false
        };
    }

    void CreateNestedPair(string folder, out string parentPath, out string childPath)
    {
        EnsureFolder(folder);
        childPath = SaveEmptyPrefab(folder + "/Child.prefab", "Child");
        GameObject parentGo = new GameObject("Parent");
        GameObject nested = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(childPath));
        nested.transform.SetParent(parentGo.transform, false);
        parentPath = folder + "/Parent.prefab";
        PrefabUtility.SaveAsPrefabAsset(parentGo, parentPath);
        UnityEngine.Object.DestroyImmediate(parentGo);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        parentPath = parentPath.Replace("\\", "/");
        childPath = childPath.Replace("\\", "/");
    }

    static string SaveEmptyPrefab(string assetPath, string name)
    {
        EnsureFolder(Path.GetDirectoryName(assetPath).Replace("\\", "/"));
        GameObject go = new GameObject(name);
        try
        {
            PrefabUtility.SaveAsPrefabAsset(go, assetPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }

        AssetDatabase.Refresh();
        return assetPath.Replace("\\", "/");
    }

    static void EnsureFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Replace("\\", "/").Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    static UnityPackageTarEntry Entry(string guid, string leaf, string text)
    {
        return new UnityPackageTarEntry
        {
            Name = guid + "/" + leaf,
            Data = Encoding.UTF8.GetBytes(text)
        };
    }
}
#endif
