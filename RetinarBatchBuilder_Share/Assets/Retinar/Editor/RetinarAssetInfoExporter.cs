using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RetinarAssetInfoExporter
{
    public static void ExportPlanJian8fInfo()
    {
        ExportAssetInfo(
            "Assets/Retinar/IncomingModels/Plan_jian8f.FBX",
            "C:/Users/小陶子/Documents/codex测试/tools/spreadsheet_work/plan_jian8f_info.json");
    }

    private static void ExportAssetInfo(string assetPath, string outputPath)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
        {
            throw new FileNotFoundException("Could not load asset", assetPath);
        }

        GameObject instance = Object.Instantiate(source);
        int rendererCount = 0;
        int materialCount = 0;
        int textureCount = 0;
        int meshCount = 0;
        long vertices = 0;
        long triangles = 0;
        var materialNames = new HashSet<string>();
        var textureNames = new HashSet<string>();
        var maxTextureSize = Vector2Int.zero;

        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            meshCount++;
            vertices += mesh.vertexCount;
            triangles += mesh.triangles.Length / 3;
        }

        foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = skinned.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            meshCount++;
            vertices += mesh.vertexCount;
            triangles += mesh.triangles.Length / 3;
        }

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            rendererCount++;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                materialNames.Add(material.name);
                foreach (string propertyName in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(propertyName);
                    if (texture == null)
                    {
                        continue;
                    }

                    textureNames.Add(texture.name);
                    maxTextureSize.x = Mathf.Max(maxTextureSize.x, texture.width);
                    maxTextureSize.y = Mathf.Max(maxTextureSize.y, texture.height);
                }
            }
        }

        materialCount = materialNames.Count;
        textureCount = textureNames.Count;

        Object.DestroyImmediate(instance);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, BuildJson(assetPath, meshCount, vertices, triangles, rendererCount, materialCount, textureCount, maxTextureSize), Encoding.UTF8);
        Debug.Log("Exported Retinar asset info: " + outputPath);
    }

    private static string BuildJson(string assetPath, int meshCount, long vertices, long triangles, int rendererCount, int materialCount, int textureCount, Vector2Int maxTextureSize)
    {
        return "{\n" +
            "  \"assetPath\": \"" + Escape(assetPath) + "\",\n" +
            "  \"meshCount\": " + meshCount + ",\n" +
            "  \"vertices\": " + vertices + ",\n" +
            "  \"triangles\": " + triangles + ",\n" +
            "  \"rendererCount\": " + rendererCount + ",\n" +
            "  \"materialCount\": " + materialCount + ",\n" +
            "  \"textureCount\": " + textureCount + ",\n" +
            "  \"maxTextureWidth\": " + maxTextureSize.x + ",\n" +
            "  \"maxTextureHeight\": " + maxTextureSize.y + "\n" +
            "}\n";
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
