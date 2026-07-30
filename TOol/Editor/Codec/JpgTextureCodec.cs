using UnityEngine;

// =====================================================================================
// 职责边界：JPG / JPEG 的编解码，直接借用 Unity 内置实现。
//
// JPG 没有 alpha 通道，编码时 Unity 会直接丢掉 alpha。所以如果一张带透明通道的图
// 被存成了 .jpg，透明信息本来就已经丢了，我们重新编码不会造成额外损失。
// =====================================================================================
public class JpgTextureCodec : ITextureFileCodec
{
    public string DisplayName
    {
        get { return "JPG（Unity 内置）"; }
    }

    public bool CanHandle(string lowerCaseExtension)
    {
        return lowerCaseExtension == ".jpg" || lowerCaseExtension == ".jpeg";
    }

    public bool TryDecode(byte[] fileBytes, out Texture2D texture, out string error)
    {
        // linear:true 的理由同 PngTextureCodec：避免 GPU 缩放时多走一次颜色空间转换。
        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (texture.LoadImage(fileBytes))
        {
            error = null;
            return true;
        }

        Object.DestroyImmediate(texture);
        texture = null;
        error = "Unity 无法解码这份 JPG 数据（文件可能已损坏，或者后缀名与真实格式不一致）。";
        return false;
    }

    public bool TryEncode(Texture2D texture, TextureProcessSettings settings, out byte[] fileBytes, out string error)
    {
        fileBytes = texture.EncodeToJPG(settings.jpgQuality);
        if (fileBytes != null && fileBytes.Length > 0)
        {
            error = null;
            return true;
        }

        fileBytes = null;
        error = "EncodeToJPG 返回空数据。";
        return false;
    }
}
