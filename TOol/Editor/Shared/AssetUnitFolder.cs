using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Shared — 只删「允许父夹下恰好一段」的单元夹。② Incoming/<三层>/、④ Art/<名>/ 共用。
// 禁止扫整棵 Incoming / Art。不是策略模块。
// =====================================================================================

/// <summary>
/// 夹级清空再写。路径必须是 <c>allowedParent/单段名</c>，拒绝父夹本身、更深路径、<c>..</c>。
/// </summary>
public static class AssetUnitFolder
{
    /// <summary>
    /// 删除 <paramref name="childFolderAssetPath"/>。夹不存在视为成功。
    /// </summary>
    public static bool TryDeleteImmediateChildFolder(string allowedParent, string childFolderAssetPath)
    {
        if (string.IsNullOrEmpty(allowedParent) || string.IsNullOrEmpty(childFolderAssetPath))
        {
            Debug.LogWarning("[AssetUnitFolder] 父夹或目标夹为空，拒绝删除");
            return false;
        }

        string parent = NormalizeFolder(allowedParent);
        string child = NormalizeFolder(childFolderAssetPath);
        if (!IsImmediateChild(parent, child))
        {
            Debug.LogWarning(
                "[AssetUnitFolder] 拒绝删夹（须为 parent/单段）: " + childFolderAssetPath +
                " 不在 " + allowedParent + " 下");
            return false;
        }

        string full = AssetPathUtility.ToFullPath(child);
        bool unityFolder = AssetDatabase.IsValidFolder(child);
        bool diskFolder = !string.IsNullOrEmpty(full) && Directory.Exists(full);
        if (!unityFolder && !diskFolder)
        {
            return true;
        }

        if (unityFolder)
        {
            if (AssetDatabase.DeleteAsset(child))
            {
                AssetDatabase.Refresh();
                return true;
            }

            Debug.LogWarning("[AssetUnitFolder] DeleteAsset 失败，尝试磁盘删除: " + child);
        }

        if (string.IsNullOrEmpty(full) || !Directory.Exists(full))
        {
            AssetDatabase.Refresh();
            return !AssetDatabase.IsValidFolder(child);
        }

        try
        {
            if (!FileUtil.DeleteFileOrDirectory(full))
            {
                Debug.LogWarning("[AssetUnitFolder] FileUtil 删除失败: " + full);
                return false;
            }

            string meta = full + ".meta";
            if (File.Exists(meta))
            {
                FileUtil.DeleteFileOrDirectory(meta);
            }

            AssetDatabase.Refresh();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AssetUnitFolder] 删除异常: " + child + " " + ex.Message);
            return false;
        }
    }

    static bool IsImmediateChild(string parent, string child)
    {
        if (child.Equals(parent, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string prefix = parent + "/";
        if (!child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = child.Substring(prefix.Length);
        if (string.IsNullOrEmpty(rest) || rest.IndexOf('/') >= 0)
        {
            return false;
        }

        if (rest == "." || rest == ".." || rest.IndexOf("..", StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        return true;
    }

    static string NormalizeFolder(string path)
    {
        string p = path.Replace("\\", "/").Trim();
        while (p.EndsWith("/", StringComparison.Ordinal) && p.Length > 1)
        {
            p = p.Substring(0, p.Length - 1);
        }

        return p;
    }
}
