using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// v1 最小实现：把模型内 Mesh 的顶点色设为 RGBA(1,1,1,1)。
//
// 时序（重要）：
//   OnPostprocessModel 阶段 AssetDatabase.LoadAllAssetsAtPath 经常仍返回空
//   （日志里的「无法加载模型资产」），必须用 context.ImportRoot 层级上的 Mesh。
//   delayCall / 手动执行时 ImportRoot 为空，再走 LoadAllAssetsAtPath。
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
                   "Assets/Art/ 自动跳过；请对手动选中的 Art/Model FBX 执行后再打包。";
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
        List<Mesh> meshes = CollectMeshes(context);
        if (meshes.Count == 0)
        {
            // 导入回调里库未就绪：不要报 Failed（会吓人且误导），交给 delayCall 再跑。
            if (context.ImportRoot != null)
            {
                return ModelOperationResult.Skipped(
                    "OnPostprocessModel 时尚未收集到 Mesh，将由 delayCall 再处理");
            }

            return ModelOperationResult.Failed("无法加载模型资产（LoadAllAssetsAtPath 为空）");
        }

        int meshCount = meshes.Count;
        int changedCount = 0;
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

            Color[] colors = mesh.colors;
            bool alreadyWhite = colors != null && colors.Length == vertexCount;
            if (alreadyWhite)
            {
                for (int c = 0; c < colors.Length; c++)
                {
                    if (colors[c] != white)
                    {
                        alreadyWhite = false;
                        break;
                    }
                }
            }

            if (alreadyWhite)
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
            changedCount++;
        }

        if (changedCount == 0)
        {
            return ModelOperationResult.Skipped("全部 " + meshCount + " 个 Mesh 顶点色已是 (1,1,1,1)");
        }

        return ModelOperationResult.Changed(
            "已将 " + changedCount + "/" + meshCount + " 个 Mesh 顶点色设为 (1,1,1,1)");
    }

    private static List<Mesh> CollectMeshes(ModelOperationContext context)
    {
        var meshes = new List<Mesh>();
        var seen = new HashSet<Mesh>();

        if (context.ImportRoot != null)
        {
            CollectFromHierarchy(context.ImportRoot, meshes, seen);
        }

        // 手动 / delayCall：从资产库拉全量子 Mesh（含未被 Renderer 引用的）。
        // OnPostprocessModel 时库常为空，作为补充尝试无害。
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
