using UnityEditor;
using UnityEngine;

// =====================================================================================
// 人工 TOol 资产在 Inspector 不显示导入期自动化字段；编排 Pipeline 资产仍显示。
// =====================================================================================
[CustomEditor(typeof(TextureProcessSettings))]
public class TextureProcessSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        string path = AssetDatabase.GetAssetPath(target);
        bool pipeline = ProcessSettingsOwnership.IsPipelineConfigPath(path);
        if (pipeline)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "applyImporterSettingsOnImport",
            "textureDisableReadWrite",
            "excludedPathPrefixes",
            "importAutoOperationIds",
            "masterBatchOperationIds");
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox(
            "导入期 Importer / 不介入目录只在「全局导入设置（2）」（Pipeline/ConfigData）里编辑。",
            MessageType.None);
    }
}
