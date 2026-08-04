using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 模型【设置自动】：导入前只改 ModelImporter 参数。
// 后处理（顶点色等）不在这里，走 ModelSourceFileProcessor → ImportPostProcessScheduler。
// =====================================================================================
public class ModelImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!ResourceProcessSwitches.IsModelSettingsEffective)
        {
            return;
        }

        ModelProcessSettings settings = ModelProcessSettings.Current;
        if (!settings.IsSupportedModelExtension(assetPath) || settings.IsExcludedPath(assetPath))
        {
            return;
        }

        var importer = (ModelImporter)assetImporter;

        if (settings.modelUseExternalMaterials)
        {
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        }

        if (settings.modelStripLightsAndCameras)
        {
            importer.importLights = false;
            importer.importCameras = false;
        }

        Debug.Log("[ModelImportSettingsProcessor] 已按模型设置自动处理: " + assetPath);
    }
}
