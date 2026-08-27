using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// ModelImporter SaveAndReimport / ForceSynchronousImport 会从 FBX 二进制重建 Mesh，
// 冲掉 TOol「顶点色设为全白」。与 Retinar SaveAndReimportPreservingMeshVertexColors 同思路：
// 重导前快照 → 重导 → 写回。
// =====================================================================================
public static class ModelMeshVertexColorUtility
{
    private struct Snapshot
    {
        public string Name;
        public int VertexCount;
        public Color[] Colors;
        public Color32[] Colors32;
    }

    /// <summary>
    /// 对 ModelImporter 资产做同步重导，并尽量保留当前 Mesh 顶点色。
    /// 非 ModelImporter（如 GLB）直接跳过。
    /// </summary>
    public static void ForceSyncReimportPreservingVertexColors(string assetPath)
    {
        ForceSyncReimportPreservingVertexColors(assetPath, setReadable: false);
    }

    /// <param name="setReadable">为 true 时先把 isReadable 写入再重导（供开 Read/Write）。</param>
    public static void ForceSyncReimportPreservingVertexColors(string assetPath, bool setReadable)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        List<Snapshot> snapshots = Capture(assetPath);

        if (setReadable && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        else
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        int restored = Restore(assetPath, snapshots);
        if (restored > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[模型处理] 同步重导后已恢复 Mesh 顶点色: " + assetPath +
                      " 数量=" + restored + "/" + snapshots.Count);
        }
    }

    private static List<Snapshot> Capture(string modelPath)
    {
        var list = new List<Snapshot>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (assets == null)
        {
            return list;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            Mesh mesh = assets[i] as Mesh;
            if (mesh == null || mesh.vertexCount <= 0)
            {
                continue;
            }

            Color[] colors = mesh.colors;
            Color[] colorsCopy = null;
            if (colors != null && colors.Length == mesh.vertexCount)
            {
                colorsCopy = new Color[colors.Length];
                System.Array.Copy(colors, colorsCopy, colors.Length);
            }

            Color32[] colors32 = mesh.colors32;
            Color32[] colors32Copy = null;
            if (colors32 != null && colors32.Length == mesh.vertexCount)
            {
                colors32Copy = new Color32[colors32.Length];
                System.Array.Copy(colors32, colors32Copy, colors32.Length);
            }

            list.Add(new Snapshot
            {
                Name = mesh.name,
                VertexCount = mesh.vertexCount,
                Colors = colorsCopy,
                Colors32 = colors32Copy
            });
        }

        return list;
    }

    private static int Restore(string modelPath, List<Snapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            return 0;
        }

        var remaining = new List<Snapshot>(snapshots);
        int restored = 0;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (assets == null)
        {
            return 0;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            Mesh mesh = assets[i] as Mesh;
            if (mesh == null || mesh.vertexCount <= 0)
            {
                continue;
            }

            int matchIndex = -1;
            for (int s = 0; s < remaining.Count; s++)
            {
                Snapshot candidate = remaining[s];
                if (candidate.VertexCount != mesh.vertexCount ||
                    !string.Equals(candidate.Name, mesh.name, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (candidate.Colors == null && candidate.Colors32 == null)
                {
                    continue;
                }

                matchIndex = s;
                break;
            }

            if (matchIndex < 0)
            {
                continue;
            }

            Snapshot snap = remaining[matchIndex];
            if (snap.Colors != null && snap.Colors.Length == mesh.vertexCount)
            {
                mesh.colors = snap.Colors;
            }

            if (snap.Colors32 != null && snap.Colors32.Length == mesh.vertexCount)
            {
                mesh.colors32 = snap.Colors32;
            }

            EditorUtility.SetDirty(mesh);
            remaining.RemoveAt(matchIndex);
            restored++;
        }

        return restored;
    }
}
