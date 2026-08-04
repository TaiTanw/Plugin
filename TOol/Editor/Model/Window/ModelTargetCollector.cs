using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ModelTargetCollector
{
    public enum Scope
    {
        Selection,
        Folder
    }

    public static List<string> Collect(Scope scope, DefaultAsset folder)
    {
        var result = new List<string>();
        ModelProcessSettings settings = ModelProcessSettings.Current;

        if (scope == Scope.Selection)
        {
            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    CollectUnderFolder(path, settings, result);
                }
                else if (settings.IsSupportedModelExtension(path) && !result.Contains(path))
                {
                    result.Add(path);
                }
            }
        }
        else if (scope == Scope.Folder && folder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                CollectUnderFolder(folderPath, settings, result);
            }
        }

        return result;
    }

    private static void CollectUnderFolder(string folderPath, ModelProcessSettings settings, List<string> result)
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (settings.IsSupportedModelExtension(path) && !result.Contains(path))
            {
                result.Add(path);
            }
        }
    }
}
