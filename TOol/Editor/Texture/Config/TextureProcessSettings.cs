using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 贴图处理规则数值唯一来源。模型 Importer 字段已拆到 ModelProcessSettings。
// =====================================================================================
public class TextureProcessSettings : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/Plugin/TOol/ConfigData/TextureProcessSettings.asset";

    [Header("源文件体积上限（压缩操作）")]
    [Tooltip("磁盘上源文件超过该体积（MB）才会触发「压缩超标的贴图源文件」。" +
             "请勿大于 5：插件 1（Retinar）交付告警线硬编码为 5MB，调高会导致超标图一路进到交付报告才暴露。")]
    public float maxSourceMegabytes = 5f;

    [Header("缩放搜索（压缩操作）")]
    [Tooltip("压缩时允许缩到的最短边下限（像素）。再小仍超标会报失败，不会静默写出超标文件。")]
    public int minDimension = 64;

    [Tooltip("开启后：源图宽高都是二的幂时，只在对折阶梯（如 2048→1024→512）上取最大达标尺寸，" +
             "避免连续二分得到非二的幂、与交付报告的 POT 检查打架。源图本身不是二的幂时仍走连续二分。")]
    public bool preservePowerOfTwo = true;

    [Tooltip("编码为 JPG 时的质量（1–100）。仅影响 JPG Codec；PNG/TGA 不受此项约束。")]
    [Range(1, 100)]
    public int jpgQuality = 90;

    [Tooltip("压缩搜索的最大试编码次数（含原尺寸重编码与二分/对折各步）。越大越精细，批量时更慢。建议 8–16。")]
    public int maxSearchIterations = 12;

    [Header("TGA 编解码 / 转 PNG")]
    [Tooltip("压缩或写回 TGA 时是否使用 RLE。多数未压缩 TGA 仅开 RLE、不降分辨率就能压进阈值。")]
    public bool tgaUseRunLengthEncoding = true;

    [Tooltip("执行「TGA 转 PNG」成功后是否删除原 TGA（及其 .meta）。仅影响转换操作，不影响压缩写回。")]
    public bool deleteTgaAfterConvert = true;

    [Header("亮度写入 Alpha（BakeLuminanceToAlpha）")]
    [Tooltip("亮度低于该阈值（0–255）的像素：Alpha 置 0（或按实现裁切）。用于玻璃/镂空类从 RGB 亮度生成遮罩。")]
    [Range(0, 255)]
    public int luminanceAlphaCutoff = 24;

    [Tooltip("开启后：亮度高于 cutoff 的像素，Alpha 按亮度做归一化映射；关闭则高于 cutoff 的 Alpha 固定为 1。")]
    public bool luminanceAlphaRemapAboveCutoff = true;

    [Tooltip("开启后：把 RGB 写成灰度（便于预览遮罩）；关闭则保留原 RGB，只改 Alpha。")]
    public bool luminanceAlphaWriteGrayscaleRgb = false;

    [Header("导入期自动执行的后处理操作")]
    [Tooltip("填写操作 Id（与贴图子面板列表一致）。总开关 +「贴图·后处理自动」开启时，导入后 delayCall 只跑这里列出的操作。" +
             "常用 Id：shrink_source_file（压缩）、convert_tga_to_png、bake_luminance_to_alpha。" +
             "留空 = 后处理自动打开也不会跑任何贴图操作。手动执行不受此列表限制（面板勾选即可）。")]
    public List<string> importAutoOperationIds = new List<string> { "shrink_source_file" };

    [Header("导入期 Importer 参数（设置自动）")]
    [Tooltip("总开关 +「贴图·设置自动」开启时：导入前关闭 TextureImporter 的 Read/Write。" +
             "可减小内存；运行时若脚本要 GetPixels 需自行再打开。不影响磁盘源文件体积。")]
    public bool textureDisableReadWrite = true;

    [Header("不介入的目录（仅自动流）")]
    [Tooltip("路径以此列表任一前缀开头时，设置自动与后处理自动都跳过。默认排除 Assets/Art/（插件 1 交付产物区）。" +
             "子面板手动处理仍可对 Art 内贴图执行。改前缀后请与插件 1 的 Art 根目录保持一致。")]
    public List<string> excludedPathPrefixes = new List<string> { "Assets/Art/" };

    private static TextureProcessSettings assetInstance;
    private static TextureProcessSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public long MaxSourceBytes
    {
        get { return (long)(maxSourceMegabytes * 1024f * 1024f); }
    }

    public bool IsExcludedPath(string assetPath)
    {
        return ResourceExcludeUtility.IsExcludedPath(assetPath, excludedPathPrefixes);
    }

    public static TextureProcessSettings Current
    {
        get
        {
            TextureProcessSettings found = FindExistingAsset();
            if (found != null)
            {
                return found;
            }

            if (fallbackInstance == null)
            {
                fallbackInstance = CreateInstance<TextureProcessSettings>();
            }

            if (!fallbackWarningLogged)
            {
                fallbackWarningLogged = true;
                Debug.LogWarning("[TextureProcessSettings] 工程里还没有配置资产，本次使用内存默认值。" +
                    "打开资源处理总面板会自动创建 " + DefaultAssetPath);
            }

            return fallbackInstance;
        }
    }

    public static TextureProcessSettings GetOrCreateAsset()
    {
        TextureProcessSettings found = FindExistingAsset();
        if (found != null)
        {
            return found;
        }

        EnsureAssetFolder(Path.GetDirectoryName(DefaultAssetPath).Replace("\\", "/"));
        var created = CreateInstance<TextureProcessSettings>();
        AssetDatabase.CreateAsset(created, DefaultAssetPath);
        AssetDatabase.SaveAssets();
        assetInstance = created;
        fallbackWarningLogged = false;
        Debug.Log("[TextureProcessSettings] 已创建配置资产: " + DefaultAssetPath);
        return created;
    }

    private static TextureProcessSettings FindExistingAsset()
    {
        if (assetInstance != null)
        {
            return assetInstance;
        }

        assetInstance = AssetDatabase.LoadAssetAtPath<TextureProcessSettings>(DefaultAssetPath);
        if (assetInstance != null)
        {
            return assetInstance;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:TextureProcessSettings"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            assetInstance = AssetDatabase.LoadAssetAtPath<TextureProcessSettings>(path);
            if (assetInstance != null)
            {
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

    private void OnValidate()
    {
        maxSourceMegabytes = Mathf.Max(0.05f, maxSourceMegabytes);
        minDimension = Mathf.Clamp(minDimension, 1, 8192);
        maxSearchIterations = Mathf.Clamp(maxSearchIterations, 1, 32);
        luminanceAlphaCutoff = Mathf.Clamp(luminanceAlphaCutoff, 0, 255);
    }
}
