using UnityEditor;
using UnityEngine;

// =====================================================================================
// 贴图导入期 Importer。闸：Art 硬跳过 → 可选钩子总开关 → SO applyImporterSettingsOnImport。
// 不读批量选择器、不读不介入目录、不读设置自动 Prefs。
// =====================================================================================
public class TextureImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (ModelImporterProfiles.IsArtDeliveryPath(assetPath))
        {
            return;
        }

        if (!ImportPipelineSettings.AreHooksEnabled())
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.ForImportCallbacks();
        if (settings == null || !settings.applyImporterSettingsOnImport)
        {
            return;
        }

        if (!TextureCodecRegistry.IsSupported(assetPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        if (settings.textureDisableReadWrite)
        {
            importer.isReadable = false;
        }
    }
}
