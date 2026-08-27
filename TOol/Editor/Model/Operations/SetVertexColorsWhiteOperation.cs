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
// 持久化要点：
//   - 任意 ModelImporter SaveAndReimport / ForceSynchronousImport 会从 FBX 二进制重建 Mesh，
//     冲掉本 Op 写入；必须 preserve（见 ModelMeshVertexColorUtility）。
//   - EnsureModelReadable 开 Read/Write 时也必须 preserve，否则⑤写白前又被冲回源色。
//   - UnityGLTF Export GLB 读 mesh.colors（ExportVertexColors）；导出须在⑤之后。
// =====================================================================================
public class SetVertexColorsWhiteOperation : IModelAssetOperation
{
    private const float WhiteEpsilon = 0.002f;

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
            return "将模型内 Mesh 顶点色设为 RGBA(1,1,1,1)。\n" +
                   "适用于 ModelImporter（典型 .fbx）：L1 手动 / 中间层⑤ 会开 Read/Write 再写。\n" +
                   "UnityGLTF 的 .glb/.gltf 为 ScriptedImporter，⑤/手动路径会跳过（非失败）。\n" +
                   "导入期自动流（OnPostprocessModel / delayCall）默认跳过 Art；交付区刷白只走⑤/L1。\n" +
                   "任意裸重导会冲掉刷白；⑤ 内用 preserve，⑥后若冲掉由中间层再调同一总批量。\n" +
                   "用 UnityGLTF 菜单导出 Prefab→GLB 验色时，须在管线⑤完成之后再导出。";
        }
    }

    public int Order
    {
        get { return 10; }
    }

    public AssetOperationEvaluation Evaluate(
        string assetPath,
        ModelProcessSettings settings,
        GameObject importRoot)
    {
        if (settings == null || !settings.IsSupportedModelExtension(assetPath))
        {
            return AssetOperationEvaluation.NotApplicable("不支持的模型扩展名");
        }

        if (importRoot == null &&
            AssetImporter.GetAtPath(assetPath) as ModelImporter == null)
        {
            return AssetOperationEvaluation.Skip(
                "非 ModelImporter（如 .glb ScriptedImporter），本 Op 仅稳妥支持 FBX 等");
        }

        List<Mesh> meshes = CollectMeshesForEvaluate(assetPath, importRoot);
        if (meshes.Count == 0)
        {
            if (importRoot != null)
            {
                return AssetOperationEvaluation.Skip("层级暂无 Mesh，将由 delayCall 再评估");
            }

            return AssetOperationEvaluation.Skip("无法加载模型 Mesh（LoadAllAssetsAtPath 为空）");
        }

        int nonWhite = 0;
        for (int i = 0; i < meshes.Count; i++)
        {
            if (!IsAllWhite(meshes[i]))
            {
                nonWhite++;
            }
        }

        if (nonWhite == 0)
        {
            return AssetOperationEvaluation.Skip(
                "全部 " + meshes.Count + " 个 Mesh 顶点色已是 (1,1,1,1)");
        }

        return AssetOperationEvaluation.NeedsWorkResult(
            nonWhite + "/" + meshes.Count + " 个 Mesh 顶点色非全白");
    }

    public bool CanProcess(string assetPath, ModelProcessSettings settings)
    {
        return Evaluate(assetPath, settings, null).NeedsWork;
    }

    public ModelOperationResult Execute(ModelOperationContext context)
    {
        if (!context.TriggeredByImport)
        {
            string readableError;
            ModelReadableStatus readable = EnsureModelReadable(context.AssetPath, out readableError);
            if (readable == ModelReadableStatus.NotModelImporter)
            {
                return ModelOperationResult.Skipped(readableError);
            }

            if (readable == ModelReadableStatus.Failed)
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

        for (int i = 0; i < meshes.Count; i++)
        {
            Mesh mesh = meshes[i];
            context.ReportSubProgress((float)i / meshes.Count, mesh.name);

            if (mesh.vertexCount <= 0)
            {
                continue;
            }

            if (IsAllWhite(mesh))
            {
                continue;
            }

            if (!TryWriteAllWhite(mesh))
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

        if (!context.TriggeredByImport &&
            !string.IsNullOrEmpty(context.AssetPath))
        {
            AssetDatabase.SaveAssets();
            string persistError;
            if (!PersistVerifyAllWhite(context.AssetPath, out persistError))
            {
                List<Mesh> again = new List<Mesh>();
                var seen = new HashSet<Mesh>();
                CollectFromAssetPath(context.AssetPath, again, seen);
                int rewrite = 0;
                for (int i = 0; i < again.Count; i++)
                {
                    if (again[i] == null || again[i].vertexCount <= 0 || IsAllWhite(again[i]))
                    {
                        continue;
                    }

                    if (TryWriteAllWhite(again[i]))
                    {
                        rewrite++;
                    }
                }

                AssetDatabase.SaveAssets();
                if (!PersistVerifyAllWhite(context.AssetPath, out persistError))
                {
                    return ModelOperationResult.Failed(
                        "落盘复检仍非全白: " + persistError +
                        "（常为随后重导冲掉；已重写 " + rewrite + " 个 Mesh）");
                }

                changedCount = Mathf.Max(changedCount, rewrite);
            }
        }

        if (changedCount == 0)
        {
            return ModelOperationResult.Skipped("全部 " + meshCount + " 个 Mesh 顶点色已是 (1,1,1,1)");
        }

        return ModelOperationResult.Changed(
            "已将 " + changedCount + "/" + meshCount + " 个 Mesh 顶点色设为 (1,1,1,1)");
    }

    private enum ModelReadableStatus
    {
        Ok,
        NotModelImporter,
        Failed
    }

    /// <summary>
    /// 开 Read/Write 必须重导；重导会冲掉已有顶点色，故走 preserve。
    /// </summary>
    private static ModelReadableStatus EnsureModelReadable(string assetPath, out string error)
    {
        error = null;
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            error = "非 ModelImporter（如 UnityGLTF 的 .glb/.gltf），手动/⑤ 路径暂不刷顶点白。" +
                    "权威在源文件；FBX 可用本 Op。路径: " + assetPath;
            return ModelReadableStatus.NotModelImporter;
        }

        if (importer.isReadable)
        {
            return ModelReadableStatus.Ok;
        }

        // 开 Read/Write 必须重导：先快照 → SaveAndReimport(isReadable) → 写回，避免裸重导冲色。
        ModelMeshVertexColorUtility.ForceSyncReimportPreservingVertexColors(assetPath, setReadable: true);
        Debug.Log("[模型处理] 已开启 Read/Write（preserve 重导），随后写入顶点色: " + assetPath);
        return ModelReadableStatus.Ok;
    }

    private static bool TryWriteAllWhite(Mesh mesh)
    {
        int vertexCount = mesh.vertexCount;
        var whiteColors = new Color[vertexCount];
        var whiteColors32 = new Color32[vertexCount];
        Color white = Color.white;
        var white32 = new Color32(255, 255, 255, 255);
        for (int c = 0; c < vertexCount; c++)
        {
            whiteColors[c] = white;
            whiteColors32[c] = white32;
        }

        mesh.colors = whiteColors;
        mesh.colors32 = whiteColors32;
        EditorUtility.SetDirty(mesh);
        return IsAllWhite(mesh);
    }

    private static bool PersistVerifyAllWhite(string assetPath, out string error)
    {
        error = null;
        var meshes = new List<Mesh>();
        var seen = new HashSet<Mesh>();
        CollectFromAssetPath(assetPath, meshes, seen);
        if (meshes.Count == 0)
        {
            error = "重载后无 Mesh: " + assetPath;
            return false;
        }

        int nonWhite = 0;
        for (int i = 0; i < meshes.Count; i++)
        {
            if (!IsAllWhite(meshes[i]))
            {
                nonWhite++;
            }
        }

        if (nonWhite > 0)
        {
            error = nonWhite + "/" + meshes.Count + " 个 Mesh 仍非全白: " + assetPath;
            return false;
        }

        return true;
    }

    private static bool IsAllWhite(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount <= 0)
        {
            return true;
        }

        Color[] colors = mesh.colors;
        if (colors != null && colors.Length == mesh.vertexCount)
        {
            return AreAllNearWhite(colors);
        }

        Color32[] colors32 = mesh.colors32;
        if (colors32 != null && colors32.Length == mesh.vertexCount)
        {
            return AreAllWhite32(colors32);
        }

        return false;
    }

    private static bool AreAllNearWhite(Color[] colors)
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

    private static bool AreAllWhite32(Color32[] colors)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            Color32 c = colors[i];
            if (c.r != 255 || c.g != 255 || c.b != 255 || c.a != 255)
            {
                return false;
            }
        }

        return true;
    }

    private static List<Mesh> CollectMeshesForEvaluate(string assetPath, GameObject importRoot)
    {
        var meshes = new List<Mesh>();
        var seen = new HashSet<Mesh>();
        if (importRoot != null)
        {
            CollectFromHierarchy(importRoot, meshes, seen);
        }

        CollectFromAssetPath(assetPath, meshes, seen);

        if (meshes.Count == 0 && importRoot == null && !string.IsNullOrEmpty(assetPath))
        {
            ModelMeshVertexColorUtility.ForceSyncReimportPreservingVertexColors(assetPath);
            CollectFromAssetPath(assetPath, meshes, seen);
        }

        return meshes;
    }

    private static List<Mesh> CollectMeshes(ModelOperationContext context)
    {
        return CollectMeshesForEvaluate(context.AssetPath, context.ImportRoot);
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
