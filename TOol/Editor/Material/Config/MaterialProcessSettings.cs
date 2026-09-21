using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 材质处理规则（⑤ Material 层）。目标 Shader / 白名单进版本库。
// =====================================================================================

/// <summary>交付材质 Shader 规范化配置。</summary>
public class MaterialProcessSettings : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Plugin/TOol/ConfigData/MaterialProcessSettings.asset";
    public const string PipelineAssetPath =
        ProcessSettingsOwnership.PipelineConfigRoot + "/MaterialProcessSettings.asset";

    public const string OpNormalizeDeliverableShader = "normalize_deliverable_shader";

    [Header("交付目标 Shader")]
    [Tooltip("不合规材质烘焙到的 Shader 名（先 Standard 对齐 FBX 现网；APP 若纯 URP 再改 URP Lit）。")]
    public string targetShaderName = "Standard";

    [Header("视为已合规的 Shader 名（精确匹配）")]
    [Tooltip("额外白名单。⑤ 主规则是：本单元 Art 内 Shader、或 Unity 内置，不靠此表。")]
    public List<string> allowedShaderNames = new List<string> { "Standard" };

    [Header("视为需烘焙的源 Shader 名子串（任一命中则烤）")]
    [Tooltip("仅文档/旧配置保留。开闸已改为：非 Art 单元且非内置则烤（如 Packages/PBRGraph）。")]
    public List<string> sourceShaderNameSubstrings = new List<string>
    {
        "UnityGLTF",
        "PBRGraph",
        "UnlitGraph"
    };

    [HideInInspector]
    public List<string> masterBatchOperationIds =
        new List<string> { OpNormalizeDeliverableShader };

    private static MaterialProcessSettings assetInstance;
    private static MaterialProcessSettings pipelineInstance;
    private static MaterialProcessSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public void EnsureMasterBatchDefaults()
    {
        if (masterBatchOperationIds == null)
        {
            masterBatchOperationIds = new List<string>();
        }

        if (masterBatchOperationIds.Count == 0)
        {
            masterBatchOperationIds.Add(OpNormalizeDeliverableShader);
            EditorUtility.SetDirty(this);
        }

        if (allowedShaderNames == null)
        {
            allowedShaderNames = new List<string>();
        }

        if (allowedShaderNames.Count == 0)
        {
            allowedShaderNames.Add("Standard");
            EditorUtility.SetDirty(this);
        }

        if (string.IsNullOrWhiteSpace(targetShaderName))
        {
            targetShaderName = "Standard";
            EditorUtility.SetDirty(this);
        }
    }

    public bool IsAllowedShader(Shader shader)
    {
        if (shader == null || allowedShaderNames == null)
        {
            return false;
        }

        string name = shader.name;
        for (int i = 0; i < allowedShaderNames.Count; i++)
        {
            if (!string.IsNullOrEmpty(allowedShaderNames[i]) &&
                string.Equals(allowedShaderNames[i], name, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool MatchesSourceSubstring(Shader shader)
    {
        if (shader == null)
        {
            return false;
        }

        if (sourceShaderNameSubstrings == null || sourceShaderNameSubstrings.Count == 0)
        {
            return !IsAllowedShader(shader);
        }

        string name = shader.name ?? string.Empty;
        for (int i = 0; i < sourceShaderNameSubstrings.Count; i++)
        {
            string sub = sourceShaderNameSubstrings[i];
            if (!string.IsNullOrEmpty(sub) &&
                name.IndexOf(sub, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static MaterialProcessSettings Current
    {
        get
        {
            MaterialProcessSettings found = FindExistingAsset();
            if (found != null)
            {
                return found;
            }

            if (fallbackInstance == null)
            {
                fallbackInstance = CreateInstance<MaterialProcessSettings>();
                fallbackInstance.EnsureMasterBatchDefaults();
            }

            if (!fallbackWarningLogged)
            {
                fallbackWarningLogged = true;
                Debug.LogWarning("[MaterialProcessSettings] 尚无配置资产，使用内存默认。" +
                    "打开资源处理总面板或跑材质批量会创建 " + DefaultAssetPath);
            }

            return fallbackInstance;
        }
    }

    public static MaterialProcessSettings GetOrCreateAsset()
    {
        MaterialProcessSettings found = FindExistingAsset();
        if (found != null)
        {
            return found;
        }

        EnsureAssetFolder(Path.GetDirectoryName(DefaultAssetPath).Replace("\\", "/"));
        var created = CreateInstance<MaterialProcessSettings>();
        created.EnsureMasterBatchDefaults();
        AssetDatabase.CreateAsset(created, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        assetInstance = created;
        fallbackWarningLogged = false;
        Debug.Log("[MaterialProcessSettings] 已创建: " + DefaultAssetPath);
        return created;
    }

    public static MaterialProcessSettings GetOrCreatePipelineAsset()
    {
        if (pipelineInstance != null)
        {
            pipelineInstance.EnsureMasterBatchDefaults();
            return pipelineInstance;
        }

        pipelineInstance = AssetDatabase.LoadAssetAtPath<MaterialProcessSettings>(PipelineAssetPath);
        if (pipelineInstance != null)
        {
            pipelineInstance.EnsureMasterBatchDefaults();
            return pipelineInstance;
        }

        ProcessSettingsOwnership.EnsureAssetFolder(
            Path.GetDirectoryName(PipelineAssetPath).Replace("\\", "/"));
        var created = CreateInstance<MaterialProcessSettings>();
        created.EnsureMasterBatchDefaults();
        AssetDatabase.CreateAsset(created, PipelineAssetPath);
        AssetDatabase.SaveAssets();
        pipelineInstance = created;
        Debug.Log("[MaterialProcessSettings] 已创建编排配置资产: " + PipelineAssetPath);
        return created;
    }

    private static MaterialProcessSettings FindExistingAsset()
    {
        if (assetInstance != null)
        {
            assetInstance.EnsureMasterBatchDefaults();
            return assetInstance;
        }

        assetInstance = AssetDatabase.LoadAssetAtPath<MaterialProcessSettings>(DefaultAssetPath);
        if (assetInstance != null)
        {
            assetInstance.EnsureMasterBatchDefaults();
            return assetInstance;
        }

        string[] guids = AssetDatabase.FindAssets("t:MaterialProcessSettings");
        if (guids == null)
        {
            return null;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (ProcessSettingsOwnership.IsPipelineConfigPath(path))
            {
                continue;
            }

            assetInstance = AssetDatabase.LoadAssetAtPath<MaterialProcessSettings>(path);
            if (assetInstance != null)
            {
                assetInstance.EnsureMasterBatchDefaults();
                return assetInstance;
            }
        }

        return null;
    }

    private static void EnsureAssetFolder(string folderPath)
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
