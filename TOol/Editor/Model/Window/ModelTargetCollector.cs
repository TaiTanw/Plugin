using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ModelTargetCollector
{
    public enum Scope
    {
        /// <summary>Project 面板当前选中；选中文件夹时递归其下模型。</summary>
        Selection,

        /// <summary>窗口临时指定的单个文件夹。</summary>
        Folder,

        /// <summary>子面板持久化的批量文件夹路径列表（总面板也只用这份）。</summary>
        BatchByPath
    }

    public static readonly string[] ScopeLabels =
    {
        "当前选中",
        "指定文件夹",
        "依据文件路径批量"
    };

    public static List<string> Collect(Scope scope, DefaultAsset folder, IList<string> batchFolders)
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
                else
                {
                    CollectFromAsset(path, settings, result);
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
        else if (scope == Scope.BatchByPath)
        {
            List<string> valid = ResourceBatchFolderStore.GetValidFolders(batchFolders);
            for (int i = 0; i < valid.Count; i++)
            {
                CollectUnderFolder(valid[i], settings, result);
            }
        }

        return result;
    }

    /// <summary>总面板：始终按批量路径收集，忽略子面板当前范围。</summary>
    public static List<string> CollectFromBatchFolders()
    {
        return Collect(Scope.BatchByPath, null, ResourceBatchFolderStore.GetModelFolders());
    }

    private static void CollectUnderFolder(string folderPath, ModelProcessSettings settings, List<string> result)
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
        foreach (string guid in modelGuids)
        {
            CollectFromAsset(AssetDatabase.GUIDToAssetPath(guid), settings, result);
        }

        // Prefab 文件夹里往往只有 .prefab；Mesh 在依赖的 FBX 上。
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        foreach (string guid in prefabGuids)
        {
            CollectModelsFromPrefab(AssetDatabase.GUIDToAssetPath(guid), settings, result);
        }
    }

    private static void CollectFromAsset(string assetPath, ModelProcessSettings settings, List<string> result)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        if (settings.IsSupportedModelExtension(assetPath))
        {
            AddUnique(result, assetPath);
            return;
        }

        if (IsPrefabAsset(assetPath))
        {
            CollectModelsFromPrefab(assetPath, settings, result);
        }
    }

    private static void CollectModelsFromPrefab(
        string prefabPath,
        ModelProcessSettings settings,
        List<string> result)
    {
        if (string.IsNullOrEmpty(prefabPath) || !IsPrefabAsset(prefabPath))
        {
            return;
        }

        foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
        {
            if (settings.IsSupportedModelExtension(dependency))
            {
                AddUnique(result, dependency);
            }
        }
    }

    private static bool IsPrefabAsset(string assetPath)
    {
        return string.Equals(Path.GetExtension(assetPath), ".prefab", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void AddUnique(List<string> result, string assetPath)
    {
        if (!string.IsNullOrEmpty(assetPath) && !result.Contains(assetPath))
        {
            result.Add(assetPath);
        }
    }
}
