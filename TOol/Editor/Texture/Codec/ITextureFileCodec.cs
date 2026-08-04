using UnityEngine;

// =====================================================================================
// 职责边界：
//   编解码层只做"一段字节 <-> 一张 Texture2D"的双向转换，是整套流程里唯一接触
//   图片二进制格式的地方。它不判断文件该不该处理、不决定缩到多大、不写磁盘、
//   不碰 AssetDatabase——这些都是 Operations/ 层的事。
//
// 为什么要把编解码抽成接口：
//   Unity 内置的 Texture2D.LoadImage / EncodeToPNG / EncodeToJPG 只支持 PNG 和 JPG，
//   TGA / TIFF / PSD 一律无能为力（既没有 EncodeToTGA，LoadImage 也解不了 TGA）。
//   所以 TGA 必须自己实现编解码。抽成接口之后，"支持一种新格式"这件事就退化成
//   "新加一个实现类"，Operations 层和窗口都不用改一行代码。
//
// 新增一种格式的做法：
//   1) 建一个类实现 ITextureFileCodec，放在 Codec 目录下；
//   2) 必须有无参构造函数（TextureCodecRegistry 用反射实例化）；
//   3) 完成，注册表会自动发现它。
// =====================================================================================
public interface ITextureFileCodec
{
    /// <summary>窗口和日志里显示的名字。</summary>
    string DisplayName { get; }

    /// <summary>能否处理这个扩展名。传入的扩展名带点且已转小写，例如 ".tga"。</summary>
    bool CanHandle(string lowerCaseExtension);

    /// <summary>
    /// 把文件字节解码成 Texture2D。失败时返回 false 并给出人能看懂的原因，
    /// 不要抛异常——批量处理时一个文件解不开不应该中断整批。
    /// </summary>
    bool TryDecode(byte[] fileBytes, out Texture2D texture, out string error);

    /// <summary>
    /// 把 Texture2D 编码回文件字节。settings 提供质量、是否启用 RLE 之类的参数。
    /// </summary>
    bool TryEncode(Texture2D texture, TextureProcessSettings settings, out byte[] fileBytes, out string error);
}
