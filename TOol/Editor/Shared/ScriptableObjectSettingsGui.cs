using UnityEditor;
using UnityEngine;

// =====================================================================================
// 在 EditorWindow 内绘制 ScriptableObject 配置，避免 Editor.CreateEditor + OnInspectorGUI。
// 后者在 Inspector 仍处于 Prefab/GO 预览上下文时（平铺后常见）会每帧刷：
//   "serializedObject/targets should not be used inside OnSceneGUI or OnPreviewGUI"
// =====================================================================================
public static class ScriptableObjectSettingsGui
{
    public static void Draw(Object target, ref SerializedObject cached)
    {
        Draw(target, ref cached, null);
    }

    public static void Draw(Object target, ref SerializedObject cached, params string[] skipPropertyNames)
    {
        if (target == null)
        {
            return;
        }

        if (cached == null || cached.targetObject != target)
        {
            cached = new SerializedObject(target);
        }

        cached.Update();
        SerializedProperty iterator = cached.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script" || ShouldSkip(iterator, skipPropertyNames))
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        cached.ApplyModifiedProperties();
    }

    /// <summary>只画列出的字段（导入设置面板用）。</summary>
    public static void DrawOnly(Object target, ref SerializedObject cached, params string[] propertyNames)
    {
        if (target == null || propertyNames == null || propertyNames.Length == 0)
        {
            return;
        }

        if (cached == null || cached.targetObject != target)
        {
            cached = new SerializedObject(target);
        }

        cached.Update();
        for (int i = 0; i < propertyNames.Length; i++)
        {
            string name = propertyNames[i];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            SerializedProperty property = cached.FindProperty(name);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        cached.ApplyModifiedProperties();
    }

    private static bool ShouldSkip(SerializedProperty property, string[] skipPropertyNames)
    {
        if (skipPropertyNames == null || skipPropertyNames.Length == 0)
        {
            return false;
        }

        string name = property.name;
        string path = property.propertyPath;
        for (int i = 0; i < skipPropertyNames.Length; i++)
        {
            string skip = skipPropertyNames[i];
            if (string.IsNullOrEmpty(skip))
            {
                continue;
            }

            if (name == skip || path == skip)
            {
                return true;
            }
        }

        return false;
    }
}
