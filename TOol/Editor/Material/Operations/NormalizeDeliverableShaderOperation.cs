using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// =====================================================================================
// 交付 Shader 规范化：不合规 .mat（如 UnityGLTF PBRGraph）→ 目标 Shader（默认 Standard）
// + 基础属性槽映射。第一刀对齐 FBX 现网能亮。
// =====================================================================================

/// <summary>把交付材质烤到 APP 可解析的 Shader。</summary>
public class NormalizeDeliverableShaderOperation : IMaterialAssetOperation
{
    public string Id
    {
        get { return MaterialProcessSettings.OpNormalizeDeliverableShader; }
    }

    public string DisplayName
    {
        get { return "规范化交付 Shader"; }
    }

    public string Description
    {
        get
        {
            return "将 UnityGLTF/PBRGraph 等不合规材质烘焙到目标 Shader（默认 Standard），" +
                   "并映射 baseColor→_MainTex/_Color 等基础槽。用于消除 APP 整片洋红。";
        }
    }

    public int Order
    {
        get { return 10; }
    }

    public AssetOperationEvaluation Evaluate(string assetPath, MaterialProcessSettings settings)
    {
        if (string.IsNullOrEmpty(assetPath) ||
            !assetPath.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
        {
            return AssetOperationEvaluation.NotApplicable("非 .mat");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            return AssetOperationEvaluation.NotApplicable("无法加载 Material");
        }

        if (settings == null)
        {
            settings = MaterialProcessSettings.Current;
        }

        settings.EnsureMasterBatchDefaults();
        Shader shader = material.shader;
        if (shader == null)
        {
            return AssetOperationEvaluation.NeedsWorkResult("Shader 为空");
        }

        string targetName = settings.targetShaderName;
        if (!string.IsNullOrEmpty(targetName) &&
            string.Equals(shader.name, targetName, System.StringComparison.Ordinal))
        {
            return AssetOperationEvaluation.Skip("已是目标 Shader: " + targetName);
        }

        if (settings.IsAllowedShader(shader) &&
            (string.IsNullOrEmpty(targetName) ||
             string.Equals(shader.name, targetName, System.StringComparison.Ordinal)))
        {
            return AssetOperationEvaluation.Skip("已在白名单: " + shader.name);
        }

        if (settings.MatchesSourceSubstring(shader) || !settings.IsAllowedShader(shader))
        {
            return AssetOperationEvaluation.NeedsWorkResult(
                "需烘焙: " + shader.name + " → " + targetName);
        }

        return AssetOperationEvaluation.Skip("无需处理: " + shader.name);
    }

    public bool CanProcess(string assetPath, MaterialProcessSettings settings)
    {
        return Evaluate(assetPath, settings).NeedsWork;
    }

    public MaterialOperationResult Execute(MaterialOperationContext context)
    {
        MaterialProcessSettings settings = context.Settings ?? MaterialProcessSettings.Current;
        settings.EnsureMasterBatchDefaults();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(context.AssetPath);
        if (material == null)
        {
            return MaterialOperationResult.Failed("无法加载: " + context.AssetPath);
        }

        AssetOperationEvaluation evaluation = Evaluate(context.AssetPath, settings);
        if (!evaluation.NeedsWork)
        {
            return MaterialOperationResult.Skipped(evaluation.Reason);
        }

        string targetName = settings.targetShaderName;
        Shader target = Shader.Find(targetName);
        if (target == null)
        {
            return MaterialOperationResult.Failed("找不到目标 Shader: " + targetName);
        }

        string oldName = material.shader != null ? material.shader.name : "(null)";

        // 先读旧槽（换 Shader 后属性名会丢）
        Texture baseMap = GetTex(material, "baseColorTexture", "_BaseMap", "_MainTex");
        Color baseColor = GetColor(material, "baseColorFactor", "_BaseColor", "_Color", Color.white);
        Texture normalMap = GetTex(material, "normalTexture", "_BumpMap", "_NormalMap");
        Texture occlusionMap = GetTex(material, "occlusionTexture", "_OcclusionMap");
        Texture emissionMap = GetTex(material, "emissiveTexture", "_EmissionMap");
        Color emission = GetColor(material, "emissiveFactor", "_EmissionColor", "_EmissionColor", Color.black);
        float metallic = GetFloat(material, "metallicFactor", "_Metallic", 0f);
        float roughness = GetFloat(material, "roughnessFactor", "_Smoothness", -1f);
        float glossiness = roughness >= 0f
            ? Mathf.Clamp01(1f - roughness)
            : GetFloat(material, "_Glossiness", "_Smoothness", 0.5f);

        material.shader = target;

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", baseMap);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        if (material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normalMap);
            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
        }

        if (material.HasProperty("_OcclusionMap"))
        {
            material.SetTexture("_OcclusionMap", occlusionMap);
        }

        if (material.HasProperty("_EmissionMap"))
        {
            material.SetTexture("_EmissionMap", emissionMap);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0.001f || emissionMap != null)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.BakedEmissive;
            }
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", glossiness);
        }

        // Opaque
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 0f);
        }

        EditorUtility.SetDirty(material);
        return MaterialOperationResult.Changed(oldName + " → " + targetName);
    }

    private static Texture GetTex(Material material, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (material.HasProperty(names[i]))
            {
                Texture t = material.GetTexture(names[i]);
                if (t != null)
                {
                    return t;
                }
            }
        }

        return null;
    }

    private static Color GetColor(
        Material material,
        string primary,
        string alt1,
        string alt2,
        Color fallback)
    {
        if (material.HasProperty(primary))
        {
            return material.GetColor(primary);
        }

        if (!string.IsNullOrEmpty(alt1) && material.HasProperty(alt1))
        {
            return material.GetColor(alt1);
        }

        if (!string.IsNullOrEmpty(alt2) && material.HasProperty(alt2))
        {
            return material.GetColor(alt2);
        }

        return fallback;
    }

    private static float GetFloat(Material material, string primary, string alt, float fallback)
    {
        if (material.HasProperty(primary))
        {
            return material.GetFloat(primary);
        }

        if (!string.IsNullOrEmpty(alt) && material.HasProperty(alt))
        {
            return material.GetFloat(alt);
        }

        return fallback;
    }
}
