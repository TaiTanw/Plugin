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
    public const string PipelineAssetPath =
        ProcessSettingsOwnership.PipelineConfigRoot + "/ModelProcessSettings.asset";

    [Header("导入区 Importer · 基线（配置导入根内必写，无开关）")]
    [Tooltip("导入时剔除 DCC 带出来的灯光与摄像机节点。\n" +
             "必须在 ③ 生成 Prefab 之前生效：③ 存出的是独立 Prefab 资产，" +
             "相机灯光一旦烤成节点，④ 再写 importCameras=false 也回收不掉。")]
    public bool modelStripLightsAndCameras = true;

    [Tooltip(".obj 导入时把法线设为 Calculate。\n" +
             "Wavefront OBJ 常无作者法线，Import 会每 Mesh 打一条 has no normals 警告；" +
             "百级子网格时能压垮 Console。代价：源文件真带 vn 时会改用重算法线。")]
    public bool modelCalculateNormalsForObj = true;

    [Header("导入区 Importer · 策略（本份 SO 勾选即导入回调；默认关）")]
    [Tooltip("导入时把材质来源设为 External（外部 .mat 由编辑器生成）。\n" +
             "D24-3：④ 拆分前保持关闭。开启会让 Incoming 旁边生成 Materials/，" +
             "改变 ④ 的输入（历史上与插件 1 的 InPrefab 互相覆盖过）。\n" +
             "编排「全局导入设置（2）」与人工高级设置各一份资产，互不影响。")]
    public bool modelUseExternalMaterials = false;

    [Header("可处理的模型扩展名")]
    [Tooltip("设置自动、后处理自动、⑤ 收集都只认这些扩展名。默认与 ToolImportApi 的 1 入库白名单对齐（.fbx/.glb/.gltf/.obj）；可在 SO 增删。\n" +
             "注意：这不是 1 入库白名单本身，那份在 ToolImportApi。两处必须同步，否则会出现「① 能入库但 ⑤ 不认」的资产。")]
    public List<string> supportedExtensions = new List<string> { ".fbx", ".glb", ".gltf", ".obj" };

    [HideInInspector]
    public List<string> importAutoOperationIds = new List<string> { "set_vertex_colors_white" };

    /// <summary>主面板总批量 / 分项批量执行的 Operation Id（SO，团队约定）。</summary>
    [HideInInspector]
    public List<string> masterBatchOperationIds = new List<string> { "set_vertex_colors_white" };

    [Header("不介入的目录（自动流）")]
    [Tooltip("路径以此列表任一前缀开头时，历史上仅「设置自动 / 后处理自动」跳过。默认排除 Assets/Art/。" +
             "现网 OnPreprocess 与⑤总批量都不读本列表；仅残留在全局导入设置高级折叠。" +
             "注意：与 TextureProcessSettings.excludedPathPrefixes、BatchFbxImportSettings.deliveryAlertPathPrefixes " +
             "是三份独立配置，改一处不会自动同步。")]
    public List<string> excludedPathPrefixes = new List<string> { "Assets/Art/" };

    private static ModelProcessSettings assetInstance;
    private static ModelProcessSettings pipelineInstance;
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
    /// 一次性迁移，每批一个键：置位后不再强行追加，避免覆盖用户的删改。
    /// D12 补 glb/gltf；D26 补 obj（① 一直能入库 OBJ，这张表却漏了，见 backlog D26-3）。
    /// </summary>
    public void EnsureSupportedExtensionsDefaults()
    {
        if (supportedExtensions == null)
        {
            supportedExtensions = new List<string>();
        }

        bool dirty = false;
        if (supportedExtensions.Count == 0)
        {
            supportedExtensions.Add(".fbx");
            supportedExtensions.Add(".glb");
            supportedExtensions.Add(".gltf");
            supportedExtensions.Add(".obj");
            dirty = true;
        }
        else
        {
            dirty |= MigrateOnce("TOol.ModelExt.GlbGltfMigrated.v1", ".glb", ".gltf");
            dirty |= MigrateOnce("TOol.ModelExt.ObjMigrated.v1", ".obj");
        }

        if (dirty)
        {
            EditorUtility.SetDirty(this);
        }
    }

    private bool MigrateOnce(string migratedKey, params string[] extensions)
    {
        if (EditorPrefs.GetBool(migratedKey, false))
        {
            return false;
        }

        bool dirty = false;
        for (int i = 0; i < extensions.Length; i++)
        {
            dirty |= AddExtensionIfMissing(extensions[i]);
        }

        EditorPrefs.SetBool(migratedKey, true);
        return dirty;
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

    public static ModelProcessSettings GetOrCreatePipelineAsset()
    {
        if (pipelineInstance != null)
        {
            pipelineInstance.EnsureMasterBatchDefaults();
            return pipelineInstance;
        }

        pipelineInstance = AssetDatabase.LoadAssetAtPath<ModelProcessSettings>(PipelineAssetPath);
        if (pipelineInstance != null)
        {
            pipelineInstance.EnsureMasterBatchDefaults();
            return pipelineInstance;
        }

        ProcessSettingsOwnership.EnsureAssetFolder(
            Path.GetDirectoryName(PipelineAssetPath).Replace("\\", "/"));
        var created = CreateInstance<ModelProcessSettings>();
        created.modelUseExternalMaterials = false;
        AssetDatabase.CreateAsset(created, PipelineAssetPath);
        AssetDatabase.SaveAssets();
        pipelineInstance = created;
        created.EnsureMasterBatchDefaults();
        Debug.Log("[ModelProcessSettings] 已创建编排配置资产: " + PipelineAssetPath);
        return created;
    }

    /// <summary>导入回调：有编排资产则用编排，否则人工 Current。</summary>
    public static ModelProcessSettings ForImportCallbacks()
    {
        ModelProcessSettings pipeline =
            AssetDatabase.LoadAssetAtPath<ModelProcessSettings>(PipelineAssetPath);
        if (pipeline != null)
        {
            return pipeline;
        }

        return Current;
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
            if (ProcessSettingsOwnership.IsPipelineConfigPath(path))
            {
                continue;
            }

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
