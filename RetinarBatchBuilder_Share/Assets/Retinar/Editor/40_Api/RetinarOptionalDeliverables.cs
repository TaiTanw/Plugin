using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 40_Api — AB 成功后的可选交付。只看预设体与 Options，不读调度层 / ctx / Art 夹名。
// 失败只打日志，不写入 RetinarAbBuildResult.FailLines。
// =====================================================================================

/// <summary>⑥ 窄口上默认关闭的模型拷贝与资源信息表。</summary>
public static class RetinarOptionalDeliverables
{
    const string TemplateAssetPath =
        "Assets/Plugin/RetinarBatchBuilder_Share/Assets/Retinar/Templates/asset_info_template.xlsx";

    /// <summary>该预设体双端 AB 已成功时调用。开关都关则立刻返回。</summary>
    public static void WriteAfterAb(
        string prefabPath,
        string assetName,
        string deliverableRoot,
        RetinarAbBuildOptions options)
    {
        if (options == null || (!options.ExportSourceModels && !options.ExportAssetInfo))
        {
            return;
        }

        if (options.ExportSourceModels)
        {
            try
            {
                CopyKernelModels(prefabPath, assetName, deliverableRoot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Retinar][Ab] 模型拷贝失败（不阻断 AB）: " + assetName + " " + ex.Message);
            }
        }

        if (options.ExportAssetInfo)
        {
            try
            {
                WriteAssetInfo(prefabPath, assetName, deliverableRoot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Retinar][Ab] 资源信息表失败（不阻断 AB）: " + assetName + " " + ex.Message);
            }
        }
    }

    static void CopyKernelModels(string prefabPath, string assetName, string deliverableRoot)
    {
        string modelDir = Path.Combine(
            RetinarDeliverableIo.GetAssetDeliverableRoot(assetName, deliverableRoot),
            RetinarPaths.DeliverableSourceFolder,
            "Model");
        RetinarEditorUtil.EnsureDiskDirectory(modelDir);

        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int copied = 0;
        for (int i = 0; i < dependencies.Length; i++)
        {
            string assetPath = (dependencies[i] ?? string.Empty).Replace("\\", "/");
            if (!IsKernelModelExtension(Path.GetExtension(assetPath)))
            {
                continue;
            }

            string sourceFull = ToProjectFullPath(assetPath);
            if (string.IsNullOrEmpty(sourceFull) || !File.Exists(sourceFull))
            {
                Debug.LogWarning("[Retinar][Ab] 模型文件不在磁盘: " + assetPath);
                continue;
            }

            copied += CopyFileToModelDir(sourceFull, Path.GetFileName(assetPath), modelDir, written);
            if (string.Equals(Path.GetExtension(assetPath), ".gltf", StringComparison.OrdinalIgnoreCase))
            {
                copied += CopyGltfSidecars(sourceFull, modelDir, written);
            }
        }

        Debug.Log("[Retinar][Ab] 模型文件 " + copied + " 个 → " + modelDir);
    }

    static int CopyGltfSidecars(
        string gltfFullPath,
        string modelDir,
        Dictionary<string, string> written)
    {
        GltfExternalScan scan = GltfPackageFiles.Scan(gltfFullPath);
        int copied = 0;
        for (int i = 0; i < scan.SidecarFullPaths.Count; i++)
        {
            string sidecar = scan.SidecarFullPaths[i];
            if (string.IsNullOrEmpty(sidecar) || !File.Exists(sidecar))
            {
                continue;
            }

            string relative = GltfPackageFiles.MakeRelativeToGltfDir(gltfFullPath, sidecar)
                .Replace("\\", "/");
            if (!IsSafeRelativePath(relative))
            {
                Debug.LogWarning("[Retinar][Ab] 跳过跳出模型目录的 glTF 伴生: " + sidecar);
                continue;
            }

            copied += CopyFileToModelDir(sidecar, relative, modelDir, written);
        }

        if (scan.MissingUris.Count > 0)
        {
            Debug.LogWarning("[Retinar][Ab] glTF 伴生缺失 " + scan.MissingUris.Count +
                             " 条（不阻断 AB）: " + gltfFullPath);
        }

        return copied;
    }

    static int CopyFileToModelDir(
        string sourceFull,
        string relativeName,
        string modelDir,
        Dictionary<string, string> written)
    {
        string dest = Path.GetFullPath(Path.Combine(modelDir, relativeName.Replace("/", Path.DirectorySeparatorChar.ToString())));
        string root = Path.GetFullPath(modelDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!dest.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !dest.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[Retinar][Ab] 拒绝写出模型目录: " + relativeName);
            return 0;
        }

        string previous;
        if (written.TryGetValue(dest, out previous) &&
            !string.Equals(previous, sourceFull, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[Retinar][Ab] 同名模型文件后者覆盖: " + Path.GetFileName(dest));
        }

        string destDir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(destDir))
        {
            RetinarEditorUtil.EnsureDiskDirectory(destDir);
        }

        File.Copy(sourceFull, dest, true);
        written[dest] = sourceFull;
        return 1;
    }

    static void WriteAssetInfo(string prefabPath, string assetName, string deliverableRoot)
    {
        string template = ResolveTemplateDiskPath();
        if (string.IsNullOrEmpty(template))
        {
            Debug.LogWarning("[Retinar][Ab] 未找到 asset_info_template.xlsx，跳过资源信息表（不阻断 AB）");
            return;
        }

        PrefabSheetStats stats = CollectPrefabStats(
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
            prefabPath);

        string docsDir = Path.Combine(
            RetinarDeliverableIo.GetAssetDeliverableRoot(assetName, deliverableRoot),
            RetinarPaths.DeliverableDocsFolder);
        RetinarEditorUtil.EnsureDiskDirectory(docsDir);
        string outputPath = Path.Combine(docsDir, "asset_info.xlsx");
        string sheet = FillResourceSheet(ReadSheet1(template), assetName, prefabPath, stats);
        WriteSheet1(template, outputPath, sheet);
        Debug.Log("[Retinar][Ab] 资源信息表 → " + outputPath +
                  " 顶点=" + stats.Vertices + " 三角面=" + stats.Triangles);
    }

    static string FillResourceSheet(
        string sheetXml,
        string assetName,
        string prefabPath,
        PrefabSheetStats stats)
    {
        sheetXml = ReplaceInlineCell(sheetXml, "A1", assetName);
        sheetXml = ReplaceInlineCell(sheetXml, "B5", assetName);
        sheetXml = ReplaceInlineCell(sheetXml, "B6", "待填写");
        sheetXml = ReplaceInlineCell(sheetXml, "B7", "待填写");
        sheetXml = ReplaceInlineCell(sheetXml, "B8", "待填写");
        sheetXml = ReplaceInlineCell(sheetXml, "B9", "待填写");
        sheetXml = ReplaceInlineCell(sheetXml, "B10", "待填写");
        sheetXml = ReplaceInlineCell(sheetXml, "B13", DateTime.Now.ToString("yyyy-MM-dd"));
        sheetXml = ReplaceInlineCell(sheetXml, "B16", "待补充");
        sheetXml = ReplaceInlineCell(sheetXml, "B17", Application.unityVersion);
        sheetXml = ReplaceInlineCell(sheetXml, "B25", stats.Triangles.ToString());
        sheetXml = ReplaceInlineCell(sheetXml, "B26", stats.Vertices.ToString());
        sheetXml = ReplaceInlineCell(sheetXml, "B27", stats.RendererCount.ToString());
        sheetXml = ReplaceInlineCell(sheetXml, "B28", stats.MaterialCount.ToString());
        sheetXml = ReplaceInlineCell(sheetXml, "B30", stats.TextureSummary);
        sheetXml = ReplaceInlineCell(sheetXml, "B32", stats.AnimationClipCount.ToString());
        sheetXml = ReplaceInlineCell(sheetXml, "B33", stats.ColliderCount.ToString());
        sheetXml = ReplaceInlineCell(sheetXml, "B34", prefabPath ?? string.Empty);
        sheetXml = ReplaceInlineCell(sheetXml, "B37", "待补充");
        sheetXml = ReplaceInlineCell(sheetXml, "B38", "待补充");
        sheetXml = ReplaceInlineCell(sheetXml, "B40", "待补充");
        sheetXml = ReplaceInlineCell(sheetXml, "B41", "待补充");
        return sheetXml;
    }

    struct PrefabSheetStats
    {
        public int Vertices;
        public int Triangles;
        public int RendererCount;
        public int MaterialCount;
        public int AnimationClipCount;
        public int ColliderCount;
        public string TextureSummary;
    }

    static PrefabSheetStats CollectPrefabStats(GameObject prefab, string prefabPath)
    {
        var stats = new PrefabSheetStats();
        stats.TextureSummary = "0 张贴图";
        if (prefab == null)
        {
            return stats;
        }

        var seenMeshes = new HashSet<int>();
        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            AddMesh(filters[i] != null ? filters[i].sharedMesh : null, seenMeshes, ref stats.Vertices, ref stats.Triangles);
        }

        SkinnedMeshRenderer[] skinned = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            AddMesh(skinned[i] != null ? skinned[i].sharedMesh : null, seenMeshes, ref stats.Vertices, ref stats.Triangles);
        }

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        stats.RendererCount = renderers.Length;
        var materials = new HashSet<int>();
        var textures = new HashSet<int>();
        int maxW = 0;
        int maxH = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] shared = renderer.sharedMaterials;
            for (int m = 0; m < shared.Length; m++)
            {
                Material material = shared[m];
                if (material == null || !materials.Add(material.GetInstanceID()))
                {
                    continue;
                }

                string[] propertyNames = material.GetTexturePropertyNames();
                for (int p = 0; p < propertyNames.Length; p++)
                {
                    Texture texture = material.GetTexture(propertyNames[p]);
                    if (texture == null || !textures.Add(texture.GetInstanceID()))
                    {
                        continue;
                    }

                    maxW = Mathf.Max(maxW, texture.width);
                    maxH = Mathf.Max(maxH, texture.height);
                }
            }
        }

        stats.MaterialCount = materials.Count;
        stats.TextureSummary = textures.Count + " 张贴图；最大 " + maxW + " x " + maxH;
        stats.ColliderCount = prefab.GetComponentsInChildren<Collider>(true).Length;
        stats.AnimationClipCount = CountAnimationClips(prefab, prefabPath);
        return stats;
    }

    static int CountAnimationClips(GameObject prefab, string prefabPath)
    {
        var seen = new HashSet<int>();
        Animation[] animations = prefab.GetComponentsInChildren<Animation>(true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animation = animations[i];
            if (animation == null)
            {
                continue;
            }

            AddClip(animation.clip, seen);
            foreach (AnimationState state in animation)
            {
                AddClip(state != null ? state.clip : null, seen);
            }
        }

        Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            RuntimeAnimatorController controller = animators[i] != null
                ? animators[i].runtimeAnimatorController
                : null;
            if (controller == null)
            {
                continue;
            }

            AnimationClip[] clips = controller.animationClips;
            for (int c = 0; c < clips.Length; c++)
            {
                AddClip(clips[c], seen);
            }
        }

        if (!string.IsNullOrEmpty(prefabPath))
        {
            string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                string path = dependencies[i];
                if (!IsKernelModelExtension(Path.GetExtension(path)))
                {
                    continue;
                }

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int a = 0; a < assets.Length; a++)
                {
                    AddClip(assets[a] as AnimationClip, seen);
                }
            }
        }

        return seen.Count;
    }

    static void AddClip(AnimationClip clip, HashSet<int> seen)
    {
        if (clip == null || clip.name.StartsWith("__preview", StringComparison.Ordinal))
        {
            return;
        }

        seen.Add(clip.GetInstanceID());
    }

    static void AddMesh(Mesh mesh, HashSet<int> seen, ref int vertices, ref int triangles)
    {
        if (mesh == null || !seen.Add(mesh.GetInstanceID()))
        {
            return;
        }

        vertices += mesh.vertexCount;
        triangles += mesh.triangles.Length / 3;
    }

    static string ReadSheet1(string templatePath)
    {
        using (var stream = File.OpenRead(templatePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/worksheets/sheet1.xml");
            if (entry == null)
            {
                throw new InvalidDataException("模板缺少 xl/worksheets/sheet1.xml");
            }

            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }

    static void WriteSheet1(string templatePath, string outputPath, string sheetXml)
    {
        string tempPath = outputPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        using (var input = File.OpenRead(templatePath))
        using (var output = File.Create(tempPath))
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var dest = new ZipArchive(output, ZipArchiveMode.Create))
        {
            foreach (ZipArchiveEntry entry in source.Entries)
            {
                ZipArchiveEntry created = dest.CreateEntry(entry.FullName, System.IO.Compression.CompressionLevel.Optimal);
                using (Stream from = entry.Open())
                using (Stream to = created.Open())
                {
                    if (string.Equals(entry.FullName.Replace("\\", "/"), "xl/worksheets/sheet1.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var writer = new StreamWriter(to, new UTF8Encoding(false)))
                        {
                            writer.Write(sheetXml);
                        }
                    }
                    else
                    {
                        from.CopyTo(to);
                    }
                }
            }
        }

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        File.Move(tempPath, outputPath);
    }

    static string ReplaceInlineCell(string sheetXml, string cellRef, string value)
    {
        var regex = new Regex(
            "(<x:c r=\"" + Regex.Escape(cellRef) + "\"[^>]*>\\s*<x:v>)(.*?)(</x:v>)",
            RegexOptions.Singleline);
        if (!regex.IsMatch(sheetXml))
        {
            Debug.LogWarning("[Retinar][Ab] 模板缺少单元格 " + cellRef + "，该格未改");
            return sheetXml;
        }

        string escaped = XmlEscape(value ?? string.Empty);
        return regex.Replace(sheetXml, m => m.Groups[1].Value + escaped + m.Groups[3].Value, 1);
    }

    static string XmlEscape(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    static string ResolveTemplateDiskPath()
    {
        string expected = ToProjectFullPath(TemplateAssetPath);
        if (!string.IsNullOrEmpty(expected) && File.Exists(expected))
        {
            return expected;
        }

        string[] guids = AssetDatabase.FindAssets("asset_info_template");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]).Replace("\\", "/");
            if (!assetPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string disk = ToProjectFullPath(assetPath);
            if (!string.IsNullOrEmpty(disk) && File.Exists(disk))
            {
                return disk;
            }
        }

        return null;
    }

    static string ToProjectFullPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        return Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            assetPath.Replace("/", Path.DirectorySeparatorChar.ToString())));
    }

    static bool IsKernelModelExtension(string extension)
    {
        return string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".obj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSafeRelativePath(string relative)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative))
        {
            return false;
        }

        string normalized = relative.Replace("\\", "/");
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.IndexOf("..", StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        return true;
    }
}
