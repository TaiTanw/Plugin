using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 批量路径列表的共用 GUI：增减 DefaultAsset 文件夹，写回 ResourceBatchFolderStore。
// =====================================================================================
public static class ResourceBatchFolderListGui
{
    public static bool DrawEditableList(string title, List<string> folders)
    {
        bool changed = false;
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "此列表供「依据文件路径批量」与总面板批量执行共用，与当前是否选中「选中/单文件夹」无关。",
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
}
