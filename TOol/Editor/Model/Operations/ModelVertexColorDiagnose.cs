using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 顶点色诊断：证明「工程内 Mesh 是否已全白」，与 UnityGLTF 菜单导出解耦。
// =====================================================================================
public static class ModelVertexColorDiagnose
{
    private const float WhiteEpsilon = 0.002f;

    [MenuItem("Tools/资源处理/诊断选中模型顶点色", false, 520)]
    private static void DiagnoseSelection()
    {
        var paths = new List<string>();
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { path });
                for (int i = 0; i < guids.Length; i++)
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
                }
            }
            else if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                {
                    if (AssetImporter.GetAtPath(dep) as ModelImporter != null)
                    {
                        paths.Add(dep);
                    }
                }
            }
            else if (AssetImporter.GetAtPath(path) as ModelImporter != null)
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            Debug.LogWarning("[顶点色诊断] 请选中 Model / Prefab / 含模型的文件夹。");
            return;
        }

        Debug.Log(DiagnosePaths(paths));
    }

    /// <summary>是否全部 ModelImporter 模型均已全白（无模型视为 true）。</summary>
    public static bool AreAllWhite(IList<string> modelOrFolderPaths)
    {
        var models = CollectModelList(modelOrFolderPaths);
        if (models.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < models.Count; i++)
        {
            int meshCount;
            int nonWhite;
            bool readable;
            Summarize(models[i], out meshCount, out nonWhite, out readable);
            if (meshCount <= 0 || nonWhite != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>供 Pipeline ⑤/⑥ 后调用：不依赖手动 Export GLB。</summary>
    public static string DiagnosePaths(IList<string> modelOrFolderPaths)
    {
        var models = CollectModelList(modelOrFolderPaths);

        var sb = new StringBuilder();
        sb.AppendLine("[顶点色诊断] 模型数=" + models.Count);
        int allWhiteModels = 0;
        for (int i = 0; i < models.Count; i++)
        {
            string path = models[i];
            int meshCount;
            int nonWhite;
            bool readable;
            Summarize(path, out meshCount, out nonWhite, out readable);
            bool ok = meshCount > 0 && nonWhite == 0;
            if (ok)
            {
                allWhiteModels++;
            }

            sb.AppendLine(
                (ok ? "  OK  " : "  BAD ") + path +
                " | Mesh=" + meshCount +
                " 非白=" + nonWhite +
                " isReadable=" + readable);
        }

        sb.AppendLine(
            "[顶点色诊断] 全白模型 " + allWhiteModels + "/" + models.Count +
            "。若此处 OK 而 UnityGLTF 导出仍黄：多为导出时机在⑤前，或看的是旧 .glb 文件。");
        return sb.ToString().TrimEnd();
    }

    private static List<string> CollectModelList(IList<string> modelOrFolderPaths)
    {
        var seen = new HashSet<string>();
        var models = new List<string>();
        if (modelOrFolderPaths == null)
        {
            return models;
        }

        for (int i = 0; i < modelOrFolderPaths.Count; i++)
        {
            CollectModels(modelOrFolderPaths[i], models, seen);
        }

        return models;
    }

    private static void CollectModels(string path, List<string> models, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        path = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(path))
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { path });
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetImporter.GetAtPath(p) as ModelImporter != null && seen.Add(p))
                {
                    models.Add(p);
                }
            }

            return;
        }

        if (AssetImporter.GetAtPath(path) as ModelImporter != null && seen.Add(path))
        {
            models.Add(path);
        }
    }

    private static void Summarize(string modelPath, out int meshCount, out int nonWhite, out bool readable)
    {
        meshCount = 0;
        nonWhite = 0;
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        readable = importer != null && importer.isReadable;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (assets == null)
        {
            return;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            Mesh mesh = assets[i] as Mesh;
            if (mesh == null || mesh.vertexCount <= 0)
            {
                continue;
            }

            meshCount++;
            if (!IsAllWhite(mesh))
            {
                nonWhite++;
            }
        }
    }

    private static bool IsAllWhite(Mesh mesh)
    {
        Color[] colors = mesh.colors;
        if (colors != null && colors.Length == mesh.vertexCount)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = colors[i];
                if (Mathf.Abs(c.r - 1f) > WhiteEpsilon ||
                    Mathf.Abs(c.g - 1f) > WhiteEpsilon ||
                    Mathf.Abs(c.b - 1f) > WhiteEpsilon ||
                    Mathf.Abs(c.a - 1f) > WhiteEpsilon)
                {
                    return false;
                }
            }

            return true;
        }

        Color32[] colors32 = mesh.colors32;
        if (colors32 != null && colors32.Length == mesh.vertexCount)
        {
            for (int i = 0; i < colors32.Length; i++)
            {
                Color32 c = colors32[i];
                if (c.r != 255 || c.g != 255 || c.b != 255 || c.a != 255)
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }
}
