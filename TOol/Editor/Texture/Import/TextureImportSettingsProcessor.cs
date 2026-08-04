using UnityEditor;
using UnityEngine;

// =====================================================================================
// 贴图【设置自动】：导入前只改 TextureImporter 参数。
// =====================================================================================
public class TextureImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!ResourceProcessSwitches.IsTextureSettingsEffective)
        {
            return;
        }

        if (!TextureCodecRegistry.IsSupported(assetPath))
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.Current;
        if (settings.IsExcludedPath(assetPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        if (settings.textureDisableReadWrite)
        {
            importer.isReadable = false;
        }

        Debug.Log("[TextureImportSettingsProcessor] 已按贴图设置自动处理: " + assetPath);
    }
}
