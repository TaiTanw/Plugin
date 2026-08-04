using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   只负责"把一个 .tga 换成同名 .png"这件事的编排。TGA 怎么解、PNG 怎么编，
//   都是问 Codec 层要的；这里唯一自己动手的是文件层面的替换和 GUID 的保全。
//
// 为什么要保住 GUID（这是这个操作里最关键的一点）：
//   Unity 里所有引用都是按 .meta 文件里的 GUID 存的，不是按路径。
//   如果老老实实"新建 foo.png、删掉 foo.tga"，foo.png 会拿到一个全新的 GUID，
//   于是所有引用过 foo.tga 的材质全部变成 None——一次批量转换就能毁掉几十个材质，
//   而且 Unity 不会报错，要等到出包或看到白模才发现。
//   替代方案是转换后扫全工程的材质去重新赋值，但那样既慢又只能覆盖材质，
//   预制体上的直接引用、脚本里的引用、动画曲线上的引用都覆盖不到。
//   这里用的办法是：把 foo.tga.meta 直接改名成 foo.png.meta。
//   .meta 里存的是 GUID 加 TextureImporter 的序列化数据，这份数据对 PNG 同样有效，
//   所以新文件会继承原来的 GUID 和全部导入设置，工程里所有引用一个都不会断。
//
// 为什么整段文件操作要包在 StartAssetEditing / StopAssetEditing 里：
//   替换过程中会有一个瞬间"png 已存在、tga 还没删、meta 还没改名"。如果这个瞬间
//   Unity 触发了一次自动导入，它会给 png 生成一个新 meta，GUID 保全就白做了。
//   StartAssetEditing 会把导入挂起，等 StopAssetEditing 时才作为一个整体处理。
// =====================================================================================
public class ConvertTgaToPngOperation : ITextureAssetOperation
{
    public string Id
    {
        get { return "convert_tga_to_png"; }
    }

    public string DisplayName
    {
        get { return "TGA 转 PNG"; }
    }

    public string Description
    {
        get
        {
            return "把 .tga 解码后重新编码成同名 .png。会把原来的 .tga.meta 改名成 .png.meta，" +
                   "因此 GUID 和导入设置都会保留，工程里已有的引用不会断开。" +
                   "PNG 是无损格式，同样内容通常比未压缩 TGA 小很多。";
        }
    }

    public int Order
    {
        get { return 200; }
    }

    public bool CanProcess(string assetPath, TextureProcessSettings settings)
    {
        if (string.IsNullOrEmpty(assetPath) ||
            Path.GetExtension(assetPath).ToLowerInvariant() != ".tga")
        {
            return false;
        }

        return AssetPathUtility.GetFileLength(assetPath) >= 0;
    }

    /// <summary>
    /// 总体流程：读 TGA 字节 → 解码 → 编码成 PNG → 在挂起导入的状态下完成文件替换与 meta 改名。
    /// </summary>
    public TextureOperationResult Execute(TextureOperationContext context)
    {
        string tgaFullPath = AssetPathUtility.ToFullPath(context.AssetPath);
        if (string.IsNullOrEmpty(tgaFullPath) || !File.Exists(tgaFullPath))
        {
            return TextureOperationResult.Failed("磁盘上找不到这个文件，可能刚刚被移动或删除。");
        }

        string pngAssetPath = Path.ChangeExtension(context.AssetPath, ".png").Replace("\\", "/");
        string pngFullPath = Path.ChangeExtension(tgaFullPath, ".png");
        if (File.Exists(pngFullPath))
        {
            return TextureOperationResult.Failed(
                "同目录下已经存在 " + Path.GetFileName(pngFullPath) + "，为避免覆盖别人的文件这里不处理。" +
                "请先确认那个 PNG 是不是上一次转换的产物，手动清理后重试。");
        }

        byte[] pngBytes;
        TextureOperationResult encodeFailure;
        if (!TryBuildPngBytes(context, tgaFullPath, out pngBytes, out encodeFailure))
        {
            return encodeFailure;
        }

        return SwapFilesPreservingGuid(context, tgaFullPath, pngFullPath, pngAssetPath, pngBytes);
    }

    private bool TryBuildPngBytes(
        TextureOperationContext context,
        string tgaFullPath,
        out byte[] pngBytes,
        out TextureOperationResult failure)
    {
        pngBytes = null;
        failure = default(TextureOperationResult);

        ITextureFileCodec tgaCodec = TextureCodecRegistry.FindByAssetPath(context.AssetPath);
        ITextureFileCodec pngCodec = TextureCodecRegistry.FindByAssetPath("dummy.png");
        if (tgaCodec == null || pngCodec == null)
        {
            failure = TextureOperationResult.Failed("缺少 TGA 或 PNG 的编解码器实现。");
            return false;
        }

        context.ReportSubProgress(0.2f, "解码 TGA…");
        Texture2D decoded;
        string decodeError;
        if (!tgaCodec.TryDecode(File.ReadAllBytes(tgaFullPath), out decoded, out decodeError))
        {
            failure = TextureOperationResult.Failed("TGA 解码失败: " + decodeError);
            return false;
        }

        try
        {
            context.ReportSubProgress(0.6f, "编码 PNG…");
            string encodeError;
            if (!pngCodec.TryEncode(decoded, context.Settings, out pngBytes, out encodeError))
            {
                failure = TextureOperationResult.Failed("PNG 编码失败: " + encodeError);
                return false;
            }

            return true;
        }
        finally
        {
            Object.DestroyImmediate(decoded);
        }
    }

    private TextureOperationResult SwapFilesPreservingGuid(
        TextureOperationContext context,
        string tgaFullPath,
        string pngFullPath,
        string pngAssetPath,
        byte[] pngBytes)
    {
        long tgaLength = new FileInfo(tgaFullPath).Length;
        bool deleteOriginal = context.Settings.deleteTgaAfterConvert;
        bool guidPreserved = false;

        context.ReportSubProgress(0.85f, "替换文件…");
        AssetDatabase.StartAssetEditing();
        try
        {
            File.WriteAllBytes(pngFullPath, pngBytes);

            if (deleteOriginal)
            {
                File.Delete(tgaFullPath);
                string tgaMetaPath = tgaFullPath + ".meta";
                string pngMetaPath = pngFullPath + ".meta";
                if (File.Exists(tgaMetaPath) && !File.Exists(pngMetaPath))
                {
                    File.Move(tgaMetaPath, pngMetaPath);
                    guidPreserved = true;
                }
            }
        }
        finally
        {
            // 必须和 StartAssetEditing 严格配对。漏掉这一句会让 AssetDatabase
            // 一直处于挂起状态，之后所有导入都不生效，表现为"改了资源没反应"。
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.ImportAsset(pngAssetPath, ImportAssetOptions.ForceUpdate);

        string message = AssetPathUtility.FormatBytes(tgaLength) + " (TGA) -> " +
            AssetPathUtility.FormatBytes(pngBytes.LongLength) + " (PNG) " + pngAssetPath;

        if (!deleteOriginal)
        {
            return TextureOperationResult.Changed(message +
                "；按配置保留了原 .tga，工程里的引用仍然指向 .tga，需要你自己决定怎么切换。");
        }

        if (!guidPreserved)
        {
            // 没有 .meta 可继承（比如这个 tga 是刚拷进来还没被 Unity 导入过），
            // 这不算失败，但必须告诉用户"引用可能需要手动重连"。
            return TextureOperationResult.Changed(message +
                "；没有找到可继承的 .tga.meta，PNG 会拿到新的 GUID，如果之前有引用请检查一遍。");
        }

        return TextureOperationResult.Changed(message + "；已继承原 GUID 与导入设置，引用不受影响。");
    }
}
