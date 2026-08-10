using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 批量路径列表 GUI：L1 可编辑；L2「使用主面板批量路径」只读展示。
// =====================================================================================
public static class ResourceBatchFolderListGui
{
    public static bool DrawEditableList(string title, List<string> folders)
    {
        bool changed = false;
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "主面板共用路径：贴图与模型总批量均扫描这些文件夹（递归子目录）。本机设置，不进版本库。",
            MessageType.None);

        if (folders == null)
        {
            return false;
        }

        for (int i = 0; i < folders.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DefaultAsset current = string.IsNullOrEmpty(folders[i])
                    ? null
                    : AssetDatabase.LoadAssetAtPath<DefaultAsset>(folders[i]);
                DefaultAsset next = (DefaultAsset)EditorGUILayout.ObjectField(current, typeof(DefaultAsset), false);
                string nextPath = next == null ? string.Empty : AssetDatabase.GetAssetPath(next);
                if (next != null && !AssetDatabase.IsValidFolder(nextPath))
                {
                    EditorGUILayout.HelpBox("请拖入文件夹", MessageType.Warning);
                    nextPath = folders[i];
                    next = current;
                }

                if (nextPath != folders[i])
                {
                    folders[i] = nextPath;
                    changed = true;
                }

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    folders.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }

        if (GUILayout.Button("添加文件夹", GUILayout.Width(100f)))
        {
            folders.Add(string.Empty);
            changed = true;
        }

        return changed;
    }

    public static void DrawReadOnlyMasterPaths(string titlePrefix)
    {
        List<string> valid = ResourceBatchFolderStore.GetValidMasterFolders();
        string title = string.IsNullOrEmpty(titlePrefix)
            ? "主面板批量路径"
            : titlePrefix;
        EditorGUILayout.LabelField(title + "：" + ResourceBatchFolderStore.FormatMasterPathsTitle(3),
            EditorStyles.wordWrappedMiniLabel);

        if (valid.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "主面板尚未配置有效路径。请到「资源处理总面板」添加文件夹；或改用「当前选中 / 指定文件夹」。",
                MessageType.Warning);
            return;
        }

        for (int i = 0; i < valid.Count; i++)
        {
            EditorGUILayout.LabelField("  " + valid[i], EditorStyles.miniLabel);
        }

        EditorGUILayout.HelpBox("只读：修改请回主面板。单独根目录请改用「指定文件夹」或「当前选中」。", MessageType.None);
    }
}
