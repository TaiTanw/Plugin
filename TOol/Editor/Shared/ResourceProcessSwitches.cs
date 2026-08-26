using UnityEditor;

// =====================================================================================
// 职责边界：
//   Shared 层。只回答"当前这台机器：自动化要不要跑 / 某类资源的设置或后处理要不要跑"。
//   不做任何贴图/模型处理。状态全部放 EditorPrefs（本机个人设置，不进版本库）。
//
// 层级：
//   总开关 MasterEnabled —— 关掉则任何设置自动、后处理自动都不跑（手动面板仍可用）
//   分项：贴图/模型 × 设置自动/后处理自动 —— 总开关打开时才生效
//
//   设置自动   = AssetPostprocessor 里改 Importer 参数
//   后处理自动 = 导入结束后 delayCall 跑的 Operation
//
// 旧版只有一个 AssetProcessSwitch.IsEnabled。首次读取时若旧 key 为 true，
// 会把四路分项一并打开一次；总开关本身默认开启。
// =====================================================================================
/// <summary>
/// 编辑器设置
/// </summary>
public static class ResourceProcessSwitches
{
    private const string LegacyEnabledKey = "TOol.AssetProcessSwitch.Enabled";
    private const string MasterEnabledKey = "TOol.Switch.MasterEnabled";
    private const string TextureSettingsKey = "TOol.Switch.Texture.SettingsAuto";
    private const string TexturePostProcessKey = "TOol.Switch.Texture.PostProcessAuto";
    private const string ModelSettingsKey = "TOol.Switch.Model.SettingsAuto";
    private const string ModelPostProcessKey = "TOol.Switch.Model.PostProcessAuto";
    private const string MasterBatchIncludeTextureKey = "TOol.Switch.MasterBatch.IncludeTexture";
    private const string MasterBatchIncludeModelKey = "TOol.Switch.MasterBatch.IncludeModel";
    private const string MasterBatchIncludeMaterialKey = "TOol.Switch.MasterBatch.IncludeMaterial";
    private const string MigratedKey = "TOol.Switch.MigratedFromLegacy";

    private static bool? masterEnabled;
    private static bool? textureSettingsAuto;
    private static bool? texturePostProcessAuto;
    private static bool? modelSettingsAuto;
    private static bool? modelPostProcessAuto;
    private static bool? masterBatchIncludeTexture;
    private static bool? masterBatchIncludeModel;
    private static bool? masterBatchIncludeMaterial;

    /// <summary>总开关。默认 true。关闭后所有导入设置自动与后处理自动均不执行。</summary>
    public static bool MasterEnabled
    {
        get { return Get(ref masterEnabled, MasterEnabledKey, true); }
        set { Set(ref masterEnabled, MasterEnabledKey, value); }
    }

    public static bool TextureSettingsAuto
    {
        get { return Get(ref textureSettingsAuto, TextureSettingsKey, false); }
        set { Set(ref textureSettingsAuto, TextureSettingsKey, value); }
    }

    public static bool TexturePostProcessAuto
    {
        get { return Get(ref texturePostProcessAuto, TexturePostProcessKey, false); }
        set { Set(ref texturePostProcessAuto, TexturePostProcessKey, value); }
    }

    public static bool ModelSettingsAuto
    {
        get { return Get(ref modelSettingsAuto, ModelSettingsKey, false); }
        set { Set(ref modelSettingsAuto, ModelSettingsKey, value); }
    }

    public static bool ModelPostProcessAuto
    {
        get { return Get(ref modelPostProcessAuto, ModelPostProcessKey, false); }
        set { Set(ref modelPostProcessAuto, ModelPostProcessKey, value); }
    }

    /// <summary>总面板「执行全部」是否跑贴图批量。默认 true；不影响分项按钮。</summary>
    public static bool MasterBatchIncludeTexture
    {
        get { return Get(ref masterBatchIncludeTexture, MasterBatchIncludeTextureKey, true); }
        set { Set(ref masterBatchIncludeTexture, MasterBatchIncludeTextureKey, value); }
    }

    /// <summary>总面板「执行全部」是否跑模型批量。默认 true；不影响分项按钮。</summary>
    public static bool MasterBatchIncludeModel
    {
        get { return Get(ref masterBatchIncludeModel, MasterBatchIncludeModelKey, true); }
        set { Set(ref masterBatchIncludeModel, MasterBatchIncludeModelKey, value); }
    }

    /// <summary>总面板「执行全部」是否跑材质批量（交付 Shader 规范化）。默认 true。</summary>
    public static bool MasterBatchIncludeMaterial
    {
        get { return Get(ref masterBatchIncludeMaterial, MasterBatchIncludeMaterialKey, true); }
        set { Set(ref masterBatchIncludeMaterial, MasterBatchIncludeMaterialKey, value); }
    }

    public static bool IsTextureSettingsEffective
    {
        get { return MasterEnabled && TextureSettingsAuto; }
    }

    public static bool IsTexturePostProcessEffective
    {
        get { return MasterEnabled && TexturePostProcessAuto; }
    }

    public static bool IsModelSettingsEffective
    {
        get { return MasterEnabled && ModelSettingsAuto; }
    }

    public static bool IsModelPostProcessEffective
    {
        get { return MasterEnabled && ModelPostProcessAuto; }
    }

    private static bool Get(ref bool? cache, string key, bool defaultValue)
    {
        EnsureMigratedFromLegacy();
        if (!cache.HasValue)
        {
            cache = EditorPrefs.GetBool(key, defaultValue);
        }

        return cache.Value;
    }

    private static void Set(ref bool? cache, string key, bool value)
    {
        EnsureMigratedFromLegacy();
        if (cache.HasValue && cache.Value == value)
        {
            return;
        }

        cache = value;
        EditorPrefs.SetBool(key, value);
    }

    private static void EnsureMigratedFromLegacy()
    {
        if (EditorPrefs.GetBool(MigratedKey, false))
        {
            return;
        }

        bool legacyOn = EditorPrefs.GetBool(LegacyEnabledKey, false);
        if (legacyOn)
        {
            EditorPrefs.SetBool(MasterEnabledKey, true);
            EditorPrefs.SetBool(TextureSettingsKey, true);
            EditorPrefs.SetBool(TexturePostProcessKey, true);
            EditorPrefs.SetBool(ModelSettingsKey, true);
            EditorPrefs.SetBool(ModelPostProcessKey, true);
        }

        EditorPrefs.SetBool(MigratedKey, true);
    }
}
