using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>步骤 11：只执行有来源凭据的贴图绑定；不猜图、不补拷、不清空、不升失败闸。</summary>
public static partial class RetinarBatchModelBuilder
{
    private static bool BindKnownTexture(Material material, string property, Texture current, FlattenTextureIdentity identity)
    {
        string owner = AssetDatabase.GetAssetPath(material);
        string reference = AssetDatabase.GetAssetPath(current);
        if (!identity.IsInUnit(owner)) return false; // 绝不修改源材质。
        string target = identity.ResolveExact(reference);
        Texture resolved = string.IsNullOrEmpty(target) ? null : AssetDatabase.LoadAssetAtPath<Texture>(target);
        if (resolved == null)
        {
            identity.Warn("没有本次合法贴图副本", owner + "." + property, reference);
            return false;
        }
        if (resolved == current) return false;
        material.SetTexture(property, resolved);
        EditorUtility.SetDirty(material);
        return true;
    }

    private static int RemapKnownModelTextures(ModelImporter importer, FlattenTextureIdentity identity)
    {
        string modelPath = importer.assetPath;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inUnitDependencies = new List<string>();
        // 观察当前引用（可能已串到兄弟单元），但不能据此注册文件来源。
        // 已经在本单元的依赖按路径绑；不拿文件名到账本里两套图之间投票。
        foreach (string dependency in AssetDatabase.GetDependencies(modelPath, true))
        {
            if (!FlattenTextureIdentity.IsTextureFile(dependency)) continue;
            string name = Path.GetFileName(dependency);
            names.Add(name);
            if (identity.IsInUnit(dependency))
            {
                if (!inUnitDependencies.Exists(p => string.Equals(p, dependency, StringComparison.OrdinalIgnoreCase)))
                    inUnitDependencies.Add(dependency);
                continue;
            }

            if (identity.ResolveForModel(modelPath, name) == null)
                identity.Warn("模型贴图没有唯一的本次合法副本", modelPath, dependency);
        }

        foreach (string dependency in inUnitDependencies)
        {
            names.Add(Path.GetFileNameWithoutExtension(dependency));
            string fileName = Path.GetFileName(dependency);
            if (string.IsNullOrEmpty(FlattenTextureIdentity.SelectUniqueCandidate(fileName, inUnitDependencies)))
                identity.Warn("模型贴图没有唯一的本次合法副本", modelPath, dependency);
        }

        foreach (var entry in importer.GetExternalObjectMap())
        {
            if (entry.Key.type == typeof(Texture) || entry.Key.type == typeof(Texture2D)) names.Add(entry.Key.name);
        }
        foreach (string path in identity.ModelTexturePaths(modelPath))
        {
            names.Add(Path.GetFileName(path));
            names.Add(Path.GetFileNameWithoutExtension(path));
        }

        int count = 0;
        var existing = importer.GetExternalObjectMap();
        foreach (string name in names)
        {
            string path = FlattenTextureIdentity.SelectUniqueCandidate(name, inUnitDependencies);
            if (string.IsNullOrEmpty(path) && !InUnitDependenciesNameThis(name, inUnitDependencies))
                path = identity.ResolveForModel(modelPath, name);
            Texture texture = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (texture == null)
            {
                identity.Warn("贴图名称没有唯一的本次合法副本", modelPath, name);
                continue;
            }
            foreach (Type type in new[] { typeof(Texture), typeof(Texture2D) })
            {
                var key = new AssetImporter.SourceAssetIdentifier(type, name);
                UnityEngine.Object previous;
                if (existing.TryGetValue(key, out previous) && previous == texture) continue;
                importer.AddRemap(key, texture);
                count++;
            }
        }
        return count;
    }

    // 本单元依赖里已经有这个名字时，账本再空也不许改投另一张。
    static bool InUnitDependenciesNameThis(string name, List<string> paths)
    {
        bool exact = false;
        bool stem = false;
        for (int i = 0; i < paths.Count; i++)
        {
            if (string.Equals(Path.GetFileName(paths[i]), name, StringComparison.OrdinalIgnoreCase))
                exact = true;
            if (!Path.HasExtension(name) &&
                string.Equals(Path.GetFileNameWithoutExtension(paths[i]), name, StringComparison.OrdinalIgnoreCase))
                stem = true;
        }

        if (exact) return true;
        return !Path.HasExtension(name) && stem;
    }
}
