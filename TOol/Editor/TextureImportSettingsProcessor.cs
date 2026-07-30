using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界（务必保持，和 FBXImportProcessor 是同一个原则）：
//   这个类只负责在纹理导入【之前】设置 TextureImporter 上的参数，
//   全程只读写 (TextureImporter)assetImporter 这一个引用本身。
//
//   不要在这里做"判断文件是否超过 5MB、缩放像素、覆盖源文件"这类操作——
//   那属于对磁盘上源文件字节的直接改写，放在这里做有两个问题：
//     1) OnPreprocessTexture 触发时，这一批资产的导入流程还没走完，
//        在流程中途去改写正在被导入的同一个源文件，容易引出重复触发导入、
//        导入到一半数据不一致之类的问题，非常难复现和排查。
//     2) 一旦以后要单独排查"到底是导入参数问题，还是源文件被改坏了"，
//        两件事混在一个方法里会互相干扰，谁也说不清。
//
//   源文件层面的 5MB 判断与像素缩放，统一放在 TextureSourceFileProcessor 里，
//   在 OnPostprocessAllAssets（这一批资产【全部】导入完成之后的统一回调）中处理。
//
//   这个脚本目前先放一个最小可用的示例设置（关闭 Read/Write，避免正式贴图
//   常驻一份可读写的内存副本占内存），你们团队后续有别的导入期参数需求
//   （比如统一贴图格式、mipmap、各平台压缩设置），都加在这个方法里就行，
//   不会影响到源文件缩放那部分逻辑。
// =====================================================================================
public class TextureImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!SwitchManagerWindow.switchValue)
        {
            return;
        }

        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" &&
            extension != ".tga" && extension != ".tif" && extension != ".tiff")
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;

        // 示例：导入完成后不保留可读写的 CPU 端副本，减少运行时内存占用。
        // 如果后面有别的脚本需要在导入之后用 texture.GetPixels() 之类的方式读取像素，
        // 再按需打开 isReadable，这里只是给一个"职能分离"的示例位置。
        importer.isReadable = false;

        Debug.Log("[TextureImportSettingsProcessor] 已设置纹理导入参数: " + assetPath);
    }
}
