using System.Collections.Generic;
using UnityEngine;

/// <summary>模型单元：文件直接落在 Model/ 根下，避免 Model/Model。</summary>
public sealed class ModelFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "Model";

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "模型"; } }

    public int Order { get { return 10; } }

    public string[] DefaultSuffixes
    {
        get { return new[] { "fbx", "obj" }; }
    }

    public string[] OutputFolderHints
    {
        get { return new[] { ProcessorId + "/（文件直接在单元根下）" }; }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        return FlattenCategorySettings.MatchesSuffix(assetPath, settings.GetSuffixes(Id, DefaultSuffixes));
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        return ProcessorId;
    }
}

/// <summary>
/// 贴图单元：多出口。Sprite → UI；Cube 仍走 Texture 职责；无法判定 → image/Unknown。
/// 子夹名写在本类 const，静态检查互不撞名。
/// </summary>
public sealed class ImageFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "image";
    public const string FolderTexture = "Texture";
    public const string FolderUi = "UI";
    public const string FolderUnknown = "Unknown";

    static ImageFlattenProcessor()
    {
        var names = new[] { FolderTexture, FolderUi, FolderUnknown };
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < names.Length; i++)
        {
            if (!seen.Add(names[i]))
            {
                Debug.LogError("[ImageFlattenProcessor] 出口文件夹 const 撞名: " + names[i] +
                    "。Texture / UI / Unknown 必须互不相同。");
            }
        }
    }

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "贴图"; } }

    public int Order { get { return 20; } }

    public string[] DefaultSuffixes
    {
        get { return new[] { "png", "jpg", "jpeg", "tga", "tif", "tiff", "exr", "hdr", "psd" }; }
    }

    public string[] OutputFolderHints
    {
        get
        {
            return new[]
            {
                ProcessorId + "/" + FolderTexture,
                ProcessorId + "/" + FolderUi,
                ProcessorId + "/" + FolderUnknown
            };
        }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        return FlattenCategorySettings.MatchesSuffix(assetPath, settings.GetSuffixes(Id, DefaultSuffixes));
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
        if (importer == null)
        {
            return ProcessorId + "/" + FolderUnknown;
        }

        // Cube 仍走 Texture 职责，不进 UI。
        if (importer.textureShape == UnityEditor.TextureImporterShape.TextureCube)
        {
            return ProcessorId + "/" + FolderTexture;
        }

        // 只认 Sprite 类型。Default 贴图的 spriteMode 在 Unity 里常残留 Single，
        // 若按 spriteMode != None 判断，几乎所有 PNG 都会被误送进 image/UI。
        if (importer.textureType == UnityEditor.TextureImporterType.Sprite)
        {
            return ProcessorId + "/" + FolderUi;
        }

        return ProcessorId + "/" + FolderTexture;
    }
}

/// <summary>材质单元：文件直接落在 Material/ 根下。</summary>
public sealed class MaterialFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "Material";

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "材质"; } }

    public int Order { get { return 30; } }

    public string[] DefaultSuffixes
    {
        get { return new[] { "mat" }; }
    }

    public string[] OutputFolderHints
    {
        get { return new[] { ProcessorId + "/（文件直接在单元根下）" }; }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        return FlattenCategorySettings.MatchesSuffix(assetPath, settings.GetSuffixes(Id, DefaultSuffixes));
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        return ProcessorId;
    }
}

/// <summary>动画单元：文件直接落在 Animation/ 根下。</summary>
public sealed class AnimationFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "Animation";

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "动画"; } }

    public int Order { get { return 40; } }

    public string[] DefaultSuffixes
    {
        get { return new[] { "anim", "controller" }; }
    }

    public string[] OutputFolderHints
    {
        get { return new[] { ProcessorId + "/（文件直接在单元根下）" }; }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        return FlattenCategorySettings.MatchesSuffix(assetPath, settings.GetSuffixes(Id, DefaultSuffixes));
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        return ProcessorId;
    }
}

/// <summary>文本单元：文件直接落在 Text/ 根下。</summary>
public sealed class TextFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "Text";

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "文本"; } }

    public int Order { get { return 50; } }

    public string[] DefaultSuffixes
    {
        get { return new[] { "txt", "bytes", "json", "xml", "csv", "lua" }; }
    }

    public string[] OutputFolderHints
    {
        get { return new[] { ProcessorId + "/（文件直接在单元根下）" }; }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        return FlattenCategorySettings.MatchesSuffix(assetPath, settings.GetSuffixes(Id, DefaultSuffixes));
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        return ProcessorId;
    }
}

/// <summary>音频单元：文件直接落在 Audio/ 根下。</summary>
public sealed class AudioFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "Audio";

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "音频"; } }

    public int Order { get { return 60; } }

    public string[] DefaultSuffixes
    {
        get { return new[] { "wav", "mp3", "ogg", "aif", "aiff" }; }
    }

    public string[] OutputFolderHints
    {
        get { return new[] { ProcessorId + "/（文件直接在单元根下）" }; }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        return FlattenCategorySettings.MatchesSuffix(assetPath, settings.GetSuffixes(Id, DefaultSuffixes));
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        return ProcessorId;
    }
}

/// <summary>自定义 Shader：只拷 Assets/ 下资源，永不拷 Packages/ 与内置。</summary>
public sealed class ShaderFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "Shader";

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "着色器"; } }

    public int Order { get { return 70; } }

    public string[] DefaultSuffixes
    {
        get { return new[] { "shader", "cginc", "hlsl", "compute" }; }
    }

    public string[] OutputFolderHints
    {
        get { return new[] { ProcessorId + "/（仅 Assets/ 自定义；Packages/ 不拷）" }; }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        string normalized = assetPath.Replace("\\", "/");
        if (!normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return FlattenCategorySettings.MatchesSuffix(assetPath, settings.GetSuffixes(Id, DefaultSuffixes));
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        return ProcessorId;
    }
}

/// <summary>
/// 根级 Unknown：没有处理器认领的 Assets 文件。仍平铺、仍打包，只提示不阻断。
/// 不可在面板关闭。
/// </summary>
public sealed class UnknownFlattenProcessor : IFlattenCategoryProcessor
{
    public const string ProcessorId = "Unknown";

    public string Id { get { return ProcessorId; } }

    public string DisplayName { get { return "未归类（Unknown）"; } }

    public int Order { get { return 1000; } }

    public string[] DefaultSuffixes
    {
        get { return new string[0]; }
    }

    public string[] OutputFolderHints
    {
        get { return new[] { ProcessorId + "/（无处理器认领时；提示不阻断）" }; }
    }

    public bool Matches(string assetPath, FlattenCategorySettings settings)
    {
        return false;
    }

    public string ResolveRelativeFolder(string assetPath)
    {
        return ProcessorId;
    }
}
