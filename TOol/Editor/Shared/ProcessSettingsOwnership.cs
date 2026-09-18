using System;
using System.IO;
using UnityEditor;

// =====================================================================================
// 人工 TOol/ConfigData 与编排 Pipeline/ConfigData 两份同类型 SO 的路径闸。
// Current / GetOrCreateAsset 不得误拿到编排那份。
// =====================================================================================
internal static class ProcessSettingsOwnership
{
    public const string PipelineConfigRoot = "Assets/Plugin/Pipeline/ConfigData";

    public static bool IsPipelineConfigPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string n = assetPath.Replace("\\", "/");
        return n.StartsWith(PipelineConfigRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
