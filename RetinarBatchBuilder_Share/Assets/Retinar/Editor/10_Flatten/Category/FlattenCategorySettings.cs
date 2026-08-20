using System;
using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// 平铺分类本机设置：勾选 + 后缀覆盖。输出路径不进 Prefs。
// =====================================================================================

/// <summary>平铺分类面板的本机勾选与后缀。</summary>
public sealed class FlattenCategorySettings
{
    private const string DisabledIdsKey = "Retinar.Flatten.DisabledIds";
    private const string SuffixKeyPrefix = "Retinar.Flatten.Suffix.";

    private readonly HashSet<string> disabledIds;
    private readonly Dictionary<string, string[]> suffixOverrides;

    public FlattenCategorySettings()
    {
        disabledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        suffixOverrides = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        string raw = EditorPrefs.GetString(DisabledIdsKey, string.Empty);
        foreach (string token in SplitList(raw))
        {
            disabledIds.Add(token);
        }
    }

    public static FlattenCategorySettings Load()
    {
        return new FlattenCategorySettings();
    }

    public bool IsEnabled(string processorId)
    {
        if (string.Equals(processorId, UnknownFlattenProcessor.ProcessorId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !disabledIds.Contains(processorId);
    }

    public void SetEnabled(string processorId, bool enabled)
    {
        if (string.Equals(processorId, UnknownFlattenProcessor.ProcessorId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (enabled)
        {
            disabledIds.Remove(processorId);
        }
        else
        {
            disabledIds.Add(processorId);
        }

        EditorPrefs.SetString(DisabledIdsKey, string.Join(",", new List<string>(disabledIds).ToArray()));
    }

    public string[] GetSuffixes(string processorId, string[] defaults)
    {
        string[] cached;
        if (suffixOverrides.TryGetValue(processorId, out cached))
        {
            return cached;
        }

        string key = SuffixKeyPrefix + processorId;
        if (!EditorPrefs.HasKey(key))
        {
            suffixOverrides[processorId] = defaults ?? new string[0];
            return suffixOverrides[processorId];
        }

        string[] parsed = SplitList(EditorPrefs.GetString(key, string.Empty));
        if (parsed.Length == 0)
        {
            parsed = defaults ?? new string[0];
        }

        suffixOverrides[processorId] = parsed;
        return parsed;
    }

    public string GetSuffixesText(string processorId, string[] defaults)
    {
        return string.Join(",", GetSuffixes(processorId, defaults));
    }

    public void SetSuffixesText(string processorId, string text)
    {
        string[] parsed = SplitList(text);
        suffixOverrides[processorId] = parsed;
        EditorPrefs.SetString(SuffixKeyPrefix + processorId, string.Join(",", parsed));
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
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            if (token.Trim().TrimStart('.').ToLowerInvariant() == extension)
            {
                return true;
            }
        }

        return false;
    }

    private static string[] SplitList(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return new string[0];
        }

        string[] parts = raw.Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim().TrimStart('.');
            if (token.Length > 0 && !result.Contains(token))
            {
                result.Add(token);
            }
        }

        return result.ToArray();
    }
}
