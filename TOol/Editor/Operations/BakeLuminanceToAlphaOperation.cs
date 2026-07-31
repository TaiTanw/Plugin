using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   只做一件事——把贴图的 RGB 亮度写入 Alpha，让黑底旋翼/光晕这类"没有真 Alpha、
//   靠亮度当透明度"的 DCC 贴图能在 Unity Standard Transparent/Fade 下正确显示。
//
// 为什么需要这个操作（取证来自武直10w 旋翼贴图）：
//   源 TGA（含 32 位版）Alpha 通道全是 255；透明度写在 RGB 亮度上（黑底 + 亮旋翼）。
//   MTL 的 map_d 在 DCC 里按亮度溶解，但 Unity Standard 透明模式只读贴图 Alpha，
//   于是黑底变成不透明黑块，旋翼透明显示异常。这是源文件约定与 Unity 着色器约定
//   不一致，不是导入参数设错。本操作把约定对齐到 Unity 侧，不改材质 Render Mode。
//
// 阈值与映射规则（读 TextureProcessSettings，可在贴图处理窗口的配置段调整）：
//   1) luminanceAlphaCutoff：亮度低于此值 → Alpha=0 且 RGB 清零（砍半透明影子）
//   2) luminanceAlphaRemapAboveCutoff：阈值以上亮度重映射到 0~255（软边从阈值起算）
//   3) luminanceAlphaWriteGrayscaleRgb：可选把 RGB 改成灰度，抑制彩色半透明边缘
//
// 不要勾进 importAutoOperationIds：
//   对普通漫反射 / ORM / 法线误跑会毁掉贴图。只允许在窗口里对明确选中的资产手动执行。
// =====================================================================================
public class BakeLuminanceToAlphaOperation : ITextureAssetOperation
{
    private const float MinLuminanceRange = 8f;
    private const float MinBlackPixelRatio = 0.05f;

    public string Id
    {
        get { return "bake_luminance_to_alpha"; }
    }

    public string DisplayName
    {
        get { return "亮度写入 Alpha（旋翼/光晕）"; }
    }

    public string Description
    {
        get
        {
            return "按配置把 RGB 亮度写入 Alpha：低于 Cutoff 的像素直接透明并清 RGB；" +
                   "阈值以上可重映射到 0~255。用于黑底旋翼模糊盘/光晕。" +
                   "参数在窗口「配置」段的「亮度写入 Alpha」里调。不要对普通漫反射或 ORM 使用；" +
                   ".fbm 内嵌缓存会跳过。可重复执行以换阈值重烤。";
        }
    }

    public int Order
    {
        get { return 150; }
    }

    public bool CanProcess(string assetPath, TextureProcessSettings settings)
    {
        return TextureCodecRegistry.IsSupported(assetPath) &&
               TextureAssetPathUtility.GetFileLength(assetPath) > 0;
    }

    public TextureOperationResult Execute(TextureOperationContext context)
    {
        if (TextureAssetPathUtility.IsInsideEmbeddedMediaFolder(context.AssetPath))
        {
            return TextureOperationResult.Skipped(
                "这是 FBX 内嵌贴图的 .fbm 缓存，模型重新导入会被覆盖。请对独立贴图目录执行本操作，" +
                "或先修好源 TGA 后重新导出/嵌入 FBX。");
        }

        string fullPath = TextureAssetPathUtility.ToFullPath(context.AssetPath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return TextureOperationResult.Failed("磁盘上找不到这个文件，可能刚刚被移动或删除。");
        }

        ITextureFileCodec codec = TextureCodecRegistry.FindByAssetPath(context.AssetPath);
        if (codec == null)
        {
            return TextureOperationResult.Skipped("没有支持这种扩展名的编解码器。");
        }

        TextureProcessSettings settings = context.Settings;
        int cutoff = settings == null ? 24 : Mathf.Clamp(settings.luminanceAlphaCutoff, 0, 255);
        bool remap = settings == null || settings.luminanceAlphaRemapAboveCutoff;
        bool grayscaleRgb = settings != null && settings.luminanceAlphaWriteGrayscaleRgb;

        byte[] originalBytes = File.ReadAllBytes(fullPath);
        context.ReportSubProgress(0f, "解码源文件…");

        Texture2D decoded;
        string decodeError;
        if (!codec.TryDecode(originalBytes, out decoded, out decodeError))
        {
            return TextureOperationResult.Failed("解码失败（" + codec.DisplayName + "）: " + decodeError);
        }

        try
        {
            Color32[] pixels = decoded.GetPixels32();
            string skipReason;
            if (!NeedsLuminanceBake(pixels, out skipReason))
            {
                return TextureOperationResult.Skipped(skipReason);
            }

            context.ReportSubProgress(0.4f, "按 Cutoff=" + cutoff + " 写入 Alpha…");
            BakeStats stats = BakeLuminanceToAlpha(pixels, cutoff, remap, grayscaleRgb);
            decoded.SetPixels32(pixels);
            decoded.Apply(false, false);

            context.ReportSubProgress(0.7f, "重新编码…");
            byte[] encoded;
            string encodeError;
            if (!codec.TryEncode(decoded, context.Settings, out encoded, out encodeError))
            {
                return TextureOperationResult.Failed("编码失败（" + codec.DisplayName + "）: " + encodeError);
            }

            File.WriteAllBytes(fullPath, encoded);
            AssetDatabase.ImportAsset(context.AssetPath, ImportAssetOptions.ForceUpdate);
            EnableAlphaIsTransparency(context.AssetPath);

            return TextureOperationResult.Changed(
                "Cutoff=" + cutoff +
                "，透明 " + stats.CutPixels +
                " 像素，保留 " + stats.KeptPixels +
                " 像素，remap=" + remap +
                "，灰度RGB=" + grayscaleRgb +
                "，已开启 Alpha Is Transparency。");
        }
        finally
        {
            Object.DestroyImmediate(decoded);
        }
    }

    /// <summary>
    /// 只拦"不像黑底透明盘"的图。不再因为 Alpha 已有变化就跳过——
    /// 用户换 Cutoff 后需要能对同一张图反复重烤。
    /// </summary>
    private static bool NeedsLuminanceBake(Color32[] pixels, out string skipReason)
    {
        int minLuma = 255;
        int maxLuma = 0;
        int blackish = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            int luma = MaxChannel(pixels[i]);
            if (luma < minLuma)
            {
                minLuma = luma;
            }

            if (luma > maxLuma)
            {
                maxLuma = luma;
            }

            if (luma < 8)
            {
                blackish++;
            }
        }

        if (maxLuma - minLuma < MinLuminanceRange)
        {
            skipReason = "RGB 亮度几乎没有变化，不像黑底透明盘贴图。";
            return false;
        }

        float blackRatio = blackish / (float)pixels.Length;
        if (blackRatio < MinBlackPixelRatio)
        {
            skipReason = "接近黑色的像素占比过低（" + (blackRatio * 100f).ToString("F1") +
                         "%），更像普通漫反射/ORM，已跳过以免误伤。";
            return false;
        }

        skipReason = null;
        return true;
    }

    private static BakeStats BakeLuminanceToAlpha(
        Color32[] pixels,
        int cutoff,
        bool remapAboveCutoff,
        bool writeGrayscaleRgb)
    {
        var stats = new BakeStats();
        int range = 255 - cutoff;
        if (range < 1)
        {
            range = 1;
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            int luma = MaxChannel(pixel);

            if (luma <= cutoff)
            {
                // 低于阈值：直接全透明，并清掉 RGB，避免 Fade 模式下暗色 × 低 Alpha 留下半透明影子。
                if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0 || pixel.a != 0)
                {
                    stats.CutPixels++;
                }

                pixels[i] = new Color32(0, 0, 0, 0);
                continue;
            }

            byte alpha;
            if (remapAboveCutoff)
            {
                alpha = (byte)Mathf.Clamp(((luma - cutoff) * 255) / range, 0, 255);
            }
            else
            {
                alpha = (byte)luma;
            }

            byte r = pixel.r;
            byte g = pixel.g;
            byte b = pixel.b;
            if (writeGrayscaleRgb)
            {
                r = g = b = (byte)luma;
            }

            if (r != pixel.r || g != pixel.g || b != pixel.b || alpha != pixel.a)
            {
                stats.KeptPixels++;
            }

            pixels[i] = new Color32(r, g, b, alpha);
        }

        return stats;
    }

    private static int MaxChannel(Color32 pixel)
    {
        int luma = pixel.r;
        if (pixel.g > luma)
        {
            luma = pixel.g;
        }

        if (pixel.b > luma)
        {
            luma = pixel.b;
        }

        return luma;
    }

    private static void EnableAlphaIsTransparency(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        if (importer.alphaIsTransparency && importer.alphaSource == TextureImporterAlphaSource.FromInput)
        {
            return;
        }

        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private struct BakeStats
    {
        public int CutPixels;
        public int KeptPixels;
    }
}
