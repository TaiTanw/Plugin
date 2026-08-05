using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// v1：把模型内 Mesh 的顶点色设为 RGBA(1,1,1,1)。
//
// 时序：
//   OnPostprocessModel：AssetDatabase.LoadAllAssetsAtPath 常为空，必须用 ImportRoot。
//   delayCall / 手动：走 LoadAllAssetsAtPath。
//
// Art 手动失效的常见原因（Plane_Jian31）：
//   1) 选中 Prefab / Prefab 文件夹时旧收集器只认 .fbx → 命中 0（由 TargetCollector 修）。
//   2) ModelImporter.isReadable=0 时，mesh.colors 写入在 Editor 里可能像成功，
//      但不稳定落库；UnityGLTF 从 Prefab 导出时仍读到 FBX 原黄顶点色。
//      手动路径先打开 Read/Write（会触发一次重导，冲掉旧 Mesh），再写全白并校验。
// =====================================================================================
public class SetVertexColorsWhiteOperation : IModelAssetOperation
{
    public string Id
    {
        get { return "set_vertex_colors_white"; }
    }

    public string DisplayName
    {
        get { return "顶点色设为全白"; }
    }

    public string Description
    {
        get
        {
            return "将模型内所有 Mesh 的顶点色设为 RGBA(1,1,1,1)。\n" +
                   "导入区自动：OnPostprocessModel（层级 Mesh）+ delayCall（资产库 Mesh）。\n" +
                   "Assets/Art/ 自动跳过；请对手动选中的 Art Model / Prefab（会解析到依赖 FBX）执行后再导 GLB。\n" +
                   "手动执行时若 FBX 未开 Read/Write，会先打开再写入（保证落盘与导出可读）。";
        }
    }

    public int Order
    {
        get { return 10; }
    }

    public bool CanProcess(string assetPath, ModelProcessSettings settings)
    {
        return settings != null && settings.IsSupportedModelExtension(assetPath);
    }

    public ModelOperationResult Execute(ModelOperationContext context)
    {
        // 导入回调里 Mesh 已在内存中可写，禁止此处 SaveAndReimport（会递归/冲掉本次写入）。
        if (!context.TriggeredByImport)
        {
            string readableError;
            if (!EnsureModelReadable(context.AssetPath, out readableError))
            {
                return ModelOperationResult.Failed(readableError);
            }
        }

        List<Mesh> meshes = CollectMeshes(context);
        if (meshes.Count == 0)
        {
            if (context.ImportRoot != null)
            {
                return ModelOperationResult.Skipped(
                    "OnPostprocessModel 时尚未收集到 Mesh，将由 delayCall 再处理");
            }

            return ModelOperationResult.Failed("无法加载模型资产（LoadAllAssetsAtPath 为空）");
        }

        int meshCount = meshes.Count;
        int changedCount = 0;
        int verifyFailedCount = 0;
        Color white = Color.white;

        for (int i = 0; i < meshes.Count; i++)
        {
            Mesh mesh = meshes[i];
            context.ReportSubProgress((float)i / meshes.Count, mesh.name);

            int vertexCount = mesh.vertexCount;
            if (vertexCount <= 0)
            {
                continue;
            }

            if (IsAllWhite(mesh, white))
            {
                continue;
            }

            var whiteColors = new Color[vertexCount];
            for (int c = 0; c < vertexCount; c++)
            {
                whiteColors[c] = white;
            }

            mesh.colors = whiteColors;
            EditorUtility.SetDirty(mesh);

            if (!IsAllWhite(mesh, white))
            {
                verifyFailedCount++;
                continue;
            }

            changedCount++;
        }

        if (verifyFailedCount > 0)
        {
            return ModelOperationResult.Failed(
                "有 " + verifyFailedCount + "/" + meshCount +
                " 个 Mesh 写入后校验仍非全白（请确认 FBX Read/Write Enabled）");
        }

        if (changedCount == 0)
        {
            return ModelOperationResult.Skipped("全部 " + meshCount + " 个 Mesh 顶点色已是 (1,1,1,1)");
        }

        return ModelOperationResult.Changed(
            "已将 " + changedCount + "/" + meshCount + " 个 Mesh 顶点色设为 (1,1,1,1)");
    }

    /// <summary>
    /// 手动路径：未开 Read/Write 时先打开并重导，否则顶点色写入不可靠。
    /// 重导会重建 Mesh（恢复 FBX 原色），调用方必须在之后立刻写全白。
    /// </summary>
    private static bool EnsureModelReadable(string assetPath, out string error)
    {
        error = null;
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            error = "不是 ModelImporter 资产: " + assetPath;
            return false;
        }

        if (importer.isReadable)
        {
            return true;
        }

        importer.isReadable = true;
        importer.SaveAndReimport();
        Debug.Log("[模型处理] 已开启 Read/Write 并重导，随后写入顶点色: " + assetPath);
        return true;
    }

    private static bool IsAllWhite(Mesh mesh, Color white)
    {
        Color[] colors = mesh.colors;
        if (colors == null || colors.Length != mesh.vertexCount)
        {
            return false;
        }

        for (int i = 0; i < colors.Length; i++)
        {
            if (colors[i] != white)
            {
                return false;
            }
        }

        return true;
    }

    private static List<Mesh> CollectMeshes(ModelOperationContext context)
    {
        var meshes = new List<Mesh>();
        var seen = new HashSet<Mesh>();

        if (context.ImportRoot != null)
        {
            CollectFromHierarchy(context.ImportRoot, meshes, seen);
        }

        CollectFromAssetPath(context.AssetPath, meshes, seen);
        return meshes;
    }

    private static void CollectFromHierarchy(GameObject root, List<Mesh> meshes, HashSet<Mesh> seen)
    {
        if (root == null)
        {
            return;
        }

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh != null && seen.Add(mesh))
            {
                meshes.Add(mesh);
            }
        }

        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            Mesh mesh = skinned[i].sharedMesh;
            if (mesh != null && seen.Add(mesh))
            {
                meshes.Add(mesh);
            }
        }
    }

    private static void CollectFromAssetPath(string assetPath, List<Mesh> meshes, HashSet<Mesh> seen)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets == null)
        {
            return;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            Mesh mesh = assets[i] as Mesh;
            if (mesh != null && seen.Add(mesh))
            {
                meshes.Add(mesh);
            }
        }
    }
}
