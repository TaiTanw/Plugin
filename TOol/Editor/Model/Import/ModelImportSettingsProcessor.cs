using UnityEditor;

// =====================================================================================
// 模型【设置自动】：导入前改 ModelImporter 参数。
// 导入区唯一入口。赋值都走 ModelImporterProfiles，本类只负责闸。
//
// 闸顺序：
//   1. Art 硬跳过（D24-6）——不看 SO 排除表，清空 excludedPathPrefixes 也不能写交付区。
//   2. 扩展名。
//   3. 基线：配置的 Incoming 根内不受本机总闸/排除表；其它路径保持原闸（D26-1）。
//   4. 策略：仍须未排除 + 总闸 +「模型 · 设置自动」同时成立。
// =====================================================================================
public class ModelImportSettingsProcessor : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (ModelImporterProfiles.IsArtDeliveryPath(assetPath))
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
            !settings.IsSupportedModelExtension(assetPath))
        {
            return;
        }

        bool masterEnabled = ResourceProcessSwitches.MasterEnabled;
        bool excluded = settings.IsExcludedPath(assetPath);
        bool isInImportRoot = false;
        if (!masterEnabled || excluded)
        {
            BatchFbxImportSettings importSettings = BatchFbxImportSettings.Current;
            isInImportRoot = importSettings != null &&
                             importSettings.ContainsAssetPath(assetPath);
        }

        if (!ShouldApplyIncomingBaseline(masterEnabled, excluded, isInImportRoot))
        {
            return;
        }

        ModelImporterProfiles.ApplyIncomingBaseline(importer, assetPath, settings);

        if (excluded ||
            !ShouldApplyIncomingPolicy(
                masterEnabled,
                ResourceProcessSwitches.ModelSettingsAuto))
        {
            return;
        }

        ModelImporterProfiles.ApplyIncomingPolicy(importer, settings);
    }

    internal static bool ShouldApplyIncomingBaseline(
        bool masterEnabled,
        bool excluded,
        bool isInImportRoot)
    {
        return isInImportRoot || (masterEnabled && !excluded);
    }

    internal static bool ShouldApplyIncomingPolicy(
        bool masterEnabled,
        bool modelSettingsAuto)
    {
        return masterEnabled && modelSettingsAuto;
    }
}
