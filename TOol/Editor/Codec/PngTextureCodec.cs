using UnityEngine;

// =====================================================================================
// 职责边界：PNG 的编解码，直接借用 Unity 内置实现。
//
// 注意 PNG 是无损格式，settings.jpgQuality 对它没有任何作用——
// 想让 PNG 变小只有降尺寸一条路，这也是 ShrinkTextureSourceOperation 对 PNG
// 只做尺寸二分、不做质量二分的原因。
// =====================================================================================
public class PngTextureCodec : ITextureFileCodec
{
    public string DisplayName
    {
        get { return "PNG（Unity 内置）"; }
    }

    public bool CanHandle(string lowerCaseExtension)
    {
        return lowerCaseExtension == ".png";
    }

    public bool TryDecode(byte[] fileBytes, out Texture2D texture, out string error)
    {
        // 这里用 linear:true 建 Texture2D。原因：我们要的是"原始字节进、原始字节出"，
        // 一旦按 sRGB 建纹理，后面用 GPU 缩放时会多走一次 sRGB<->线性 的颜色空间转换，
        // 在 Linear 色彩空间的工程里会让重新编码后的图整体偏亮/偏暗。
        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (texture.LoadImage(fileBytes))
        {
            error = null;
            return true;
        }

        Object.DestroyImmediate(texture);
        texture = null;
        error = "Unity 无法解码这份 PNG 数据（文件可能已损坏，或者后缀名与真实格式不一致）。";
        return false;
    }

    public bool TryEncode(Texture2D texture, TextureProcessSettings settings, out byte[] fileBytes, out string error)
    {
        fileBytes = texture.EncodeToPNG();
        if (fileBytes != null && fileBytes.Length > 0)
        {
            error = null;
            return true;
        }

        fileBytes = null;
        error = "EncodeToPNG 返回空数据。";
        return false;
    }
}
