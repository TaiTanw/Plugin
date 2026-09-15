using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// =====================================================================================
// Shared — FBX 外置贴图：同目录图、Texture/.fbm 夹、FBX 内写出的相对路径。
// ① 入库跟拷，与 ObjPackageFiles / GltfPackageFiles 同类。不改 Importer，不 Extract。
// =====================================================================================

/// <summary>.fbx 旁路扫描结果。</summary>
public sealed class FbxExternalScan
{
    public readonly List<string> SidecarFullPaths = new List<string>();
    public readonly List<string> MissingUris = new List<string>();
}

/// <summary>收集 FBX 导入时必须跟在旁边的独立贴图文件。</summary>
public static class FbxPackageFiles
{
    static readonly string[] TextureExtensions =
    {
        ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".bmp"
    };

    static readonly string[] CompanionFolderNames =
    {
        "Texture", "Textures", "Maps", "Tex", "Material", "Materials", "Mats"
    };

    /// <summary>FBX 同目录及常见伴生夹下的独立贴图。不拷其它模型。</summary>
    public static FbxExternalScan Scan(string fbxFullPath)
    {
        var scan = new FbxExternalScan();
        if (string.IsNullOrEmpty(fbxFullPath) || !File.Exists(fbxFullPath))
        {
            return scan;
        }

        string fbxDir;
        try
        {
            fbxDir = Path.GetFullPath(Path.GetDirectoryName(fbxFullPath) ?? string.Empty)
                .Replace("\\", "/");
        }
        catch (Exception)
        {
            return scan;
        }

        if (string.IsNullOrEmpty(fbxDir) || !Directory.Exists(fbxDir))
        {
            return scan;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectSameDirectoryTextures(fbxDir, scan, seen);
        CollectCompanionFolders(fbxDir, scan, seen);
        CollectFbmFolders(fbxDir, scan, seen);
        CollectReferencedInFbx(fbxFullPath, fbxDir, scan, seen);
        return scan;
    }

    public static string MakeRelativeToFbxDir(string fbxFullPath, string sidecarFullPath)
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(fbxFullPath ?? string.Empty));
        string path = Path.GetFullPath(sidecarFullPath ?? string.Empty).Replace("\\", "/");
        if (string.IsNullOrEmpty(root))
        {
            return Path.GetFileName(path);
        }

        root = Path.GetFullPath(root).Replace("\\", "/").TrimEnd('/') + "/";
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(root.Length);
        }

        return Path.GetFileName(path);
    }

    public static bool IsTextureFileName(string path)
    {
        string ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        for (int i = 0; i < TextureExtensions.Length; i++)
        {
            if (ext == TextureExtensions[i])
            {
                return true;
            }
        }

        return false;
    }

    static void CollectSameDirectoryTextures(string fbxDir, FbxExternalScan scan, HashSet<string> seen)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(fbxDir);
        }
        catch (Exception)
        {
            return;
        }

        for (int i = 0; i < files.Length; i++)
        {
            TryAddExisting(files[i], fbxDir, scan, seen, false);
        }
    }

    static void CollectCompanionFolders(string fbxDir, FbxExternalScan scan, HashSet<string> seen)
    {
        for (int i = 0; i < CompanionFolderNames.Length; i++)
        {
            string folder = Path.Combine(fbxDir, CompanionFolderNames[i]);
            CollectTexturesUnderFolder(folder, fbxDir, scan, seen);
        }
    }

    static void CollectFbmFolders(string fbxDir, FbxExternalScan scan, HashSet<string> seen)
    {
        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories(fbxDir);
        }
        catch (Exception)
        {
            return;
        }

        for (int i = 0; i < dirs.Length; i++)
        {
            if (!dirs[i].EndsWith(".fbm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CollectTexturesUnderFolder(dirs[i], fbxDir, scan, seen);
        }
    }

    static void CollectTexturesUnderFolder(
        string folder,
        string fbxDir,
        FbxExternalScan scan,
        HashSet<string> seen)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
        }
        catch (Exception)
        {
            return;
        }

        for (int i = 0; i < files.Length; i++)
        {
            TryAddExisting(files[i], fbxDir, scan, seen, false);
        }
    }

    static void CollectReferencedInFbx(
        string fbxFullPath,
        string fbxDir,
        FbxExternalScan scan,
        HashSet<string> seen)
    {
        string text;
        try
        {
            byte[] bytes = File.ReadAllBytes(fbxFullPath);
            text = Encoding.ASCII.GetString(bytes);
        }
        catch (Exception)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ExtractTextureLikeTokens(text, names);
        foreach (string token in names)
        {
            string n = token.Replace("\\", "/").Trim();
            if (n.StartsWith("./"))
            {
                n = n.Substring(2);
            }

            if (n.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                continue;
            }

            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(fbxDir, n)).Replace("\\", "/");
            }
            catch (Exception)
            {
                scan.MissingUris.Add(token);
                continue;
            }

            if (!IsUnderDirectory(full, fbxDir))
            {
                continue;
            }

            TryAddExisting(full, fbxDir, scan, seen, true);
        }
    }

    static void ExtractTextureLikeTokens(string text, HashSet<string> into)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var current = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (IsPathChar(c))
            {
                current.Append(c);
                continue;
            }

            FlushTextureToken(current, into);
        }

        FlushTextureToken(current, into);
    }

    static bool IsPathChar(char c)
    {
        if (char.IsLetterOrDigit(c))
        {
            return true;
        }

        return c == '.' || c == '_' || c == '-' || c == '/' || c == '\\' || c == ' ';
    }

    static void FlushTextureToken(StringBuilder current, HashSet<string> into)
    {
        if (current.Length == 0)
        {
            return;
        }

        string token = current.ToString().Trim();
        current.Length = 0;
        if (token.Length < 5 || token.Length > 260 || !IsTextureFileName(token))
        {
            return;
        }

        into.Add(token);
    }

    static void TryAddExisting(
        string candidate,
        string fbxDir,
        FbxExternalScan scan,
        HashSet<string> seen,
        bool recordMissing)
    {
        string full;
        try
        {
            full = Path.GetFullPath(candidate).Replace("\\", "/");
        }
        catch (Exception)
        {
            if (recordMissing)
            {
                scan.MissingUris.Add(candidate);
            }

            return;
        }

        if (!IsTextureFileName(full) || !IsUnderDirectory(full, fbxDir))
        {
            return;
        }

        if (!File.Exists(full))
        {
            if (recordMissing)
            {
                string dir = fbxDir.Replace("\\", "/").TrimEnd('/') + "/";
                string rel = full.StartsWith(dir, StringComparison.OrdinalIgnoreCase)
                    ? full.Substring(dir.Length)
                    : Path.GetFileName(full);
                scan.MissingUris.Add(rel);
            }

            return;
        }

        if (!seen.Add(full))
        {
            return;
        }

        scan.SidecarFullPaths.Add(full);
    }

    static bool IsUnderDirectory(string fullPath, string directory)
    {
        string dir = directory.Replace("\\", "/").TrimEnd('/') + "/";
        string path = fullPath.Replace("\\", "/");
        return path.StartsWith(dir, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   path,
                   directory.Replace("\\", "/").TrimEnd('/'),
                   StringComparison.OrdinalIgnoreCase);
    }
}
