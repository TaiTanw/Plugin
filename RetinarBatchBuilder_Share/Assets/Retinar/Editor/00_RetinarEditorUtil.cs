using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 00 — 编辑器共用小工具（弹窗时序、磁盘目录、安全文件名）
// =====================================================================================

/// <summary>
/// 跨「平铺 / 批量导出 / 成品直通」共用的编辑器工具，避免各调度器复制粘贴。
/// </summary>
public static class RetinarEditorUtil
{
    /// <summary>
    /// 推迟到下一编辑器 tick 再弹单按钮对话框，避免打断 Inspector 3D 预览
    /// （PreviewRenderUtility Begin/End 未配对会抛 InvalidOperationException）。
    /// </summary>
    public static void ShowDialogDeferred(string title, string message, string ok)
    {
        EditorApplication.delayCall += () => EditorUtility.DisplayDialog(title, message, ok);
    }

    /// <summary>Play Mode 下拒绝开跑；返回 true 表示应中止当前菜单。</summary>
    public static bool StopIfEditorIsPlaying()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return false;
        }

        EditorApplication.isPlaying = false;
        ShowDialogDeferred(
            "Retinar",
            "请先退出 Play Mode，再执行 Retinar 菜单。\n半成品打包已被禁止。",
            "OK");
        return true;
    }
    /// <summary>
    /// 特殊字符替换为下划线
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string MakeSafeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "asset";
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Replace(' ', '_');
    }
    /// <summary>
    /// 确保文件存在
    /// </summary>
    /// <param name="path"></param>
    public static void EnsureDiskDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public static string GetDeliverablesAbsolutePath()
    {
        return Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), RetinarPaths.DeliverableRoot));
    }

    public static void OpenDeliverablesFolder()
    {
        string path = GetDeliverablesAbsolutePath();
        EnsureDiskDirectory(path);
        EditorUtility.RevealInFinder(path);
    }
    /// <summary>
    /// 目标平台文件夹名返回
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public static string ToPlatformFolder(BuildTarget target)
    {
        if (target == BuildTarget.iOS)
        {
            return RetinarPaths.PlatformIOS;
        }

        if (target == BuildTarget.Android)
        {
            return RetinarPaths.PlatformAndroid;
        }

        return target.ToString();
    }

    /// <summary>历史交付文件名：小写资产名 + "." + variant。</summary>
    public static string BuildBundleFileName(string assetName)
    {
        return assetName.ToLowerInvariant() + "." + RetinarPaths.AssetBundleVariant;
    }
}
