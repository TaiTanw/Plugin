using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   这个类专门负责"源文件"层面的操作——判断磁盘上的贴图文件是否超过 5MB，
//   超过的话就把像素长宽等比缩小并重新编码，覆盖回原文件。
//   不在这里设置任何 TextureImporter 的属性，导入期参数一律交给
//   TextureImportSettingsProcessor 处理，两者互不干扰。
//
// 为什么放在 OnPostprocessAllAssets 里，而不是 OnPreprocessTexture / OnPostprocessTexture：
//   - OnPreprocessTexture / OnPostprocessTexture 是"每个资产各自的"回调，触发时
//     Unity 正在这个资产自己的导入流程中间，这时候去改写它自己的源文件字节，
//     容易导致同一次导入内部状态不一致。
//   - OnPostprocessAllAssets 是【静态】回调，在这一批资产【全部】导入结束之后
//     才统一触发一次，天然适合做"导入流程结束后，再回头处理源文件"这种事情，
//     即"最后统一在 importer 结束后处理源文件"。
//
// 关于死循环的说明：
//   缩放完成后会调用 AssetDatabase.ImportAsset(assetPath, ForceUpdate) 强制重新导入，
//   这会再触发一次 OnPostprocessAllAssets。但重新导入时，源文件已经被缩小到
//   5MB 以内，TryShrinkSourceFile 里的大小判断会直接提前返回，不会再次缩放，
//   所以不会无限循环。
//
// 格式限制：
//   Texture2D.LoadImage 只能解码 PNG / JPG 两种格式的字节数据，无法处理
//   TGA / TIFF 等格式；遇到无法解码的格式会打印警告并跳过，不会报错中断整批导入。
// =====================================================================================
public class TextureSourceFileProcessor : AssetPostprocessor
{
    private const long MaxSourceBytes = 5L * 1024L * 1024L; // 5MB，超过这个体积才处理
    private const float ShrinkStep = 0.5f;                  // 每一轮缩小到当前尺寸的一半
    private const int MinDimension = 64;                    // 保底最小边长，避免缩成 0
    private const int MaxShrinkIterations = 10;              // 保底循环次数上限，避免极端情况死循环

    // 静态方法名和签名是 Unity 约定好的固定写法，不需要手动注册，
    // 只要类继承 AssetPostprocessor 并放在 Editor 文件夹下就会被自动调用。
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!SwitchManagerWindow.switchValue)
        {
            return;
        }

        foreach (string assetPath in importedAssets)
        {
            if (!IsShrinkableTextureExtension(assetPath))
            {
                continue;
            }

            TryShrinkSourceFileIfTooLarge(assetPath);
        }
    }
    /// <summary>
    /// 文件后缀判断
    /// </summary>
    /// <param name="assetPath"></param>
    /// <returns></returns>
    private static bool IsShrinkableTextureExtension(string assetPath)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        // 只有 png / jpg 能用 Texture2D.LoadImage 解码，tga/tif 等格式先不处理。
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
    }
    /// <summary>
    /// 源文件压缩操作
    /// </summary>
    /// <param name="assetPath">内部相对路径（始于/Assets/)</param>
    private static void TryShrinkSourceFileIfTooLarge(string assetPath)
    {
        //补全路径
        string fullPath = Path.GetFullPath(assetPath);
        //调用原生IO
        if (!File.Exists(fullPath))
        {
            return;
        }

        long originalLength = new FileInfo(fullPath).Length;
        if (originalLength <= MaxSourceBytes)
        {
            // 没超限，完全不动源文件——这一点很重要，保证正常大小的贴图
            // 不会被无谓地重新编码（重新编码可能带来轻微的画质损失）。
            return;
        }

        byte[] originalBytes = File.ReadAllBytes(fullPath);

        // 用一张临时 Texture2D 解码源文件的原始字节，只是用来读像素数据，
        // 跟 TextureImporter/贴图的导入设置没有任何关系。
        var decodedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool decoded = decodedTexture.LoadImage(originalBytes);
        if (!decoded)
        {
            Debug.LogWarning("[TextureSourceFileProcessor] 贴图解码失败，跳过缩放处理: " + assetPath);
            Object.DestroyImmediate(decodedTexture);
            return;
        }

        bool isPng = Path.GetExtension(assetPath).ToLowerInvariant() == ".png";
        int width = decodedTexture.width;
        int height = decodedTexture.height;
        byte[] resizedBytes = originalBytes;
        int iteration = 0;

        // 每轮缩小一半尺寸、重新编码、检查体积，直到达标、到最小边长，或者到迭代次数上限为止。
        while (resizedBytes.Length > MaxSourceBytes &&
               width > MinDimension &&
               height > MinDimension &&
               iteration < MaxShrinkIterations)
        {
            width = Mathf.Max(MinDimension, Mathf.RoundToInt(width * ShrinkStep));//四舍六入五双
            height = Mathf.Max(MinDimension, Mathf.RoundToInt(height * ShrinkStep));

            Texture2D scaledTexture = ScaleTexture(decodedTexture, width, height);
            resizedBytes = isPng ? scaledTexture.EncodeToPNG() : scaledTexture.EncodeToJPG(90);
            Object.DestroyImmediate(scaledTexture);
            iteration++;
        }

        Object.DestroyImmediate(decodedTexture);

        if (resizedBytes.Length >= originalBytes.Length)
        {
            // 极少数情况下重新编码后体积反而没变小（比如已经高度压缩过的 JPG），
            // 这种情况下不覆盖源文件，避免做无意义的画质损失。
            Debug.LogWarning("[TextureSourceFileProcessor] 缩放后体积未减小，放弃覆盖源文件: " + assetPath);
            return;
        }

        File.WriteAllBytes(fullPath, resizedBytes);

        Debug.Log(string.Format(
            "[TextureSourceFileProcessor] 源文件超过 5MB，已缩放覆盖: {0}\n" +
            "  缩放前体积: {1:F2} MB -> 缩放后体积: {2:F2} MB\n" +
            "  缩放后尺寸: {3} x {4}",
            assetPath,
            originalLength / 1024f / 1024f,
            resizedBytes.Length / 1024f / 1024f,
            width, height));

        // 源文件字节已经被替换，必须强制重新导入一次，让 Unity 用新的像素数据
        // 重新生成这个贴图资产，否则编辑器里显示、打包进去的还是旧的导入缓存。
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    // 用 GPU Blit 的方式对贴图做等比缩放，比逐像素采样快很多，
    // 这里只是纯粹的图像处理，不涉及任何 AssetImporter/资产引用。
    private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;

        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;

        var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();

        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(temporary);
        return result;
    }
}
