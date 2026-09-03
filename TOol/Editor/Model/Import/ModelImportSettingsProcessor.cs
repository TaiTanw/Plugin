using UnityEditor;

// =====================================================================================
// 模型【设置自动】：导入前改 ModelImporter 参数。
// 导入区唯一入口。赋值都走 ModelImporterProfiles，本类只负责闸。
//
// 闸顺序：
//   1. Art 硬跳过（D24-6）——不看 SO 排除表，清空 excludedPathPrefixes 也不能写交付区。
//   2. 总闸。基线是否脱离总闸见 backlog D26-1，本文件暂不动。
//   3. 扩展名 / SO 排除表。
//   4. Incoming 基线；勾了「模型 · 设置自动」再跑 Incoming 策略。
// =====================================================================================
public class ModelImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (ModelImporterProfiles.IsArtDeliveryPath(assetPath))
        {
            return;
        }

        if (!ResourceProcessSwitches.MasterEnabled)
        {
            return;
        }

        var importer = assetImporter as ModelImporter;
        if (importer == null)
        {
            return;
        }

        ModelProcessSettings settings = ModelProcessSettings.Current;
        if (settings == null ||
            !settings.IsSupportedModelExtension(assetPath) ||
            settings.IsExcludedPath(assetPath))
        {
            return;
        }

        ModelImporterProfiles.ApplyIncomingBaseline(importer, assetPath, settings);

        if (!ResourceProcessSwitches.ModelSettingsAuto)
        {
            return;
        }

        ModelImporterProfiles.ApplyIncomingPolicy(importer, settings);
    }
}
