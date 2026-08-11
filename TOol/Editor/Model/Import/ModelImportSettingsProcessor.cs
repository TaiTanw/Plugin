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
        // 成功时不打 Log：批量入库时每 FBX 一条会刷屏，易被当成警告。
    }
}
