using System;
using System.Collections.Generic;

// =====================================================================================
// 平铺分类运行快照。来源是 FlattenOperationSettings SO，不再读取/写入 EditorPrefs。
// =====================================================================================

/// <summary>一次运行内固定的大类开关与后缀。</summary>
public sealed class FlattenCategorySettings
{
    private readonly HashSet<string> disabledIds;
    private readonly Dictionary<string, string[]> suffixOverrides;

    private FlattenCategorySettings(
        HashSet<string> disabledIds,
        Dictionary<string, string[]> suffixOverrides)
    {
        this.disabledIds = disabledIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        this.suffixOverrides = suffixOverrides ??
                               new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    public static FlattenCategorySettings CreateDefaults()
    {
        return new FlattenCategorySettings(null, null);
    }

    public static FlattenCategorySettings Load()
    {
        FlattenOperationSettings settings = FlattenOperationSettings.LoadManualOrDefaults();
        return settings == null ? CreateDefaults() : FromOperationSettings(settings);
    }

    public static FlattenCategorySettings FromOperationSettings(FlattenOperationSettings settings)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suffixes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (settings != null)
        {
            foreach (FlattenCategoryRuleSetting rule in settings.CategoryRules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.processorId))
                {
                    continue;
                }

                string id = rule.processorId.Trim();
                if (!rule.enabled && !string.Equals(
                        id, UnknownFlattenProcessor.ProcessorId, StringComparison.OrdinalIgnoreCase))
                {
                    disabled.Add(id);
                }

                string[] parsed = ParseSuffixes(rule.suffixes);
                if (parsed.Length > 0)
                {
                    suffixes[id] = parsed;
                }
            }
        }

        return new FlattenCategorySettings(disabled, suffixes);
    }

    public bool IsEnabled(string processorId)
    {
        if (string.Equals(processorId, UnknownFlattenProcessor.ProcessorId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !disabledIds.Contains(processorId);
    }

    public string[] GetSuffixes(string processorId, string[] defaults)
    {
        string[] configured;
        if (suffixOverrides.TryGetValue(processorId, out configured))
        {
            return configured;
        }

        return defaults ?? new string[0];
    }

    public string GetSuffixesText(string processorId, string[] defaults)
    {
        return string.Join(",", GetSuffixes(processorId, defaults));
    }

    public static bool MatchesSuffix(string assetPath, string[] suffixes)
    {
        if (string.IsNullOrEmpty(assetPath) || suffixes == null || suffixes.Length == 0)
        {
            return false;
        }

        string extension = System.IO.Path.GetExtension(assetPath).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        for (int i = 0; i < suffixes.Length; i++)
        {
            string token = suffixes[i];
            if (!string.IsNullOrEmpty(token) &&
                token.Trim().TrimStart('.').ToLowerInvariant() == extension)
            {
                return true;
            }
        }

        return false;
    }

    public static string[] ParseSuffixes(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return new string[0];
        }

        string[] parts = raw.Split(
            new[] { ',', ';', ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim().TrimStart('.');
            if (token.Length > 0 && seen.Add(token))
            {
                result.Add(token);
            }
        }

        return result.ToArray();
    }
}
