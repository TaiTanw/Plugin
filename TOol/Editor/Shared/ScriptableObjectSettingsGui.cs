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
            if (iterator.propertyPath == "m_Script")
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        cached.ApplyModifiedProperties();
    }
}
