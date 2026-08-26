using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 第一刀菜单：对选中夹 / Art/ggdddd / L1 批量路径跑交付 Shader 规范化。
// =====================================================================================

/// <summary>材质交付 Shader 规范化菜单。</summary>
public static class MaterialNormalizeMenu
{
    private const string GgddddMaterialFolder = "Assets/Art/ggdddd/Material";

    [MenuItem("Tools/资源处理/规范化交付 Shader（选中夹或 ggdddd）", false, 52)]
    public static void NormalizeSelectedOrGgdddd()
    {
        RunNormalize(ResolveFolders(), showDialog: !Application.isBatchMode);
    }

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

        RunNormalize(folders, showDialog: false);
    }

    private static void RunNormalize(List<string> folders, bool showDialog)
    {
        MaterialProcessSettings settings = MaterialProcessSettings.GetOrCreateAsset();
        if (folders == null || folders.Count == 0)
        {
            string err = "未找到可用文件夹。请选中 Assets 下文件夹，或确保存在 " + GgddddMaterialFolder;
            Debug.LogError("[MaterialNormalizeMenu] " + err);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("规范化交付 Shader", err, "OK");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        List<string> mats = MaterialTargetCollector.CollectFromFolders(folders);
        List<IMaterialAssetOperation> ops =
            MaterialOperationRegistry.GetMasterBatchOperations(settings);
        if (ops.Count == 0)
        {
            string err = "MaterialProcessSettings.masterBatchOperationIds 为空。";
            Debug.LogError("[MaterialNormalizeMenu] " + err);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("规范化交付 Shader", err, "OK");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            return;
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
        if (showDialog)
        {
            EditorUtility.DisplayDialog("规范化交付 Shader", msg, "OK");
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(summary.FailedCount > 0 ? 2 : 0);
        }
    }

    private static List<string> ResolveFolders()
    {
        var folders = new List<string>();
        Object[] selected = Selection.objects;
        if (selected != null)
        {
            for (int i = 0; i < selected.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selected[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    if (!folders.Contains(path))
                    {
                        folders.Add(path);
                    }
                }
                else if (path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
                {
                    string dir = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                    if (!string.IsNullOrEmpty(dir) && !folders.Contains(dir))
                    {
                        folders.Add(dir);
                    }
                }
            }
        }

        if (folders.Count == 0 && AssetDatabase.IsValidFolder(GgddddMaterialFolder))
        {
            folders.Add(GgddddMaterialFolder);
        }

        return folders;
    }
}
