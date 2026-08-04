using System.Collections.Generic;

// =====================================================================================
// 职责边界：
//   Shared 层。统一实现"路径是否落在不介入目录"的判断，避免贴图/模型两套 Settings
//   各写一份 StartsWith 逻辑后来漂移。
//   排除列表本身仍存在各自的 Settings 资产里（团队可改），本类只做判断。
// =====================================================================================
public static class ResourceExcludeUtility
{
    public static bool IsExcludedPath(string assetPath, IList<string> excludedPathPrefixes)
    {
        if (string.IsNullOrEmpty(assetPath) || excludedPathPrefixes == null)
        {
            return false;
        }

        string normalized = assetPath.Replace("\\", "/");
        foreach (string prefix in excludedPathPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) &&
                normalized.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
