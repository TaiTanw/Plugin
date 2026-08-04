using UnityEditor;
using UnityEngine;

// =====================================================================================
// v1 最小实现：把 FBX 内所有 Mesh 的顶点色设为 RGBA(1,1,1,1)。
// 后续若要改成"仅非默认才处理 / 只清 Alpha"等策略，只改本文件。
//
// 注意：改的是导入后的 Mesh 子资产，不是 FBX 二进制。
// FBX 再导入会重建 Mesh；打包工具（Retinar v1.2.9+）在交付区 Model 的
// SaveAndReimport 前后会快照/恢复顶点色。Assets/Art/ 自动流仍不跑本操作——
// 请在 Art 模型上手动跑一次；之后再 Batch Build 不应再被冲掉。
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
                   "用于缓解部分 GLB 导出链路里顶点色引起的异常。\n" +
                   "自动流跳过 Assets/Art/；请对 Art/Model 下 FBX 手动执行。" +
                   "打包工具重导交付区模型时会保留已写入的顶点色（勿删 Art 后指望自动恢复）。";
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
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(context.AssetPath);
        if (assets == null || assets.Length == 0)
        {
            return ModelOperationResult.Failed("无法加载模型资产");
        }

        int meshCount = 0;
        int changedCount = 0;
        Color white = Color.white;

        for (int i = 0; i < assets.Length; i++)
        {
            Mesh mesh = assets[i] as Mesh;
            if (mesh == null)
            {
                continue;
            }

            meshCount++;
            context.ReportSubProgress((float)i / assets.Length, mesh.name);

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

        if (meshCount == 0)
        {
            return ModelOperationResult.Skipped("资产内没有 Mesh");
        }

        if (changedCount == 0)
        {
            return ModelOperationResult.Skipped("全部 " + meshCount + " 个 Mesh 顶点色已是 (1,1,1,1)");
        }

        return ModelOperationResult.Changed(
            "已将 " + changedCount + "/" + meshCount + " 个 Mesh 顶点色设为 (1,1,1,1)");
    }
}
