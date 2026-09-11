using System;
using System.IO;
using UnityEditor;

// =====================================================================================
// 模型 Importer 两档口径（D24-6）。
//
// 同一职责以前写了两遍：插件 2 OnPreprocessModel 与插件 1 ApplyModelImportSettings。
// SaveAndReimport 会再进 OnPreprocessModel，两边若写同一路径就会来回覆盖。
//
// 现网分区：
//   Incoming —— 只由本类 ApplyIncoming* 写，入口是 ModelImportSettingsProcessor。
//   Art      —— 只由 ApplyArtDelivery 写，入口是 ④ FlattenBuildService.ApplyImportAndExtract。
// Art 路径在 Processor 里硬跳过（不只靠 SO 排除表），清空 excludedPathPrefixes 也不能复现打架。
// =====================================================================================

/// <summary>导入区与交付区 ModelImporter 的唯一赋值点。</summary>
public static class ModelImporterProfiles
{
    public static bool IsArtDeliveryPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string p = assetPath.Replace("\\", "/");
        return p.Equals(FlattenBuildSettings.ArtRoot, StringComparison.OrdinalIgnoreCase) ||
               p.StartsWith(FlattenBuildSettings.ArtRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>导入区基线。读 SO，不写材质来源。</summary>
    public static void ApplyIncomingBaseline(ModelImporter importer, string assetPath, ModelProcessSettings settings)
    {
        if (importer == null || settings == null)
        {
            return;
        }

        if (settings.modelStripLightsAndCameras)
        {
            importer.importLights = false;
            importer.importCameras = false;
        }

        if (settings.modelCalculateNormalsForObj && IsObj(assetPath))
        {
            importer.importNormals = ModelImporterNormals.Calculate;
        }
    }

    /// <summary>导入区策略。仅「模型 · 设置自动」勾选时调用。</summary>
    public static void ApplyIncomingPolicy(ModelImporter importer, ModelProcessSettings settings)
    {
        if (importer == null || settings == null || !settings.modelUseExternalMaterials)
        {
            return;
        }

        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
    }

    /// <summary>
    /// 交付区硬约束，不读导入区 SO。
    /// InPrefab + Local 见 PACKAGING_RULES 20/21/37；isReadable 给 ⑤ 刷顶点色。
    /// 本方法不 SaveAndReimport——调用方必须走保留顶点色的那条重导。
    /// </summary>
    public static void ApplyArtDelivery(ModelImporter importer, string assetPath)
    {
        if (importer == null)
        {
            return;
        }

        importer.globalScale = 1f;
        importer.importNormals = IsObj(assetPath)
            ? ModelImporterNormals.Calculate
            : ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        TrySetMaterialImportMode(importer, "ImportViaMaterialDescription");

        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialSearch = ModelImporterMaterialSearch.Local;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.addCollider = false;
        importer.isReadable = true;
    }

    private static bool IsObj(string assetPath)
    {
        return string.Equals(Path.GetExtension(assetPath), ".obj", StringComparison.OrdinalIgnoreCase);
    }

    private static void TrySetMaterialImportMode(ModelImporter importer, string enumName)
    {
        System.Reflection.PropertyInfo property = typeof(ModelImporter).GetProperty("materialImportMode");
        if (property == null || !property.CanWrite)
        {
            return;
        }

        try
        {
            object value = Enum.Parse(property.PropertyType, enumName);
            property.SetValue(importer, value, null);
        }
        catch (Exception)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
