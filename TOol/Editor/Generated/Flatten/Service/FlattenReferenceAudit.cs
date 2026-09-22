using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Missing Script：Begin 可剥再存 Art，或失败列出。Object 槽 Missing 只报不阻断。
// 空槽（None）不算；Missing Script = Component 为 null。
// =====================================================================================

/// <summary>平铺前检查 / 剥 Missing Script。</summary>
public static class FlattenReferenceAudit
{
    public static List<string> ListMissingScripts(GameObject root)
    {
        var misses = new List<string>();
        CollectOnInstance(root, misses, objectSlots: null);
        return misses;
    }

    public static int StripMissingScripts(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int stripped = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int t = 0; t < transforms.Length; t++)
        {
            stripped += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[t].gameObject);
        }

        return stripped;
    }

    public static void LogObjectSlotMisses(GameObject root, string sourcePrefabPath)
    {
        var slots = new List<string>();
        CollectOnInstance(root, null, slots);
        if (slots.Count == 0)
        {
            return;
        }

        Debug.LogWarning("[Retinar] 源预制体有 " + slots.Count +
            " 条 Object 槽 Missing（不修、不阻断）：\n  源: " + sourcePrefabPath + "\n" +
            string.Join("\n", slots.ToArray()));
    }

    public static string FormatMissingScriptRefusal(string sourcePrefabPath, IList<string> scriptLines)
    {
        int n = scriptLines == null ? 0 : scriptLines.Count;
        string body = n == 0
            ? string.Empty
            : "\n" + string.Join("\n", scriptLines);
        return "源 Prefab 有 " + n +
               " 处 Missing Script（未剥）。人工可在平铺设置勾选剥后再存 Art。源=" +
               sourcePrefabPath + body;
    }

    static void CollectOnInstance(GameObject root, List<string> scriptLines, List<string> objectSlots)
    {
        if (root == null)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int t = 0; t < transforms.Length; t++)
        {
            Transform transform = transforms[t];
            Component[] components = transform.GetComponents<Component>();
            for (int c = 0; c < components.Length; c++)
            {
                Component component = components[c];
                if (component == null)
                {
                    if (scriptLines == null)
                    {
                        continue;
                    }

                    string line = "  " + HierarchyPath(transform) + "  Missing Script";
                    if (!scriptLines.Contains(line))
                    {
                        scriptLines.Add(line);
                    }

                    continue;
                }

                if (objectSlots == null || component is Transform)
                {
                    continue;
                }

                CollectMissingObjectSlots(component, objectSlots);
            }
        }
    }

    static void CollectMissingObjectSlots(Component component, List<string> misses)
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

    static string HierarchyPath(Transform transform)
    {
        if (transform.parent == null)
        {
            return transform.name;
        }

        return HierarchyPath(transform.parent) + "/" + transform.name;
    }
}
