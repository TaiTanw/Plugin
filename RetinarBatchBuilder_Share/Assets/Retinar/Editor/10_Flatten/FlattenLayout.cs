using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Art/<名>/ 下的物理路径。夹名来自各 Processor 的 const（不用反射取夹名）。
// Prefab/ 不是资源大类，是平铺产物槽。
// =====================================================================================

/// <summary>平铺后 Art 包内的标准路径。</summary>
public static class FlattenLayout
{
    public const string PrefabFolderName = "Prefab";

    public static string PrefabFolder(string assetFolder)
    {
        return Combine(assetFolder, PrefabFolderName);
    }

    public static string ModelFolder(string assetFolder)
    {
        return Combine(assetFolder, ModelFlattenProcessor.ProcessorId);
    }

    public static string MaterialFolder(string assetFolder)
    {
        return Combine(assetFolder, MaterialFlattenProcessor.ProcessorId);
    }

    public static string AnimationFolder(string assetFolder)
    {
        return Combine(assetFolder, AnimationFlattenProcessor.ProcessorId);
    }

    public static string TextFolder(string assetFolder)
    {
        return Combine(assetFolder, TextFlattenProcessor.ProcessorId);
    }

    public static string AudioFolder(string assetFolder)
    {
        return Combine(assetFolder, AudioFlattenProcessor.ProcessorId);
    }

    public static string ShaderFolder(string assetFolder)
    {
        return Combine(assetFolder, ShaderFlattenProcessor.ProcessorId);
    }

    public static string TextureFolder(string assetFolder)
    {
        return Combine(assetFolder, ImageFlattenProcessor.ProcessorId, ImageFlattenProcessor.FolderTexture);
    }

    public static string UiFolder(string assetFolder)
    {
        return Combine(assetFolder, ImageFlattenProcessor.ProcessorId, ImageFlattenProcessor.FolderUi);
    }

    public static string ImageUnknownFolder(string assetFolder)
    {
        return Combine(assetFolder, ImageFlattenProcessor.ProcessorId, ImageFlattenProcessor.FolderUnknown);
    }

    public static string RootUnknownFolder(string assetFolder)
    {
        return Combine(assetFolder, UnknownFlattenProcessor.ProcessorId);
    }

    public static string Combine(string assetFolder, params string[] parts)
    {
        string current = (assetFolder ?? string.Empty).Replace("\\", "/").TrimEnd('/');
        if (parts == null)
        {
            return current;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            current = current + "/" + parts[i].Replace("\\", "/").Trim('/');
        }

        return current;
    }

    public static void EnsureStandardFolders(string assetFolder)
    {
        EnsureFolder(assetFolder);
        EnsureFolder(PrefabFolder(assetFolder));
        EnsureFolder(ModelFolder(assetFolder));
        EnsureFolder(MaterialFolder(assetFolder));
        EnsureFolder(AnimationFolder(assetFolder));
        EnsureFolder(TextFolder(assetFolder));
        EnsureFolder(TextureFolder(assetFolder));
    }

    public static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Replace("\\", "/").Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    /// <summary>贴图已在本包 image 单元（含 UI/Unknown）或旧顶层 Texture/ 内。</summary>
    public static bool IsLocalArtTexture(string assetFolder, string texturePath)
    {
        if (string.IsNullOrEmpty(assetFolder) || string.IsNullOrEmpty(texturePath))
        {
            return false;
        }

        string path = texturePath.Replace("\\", "/");
        string folder = assetFolder.Replace("\\", "/").TrimEnd('/');
        return path.StartsWith(TextureFolder(folder) + "/", System.StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(UiFolder(folder) + "/", System.StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(ImageUnknownFolder(folder) + "/", System.StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(folder + "/Texture/", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>按文件名在本包贴图出口中找已有副本；都没有则返回 image/Texture 作为拷贝目标。</summary>
    public static string ResolveExistingOrDefaultTexturePath(string assetFolder, string fileName)
    {
        string[] candidates =
        {
            TextureFolder(assetFolder) + "/" + fileName,
            UiFolder(assetFolder) + "/" + fileName,
            ImageUnknownFolder(assetFolder) + "/" + fileName,
            assetFolder + "/Texture/" + fileName
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture>(candidates[i]) != null)
            {
                return candidates[i];
            }
        }

        return TextureFolder(assetFolder) + "/" + fileName;
    }

    public static string AssetFolderFromTextureFolder(string textureFolder)
    {
        string norm = (textureFolder ?? string.Empty).Replace("\\", "/").TrimEnd('/');
        string unitSuffix = "/" + ImageFlattenProcessor.ProcessorId + "/" + ImageFlattenProcessor.FolderTexture;
        if (norm.EndsWith(unitSuffix, System.StringComparison.OrdinalIgnoreCase))
        {
            return norm.Substring(0, norm.Length - unitSuffix.Length);
        }

        if (norm.EndsWith("/Texture", System.StringComparison.OrdinalIgnoreCase))
        {
            return norm.Substring(0, norm.Length - "/Texture".Length);
        }

        return Path.GetDirectoryName(norm).Replace("\\", "/");
    }
}
