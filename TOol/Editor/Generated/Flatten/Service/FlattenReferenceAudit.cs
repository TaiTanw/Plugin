using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 源预制体可能已带 Missing（丢脚本 / 槽上对象已删）。
// 平铺入口只读扫描、LogError，不补引用、不阻断。
// 空槽（None）不算；只有 Unity 的 Missing（instanceID≠0 但对象为 null）才报。
// =====================================================================================

/// <summary>平铺前检查源预制体自身的 Missing 引用。</summary>
public static class FlattenReferenceAudit
{
    public static void LogSourcePrefabMissingReferences(string sourcePrefabPath, string assetName)
    {
        if (string.IsNullOrEmpty(sourcePrefabPath) ||
            !sourcePrefabPath.Replace("\\", "/").EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<string> misses = CollectSourceMissingReferences(sourcePrefabPath);
        if (misses.Count == 0)
        {
            return;
        }

        Debug.LogError("[Retinar] " + assetName + "：源预制体自身带有 " + misses.Count +
            " 条 Missing 引用（平铺不修复，请回源 Prefab 补）：\n  源: " + sourcePrefabPath + "\n" +
            string.Join("\n", misses.ToArray()));
    }

    private static List<string> CollectSourceMissingReferences(string sourcePrefabPath)
    {
        var misses = new List<string>();
        GameObject instance;
        try
        {
            instance = PrefabUtility.LoadPrefabContents(sourcePrefabPath);
        }
        catch (System.Exception)
        {
            return misses;
        }

        if (instance == null)
        {
            return misses;
        }

        try
        {
            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < transforms.Length; t++)
            {
                Transform transform = transforms[t];
                Component[] components = transform.GetComponents<Component>();
                for (int c = 0; c < components.Length; c++)
                {
                    Component component = components[c];
                    if (component == null)
                    {
                        string line = "  " + HierarchyPath(transform) + "  Missing Script";
                        if (!misses.Contains(line))
                        {
                            misses.Add(line);
                        }

                        continue;
                    }

                    if (component is Transform)
                    {
                        continue;
                    }

                    CollectMissingObjectSlots(component, misses);
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }

        return misses;
    }

    private static void CollectMissingObjectSlots(Component component, List<string> misses)
    {
        SerializedObject serializedObject;
        try
        {
            serializedObject = new SerializedObject(component);
        }
        catch (System.Exception)
        {
            return;
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        while (iterator.NextVisible(true))
        {
            if (iterator.propertyType != SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            // None：值为空且 instanceID=0。Missing：值为空但 instanceID 仍在。
            if (iterator.objectReferenceValue != null || iterator.objectReferenceInstanceIDValue == 0)
            {
                continue;
            }

            string line = "  " + HierarchyPath(component.transform) +
                " (" + component.GetType().Name + ")." + iterator.propertyPath +
                "  Missing";
            if (!misses.Contains(line))
            {
                misses.Add(line);
            }
        }
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform.parent == null)
        {
            return transform.name;
        }

        return HierarchyPath(transform.parent) + "/" + transform.name;
    }
}
