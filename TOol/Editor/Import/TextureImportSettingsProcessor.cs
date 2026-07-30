using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界（和 ModelImportSettingsProcessor 是同一个原则）：
//   只负责在纹理导入【之前】设置 TextureImporter 上的参数，
//   全程只读写 (TextureImporter)assetImporter 这一个引用本身。
//
//   不要在这里做"判断文件是否超标、缩放像素、覆盖源文件"这类操作，原因有两个：
//     1) OnPreprocessTexture 触发时，这一批资产的导入流程还没走完。在流程中途
//        改写正在被导入的同一个源文件，会引出重复触发导入、导入到一半数据不一致
//        之类的问题，非常难复现和排查。
//     2) 一旦要单独排查"到底是导入参数问题，还是源文件被改坏了"，两件事混在
//        一个方法里会互相干扰，谁也说不清。
//
//   源文件层面的体积判断与压缩，统一由 TextureSourceFileProcessor 在这一批资产
//   全部导入完成之后调度，具体动作在 Operations/ 层。
//
// 支持哪些扩展名不再写死在这里，而是问 TextureCodecRegistry。
// 这样以后新增一种格式的编解码器，这个方法自动就覆盖到它了。
// =====================================================================================
public class TextureImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!AssetProcessSwitch.IsEnabled)
        {
            return;
        }

        if (!TextureCodecRegistry.IsSupported(assetPath))
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.Current;

        // 和 ModelImportSettingsProcessor 用同一条边界：不介入打包工具的产物区。
        // 那里的贴图是交付工作副本，它的 .meta / GUID / 图像内容都由打包工具按自己的
        // 规范维护（PACKAGING_RULES.md 规则 29），两边同时改会出现谁也说不清的结果。
        if (settings.IsExcludedPath(assetPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;

        if (settings.textureDisableReadWrite)
        {
            // 导入完成后不保留可读写的 CPU 端副本，减少运行时内存占用。
            // 注意这不影响本插件的压缩流程——我们从来不用 texture.GetPixels 读
            // 导入后的贴图，而是直接解码磁盘上的原始字节（见 Codec 层）。
            importer.isReadable = false;
        }

        Debug.Log("[TextureImportSettingsProcessor] 已设置纹理导入参数: " + assetPath);
    }
}
