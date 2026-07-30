using UnityEngine;

// =====================================================================================
// 职责边界：
//   只做一件事——把一张 Texture2D 等比缩放成指定尺寸，返回一张新的 Texture2D。
//   不读写文件、不管阈值、不碰 AssetDatabase。
//
// 为什么用 GPU Blit 而不是逐像素采样：
//   一张 4K 图有 1600 万像素，用 C# 逐像素双线性插值要几秒；Blit 交给 GPU 做，
//   同样的图是毫秒级。二分搜索会反复缩放同一张图，这个差距会被放大十几倍。
//
// 关于颜色空间（这里踩过坑，改动前请先看完）：
//   RenderTexture.GetTemporary 默认的 readWrite 是 Default，也就是跟随工程的色彩空间：
//   工程设成 Linear 时，采样源纹理会做一次 sRGB->线性 转换，ReadPixels 再做一次
//   线性->sRGB 转换。两次转换在数学上并不互相抵消（精度截断 + 目标纹理是否 sRGB 决定），
//   结果就是重新编码出来的图整体偏亮或偏暗，而且只在 Linear 工程里出现，非常难查。
//   这里显式指定 RenderTextureReadWrite.Linear，配合 Codec 层用 linear:true 建的
//   Texture2D，全程不做任何颜色空间转换，保证"字节进、字节出"，只有缩放这一次损失。
// =====================================================================================
public static class TextureScaler
{
    public static Texture2D Scale(Texture2D source, int targetWidth, int targetHeight)
    {
        targetWidth = Mathf.Max(1, targetWidth);
        targetHeight = Mathf.Max(1, targetHeight);

        // Blit 用的是源纹理自己的 filterMode，Point 会产生明显锯齿，这里强制双线性。
        FilterMode previousFilterMode = source.filterMode;
        source.filterMode = FilterMode.Bilinear;

        RenderTexture temporary = RenderTexture.GetTemporary(
            targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previousActive = RenderTexture.active;

        Texture2D result;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, true);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply(false, false);
        }
        finally
        {
            // 无论中途出什么错，都必须还原 active 并归还临时 RT，
            // 否则会污染编辑器后续所有渲染（Inspector 预览会开始报错）。
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(temporary);
            source.filterMode = previousFilterMode;
        }

        return result;
    }

    /// <summary>
    /// 按"最长边缩到 targetLongestSide"计算等比尺寸，短边至少保留 1 像素。
    /// 二分搜索只需要在一个维度上二分，另一维由这里换算，避免长宽比被改坏。
    /// </summary>
    public static void CalculateProportionalSize(
        int sourceWidth, int sourceHeight, int targetLongestSide,
        out int targetWidth, out int targetHeight)
    {
        int sourceLongestSide = Mathf.Max(sourceWidth, sourceHeight);
        if (sourceLongestSide <= 0)
        {
            targetWidth = 1;
            targetHeight = 1;
            return;
        }

        float ratio = (float)targetLongestSide / sourceLongestSide;
        targetWidth = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * ratio));
        targetHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * ratio));
    }
}
