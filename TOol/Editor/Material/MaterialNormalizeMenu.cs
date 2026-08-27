using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 交付 Shader 规范化：batchmode / 代码入口。
// 精准人机入口：资源总面板「材质」分项（后续再做独立材质精准面板）；不挂 Tools/资源处理 子菜单。
// =====================================================================================

/// <summary>材质交付 Shader 规范化（无菜单；总面板 / executeMethod）。</summary>
public static class MaterialNormalizeMenu
{
    private const string GgddddMaterialFolder = "Assets/Art/ggdddd/Material";

    /// <summary>
    /// batchmode：Unity.exe -batchmode -quit -projectPath … -executeMethod MaterialNormalizeMenu.NormalizeGgddddBatch
    /// </summary>
    public static void NormalizeGgddddBatch()
    {
        var folders = new List<string>();
        if (AssetDatabase.IsValidFolder(GgddddMaterialFolder))
        {
            folders.Add(GgddddMaterialFolder);
        }

        RunNormalize(folders);
    }

    /// <summary>对指定夹跑主批量材质 Op（供面板/测试调用）。</summary>
    public static MaterialOperationRunSummary RunNormalize(List<string> folders)
    {
        MaterialProcessSettings settings = MaterialProcessSettings.GetOrCreateAsset();
        if (folders == null || folders.Count == 0)
        {
            string err = "未找到可用文件夹。请确保存在 " + GgddddMaterialFolder + " 或传入有效夹。";
            Debug.LogError("[MaterialNormalizeMenu] " + err);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            return new MaterialOperationRunSummary();
        }

        List<string> mats = MaterialTargetCollector.CollectFromFolders(folders);
        List<IMaterialAssetOperation> ops =
            MaterialOperationRegistry.GetMasterBatchOperations(settings);
        if (ops.Count == 0)
        {
            string err = "MaterialProcessSettings.masterBatchOperationIds 为空。";
            Debug.LogError("[MaterialNormalizeMenu] " + err);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            return new MaterialOperationRunSummary();
        }

        MaterialOperationRunSummary summary = MaterialOperationRunner.Run(ops, mats, settings);
        string msg =
            "文件夹 × " + folders.Count + "\n" +
            "材质 × " + mats.Count + "\n" +
            "改动 " + summary.ChangedCount +
            " / 跳过 " + summary.SkippedCount +
            " / 失败 " + summary.FailedCount +
            (summary.Canceled ? "\n（已取消）" : string.Empty) +
            "\n\n目标 Shader: " + settings.targetShaderName;
        Debug.Log("[MaterialNormalizeMenu]\n" + msg);

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(summary.FailedCount > 0 ? 2 : 0);
        }

        return summary;
    }
}
