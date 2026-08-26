using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Generated / Prefab / Service
// 中文：③ 预设体制作 — 执行层（已导入模型 → 独立 Prefab 写盘）。
// 层级：L-内核。不调用插件 1 平铺/导出；不做材质贴图 remap（remap 属插件 1 ④）。
// =====================================================================================

/// <summary>
/// 自动化预设体制作服务。
/// </summary>
public static class PrefabBuildService
{
    private static readonly string[] ModelExtensions =
    {
        ".fbx", ".glb", ".gltf", ".obj"
    };

    /// <summary>
    /// 对给定工程内模型路径生成独立 Prefab 并写盘。
    /// </summary>
    /// <param name="sourceModelPaths">Assets 下 .fbx / .glb 等路径</param>
    /// <param name="materialId">非空时覆盖三层目录命名（任务/CLI）</param>
    /// <returns>成功写出的 Prefab 资产路径列表</returns>
    public static List<string> BuildPrefabsFromModels(
        IList<string> sourceModelPaths,
        string materialId = null)
    {
        var written = new List<string>();
        if (sourceModelPaths == null || sourceModelPaths.Count == 0)
        {
            Debug.LogWarning("[TOol][Prefab] 源模型路径列表为空。");
            return written;
        }

        EnsureAssetFolder(PrefabIncomingPaths.PrefabRoot);

        var usedBaseNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        bool multiWithMaterialId = !string.IsNullOrEmpty(materialId) && sourceModelPaths.Count > 1;

        for (int i = 0; i < sourceModelPaths.Count; i++)
        {
            string source = (sourceModelPaths[i] ?? string.Empty).Replace("\\", "/");
            if (string.IsNullOrEmpty(source) || !IsSupportedModelPath(source))
            {
                Debug.LogWarning("[TOol][Prefab] 跳过非模型路径: " + source);
                continue;
            }

            if (!source.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[TOol][Prefab] 仅支持工程内 Assets 路径: " + source);
                continue;
            }

            string stem = Path.GetFileNameWithoutExtension(source);
            string disambiguator = multiWithMaterialId ? stem : null;
            string target = PrefabIncomingPaths.PrefabPathForSourceModel(source, materialId, disambiguator);
            string baseName = Path.GetFileNameWithoutExtension(target);

            int suffix = 0;
            while (!usedBaseNames.Add(baseName))
            {
                suffix++;
                disambiguator = string.IsNullOrEmpty(stem) ? ("i" + suffix) : (stem + "_" + suffix);
                target = PrefabIncomingPaths.PrefabPathForSourceModel(source, materialId, disambiguator);
                baseName = Path.GetFileNameWithoutExtension(target);
            }

            if (TryBuildOnePrefab(source, target))
            {
                written.Add(target);
            }
            else
            {
                usedBaseNames.Remove(baseName);
            }
        }

        if (written.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log("[TOol][Prefab] 完成：成功 " + written.Count + " / 输入 " + sourceModelPaths.Count +
                  " → 根目录 " + PrefabIncomingPaths.PrefabRoot);
        return written;
    }

    /// <summary>确保专用 Prefab 根文件夹存在，并返回路径。</summary>
    public static string GetOrDescribePrefabRoot()
    {
        EnsureAssetFolder(PrefabIncomingPaths.PrefabRoot);
        return PrefabIncomingPaths.PrefabRoot;
    }

    /// <summary>从 Project 选中资产收集模型路径并生成 Prefab（编辑器验证入口）。</summary>
    public static List<string> BuildPrefabsFromSelection()
    {
        var paths = new List<string>();
        Object[] selected = Selection.objects;
        if (selected != null)
        {
            for (int i = 0; i < selected.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selected[i]);
                if (IsSupportedModelPath(path))
                {
                    paths.Add(path.Replace("\\", "/"));
                }
            }
        }

        if (paths.Count == 0)
        {
            Debug.LogWarning("[TOol][Prefab] 请在 Project 中选中 .fbx / .glb / .gltf / .obj 后再执行。");
        }

        return BuildPrefabsFromModels(paths);
    }

    private static bool TryBuildOnePrefab(string sourceModelPath, string targetPrefabPath)
    {
        GameObject main = AssetDatabase.LoadAssetAtPath<GameObject>(sourceModelPath);
        if (main == null)
        {
            Debug.LogError("[TOol][Prefab] 无法加载主 GameObject: " + sourceModelPath +
                           "（GLB 需宿主已装 UnityGLTF 并完成导入）");
            return false;
        }

        string parent = Path.GetDirectoryName(targetPrefabPath);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureAssetFolder(parent.Replace("\\", "/"));
        }

        GameObject instance = Object.Instantiate(main);
        instance.name = Path.GetFileNameWithoutExtension(targetPrefabPath);

        try
        {
            if (PrefabBuildSettings.PreferUnpackCompletely)
            {
                UnpackCompletelyIfNeeded(instance);
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, targetPrefabPath);
            if (saved == null)
            {
                Debug.LogError("[TOol][Prefab] SaveAsPrefabAsset 失败: " + targetPrefabPath);
                return false;
            }

            Debug.Log("[TOol][Prefab] 已生成 " + targetPrefabPath + " ← " + sourceModelPath);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void UnpackCompletelyIfNeeded(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = transforms.Length - 1; i >= 0; i--)
        {
            GameObject go = transforms[i].gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(go) &&
                PrefabUtility.IsAnyPrefabInstanceRoot(go))
            {
                PrefabUtility.UnpackPrefabInstance(
                    go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }
    }

    private static bool IsSupportedModelPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        for (int i = 0; i < ModelExtensions.Length; i++)
        {
            if (ext == ModelExtensions[i])
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder))
        {
            return;
        }

        assetFolder = assetFolder.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            Debug.LogError("[TOol][Prefab] 文件夹必须在 Assets 下: " + assetFolder);
            return;
        }

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }

            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
