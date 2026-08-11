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
        // 成功时不打 Log：批量入库时每贴图一条会刷屏，易被当成警告。
    }
}
