using UnityEditor;

// =====================================================================================
// 模型导入期 Importer。赋值走 ModelImporterProfiles，本类只负责闸。
//
// 闸顺序：
//   1. Art 硬跳过（交付区由④写，避免和导入钩子打架）。
//   2. 可选钩子总闸 ImportPipelineSettings.importHooksEnabled（关则整段不跑）。
//   3. 扩展名；读编排 ModelProcessSettings（若有），否则人工 Current。
//   4. 基线剔灯/相机、OBJ 法线；策略 External。不读批量选择器导入根，不读不介入目录。
// =====================================================================================
public class ModelImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (ModelImporterProfiles.IsArtDeliveryPath(assetPath))
        {
            return;
        }

        if (!ImportPipelineSettings.AreHooksEnabled())
        {
            return;
        }

        var importer = assetImporter as ModelImporter;
        if (importer == null)
        {
            return;
        }

        ModelProcessSettings settings = ModelProcessSettings.ForImportCallbacks();
        if (settings == null ||
            !settings.IsSupportedModelExtension(assetPath))
        {
            return;
        }

        ModelImporterProfiles.ApplyIncomingBaseline(importer, assetPath, settings);

        if (!ShouldApplyIncomingPolicy(settings.modelUseExternalMaterials))
        {
            return;
        }

        ModelImporterProfiles.ApplyIncomingPolicy(importer, settings);
    }

    internal static bool ShouldApplyIncomingPolicy(bool modelUseExternalMaterials)
    {
        return modelUseExternalMaterials;
    }
}
