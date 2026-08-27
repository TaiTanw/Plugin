using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   模型处理的规则数值唯一来源。不改 Mesh、不改 Importer——那些在 Model/Operations
//   与 Model/Import 里。Current 不创建资产（给导入回调用）；GetOrCreateAsset 给窗口用。
// =====================================================================================
public class ModelProcessSettings : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Plugin/TOol/ConfigData/ModelProcessSettings.asset";

    [Header("导入期 Importer 参数（设置自动）")]
    [Tooltip("FBX 导入时把材质来源设为 External（外部 .mat 由编辑器生成）。")]
    public bool modelUseExternalMaterials = true;

    [Tooltip("FBX 导入时剔除 DCC 软件带出来的灯光与摄像机节点。")]
    public bool modelStripLightsAndCameras = true;

    [Header("可处理的模型扩展名")]
    [Tooltip("后处理与设置自动都只认这些扩展名。默认含 .fbx/.glb/.gltf（与管线导入对齐）；可在 SO 增删。")]
    public List<string> supportedExtensions = new List<string> { ".fbx", ".glb", ".gltf" };

    [HideInInspector]
    public List<string> importAutoOperationIds = new List<string> { "set_vertex_colors_white" };

    /// <summary>主面板总批量 / 分项批量执行的 Operation Id（SO，团队约定）。</summary>
    [HideInInspector]
    public List<string> masterBatchOperationIds = new List<string> { "set_vertex_colors_white" };

    [Header("不介入的目录（自动流）")]
    [Tooltip("路径以此列表任一前缀开头时，仅「设置自动 / 后处理自动」（导入期钩子）跳过。默认排除 Assets/Art/。" +
             "L1「执行全部」、子面板手动、中间层⑤（代跑同一总批量）都不读本列表，可以对 Art 刷顶点色。" +
             "注意：本列表与 TextureProcessSettings.excludedPathPrefixes、BatchFbxImportSettings.deliveryAlertPathPrefixes " +
             "是三份独立配置（默认都写 Assets/Art/），改一处不会自动同步；改交付根时请三处对照。")]
    public List<string> excludedPathPrefixes = new List<string> { "Assets/Art/" };

    private static ModelProcessSettings assetInstance;
    private static ModelProcessSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public bool IsExcludedPath(string assetPath)
    {
        return ResourceExcludeUtility.IsExcludedPath(assetPath, excludedPathPrefixes);
    }

    public void EnsureMasterBatchDefaults()
    {
        EnsureSupportedExtensionsDefaults();

        const string seededKey = "TOol.MasterBatchOps.ModelSeeded";
        if (EditorPrefs.GetBool(seededKey, false))
        {
            if (masterBatchOperationIds == null)
            {
                masterBatchOperationIds = new List<string>();
            }

            return;
        }

        if (masterBatchOperationIds == null)
        {
            masterBatchOperationIds = new List<string>();
        }

        if (masterBatchOperationIds.Count == 0)
        {
            if (importAutoOperationIds != null && importAutoOperationIds.Count > 0)
            {
                masterBatchOperationIds.AddRange(importAutoOperationIds);
            }
            else
            {
                masterBatchOperationIds.Add("set_vertex_colors_white");
            }

            EditorUtility.SetDirty(this);
        }

        EditorPrefs.SetBool(seededKey, true);
    }

    /// <summary>
    /// D12 一次性迁移：空列表填默认；旧「仅 fbx」资产补 glb/gltf。之后不再强行追加，避免覆盖用户删改。
    /// </summary>
    public void EnsureSupportedExtensionsDefaults()
    {
        const string migratedKey = "TOol.ModelExt.GlbGltfMigrated.v1";

        if (supportedExtensions == null)
        {
            supportedExtensions = new List<string>();
        }

        if (EditorPrefs.GetBool(migratedKey, false))
        {
            return;
        }

        bool dirty = false;
        if (supportedExtensions.Count == 0)
        {
            supportedExtensions.Add(".fbx");
            supportedExtensions.Add(".glb");
            supportedExtensions.Add(".gltf");
            dirty = true;
        }
        else
        {
            dirty |= AddExtensionIfMissing(".glb");
            dirty |= AddExtensionIfMissing(".gltf");
        }

        if (dirty)
        {
            EditorUtility.SetDirty(this);
        }

        EditorPrefs.SetBool(migratedKey, true);
    }

    private bool AddExtensionIfMissing(string extension)
    {
        string want = extension.ToLowerInvariant();
        for (int i = 0; i < supportedExtensions.Count; i++)
        {
            string e = supportedExtensions[i];
            if (string.IsNullOrEmpty(e))
            {
                continue;
            }

            string n = e.StartsWith(".") ? e.ToLowerInvariant() : ("." + e.ToLowerInvariant());
            if (n == want)
            {
                return false;
            }
        }

        supportedExtensions.Add(want);
        return true;
    }

    public bool IsSupportedModelExtension(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || supportedExtensions == null)
        {
            return false;
        }

        string ext = Path.GetExtension(assetPath).ToLowerInvariant();
        foreach (string candidate in supportedExtensions)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            string normalized = candidate.StartsWith(".")
                ? candidate.ToLowerInvariant()
                : "." + candidate.ToLowerInvariant();
            if (ext == normalized)
            {
                return true;
            }
        }

        return false;
    }

    public static ModelProcessSettings Current
    {
        get
        {
            ModelProcessSettings found = FindExistingAsset();
            if (found != null)
            {
                return found;
            }

            if (fallbackInstance == null)
            {
                fallbackInstance = CreateInstance<ModelProcessSettings>();
                fallbackInstance.EnsureSupportedExtensionsDefaults();
            }

            if (!fallbackWarningLogged)
            {
                fallbackWarningLogged = true;
                Debug.LogWarning("[ModelProcessSettings] 工程里还没有配置资产，本次使用内存默认值。" +
                    "打开资源处理总面板会自动创建 " + DefaultAssetPath);
            }

            return fallbackInstance;
        }
    }

    public static ModelProcessSettings GetOrCreateAsset()
    {
        ModelProcessSettings found = FindExistingAsset();
        if (found != null)
        {
            return found;
        }

        EnsureAssetFolder(Path.GetDirectoryName(DefaultAssetPath).Replace("\\", "/"));
        var created = CreateInstance<ModelProcessSettings>();
        AssetDatabase.CreateAsset(created, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        assetInstance = created;
        fallbackWarningLogged = false;
        created.EnsureMasterBatchDefaults();
        Debug.Log("[ModelProcessSettings] 已创建配置资产: " + DefaultAssetPath);
        return created;
    }

    private static ModelProcessSettings FindExistingAsset()
    {
        if (assetInstance != null)
        {
            assetInstance.EnsureMasterBatchDefaults();
            return assetInstance;
        }

        assetInstance = AssetDatabase.LoadAssetAtPath<ModelProcessSettings>(DefaultAssetPath);
        if (assetInstance != null)
        {
            assetInstance.EnsureMasterBatchDefaults();
            return assetInstance;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ModelProcessSettings"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            assetInstance = AssetDatabase.LoadAssetAtPath<ModelProcessSettings>(path);
            if (assetInstance != null)
            {
                assetInstance.EnsureMasterBatchDefaults();
                return assetInstance;
            }
        }

        return null;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
