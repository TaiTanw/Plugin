using System.IO;
using UnityEngine;

// =====================================================================================
// 职责边界：TGA 的编解码，全部手写实现。
//
// 为什么必须手写：
//   Unity 完全没有 TGA 的读写 API——Texture2D.LoadImage 只认 PNG/JPG 的字节头，
//   也不存在 EncodeToTGA。而 AssetDatabase.LoadAssetAtPath 拿到的 Texture2D 是
//   Unity 导入后的产物（已经被 Max Size 限制过、可能已被压缩成 DXT/ASTC），
//   拿它去重新编码会二次损失画质，而且拿不到原始分辨率，不能用来算"源文件要压到多大"。
//   所以只能自己按 TGA 规范解析磁盘上的原始字节。
//
// 支持的范围（覆盖所有主流 DCC 软件的 TGA 输出）：
//   图像类型 2（未压缩真彩色）、10（RLE 真彩色）、3（未压缩灰度）、11（RLE 灰度）
//   位深      8（灰度）、16（A1R5G5B5）、24（BGR）、32（BGRA）
//   不支持    色彩表索引图（类型 1 / 9），这种在游戏贴图里基本不会出现，会给出明确报错。
//
// 写出的格式（按像素实际内容自动选，不是固定 32 位）：
//   有半透明像素            -> 32 位 BGRA
//   全不透明但 R=G=B        -> 8 位灰度（粗糙度/金属度/AO 这类遮罩图很常见）
//   其它                    -> 24 位 BGR
//   为什么要区分灰度：一张 4096 的 8 位灰度 TGA 本来 16 MB，若统一提升成 24 位就变成
//   48 MB，压缩流程为了压到阈值以下只能把分辨率砍得更狠，白白多损失画质。
//   行序统一为从下到上（TGA 默认，也正好是 Unity 像素数组的顺序，写出时无需翻转），
//   按配置决定是否 RLE，并附带 TGA 2.0 的 26 字节 footer 以提升兼容性。
// =====================================================================================
public class TgaTextureCodec : ITextureFileCodec
{
    private const int HeaderLength = 18;

    // imageDescriptor 的第 5 位：1 表示文件里第一行是图像的【顶】行。
    private const byte TopDownFlag = 0x20;

    // imageDescriptor 的第 4 位：1 表示每一行的像素是从右到左存放的。
    private const byte RightToLeftFlag = 0x10;

    public string DisplayName
    {
        get { return "TGA（本插件自带实现）"; }
    }

    public bool CanHandle(string lowerCaseExtension)
    {
        return lowerCaseExtension == ".tga";
    }

    // ---------------------------------------------------------------------------
    // 解码：字节 -> Texture2D
    // ---------------------------------------------------------------------------

    public bool TryDecode(byte[] fileBytes, out Texture2D texture, out string error)
    {
        texture = null;

        TgaHeader header;
        if (!TryReadHeader(fileBytes, out header, out error))
        {
            return false;
        }

        Color32[] storedPixels;
        if (!TryReadPixels(fileBytes, header, out storedPixels, out error))
        {
            return false;
        }

        // linear:true 的理由和 PNG/JPG 一致：我们要的是原始字节进、原始字节出，
        // 不希望 GPU 缩放时被额外插入一次 sRGB <-> 线性 的颜色空间转换。
        texture = new Texture2D(header.Width, header.Height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(ReorderToUnityLayout(storedPixels, header));
        texture.Apply(false, false);
        error = null;
        return true;
    }

    private static bool TryReadHeader(byte[] fileBytes, out TgaHeader header, out string error)
    {
        header = new TgaHeader();
        if (fileBytes == null || fileBytes.Length < HeaderLength)
        {
            error = "文件长度不足 18 字节，不是一份完整的 TGA。";
            return false;
        }

        header.ImageType = fileBytes[2];
        header.Width = fileBytes[12] | (fileBytes[13] << 8);
        header.Height = fileBytes[14] | (fileBytes[15] << 8);
        header.PixelDepth = fileBytes[16];
        header.Descriptor = fileBytes[17];

        // 数据起点 = 18 字节头 + 图像 ID 段 + 色彩表段。
        int idLength = fileBytes[0];
        int colorMapLength = fileBytes[5] | (fileBytes[6] << 8);
        int colorMapEntryBytes = (fileBytes[7] + 7) / 8;
        header.DataOffset = HeaderLength + idLength + colorMapLength * colorMapEntryBytes;

        if (header.ImageType == 1 || header.ImageType == 9)
        {
            error = "这是色彩表索引（color-mapped）TGA，本插件不支持。请在 DCC 软件里另存为 24/32 位真彩色 TGA。";
            return false;
        }

        if (header.ImageType != 2 && header.ImageType != 3 && header.ImageType != 10 && header.ImageType != 11)
        {
            error = "不支持的 TGA 图像类型: " + header.ImageType + "（仅支持 2 / 3 / 10 / 11）。";
            return false;
        }

        if (header.PixelDepth != 8 && header.PixelDepth != 16 && header.PixelDepth != 24 && header.PixelDepth != 32)
        {
            error = "不支持的 TGA 位深: " + header.PixelDepth + "（仅支持 8 / 16 / 24 / 32）。";
            return false;
        }

        if (header.Width <= 0 || header.Height <= 0 || header.Width > 16384 || header.Height > 16384)
        {
            error = "TGA 头里的尺寸不合理: " + header.Width + " x " + header.Height + "。";
            return false;
        }

        if (header.DataOffset >= fileBytes.Length)
        {
            error = "TGA 像素数据起始位置越界，文件可能被截断。";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// 按文件里的存放顺序读出全部像素（还没有做上下/左右翻转）。
    /// 未压缩和 RLE 两种排布的差别只在这一层，上面的调用方不需要区分。
    /// </summary>
    private static bool TryReadPixels(byte[] fileBytes, TgaHeader header, out Color32[] pixels, out string error)
    {
        int pixelCount = header.Width * header.Height;
        int bytesPerPixel = header.PixelDepth / 8;
        pixels = new Color32[pixelCount];
        bool runLengthEncoded = header.ImageType == 10 || header.ImageType == 11;
        int cursor = header.DataOffset;

        if (!runLengthEncoded)
        {
            if (cursor + pixelCount * bytesPerPixel > fileBytes.Length)
            {
                pixels = null;
                error = "TGA 未压缩像素数据长度不足，文件可能被截断。";
                return false;
            }

            for (int i = 0; i < pixelCount; i++)
            {
                pixels[i] = ReadPixel(fileBytes, cursor, bytesPerPixel);
                cursor += bytesPerPixel;
            }

            error = null;
            return true;
        }

        int written = 0;
        while (written < pixelCount)
        {
            if (cursor >= fileBytes.Length)
            {
                pixels = null;
                error = "TGA RLE 数据在读满 " + pixelCount + " 个像素前就结束了（已读 " + written + " 个），文件可能被截断。";
                return false;
            }

            byte packet = fileBytes[cursor++];
            int count = (packet & 0x7F) + 1;
            if (written + count > pixelCount)
            {
                // 少数导出器会在最后一个包上多写几个像素，截断即可，不当作错误。
                count = pixelCount - written;
            }

            bool isRun = (packet & 0x80) != 0;
            int neededBytes = isRun ? bytesPerPixel : bytesPerPixel * count;
            if (cursor + neededBytes > fileBytes.Length)
            {
                pixels = null;
                error = "TGA RLE 包声明的数据超出了文件长度，文件可能被截断。";
                return false;
            }

            if (isRun)
            {
                Color32 repeated = ReadPixel(fileBytes, cursor, bytesPerPixel);
                cursor += bytesPerPixel;
                for (int i = 0; i < count; i++)
                {
                    pixels[written++] = repeated;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    pixels[written++] = ReadPixel(fileBytes, cursor, bytesPerPixel);
                    cursor += bytesPerPixel;
                }
            }
        }

        error = null;
        return true;
    }

    private static Color32 ReadPixel(byte[] bytes, int offset, int bytesPerPixel)
    {
        if (bytesPerPixel == 1)
        {
            byte gray = bytes[offset];
            return new Color32(gray, gray, gray, 255);
        }

        if (bytesPerPixel == 2)
        {
            // A1R5G5B5：每个通道 5 位，按比例放大到 8 位（乘 255/31 = 8.226）。
            int packed = bytes[offset] | (bytes[offset + 1] << 8);
            byte r = (byte)(((packed >> 10) & 0x1F) * 255 / 31);
            byte g = (byte)(((packed >> 5) & 0x1F) * 255 / 31);
            byte b = (byte)((packed & 0x1F) * 255 / 31);
            return new Color32(r, g, b, 255);
        }

        // TGA 真彩色的通道顺序是 BGR / BGRA，不是 RGB。
        if (bytesPerPixel == 3)
        {
            return new Color32(bytes[offset + 2], bytes[offset + 1], bytes[offset], 255);
        }

        return new Color32(bytes[offset + 2], bytes[offset + 1], bytes[offset], bytes[offset + 3]);
    }

    /// <summary>
    /// 把"文件存放顺序"的像素重排成 Unity 的顺序。
    /// Unity 的 SetPixels32 要求 index 0 是【左下角】、逐行往上；
    /// TGA 默认也是从下往上存，但 descriptor 的两个方向位可以把它改成从上往下、
    /// 或者每行从右往左，所以必须按位判断，否则会出现上下颠倒或镜像的图。
    /// </summary>
    private static Color32[] ReorderToUnityLayout(Color32[] stored, TgaHeader header)
    {
        bool topDown = (header.Descriptor & TopDownFlag) != 0;
        bool rightToLeft = (header.Descriptor & RightToLeftFlag) != 0;
        if (!topDown && !rightToLeft)
        {
            return stored;
        }

        var result = new Color32[stored.Length];
        for (int row = 0; row < header.Height; row++)
        {
            int targetRow = topDown ? header.Height - 1 - row : row;
            for (int column = 0; column < header.Width; column++)
            {
                int targetColumn = rightToLeft ? header.Width - 1 - column : column;
                result[targetRow * header.Width + targetColumn] = stored[row * header.Width + column];
            }
        }

        return result;
    }

    // ---------------------------------------------------------------------------
    // 编码：Texture2D -> 字节
    // ---------------------------------------------------------------------------

    public bool TryEncode(Texture2D texture, TextureProcessSettings settings, out byte[] fileBytes, out string error)
    {
        fileBytes = null;
        if (texture == null)
        {
            error = "待编码的 Texture2D 为空。";
            return false;
        }

        Color32[] pixels = texture.GetPixels32();
        PixelLayout layout = ChooseLayout(pixels);
        bool useRle = settings == null || settings.tgaUseRunLengthEncoding;

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            WriteHeader(writer, texture.width, texture.height, layout, useRle);
            if (useRle)
            {
                WriteRunLengthEncodedPixels(writer, pixels, texture.width, texture.height, layout);
            }
            else
            {
                WriteRawPixels(writer, pixels, layout);
            }

            WriteFooter(writer);
            writer.Flush();
            fileBytes = stream.ToArray();
        }

        error = null;
        return true;
    }

    /// <summary>
    /// 按像素的实际内容选最省的排布。只扫一遍数组，同时判断"有没有透明"和"是不是灰度"，
    /// 遇到彩色像素就能立刻确定是 24 位并提前结束灰度判断。
    /// </summary>
    private static PixelLayout ChooseLayout(Color32[] pixels)
    {
        bool isGrayscale = true;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (pixel.a != 255)
            {
                return PixelLayout.Bgra32;
            }

            if (isGrayscale && (pixel.r != pixel.g || pixel.g != pixel.b))
            {
                isGrayscale = false;
            }
        }

        return isGrayscale ? PixelLayout.Gray8 : PixelLayout.Bgr24;
    }

    private static void WriteHeader(BinaryWriter writer, int width, int height, PixelLayout layout, bool useRle)
    {
        bool grayscale = layout == PixelLayout.Gray8;

        // TGA 的图像类型是"内容种类 + 是否 RLE"的组合：
        //   2 未压缩真彩色 / 10 RLE 真彩色 / 3 未压缩灰度 / 11 RLE 灰度
        byte imageType = grayscale
            ? (byte)(useRle ? 11 : 3)
            : (byte)(useRle ? 10 : 2);

        writer.Write((byte)0);                              // 图像 ID 段长度：不写 ID
        writer.Write((byte)0);                              // 色彩表类型：无色彩表
        writer.Write(imageType);
        writer.Write((byte)0); writer.Write((byte)0);       // 色彩表起始索引
        writer.Write((byte)0); writer.Write((byte)0);       // 色彩表长度
        writer.Write((byte)0);                              // 色彩表单项位数
        writer.Write((byte)0); writer.Write((byte)0);       // X 原点
        writer.Write((byte)0); writer.Write((byte)0);       // Y 原点
        writer.Write((byte)(width & 0xFF));                 // 宽（小端）
        writer.Write((byte)((width >> 8) & 0xFF));
        writer.Write((byte)(height & 0xFF));                // 高（小端）
        writer.Write((byte)((height >> 8) & 0xFF));
        writer.Write((byte)(GetBytesPerPixel(layout) * 8)); // 位深
        // imageDescriptor：低 4 位是 alpha 位数；第 5 位留 0 表示从下往上存，
        // 正好和 Unity 的像素数组顺序一致，写出去不用做任何翻转。
        writer.Write((byte)(layout == PixelLayout.Bgra32 ? 8 : 0));
    }

    private static int GetBytesPerPixel(PixelLayout layout)
    {
        if (layout == PixelLayout.Gray8)
        {
            return 1;
        }

        return layout == PixelLayout.Bgr24 ? 3 : 4;
    }

    private static void WriteRawPixels(BinaryWriter writer, Color32[] pixels, PixelLayout layout)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            WritePixel(writer, pixels[i], layout);
        }
    }

    /// <summary>
    /// 逐行做 RLE 压缩。按 TGA 规范，一个数据包不允许跨行，所以外层必须按行切开，
    /// 否则很多读图器（包括部分 DCC 软件）会解析出错位的图。
    /// </summary>
    private static void WriteRunLengthEncodedPixels(BinaryWriter writer, Color32[] pixels, int width, int height, PixelLayout layout)
    {
        for (int row = 0; row < height; row++)
        {
            int rowStart = row * width;
            int column = 0;
            while (column < width)
            {
                int runLength = CountIdenticalRun(pixels, rowStart, column, width, layout);
                if (runLength >= 2)
                {
                    // 重复包：最高位置 1，低 7 位存"重复次数 - 1"，后面只跟一个像素。
                    writer.Write((byte)(0x80 | (runLength - 1)));
                    WritePixel(writer, pixels[rowStart + column], layout);
                    column += runLength;
                    continue;
                }

                // 原样包：最高位置 0，低 7 位存"像素个数 - 1"，后面跟这么多个像素。
                int rawLength = CountRawRun(pixels, rowStart, column, width, layout);
                writer.Write((byte)(rawLength - 1));
                for (int i = 0; i < rawLength; i++)
                {
                    WritePixel(writer, pixels[rowStart + column + i], layout);
                }

                column += rawLength;
            }
        }
    }

    private static int CountIdenticalRun(Color32[] pixels, int rowStart, int column, int width, PixelLayout layout)
    {
        Color32 first = pixels[rowStart + column];
        int length = 1;

        // 单个包最多表达 128 个像素（低 7 位存 0-127）。
        while (column + length < width && length < 128 &&
               AreEqual(pixels[rowStart + column + length], first, layout))
        {
            length++;
        }

        return length;
    }

    private static int CountRawRun(Color32[] pixels, int rowStart, int column, int width, PixelLayout layout)
    {
        int length = 1;
        while (column + length < width && length < 128)
        {
            int index = rowStart + column + length;

            // 一旦往后看到"连续两个相同"，就在这里收尾，把它留给下一个重复包，
            // 这样才能真正压出体积；否则整行都会被当成原样包写出去。
            if (column + length + 1 < width && AreEqual(pixels[index], pixels[index + 1], layout))
            {
                break;
            }

            length++;
        }

        return length;
    }

    /// <summary>
    /// 比较必须只看真正会被写进文件的通道。比如灰度排布下只写 R，
    /// 如果还去比 G/B/A，两个写出来完全一样的像素会被误判为不同，RLE 就压不动了。
    /// </summary>
    private static bool AreEqual(Color32 left, Color32 right, PixelLayout layout)
    {
        if (layout == PixelLayout.Gray8)
        {
            return left.r == right.r;
        }

        bool colorEqual = left.r == right.r && left.g == right.g && left.b == right.b;
        return layout == PixelLayout.Bgr24 ? colorEqual : colorEqual && left.a == right.a;
    }

    private static void WritePixel(BinaryWriter writer, Color32 pixel, PixelLayout layout)
    {
        if (layout == PixelLayout.Gray8)
        {
            writer.Write(pixel.r);
            return;
        }

        // TGA 真彩色是 BGR / BGRA 顺序，不是 RGB。
        writer.Write(pixel.b);
        writer.Write(pixel.g);
        writer.Write(pixel.r);
        if (layout == PixelLayout.Bgra32)
        {
            writer.Write(pixel.a);
        }
    }

    /// <summary>写出 TGA 时每个像素的排布方式，由 ChooseLayout 根据像素实际内容决定。</summary>
    private enum PixelLayout
    {
        Gray8,
        Bgr24,
        Bgra32
    }

    /// <summary>
    /// TGA 2.0 的 26 字节 footer。内容可以全为空，但带上这个签名能让更多读图器
    /// 直接把文件识别为标准 TGA 2.0，避免个别工具报"未知 TGA 版本"。
    /// </summary>
    private static void WriteFooter(BinaryWriter writer)
    {
        writer.Write(0);        // 扩展区偏移：无
        writer.Write(0);        // 开发者目录偏移：无
        foreach (char character in "TRUEVISION-XFILE")
        {
            writer.Write((byte)character);
        }

        writer.Write((byte)'.');
        writer.Write((byte)0);
    }

    private struct TgaHeader
    {
        public byte ImageType;
        public int Width;
        public int Height;
        public byte PixelDepth;
        public byte Descriptor;
        public int DataOffset;
    }
}
