using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   只负责"按扩展名找到对应的编解码器"。不解码、不编码、不判断该不该处理。
//
// 为什么用反射发现而不是写一个硬编码的 new PngTextureCodec() 列表：
//   硬编码列表意味着每加一种格式都要回来改这个文件，很容易漏。用反射之后，
//   新增格式只需要"加一个实现 ITextureFileCodec 的类"，注册表自动认得它，
//   窗口里能处理的扩展名列表也会自动跟着变长。
//   反射只在第一次访问时扫一遍当前程序集并缓存，之后都是字典查找，没有性能问题。
// =====================================================================================
public static class TextureCodecRegistry
{
    private static List<ITextureFileCodec> codecs;

    public static IList<ITextureFileCodec> All
    {
        get
        {
            EnsureDiscovered();
            return codecs;
        }
    }

    /// <summary>
    /// 找不到对应编解码器时返回 null，调用方需要自己处理（通常是记一条"跳过"并继续）。
    /// </summary>
    public static ITextureFileCodec FindByAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        EnsureDiscovered();
        foreach (ITextureFileCodec codec in codecs)
        {
            if (codec.CanHandle(extension))
            {
                return codec;
            }
        }

        return null;
    }

    public static bool IsSupported(string assetPath)
    {
        return FindByAssetPath(assetPath) != null;
    }

    private static void EnsureDiscovered()
    {
        if (codecs != null)
        {
            return;
        }

        codecs = new List<ITextureFileCodec>();
        Type interfaceType = typeof(ITextureFileCodec);

        // 只扫这个接口所在的程序集就够了：编解码器按约定和接口放在同一个 Editor 目录下，
        // 扫全部已加载程序集会拖慢首次访问，而且可能撞上第三方插件里同名的类型。
        foreach (Type type in interfaceType.Assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !interfaceType.IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogWarning("[TextureCodecRegistry] " + type.Name + " 实现了 ITextureFileCodec 但没有无参构造函数，已跳过。");
                continue;
            }

            codecs.Add((ITextureFileCodec)Activator.CreateInstance(type));
        }

        codecs = codecs.OrderBy(codec => codec.DisplayName).ToList();
    }
}
