using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;

/// <summary>
/// ④ 单行贴图来源账本：只认迁移前依赖的副本、本轮 Extract 新写出的文件、
/// 以及本单元内与冻结来源字节相同的文件（伴生搬移后的套层 GUID）。
/// 不读 ctx，不做文件复制，不决定批量是否继续；不存在全局可变状态。
/// </summary>
public sealed class FlattenTextureIdentity
{
    readonly string unitFolder;
    readonly HashSet<string> sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> sourceFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string[]> modelSources = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HashSet<string>> modelTextures = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> trustedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> warningKeys = new HashSet<string>(StringComparer.Ordinal);
    public readonly List<string> Warnings = new List<string>();

    public FlattenTextureIdentity(string assetFolder)
    {
        unitFolder = Normalize(assetFolder).TrimEnd('/');
    }

    public static FlattenTextureIdentity Capture(string sourcePrefab, string assetFolder)
    {
        var identity = new FlattenTextureIdentity(assetFolder);
        // Begin 写 Art 之前冻结。后续重导的 GetDependencies 只能用于观察，不能扩充来源。
        foreach (string path in AssetDatabase.GetDependencies(sourcePrefab, true))
        {
            identity.sourcePaths.Add(Normalize(path));
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if ((IsTextureFile(path) || ext == ".fbx" || ext == ".obj") && File.Exists(path))
                identity.sourceFingerprints[Normalize(path)] = Fingerprint(path);
            if (ext == ".fbx" || ext == ".obj")
                identity.modelSources[Normalize(path)] = AssetDatabase.GetDependencies(path, true);
        }
        return identity;
    }

    public bool IsOriginalSource(string path) { return sourcePaths.Contains(Normalize(path)); }

    public bool IsInUnit(string path)
    {
        return !string.IsNullOrEmpty(unitFolder) && Normalize(path).StartsWith(unitFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsTrustedLocal(string path)
    {
        return IsInUnit(path) && trustedGuids.Contains(AssetDatabase.AssetPathToGUID(path));
    }

    public void RegisterCopies(IDictionary<string, string> copied)
    {
        if (copied == null) return;
        foreach (var pair in copied)
        {
            if (!IsOriginalSource(pair.Key) || !IsInUnit(pair.Value) || !IsTextureFile(pair.Key)) continue;
            // 人工不清夹：同名旧文件不能仅因位置正确就成为合法副本。
            if (!MatchesCapturedSource(pair.Key, pair.Value))
            {
                Warn("副本内容未核对一致（可能为旧同名文件）", pair.Key, pair.Value);
                continue;
            }
            string guid = AssetDatabase.AssetPathToGUID(pair.Value);
            if (string.IsNullOrEmpty(guid)) continue;
            copies[Normalize(pair.Key)] = guid;
            trustedGuids.Add(guid);
        }
        foreach (var model in modelSources)
        {
            string target;
            if (!copied.TryGetValue(model.Key, out target)) continue;
            string modelGuid = AssetDatabase.AssetPathToGUID(target);
            foreach (string texture in model.Value)
            {
                string textureGuid;
                if (copies.TryGetValue(Normalize(texture), out textureGuid)) AddModelTexture(modelGuid, textureGuid);
            }
        }
    }

    public string ResolveExact(string sourcePath)
    {
        if (IsTrustedLocal(sourcePath)) return Normalize(sourcePath);
        string guid;
        if (!copies.TryGetValue(Normalize(sourcePath), out guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return IsInUnit(path) ? path : null;
    }

    public string ResolveForModel(string modelPath, string textureName)
    {
        HashSet<string> guids;
        if (!modelTextures.TryGetValue(AssetDatabase.AssetPathToGUID(modelPath), out guids)) return null;
        return SelectUniqueCandidate(textureName, guids.Select(AssetDatabase.GUIDToAssetPath).Where(IsInUnit));
    }

    // 名称只是本模型已知来源内的别名，不是全单元/兄弟单元的搜索条件。
    public static string SelectUniqueCandidate(string name, IEnumerable<string> candidates)
    {
        string[] paths = candidates.Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] exact = paths.Where(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (exact.Length > 0) return PreferUniqueFbmIfAmbiguous(exact);
        // 带后缀却没匹配，不把 hull.jpg 猜成 hull.png。
        if (Path.HasExtension(name)) return null;
        string[] stem = paths.Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return PreferUniqueFbmIfAmbiguous(stem);
    }

    public IEnumerable<string> ModelTexturePaths(string modelPath)
    {
        HashSet<string> guids;
        return modelTextures.TryGetValue(AssetDatabase.AssetPathToGUID(modelPath), out guids)
            ? guids.Select(AssetDatabase.GUIDToAssetPath).Where(IsInUnit).ToArray()
            : new string[0];
    }

    public Dictionary<string, string> SnapshotExtractFolder(string folder)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder)) return result;
        foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            if (IsTextureFile(path)) result[Normalize(path)] = Fingerprint(path);
        return result;
    }

    // CopyAsset 导入 FBX 也会写伴生图；只观察已登记模型对应的 <模型名>.fbm。
    public Dictionary<string, string> SnapshotModelImport(string sourceModel, string targetModel)
    {
        if (!modelSources.ContainsKey(Normalize(sourceModel)) || !IsInUnit(targetModel) ||
            !string.Equals(Path.GetExtension(sourceModel), ".fbx", StringComparison.OrdinalIgnoreCase))
            return null;
        return SnapshotExtractFolder(ModelCompanionFolder(targetModel));
    }

    public void RegisterModelImport(string sourceModel, string targetModel, IDictionary<string, string> before)
    {
        if (before == null || !modelSources.ContainsKey(Normalize(sourceModel)) || !IsInUnit(targetModel)) return;
        if (!MatchesCapturedSource(sourceModel, targetModel))
        {
            Warn("模型副本与 Begin 冻结字节不一致，未接纳自动生成伴生", targetModel, sourceModel);
            return;
        }
        RegisterExtracted(targetModel, ModelCompanionFolder(targetModel), before);
    }

    static string ModelCompanionFolder(string modelPath)
    {
        return Normalize(Path.Combine(Path.GetDirectoryName(modelPath), Path.GetFileNameWithoutExtension(modelPath) + ".fbm"));
    }

    /// <summary>伴生搬移后的套层落点：image/Texture/&lt;模型名&gt;.fbm/。</summary>
    public static string DestFbmFolder(string artModelPath, string textureFolder)
    {
        return Normalize((textureFolder ?? string.Empty).TrimEnd('/') + "/" +
                         Path.GetFileNameWithoutExtension(artModelPath) + ".fbm");
    }

    public void RegisterExtracted(string modelPath, string folder, IDictionary<string, string> before)
    {
        if (before == null) before = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in SnapshotExtractFolder(folder))
        {
            string oldHash;
            if (before.TryGetValue(pair.Key, out oldHash) && oldHash == pair.Value) continue;
            if (!IsInUnit(pair.Key)) continue;
            string guid = AssetDatabase.AssetPathToGUID(pair.Key);
            if (string.IsNullOrEmpty(guid)) continue;
            trustedGuids.Add(guid);
            AddModelTexture(AssetDatabase.AssetPathToGUID(modelPath), guid);
        }
    }

    /// <summary>
    /// 伴生/抽出搬进本单元后：与冻结来源字节相同的贴图补进 trustedGuids。
    /// 不改 copies（一份来源仍只记一条拷贝落点）。
    /// </summary>
    public void TrustMatchingUnitTextures()
    {
        if (string.IsNullOrEmpty(unitFolder) || !AssetDatabase.IsValidFolder(unitFolder))
        {
            return;
        }

        var sourceHashes = new HashSet<string>(sourceFingerprints
            .Where(pair => IsTextureFile(pair.Key)).Select(pair => pair.Value), StringComparer.Ordinal);

        if (sourceHashes.Count == 0)
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { unitFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            if (trustedGuids.Contains(guids[i]))
            {
                continue;
            }

            string path = Normalize(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (!IsTextureFile(path) || !IsInUnit(path) || !File.Exists(path))
            {
                continue;
            }

            if (sourceHashes.Contains(Fingerprint(path)))
                trustedGuids.Add(guids[i]);
        }
    }

    public void Warn(string reason, string owner, string reference)
    {
        string message = "[Flatten][TextureIdentity] 单元=" + unitFolder + "\n  位置=" + owner +
                         "\n  贴图=" + reference + "\n  " + reason +
                         "；保留原引用，不按同名补拷/改绑。仅警告，继续后续流程；外部引用仍可能进入交付包。";
        if (!warningKeys.Add(message)) return;
        Warnings.Add(message);
        UnityEngine.Debug.LogWarning(message);
    }

    void AddModelTexture(string modelGuid, string textureGuid)
    {
        if (string.IsNullOrEmpty(modelGuid)) return;
        HashSet<string> textures;
        if (!modelTextures.TryGetValue(modelGuid, out textures))
            modelTextures[modelGuid] = textures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        textures.Add(textureGuid);
    }

    static string PreferUniqueFbmIfAmbiguous(string[] matches)
    {
        if (matches == null || matches.Length == 0) return null;
        if (matches.Length == 1) return matches[0];
        string[] nested = matches.Where(IsInsideFbmFolder).ToArray();
        return nested.Length == 1 ? nested[0] : null;
    }

    static bool IsInsideFbmFolder(string path)
    {
        string[] segments = Normalize(path).Split('/');
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].EndsWith(".fbm", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    bool MatchesCapturedSource(string source, string copy)
    {
        string hash;
        return sourceFingerprints.TryGetValue(Normalize(source), out hash) &&
               File.Exists(copy) && Fingerprint(copy) == hash;
    }

    static string Fingerprint(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(stream));
    }

    public static bool IsTextureFile(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".png": case ".jpg": case ".jpeg": case ".tga": case ".tif": case ".tiff":
            case ".bmp": case ".psd": case ".exr": case ".hdr": return true;
            default: return false;
        }
    }

    static string Normalize(string path) { return (path ?? string.Empty).Replace('\\', '/'); }
}
