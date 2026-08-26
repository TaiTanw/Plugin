using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 20_Package — 成品直达：薄门面 → RetinarAbApi.Build（强制 UP）
// =====================================================================================

/// <summary>
/// 选中 Prefab 直通打包。输出根 / AB 根来自 <see cref="RetinarExportSettings"/>。
/// </summary>
public static class RetinarDirectPackage
{
    /// <summary>菜单：成品直达 / 选中预制体直通打包。</summary>
    public static void PackageSelectedPrefabsDirect()
    {
        if (RetinarEditorUtil.StopIfEditorIsPlaying())
        {
            return;
        }

        List<string> prefabPaths = CollectSelectedPrefabPaths();
        if (prefabPaths.Count == 0)
        {
            RetinarEditorUtil.ShowDialogDeferred(
                "Retinar 成品直达",
                "请在 Project 中选中一个或多个 Prefab（可多选）。\n\n" +
                "本入口不会平铺、不会改 Art、不会加碰撞体。\n" +
                "未整理的外部 FBX/散落资源请走「批量汇总 > 平铺到 Art」。",
                "OK");
            return;
        }

        RetinarExportSettings exportSettings = RetinarExportSettings.GetOrCreateAsset();
        RetinarAbBuildOptions options = RetinarAbBuildOptions.FromExportSettings(
            exportSettings,
            exportUnityPackageOverride: true,
            quietOverride: false);

        if (!EditorUtility.DisplayDialog(
                "Retinar 成品直达",
                "将对 " + prefabPaths.Count + " 个预制体打包：\n" +
                "· Android/iOS AB" + (options.ExportUnityPackage ? " + UnityPackage" : string.Empty) + "\n" +
                "· 交付根: " + options.NormalizedDeliverableRoot + "\n" +
                "· AB 构建根: " + options.NormalizedAssetBundleRoot + "\n" +
                "· 不修改 Prefab / 不跑门禁\n\n是否继续？",
                "打包",
                "取消"))
        {
            return;
        }

        RetinarAbBuildResult built = RetinarAbApi.Build(prefabPaths, options);

        string failText = built.FailLines.Count == 0
            ? string.Empty
            : "\n\n失败 " + built.FailLines.Count + " 项：\n" + string.Join("\n", built.FailLines.ToArray());

        RetinarEditorUtil.ShowDialogDeferred(
            "Retinar 成品直达",
            "完成。成功 " + built.OkNames.Count + " / " + prefabPaths.Count + "。\n\n" +
            "交付目录: " + Path.Combine(Directory.GetCurrentDirectory(), options.NormalizedDeliverableRoot) +
            "\n（AB" + (options.ExportUnityPackage ? " + 02_unity" : string.Empty) + "）" +
            failText,
            "OK");
    }

    public static bool ValidatePackageSelectedPrefabsDirect()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static List<string> CollectSelectedPrefabPaths()
    {
        var list = new List<string>();
        Object[] objects = Selection.objects;
        if (objects == null)
        {
            return list;
        }

        foreach (Object obj in objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            path = path.Replace("\\", "/");
            if (Path.GetExtension(path).ToLowerInvariant() != ".prefab")
            {
                continue;
            }

            if (!list.Contains(path))
            {
                list.Add(path);
            }
        }

        return list;
    }
}
