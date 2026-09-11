using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// ④ 平铺操作配置：手动与管线使用同一数据类、不同 SO 实例。
// 资产所在目录决定所有权；执行前冻结成 FlattenOperationPolicy，内核不再读 EditorPrefs。
// =====================================================================================

public enum FlattenSettingsScope
{
    Unclassified = 0,
    Manual = 1,
    Pipeline = 2
}

[Serializable]
public sealed class FlattenCategoryRuleSetting
{
    public string processorId;
    public bool enabled = true;
    public string suffixes = string.Empty;
}

/// <summary>一次平铺运行使用的配置快照。</summary>
public sealed class FlattenOperationPolicy
{
    public readonly string SourceAssetPath;
    public readonly FlattenSettingsScope SourceScope;
    public readonly bool ClearDestinationArtFolder;
    public readonly bool AddBoxCollider;
    public readonly FlattenCategorySettings Categories;

    public FlattenOperationPolicy(
        string sourceAssetPath,
        FlattenSettingsScope sourceScope,
        bool clearDestinationArtFolder,
        bool addBoxCollider,
        FlattenCategorySettings categories)
    {
        SourceAssetPath = sourceAssetPath ?? string.Empty;
        SourceScope = sourceScope;
        ClearDestinationArtFolder = clearDestinationArtFolder;
        AddBoxCollider = addBoxCollider;
        Categories = categories ?? FlattenCategorySettings.CreateDefaults();
    }

    public static FlattenOperationPolicy CreateDefaults(FlattenSettingsScope scope)
    {
        return new FlattenOperationPolicy(
            string.Empty,
            scope,
            scope == FlattenSettingsScope.Pipeline,
            false,
            FlattenCategorySettings.CreateDefaults());
    }

    public string ToLogString()
    {
        var categoryParts = new List<string>();
        IList<IFlattenCategoryProcessor> processors = FlattenCategoryRegistry.All;
        for (int i = 0; i < processors.Count; i++)
        {
            IFlattenCategoryProcessor processor = processors[i];
            if (processor.Id == UnknownFlattenProcessor.ProcessorId)
            {
                continue;
            }

            categoryParts.Add(
                processor.Id + "=" + (Categories.IsEnabled(processor.Id) ? "on" : "off") +
                "[" + Categories.GetSuffixesText(processor.Id, processor.DefaultSuffixes) + "]");
        }

        return "scope=" + SourceScope +
               " source=" + (string.IsNullOrEmpty(SourceAssetPath) ? "<defaults>" : SourceAssetPath) +
               " clearArt=" + ClearDestinationArtFolder +
               " addCollider=" + AddBoxCollider +
               " categories=" + string.Join(";", categoryParts.ToArray());
    }
}

[CreateAssetMenu(fileName = "FlattenOperationSettings", menuName = "Retinar/平铺操作配置")]
public sealed class FlattenOperationSettings : ScriptableObject
{
    public const string ManualConfigRoot = "Assets/Plugin/TOol/ConfigData/Manual";
    public const string PipelineConfigRoot = "Assets/Plugin/Pipeline/ConfigData";
    public const string DefaultManualAssetPath =
        ManualConfigRoot + "/FlattenOperationSettings.asset";
    public const string DefaultPipelineAssetPath =
        PipelineConfigRoot + "/FlattenOperationSettings.asset";

    [SerializeField]
    [Tooltip("执行前清空本次 Assets/Art/<单元>/。管线默认开，人工默认关。")]
    private bool clearDestinationArtFolder;

    [SerializeField]
    [Tooltip("在最终 Prefab 根节点添加或更新 BoxCollider。默认关。")]
    private bool addBoxCollider;

    [SerializeField]
    private List<FlattenCategoryRuleSetting> categoryRules = new List<FlattenCategoryRuleSetting>();

    public bool ClearDestinationArtFolder
    {
        get { return clearDestinationArtFolder; }
        set { clearDestinationArtFolder = value; }
    }

    public bool AddBoxCollider
    {
        get { return addBoxCollider; }
        set { addBoxCollider = value; }
    }

    public FlattenOperationPolicy CreatePolicy()
    {
        string path = AssetDatabase.GetAssetPath(this).Replace("\\", "/");
        return new FlattenOperationPolicy(
            path,
            GetScope(this),
            clearDestinationArtFolder,
            addBoxCollider,
            FlattenCategorySettings.FromOperationSettings(this));
    }

    public bool IsCategoryEnabled(string processorId, bool defaultValue = true)
    {
        FlattenCategoryRuleSetting rule = FindRule(processorId);
        return rule == null ? defaultValue : rule.enabled;
    }

    public string GetCategorySuffixes(string processorId, string[] defaults)
    {
        FlattenCategoryRuleSetting rule = FindRule(processorId);
        if (rule == null || string.IsNullOrWhiteSpace(rule.suffixes))
        {
            return string.Join(",", defaults ?? new string[0]);
        }

        return string.Join(",", FlattenCategorySettings.ParseSuffixes(rule.suffixes));
    }

    public void SetCategory(string processorId, bool enabled, string suffixes)
    {
        FlattenCategoryRuleSetting rule = FindRule(processorId);
        if (rule == null)
        {
            rule = new FlattenCategoryRuleSetting { processorId = processorId };
            categoryRules.Add(rule);
        }

        rule.enabled = enabled;
        rule.suffixes = string.Join(",", FlattenCategorySettings.ParseSuffixes(suffixes));
    }

    internal IEnumerable<FlattenCategoryRuleSetting> CategoryRules
    {
        get { return categoryRules ?? new List<FlattenCategoryRuleSetting>(); }
    }

    public static FlattenSettingsScope GetScope(FlattenOperationSettings settings)
    {
        if (settings == null)
        {
            return FlattenSettingsScope.Unclassified;
        }

        string path = AssetDatabase.GetAssetPath(settings).Replace("\\", "/");
        if (IsPathInside(path, ManualConfigRoot))
        {
            return FlattenSettingsScope.Manual;
        }

        if (IsPathInside(path, PipelineConfigRoot))
        {
            return FlattenSettingsScope.Pipeline;
        }

        return FlattenSettingsScope.Unclassified;
    }

    public static FlattenOperationSettings LoadManualOrDefaults()
    {
        return AssetDatabase.LoadAssetAtPath<FlattenOperationSettings>(DefaultManualAssetPath);
    }

    public static FlattenOperationSettings LoadPipelineOrDefaults()
    {
        return AssetDatabase.LoadAssetAtPath<FlattenOperationSettings>(DefaultPipelineAssetPath);
    }

    public static FlattenOperationSettings GetOrCreateManualAsset()
    {
        return GetOrCreateAsset(DefaultManualAssetPath, false);
    }

    public static FlattenOperationSettings GetOrCreatePipelineAsset()
    {
        return GetOrCreateAsset(DefaultPipelineAssetPath, true);
    }

    private FlattenCategoryRuleSetting FindRule(string processorId)
    {
        if (categoryRules == null)
        {
            categoryRules = new List<FlattenCategoryRuleSetting>();
        }

        for (int i = 0; i < categoryRules.Count; i++)
        {
            FlattenCategoryRuleSetting rule = categoryRules[i];
            if (rule != null && string.Equals(
                    rule.processorId, processorId, StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }

        return null;
    }

    private static FlattenOperationSettings GetOrCreateAsset(string assetPath, bool pipelineDefaults)
    {
        FlattenOperationSettings found = AssetDatabase.LoadAssetAtPath<FlattenOperationSettings>(assetPath);
        if (found != null)
        {
            return found;
        }

        string folder = Path.GetDirectoryName(assetPath).Replace("\\", "/");
        EnsureAssetFolder(folder);
        var created = CreateInstance<FlattenOperationSettings>();
        created.clearDestinationArtFolder = pipelineDefaults;
        AssetDatabase.CreateAsset(created, assetPath);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static bool IsPathInside(string assetPath, string root)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string normalized = assetPath.Replace("\\", "/").TrimEnd('/');
        string normalizedRoot = root.Replace("\\", "/").TrimEnd('/');
        return normalized.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Split('/');
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
