using UnityEditor;
using UnityEngine;

// =====================================================================================
// 固定路径 SO：只展示路径 + 定位，禁止拖放换绑。
// =====================================================================================
public static class SettingsAssetPathGui
{
    public static void DrawPinned(Object asset)
    {
        string path = asset != null ? AssetDatabase.GetAssetPath(asset) : "（尚未创建）";
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("资产路径", path);
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("定位", GUILayout.Width(48f)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
        }
    }
}
