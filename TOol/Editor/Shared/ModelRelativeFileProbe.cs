using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// File references that must keep their paths relative to a model when the model is copied.
/// A format-specific scanner supplies these facts; Unity's AssetDatabase dependencies alone
/// are not sufficient for files such as a glTF buffer.
/// </summary>
public sealed class ModelRelativeFileScan
{
    public bool FileOk;
    public bool HasRelativeFiles;
    public readonly List<string> SidecarFullPaths = new List<string>();
    public readonly List<string> MissingReferences = new List<string>();
    public readonly List<string> Notes = new List<string>();
}

public static class ModelRelativeFileProbe
{
    // Add a scanner for each format whose source file contains relative file references.
    // OBJ currently has a separate, tested mtllib/texture path in the categorized copier.
    private static readonly Dictionary<string, Func<string, ModelRelativeFileScan>> Scanners =
        new Dictionary<string, Func<string, ModelRelativeFileScan>>(StringComparer.OrdinalIgnoreCase)
        {
            { ".gltf", ScanGltf }
        };

    public static bool TryScan(string modelFullPath, out ModelRelativeFileScan scan)
    {
        scan = null;
        if (string.IsNullOrEmpty(modelFullPath)) return false;

        Func<string, ModelRelativeFileScan> scanner;
        if (!Scanners.TryGetValue(Path.GetExtension(modelFullPath), out scanner)) return false;

        scan = scanner(modelFullPath);
        return true;
    }

    public static string MakeRelativeToModelDir(string modelFullPath, string sidecarFullPath)
    {
        string root = Path.GetFullPath(Path.GetDirectoryName(modelFullPath) ?? string.Empty)
            .Replace("\\", "/").TrimEnd('/');
        string path = Path.GetFullPath(sidecarFullPath ?? string.Empty).Replace("\\", "/");
        return path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)
            ? path.Substring(root.Length + 1)
            : null;
    }

    private static ModelRelativeFileScan ScanGltf(string modelFullPath)
    {
        GltfExternalScan source = GltfPackageFiles.Scan(modelFullPath);
        var scan = new ModelRelativeFileScan
        {
            FileOk = source.FileOk,
            HasRelativeFiles = source.HasExternalUris
        };
        scan.SidecarFullPaths.AddRange(source.SidecarFullPaths);
        scan.MissingReferences.AddRange(source.MissingUris);
        scan.Notes.AddRange(source.Notes);
        foreach (string sidecar in source.SidecarFullPaths)
        {
            if (MakeRelativeToModelDir(modelFullPath, sidecar) == null)
                scan.MissingReferences.Add("模型目录外引用: " + sidecar);
        }
        return scan;
    }
}
