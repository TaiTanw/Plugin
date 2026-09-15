#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;

public class FbxPackageFilesTests
{
    string root;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "FbxPackageFilesTests-" + Path.GetRandomFileName());
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
    public void Scan_SameDirectoryJpg_IsSidecar()
    {
        string fbx = Path.Combine(root, "fbx.FBX");
        string jpg = Path.Combine(root, "j6_11.jpg");
        File.WriteAllBytes(fbx, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(jpg, new byte[] { 4, 5, 6 });
        File.WriteAllText(Path.Combine(root, "obj.mtl"), "newmtl x");

        FbxExternalScan scan = FbxPackageFiles.Scan(fbx);

        Assert.That(scan.SidecarFullPaths.Count, Is.EqualTo(1));
        Assert.That(Path.GetFileName(scan.SidecarFullPaths[0]), Is.EqualTo("j6_11.jpg").IgnoreCase);
        Assert.That(scan.MissingUris, Is.Empty);
    }

    [Test]
    public void Scan_TextureSubfolder_IsSidecar()
    {
        string fbx = Path.Combine(root, "model.fbx");
        string texDir = Path.Combine(root, "Texture");
        Directory.CreateDirectory(texDir);
        File.WriteAllBytes(fbx, new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(texDir, "hull.png"), new byte[] { 2 });

        FbxExternalScan scan = FbxPackageFiles.Scan(fbx);

        Assert.That(scan.SidecarFullPaths.Count, Is.EqualTo(1));
        Assert.That(FbxPackageFiles.MakeRelativeToFbxDir(fbx, scan.SidecarFullPaths[0]).Replace("\\", "/"),
            Is.EqualTo("Texture/hull.png").IgnoreCase);
    }

    [Test]
    public void Scan_FbmFolder_IsSidecar()
    {
        string fbx = Path.Combine(root, "plane.fbx");
        string fbm = Path.Combine(root, "plane.fbm");
        Directory.CreateDirectory(fbm);
        File.WriteAllBytes(fbx, new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(fbm, "a.tga"), new byte[] { 2 });

        FbxExternalScan scan = FbxPackageFiles.Scan(fbx);

        Assert.That(scan.SidecarFullPaths.Count, Is.EqualTo(1));
        Assert.That(Path.GetFileName(scan.SidecarFullPaths[0]), Is.EqualTo("a.tga").IgnoreCase);
    }

    [Test]
    public void Scan_J6Source_FindsSameDirectoryJpg()
    {
        string fbx = @"D:\工程\飞机模型待处理\【m1936】歼6-yy3d\3d\fbx.FBX";
        if (!File.Exists(fbx))
        {
            Assert.Ignore("本机没有歼6测试源");
        }

        FbxExternalScan scan = FbxPackageFiles.Scan(fbx);
        string joined = string.Join("|", scan.SidecarFullPaths.ToArray());
        Assert.That(joined.ToLowerInvariant(), Does.Contain("j6_11.jpg"));
    }

    [Test]
    public void Scan_MissingReferencedJpg_GoesToMissingUris()
    {
        string fbx = Path.Combine(root, "ref.fbx");
        File.WriteAllText(fbx, "RelativeFilename: \"gone.jpg\"");

        FbxExternalScan scan = FbxPackageFiles.Scan(fbx);

        Assert.That(scan.SidecarFullPaths, Is.Empty);
        Assert.That(scan.MissingUris.Count, Is.EqualTo(1));
        Assert.That(scan.MissingUris[0], Does.Contain("gone.jpg"));
    }
}
#endif
