using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 批量 FBX 导入：路径命名、冲突检测、单 FBX 拷贝+Import。
// 不建 Prefab、不平铺、不导出；交付文件名仍以人工 Prefab 名为准。
// 同基名多 FBX：夹名追加无扩展文件名并 Warning，允许导入；目标已存在/交付区仍 Conflict。
// =====================================================================================
public static class BatchFbxImportService
{
    public enum ItemStatus
    {
        Ready,
        Warning,
        Conflict,
        Skipped,
        Success,
        Failed
    }

    public sealed class ImportItem
    {
        public string SourceFbxPath;
        public string FolderName;
        public string TargetFolderAssetPath;
        public string TargetFbxAssetPath;
        public ItemStatus Status;
        public string Message;
        public bool UsedFullPathFallback;
        /// <summary>同基名多 FBX 时已追加无扩展文件名做夹名消歧。</summary>
        public bool UsedFbxNameDisambiguation;
    }

    public sealed class BatchResult
    {
        public int Success;
        public int Skipped;
        public int Failed;
        public int CancelledRemaining;
        public string SummaryMessage;
    }

    private static readonly char[] InvalidFolderChars =
    {
        '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\0'
    };

    /// <summary>
    /// 夹名 = 文件自身向上连续 3 层目录名，用 "_" 拼接（不是只取最外那一层）。
    /// 例：…/飞机模型待处理/【m2222】歼15-yy3d/fbx/a.FBX → 飞机模型待处理_【m2222】歼15-yy3d_fbx
    /// 例：…/飞机模型待处理/【m2222】歼15-yy3d/3d/fbx/a.FBX → 【m2222】歼15-yy3d_3d_fbx
    /// 不足 3 层则用整段源路径消毒名，并标 Warning。
    /// </summary>
    public static string ResolveFolderName(string sourceFbxPath, out bool usedFullPathFallback, out string warning)
    {
        usedFullPathFallback = false;
        warning = null;

        if (string.IsNullOrEmpty(sourceFbxPath))
        {
            usedFullPathFallback = true;
            warning = "源路径为空，无法解析夹名。";
            return "unnamed_fbx";
        }

        string full = Path.GetFullPath(sourceFbxPath).Replace("\\", "/");
        string dir = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(dir))
        {
            usedFullPathFallback = true;
            warning = "路径不足 3 层，已用全路径消毒名。";
            return SanitizeFolderName(full);
        }

        // 上 1 = 文件所在目录；上 2 / 上 3 再往上。夹名 = 上3名_上2名_上1名。
        string up1 = dir.Replace("\\", "/").TrimEnd('/');
        string up2 = Path.GetDirectoryName(up1);
        if (string.IsNullOrEmpty(up2))
        {
            usedFullPathFallback = true;
            warning = "路径不足 3 层，已用全路径消毒名。";
            return SanitizeFolderName(full);
        }

        up2 = up2.Replace("\\", "/").TrimEnd('/');
        string up3 = Path.GetDirectoryName(up2);
        if (string.IsNullOrEmpty(up3))
        {
            usedFullPathFallback = true;
            warning = "路径不足 3 层，已用全路径消毒名。";
            return SanitizeFolderName(full);
        }

        up3 = up3.Replace("\\", "/").TrimEnd('/');
        string seg3 = Path.GetFileName(up3);
        string seg2 = Path.GetFileName(up2);
        string seg1 = Path.GetFileName(up1);
        if (string.IsNullOrWhiteSpace(seg3) ||
            string.IsNullOrWhiteSpace(seg2) ||
            string.IsNullOrWhiteSpace(seg1))
        {
            usedFullPathFallback = true;
            warning = "向上三层目录名不完整，已用全路径消毒名。";
            return SanitizeFolderName(full);
        }

        // 斜杠位用下划线：上3/上2/上1 → 上3_上2_上1
        return SanitizeFolderName(seg3 + "_" + seg2 + "_" + seg1);
    }

    public static string SanitizeFolderName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unnamed_fbx";
        }

        string s = raw.Trim().Replace("\\", "_").Replace("/", "_");
        foreach (char c in InvalidFolderChars)
        {
            s = s.Replace(c, '_');
        }

        while (s.Contains("__"))
        {
            s = s.Replace("__", "_");
        }

        s = s.Trim('_', '.', ' ');
        if (string.IsNullOrEmpty(s))
        {
            s = "unnamed_fbx";
        }

        // 避免超长路径在 Windows 上炸；保留可读前缀。
        const int maxLen = 80;
        if (s.Length > maxLen)
        {
            string hash = Mathf.Abs(s.GetHashCode()).ToString("x8");
            s = s.Substring(0, maxLen - 9) + "_" + hash;
        }

        return s;
    }

    public static List<ImportItem> CollectFromDroppedPaths(
        IEnumerable<string> droppedPaths,
        BatchFbxImportSettings settings)
    {
        var fbxFiles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (droppedPaths != null)
        {
            foreach (string raw in droppedPaths)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    continue;
                }

                string path = Path.GetFullPath(raw);
                if (Directory.Exists(path))
                {
                    CollectFbxUnderDirectory(path, fbxFiles, seen);
                }
                else if (File.Exists(path) && IsFbxFile(path) && seen.Add(path))
                {
                    fbxFiles.Add(path);
                }
            }
        }

        fbxFiles.Sort(StringComparer.OrdinalIgnoreCase);
        return BuildItems(fbxFiles, settings);
    }

    public static List<ImportItem> RebuildItems(IList<ImportItem> existing, BatchFbxImportSettings settings)
    {
        var paths = new List<string>();
        if (existing != null)
        {
            foreach (ImportItem item in existing)
            {
                if (item != null && !string.IsNullOrEmpty(item.SourceFbxPath))
                {
                    paths.Add(item.SourceFbxPath);
                }
            }
        }

        return BuildItems(paths, settings);
    }

    public static bool HasBlockingAlerts(IList<ImportItem> items, BatchFbxImportSettings settings, out string reason)
    {
        if (settings == null)
        {
            reason = "缺少配置。";
            return true;
        }

        if (!settings.TryValidateImportRoot(out string rootError))
        {
            reason = rootError;
            return true;
        }

        if (items == null || items.Count == 0)
        {
            reason = "列表为空。";
            return true;
        }

        foreach (ImportItem item in items)
        {
            if (item != null && item.Status == ItemStatus.Conflict)
            {
                reason = "存在 Conflict 项（目标已存在 / 交付区 / 消歧后仍重名），请先处理列表。";
                return true;
            }
        }

        reason = null;
        return false;
    }

    /// <summary>
    /// 串行执行：每条 FBX 为最小单位（建夹→拷贝→Import）。
    /// 取消协作式：当前条整段做完后再停，不开始下一条。
    /// </summary>
    public static BatchResult ExecuteBatch(IList<ImportItem> items, BatchFbxImportSettings settings)
    {
        var result = new BatchResult();
        if (HasBlockingAlerts(items, settings, out string blockReason))
        {
            result.SummaryMessage = "未执行：" + blockReason;
            return result;
        }

        bool cancelRequested = false;
        int total = items.Count;

        try
        {
            for (int i = 0; i < total; i++)
            {
                if (cancelRequested)
                {
                    result.CancelledRemaining = total - i;
                    for (int j = i; j < total; j++)
                    {
                        items[j].Status = ItemStatus.Skipped;
                        items[j].Message = "已取消，未开始本条。";
                    }

                    break;
                }

                ImportItem item = items[i];
                float progress = total <= 1 ? 0f : (float)i / total;
                if (EditorUtility.DisplayCancelableProgressBar(
                        "批量 FBX 导入",
                        (i + 1) + "/" + total + "  " + item.FolderName,
                        progress))
                {
                    cancelRequested = true;
                }

                // 面板已拦冲突；执行期再跳过一次以防状态漂移。
                if (item.Status == ItemStatus.Conflict)
                {
                    item.Status = ItemStatus.Skipped;
                    item.Message = "重名冲突，已跳过。";
                    result.Skipped++;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "批量 FBX 导入",
                            (i + 1) + "/" + total + " 跳过冲突",
                            (float)(i + 1) / total))
                    {
                        cancelRequested = true;
                    }

                    continue;
                }

                try
                {
                    ImportOne(item, settings);
                    if (item.Status == ItemStatus.Success)
                    {
                        result.Success++;
                    }
                    else if (item.Status == ItemStatus.Skipped)
                    {
                        result.Skipped++;
                    }
                    else
                    {
                        result.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    item.Status = ItemStatus.Failed;
                    item.Message = ex.Message;
                    result.Failed++;
                    Debug.LogException(ex);
                }

                if (EditorUtility.DisplayCancelableProgressBar(
                        "批量 FBX 导入",
                        (i + 1) + "/" + total + " 完成 " + item.FolderName,
                        (float)(i + 1) / total))
                {
                    cancelRequested = true;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        var sb = new StringBuilder();
        sb.Append("完成：成功 ").Append(result.Success)
            .Append("，跳过 ").Append(result.Skipped)
            .Append("，失败 ").Append(result.Failed);
        if (result.CancelledRemaining > 0)
        {
            sb.Append("；取消后未开始 ").Append(result.CancelledRemaining);
        }

        result.SummaryMessage = sb.ToString();
        return result;
    }

    public static void ImportOne(ImportItem item, BatchFbxImportSettings settings)
    {
        if (item == null || settings == null)
        {
            throw new ArgumentNullException(item == null ? "item" : "settings");
        }

        if (!File.Exists(item.SourceFbxPath))
        {
            item.Status = ItemStatus.Failed;
            item.Message = "源 FBX 不存在。";
            return;
        }

        if (settings.IsDeliveryAlertPath(item.TargetFolderAssetPath) ||
            settings.IsDeliveryAlertPath(item.TargetFolderAssetPath + "/"))
        {
            item.Status = ItemStatus.Skipped;
            item.Message = "目标落在交付区警报路径，已跳过。";
            return;
        }

        if (AssetDatabase.IsValidFolder(item.TargetFolderAssetPath) ||
            Directory.Exists(AssetPathUtility.ToFullPath(item.TargetFolderAssetPath)))
        {
            item.Status = ItemStatus.Skipped;
            item.Message = "目标文件夹已存在，已跳过。";
            Debug.LogWarning("[BatchFbxImport] 跳过已存在夹: " + item.TargetFolderAssetPath);
            return;
        }

        EnsureAssetFolder(settings.NormalizedImportRoot);
        EnsureAssetFolder(item.TargetFolderAssetPath);

        string destFull = AssetPathUtility.ToFullPath(item.TargetFbxAssetPath);
        if (string.IsNullOrEmpty(destFull))
        {
            item.Status = ItemStatus.Failed;
            item.Message = "无法解析目标磁盘路径。";
            return;
        }

        string destDir = Path.GetDirectoryName(destFull);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        File.Copy(item.SourceFbxPath, destFull, false);
        AssetDatabase.ImportAsset(item.TargetFbxAssetPath, ImportAssetOptions.ForceUpdate);
        item.Status = ItemStatus.Success;
        item.Message = "已导入。";
    }

    private static List<ImportItem> BuildItems(IList<string> fbxFiles, BatchFbxImportSettings settings)
    {
        var items = new List<ImportItem>();
        if (fbxFiles == null || settings == null)
        {
            return items;
        }

        string root = settings.NormalizedImportRoot;
        var baseNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (string fbx in fbxFiles)
        {
            bool fallback;
            string warning;
            string folderName = ResolveFolderName(fbx, out fallback, out warning);
            string targetFolder = root + "/" + folderName;
            string fileName = Path.GetFileName(fbx);

            var item = new ImportItem
            {
                SourceFbxPath = fbx,
                FolderName = folderName,
                TargetFolderAssetPath = targetFolder,
                TargetFbxAssetPath = targetFolder + "/" + fileName,
                UsedFullPathFallback = fallback,
                UsedFbxNameDisambiguation = false,
                Status = ItemStatus.Ready,
                Message = warning
            };

            if (baseNameCounts.ContainsKey(folderName))
            {
                baseNameCounts[folderName]++;
            }
            else
            {
                baseNameCounts[folderName] = 1;
            }

            items.Add(item);
        }

        // 同基名（典型：同文件夹多 FBX）→ 夹名追加无扩展文件名，降为 Warning，允许导入。
        foreach (ImportItem item in items)
        {
            if (!baseNameCounts.TryGetValue(item.FolderName, out int baseCount) || baseCount <= 1)
            {
                continue;
            }

            string stem = Path.GetFileNameWithoutExtension(item.SourceFbxPath);
            string disambiguated = SanitizeFolderName(item.FolderName + "_" + stem);
            item.FolderName = disambiguated;
            item.TargetFolderAssetPath = root + "/" + disambiguated;
            item.TargetFbxAssetPath = item.TargetFolderAssetPath + "/" + Path.GetFileName(item.SourceFbxPath);
            item.UsedFbxNameDisambiguation = true;
        }

        var finalNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (ImportItem item in items)
        {
            if (finalNameCounts.ContainsKey(item.FolderName))
            {
                finalNameCounts[item.FolderName]++;
            }
            else
            {
                finalNameCounts[item.FolderName] = 1;
            }
        }

        foreach (ImportItem item in items)
        {
            var problems = new List<string>();

            if (finalNameCounts.TryGetValue(item.FolderName, out int count) && count > 1)
            {
                problems.Add("列表内夹名重名（追加文件名后仍冲突）");
            }

            if (AssetDatabase.IsValidFolder(item.TargetFolderAssetPath) ||
                Directory.Exists(AssetPathUtility.ToFullPath(item.TargetFolderAssetPath)))
            {
                problems.Add("目标文件夹已存在");
            }

            if (settings.IsDeliveryAlertPath(item.TargetFolderAssetPath) ||
                settings.IsDeliveryAlertPath(item.TargetFolderAssetPath + "/"))
            {
                problems.Add("目标落在交付区警报路径");
            }

            if (problems.Count > 0)
            {
                item.Status = ItemStatus.Conflict;
                string conflictMsg = string.Join("；", problems);
                item.Message = string.IsNullOrEmpty(item.Message)
                    ? conflictMsg
                    : item.Message + " | " + conflictMsg;
                continue;
            }

            var warnParts = new List<string>();
            if (item.UsedFullPathFallback)
            {
                warnParts.Add(string.IsNullOrEmpty(item.Message)
                    ? "路径不足 3 层，已用全路径消毒名。"
                    : item.Message);
            }

            if (item.UsedFbxNameDisambiguation)
            {
                warnParts.Add("同夹多 FBX，已追加文件名消歧：" + item.FolderName);
            }

            if (warnParts.Count > 0)
            {
                item.Status = ItemStatus.Warning;
                item.Message = string.Join(" | ", warnParts);
            }
            else
            {
                item.Status = ItemStatus.Ready;
                item.Message = "就绪";
            }
        }

        return items;
    }

    private static void CollectFbxUnderDirectory(string directory, List<string> into, HashSet<string> seen)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BatchFbxImport] 无法扫描目录: " + directory + " — " + ex.Message);
            return;
        }

        foreach (string file in files)
        {
            if (!IsFbxFile(file))
            {
                continue;
            }

            string full = Path.GetFullPath(file);
            if (seen.Add(full))
            {
                into.Add(full);
            }
        }
    }

    private static bool IsFbxFile(string path)
    {
        return Path.GetExtension(path).Equals(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string normalized = folderPath.Replace("\\", "/").TrimEnd('/');
        string[] parts = normalized.Split('/');
        if (parts.Length == 0 || !parts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("只能在 Assets/ 下创建文件夹: " + folderPath);
        }

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
