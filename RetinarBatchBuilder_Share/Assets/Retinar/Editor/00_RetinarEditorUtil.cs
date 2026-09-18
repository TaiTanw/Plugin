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

    /// <summary>平台写入文件名的后缀（小写），用于 <c>name_android.assetbundle</c>。</summary>
    public static string ToPlatformFileSuffix(BuildTarget target)
    {
        if (target == BuildTarget.iOS)
        {
            return "ios";
        }

        if (target == BuildTarget.Android)
        {
            return "android";
        }

        return target.ToString().ToLowerInvariant();
    }

    /// <summary>交付文件名：<c>name_android.assetbundle</c> / <c>name_ios.assetbundle</c>。</summary>
    public static string BuildBundleFileName(string assetName, BuildTarget target)
    {
        string stem = MakeSafeName(assetName).ToLowerInvariant();
        return stem + "_" + ToPlatformFileSuffix(target) + "." + RetinarPaths.AssetBundleVariant;
    }

    /// <summary>Unity BuildAssetBundles 在输出目录里用的主文件名（无平台后缀）。</summary>
    public static string BuildUnityBundleFileName(string assetName)
    {
        return MakeSafeName(assetName).ToLowerInvariant() + "." + RetinarPaths.AssetBundleVariant;
    }
}
