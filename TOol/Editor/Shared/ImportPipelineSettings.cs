using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// [2] 导入管线可选钩子总闸。编排 Pipeline/ConfigData，进版本库。
// 本闸关则 OnPreprocess 整段不跑。不管线⑤。不读批量选择器。
// =====================================================================================

/// <summary>Unity 导入期钩子总开关。关则 OnPreprocess 不跑。</summary>
public class ImportPipelineSettings : ScriptableObject
{
    public const string AssetPath =
        ProcessSettingsOwnership.PipelineConfigRoot + "/ImportPipelineSettings.asset";

    [Tooltip("可选导入钩子总开关：关则 OnPreprocessModel / OnPreprocessTexture 整段不跑（含剔灯/OBJ）。\n" +
             "开则按本页模型/贴图分项写 Importer。Art 交付区硬跳过。不读批量选择器导入根。")]
    public bool importHooksEnabled = true;

    private static ImportPipelineSettings cached;

    public static bool AreHooksEnabled()
    {
        ImportPipelineSettings settings =
            AssetDatabase.LoadAssetAtPath<ImportPipelineSettings>(AssetPath);
        if (settings == null)
        {
            return true;
        }

        return settings.importHooksEnabled;
    }

    public static ImportPipelineSettings GetOrCreateAsset()
    {
        if (cached != null)
        {
            return cached;
        }

        cached = AssetDatabase.LoadAssetAtPath<ImportPipelineSettings>(AssetPath);
        if (cached != null)
        {
            return cached;
        }

        ProcessSettingsOwnership.EnsureAssetFolder(
            Path.GetDirectoryName(AssetPath).Replace("\\", "/"));
        cached = CreateInstance<ImportPipelineSettings>();
        cached.importHooksEnabled = true;
        AssetDatabase.CreateAsset(cached, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[ImportPipelineSettings] 已创建: " + AssetPath);
        return cached;
    }
}
