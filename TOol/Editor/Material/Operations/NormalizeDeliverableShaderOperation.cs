using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// =====================================================================================
// 交付 Shader 规范化：不合规 .mat（如 UnityGLTF PBRGraph）→ 目标 Shader（默认 Standard）
// + 基础属性槽映射，并保留材质自身的 Opaque / Cutout / Blend 语义。
// =====================================================================================

/// <summary>把交付材质烤到 APP 可解析的 Shader。</summary>
public class NormalizeDeliverableShaderOperation : IMaterialAssetOperation
{
    private static readonly string[] SourceSurfaceKeywords =
    {
        "_ALPHATEST_ON",
        "_ALPHABLEND_ON",
        "_ALPHAPREMULTIPLY_ON",
        "_SURFACE_TYPE_TRANSPARENT",
        "_BUILTIN_ALPHATEST_ON",
        "_BUILTIN_AlphaClip",
        "_BUILTIN_ALPHABLEND_ON",
        "_BUILTIN_ALPHAPREMULTIPLY_ON",
        "_BUILTIN_SURFACE_TYPE_TRANSPARENT",
        "_DISABLE_SSR_TRANSPARENT",
        "_ENABLE_FOG_ON_TRANSPARENT"
    };

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
                   "映射基础槽并保留透明/裁切语义。用于消除 APP 洋红与透明材质失真。";
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

        // 先读旧槽与表面类型（换 Shader 后源属性名会丢）
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
        MaterialSurfaceSnapshot surface = CaptureSurface(material);

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

        ApplyTargetSurface(material, surface);

        EditorUtility.SetDirty(material);
        return MaterialOperationResult.Changed(
            oldName + " → " + targetName + "；表面=" + surface.Mode);
    }

    /// <summary>
    /// 在替换 Shader 前捕获材质自己的表面契约。颜色 alpha 只是颜色数据，
    /// 不能单独证明材质声明为透明，因此不参与分类。
    /// </summary>
    internal static MaterialSurfaceSnapshot CaptureSurface(Material material)
    {
        float cutoff = GetFirstFloat(
            material,
            0.5f,
            "_Cutoff",
            "alphaCutoff",
            "_AlphaCutoff");

        string renderType = material != null
            ? material.GetTag("RenderType", false, string.Empty)
            : string.Empty;

        if (HasAnyEnabledFloat(material, "_BUILTIN_AlphaClip", "_AlphaClip", "_AlphaClipEnabled") ||
            HasMode(material, 1f) ||
            HasKeyword(material, "_ALPHATEST_ON", "_BUILTIN_ALPHATEST_ON", "_BUILTIN_AlphaClip") ||
            string.Equals(renderType, "TransparentCutout", System.StringComparison.OrdinalIgnoreCase) ||
            (material != null && material.renderQueue == (int)RenderQueue.AlphaTest))
        {
            return new MaterialSurfaceSnapshot(MaterialSurfaceMode.Cutout, cutoff);
        }

        if (HasAnyEnabledFloat(material, "_BUILTIN_Surface", "_Surface") ||
            HasMode(material, 2f, 3f) ||
            HasKeyword(
                material,
                "_ALPHABLEND_ON",
                "_ALPHAPREMULTIPLY_ON",
                "_SURFACE_TYPE_TRANSPARENT",
                "_BUILTIN_ALPHABLEND_ON",
                "_BUILTIN_ALPHAPREMULTIPLY_ON",
                "_BUILTIN_SURFACE_TYPE_TRANSPARENT") ||
            string.Equals(renderType, "Transparent", System.StringComparison.OrdinalIgnoreCase) ||
            (material != null && material.renderQueue >= (int)RenderQueue.Transparent))
        {
            return new MaterialSurfaceSnapshot(MaterialSurfaceMode.Blend, cutoff);
        }

        return new MaterialSurfaceSnapshot(MaterialSurfaceMode.Opaque, cutoff);
    }

    /// <summary>把已捕获的表面契约应用到目标 Shader；当前完整覆盖 Standard，并兼容常见 _Surface 目标。</summary>
    internal static void ApplyTargetSurface(Material material, MaterialSurfaceSnapshot surface)
    {
        if (material == null)
        {
            return;
        }

        bool cutout = surface.Mode == MaterialSurfaceMode.Cutout;
        bool blend = surface.Mode == MaterialSurfaceMode.Blend;
        bool usesSurfaceProperty = material.HasProperty("_Surface");

        SetFloatIfPresent(material, "_Mode", cutout ? 1f : blend ? 2f : 0f);
        SetFloatIfPresent(material, "_Surface", blend ? 1f : 0f);
        SetFloatIfPresent(material, "_BUILTIN_Surface", blend ? 1f : 0f);
        SetFloatIfPresent(material, "_AlphaClip", cutout ? 1f : 0f);
        SetFloatIfPresent(material, "_BUILTIN_AlphaClip", cutout ? 1f : 0f);
        ClearSurfaceKeywords(material);

        if (cutout)
        {
            material.SetOverrideTag("RenderType", "TransparentCutout");
            SetIntIfPresent(material, "_SrcBlend", (int)BlendMode.One);
            SetIntIfPresent(material, "_DstBlend", (int)BlendMode.Zero);
            SetIntIfPresent(material, "_ZWrite", 1);
            SetFloatIfPresent(material, "_Cutoff", surface.Cutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            return;
        }

        if (blend)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            SetIntIfPresent(material, "_SrcBlend", (int)BlendMode.SrcAlpha);
            SetIntIfPresent(material, "_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            SetIntIfPresent(material, "_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            if (usesSurfaceProperty)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            return;
        }

        material.SetOverrideTag("RenderType", "Opaque");
        SetIntIfPresent(material, "_SrcBlend", (int)BlendMode.One);
        SetIntIfPresent(material, "_DstBlend", (int)BlendMode.Zero);
        SetIntIfPresent(material, "_ZWrite", 1);
        material.renderQueue = (int)RenderQueue.Geometry;
    }

    /// <summary>
    /// 主贴图已标 Alpha Is Transparency 时，把仍为 Opaque 的 Standard 改成 Fade。
    /// 亮度烤透明会打开该 importer 标记；④从 OBJ 拷出的材质默认 Opaque，不改则旋翼仍是实心黑盘。
    /// 已是 Cutout / Fade / Transparent 的不改。
    /// </summary>
    internal static bool TryApplyFadeIfMainTexMarksTransparency(Material material)
    {
        if (material == null || !material.HasProperty("_MainTex"))
        {
            return false;
        }

        Texture main = material.GetTexture("_MainTex");
        if (main == null)
        {
            return false;
        }

        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(main)) as TextureImporter;
        if (importer == null || !importer.alphaIsTransparency)
        {
            return false;
        }

        if (HasMode(material, 1f, 2f, 3f))
        {
            return false;
        }

        ApplyTargetSurface(material, new MaterialSurfaceSnapshot(MaterialSurfaceMode.Blend, 0.5f));
        EditorUtility.SetDirty(material);
        return true;
    }

    internal static int ApplyFadeToMaterialsUsingMainTexture(string textureAssetPath)
    {
        if (string.IsNullOrEmpty(textureAssetPath))
        {
            return 0;
        }

        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(textureAssetPath);
        if (texture == null)
        {
            return 0;
        }

        int changed = 0;
        string[] guids = AssetDatabase.FindAssets("t:Material");
        for (int i = 0; i < guids.Length; i++)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (material == null ||
                !material.HasProperty("_MainTex") ||
                material.GetTexture("_MainTex") != texture)
            {
                continue;
            }

            if (TryApplyFadeIfMainTexMarksTransparency(material))
            {
                changed++;
            }
        }

        return changed;
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

    private static float GetFirstFloat(Material material, float fallback, params string[] names)
    {
        if (material == null || names == null)
        {
            return fallback;
        }

        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrEmpty(names[i]) && material.HasProperty(names[i]))
            {
                return material.GetFloat(names[i]);
            }
        }

        return fallback;
    }

    private static bool HasAnyEnabledFloat(Material material, params string[] names)
    {
        if (material == null || names == null)
        {
            return false;
        }

        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrEmpty(names[i]) &&
                material.HasProperty(names[i]) &&
                material.GetFloat(names[i]) > 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMode(Material material, params float[] expectedModes)
    {
        if (material == null || !material.HasProperty("_Mode") || expectedModes == null)
        {
            return false;
        }

        float actual = material.GetFloat("_Mode");
        for (int i = 0; i < expectedModes.Length; i++)
        {
            if (Mathf.Approximately(actual, expectedModes[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasKeyword(Material material, params string[] keywords)
    {
        if (material == null || keywords == null)
        {
            return false;
        }

        for (int i = 0; i < keywords.Length; i++)
        {
            if (material.IsKeywordEnabled(keywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearSurfaceKeywords(Material material)
    {
        for (int i = 0; i < SourceSurfaceKeywords.Length; i++)
        {
            material.DisableKeyword(SourceSurfaceKeywords[i]);
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetIntIfPresent(Material material, string propertyName, int value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetInt(propertyName, value);
        }
    }
}

internal enum MaterialSurfaceMode
{
    Opaque,
    Cutout,
    Blend
}

internal struct MaterialSurfaceSnapshot
{
    public readonly MaterialSurfaceMode Mode;
    public readonly float Cutoff;

    public MaterialSurfaceSnapshot(MaterialSurfaceMode mode, float cutoff)
    {
        Mode = mode;
        Cutoff = Mathf.Clamp01(cutoff);
    }
}
