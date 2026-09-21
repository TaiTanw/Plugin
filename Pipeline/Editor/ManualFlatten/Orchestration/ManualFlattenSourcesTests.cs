#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ManualFlattenSourcesTests
{
    string assetsRoot;

    [SetUp]
    public void SetUp()
    {
        assetsRoot = "Assets/__ManualFlattenSources_" + Guid.NewGuid().ToString("N");
        EnsureFolder(assetsRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(assetsRoot) &&
            assetsRoot.StartsWith("Assets/__ManualFlattenSources_", StringComparison.Ordinal) &&
            AssetDatabase.IsValidFolder(assetsRoot))
        {
            AssetDatabase.DeleteAsset(assetsRoot);
        }
    }

    [Test]
    public void Expand_Folder_NestedPrefab_OnlyParent()
    {
        string child;
        string parent;
        CreateNestedPair(assetsRoot, out parent, out child);

        List<string> errors;
        List<string> jobs = ManualFlattenSources.Expand(new[] { assetsRoot }, out errors);

        Assert.That(errors, Is.Empty);
        Assert.That(jobs, Is.EqualTo(new[] { parent }));
        Assert.That(File.Exists(AssetPathUtility.ToFullPath(child)), Is.True);
    }

    [Test]
    public void Expand_EmptyFolder_ReportsNoRootPrefab()
    {
        List<string> errors;
        List<string> jobs = ManualFlattenSources.Expand(new[] { assetsRoot }, out errors);

        Assert.That(jobs, Is.Empty);
        Assert.That(errors.Count, Is.EqualTo(1));
        Assert.That(errors[0], Does.Contain("无根 Prefab"));
    }

    [Test]
    public void Expand_UnityPackageFile_Rejected()
    {
        List<string> errors;
        List<string> jobs = ManualFlattenSources.Expand(
            new[] { "Assets/foo.unitypackage" }, out errors);

        Assert.That(jobs, Is.Empty);
        Assert.That(errors[0], Does.Contain(".unitypackage"));
    }

    [Test]
    public void Expand_DirectPrefabAndModel_Unchanged()
    {
        List<string> errors;
        List<string> jobs = ManualFlattenSources.Expand(
            new[] { "Assets/a.prefab", "Assets/b.fbx", "Assets/c.cs" }, out errors);

        Assert.That(errors, Is.Empty);
        Assert.That(jobs, Is.EqualTo(new[] { "Assets/a.prefab", "Assets/b.fbx" }));
        Assert.That(
            ToolImportApi.GetSupportedModelExtensions(),
            Is.EqualTo(new[] { ".fbx", ".glb", ".gltf", ".obj" }));
    }

    void CreateNestedPair(string folder, out string parentPath, out string childPath)
    {
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
}
#endif
