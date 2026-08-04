using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   这个类【只】负责"规则数值"的存放与读取，是整套贴图处理流程唯一的参数来源。
//   它不解码任何图片、不读写任何贴图文件、不设置任何 Importer——所有实际动作
//   都在 Operations/ 和 Codec/ 下面的类里做。把参数单独拎出来的好处是：
//   以后调阈值、调质量只改这一个资产，不用碰任何一行代码，也不会连带影响流程。
//
// 为什么用 ScriptableObject 资产而不是 EditorPrefs：
//   阈值这类东西是"团队约定"，必须所有人一致，所以要能随工程一起提交进版本库。
//   EditorPrefs 存在每台机器的注册表里，A 改了 B 看不到，出包结果会因人而异。
//   相对的，"我现在这台机器要不要介入导入"这种临时状态才适合放 EditorPrefs，
//   那部分在 AssetProcessSwitch 里。
//
// 关于 Current 和 GetOrCreateAsset 的区别（很重要）：
//   GetOrCreateAsset 会在资产不存在时创建它，只能由窗口/菜单这类"用户主动操作"
//   的入口调用。Current 不会创建任何资产，因为它要在 AssetPostprocessor 回调里被
//   读到——在导入回调的调用栈里调 AssetDatabase.CreateAsset 会引发嵌套导入，
//   属于必须避免的操作。所以 Current 在找不到资产时退回一份纯内存的默认值。
// =====================================================================================
/// <summary>
/// 纹理操作配置数据（SO）
/// </summary>
public class TextureProcessSettings : ScriptableObject
{
    // 默认存放位置。如果你们把这套插件整体挪到别的目录，不用改这里——
    // 下面的查找逻辑会先在整个工程里搜一遍同类型资产，搜到就用搜到的那一份。
    public const string DefaultAssetPath = "Assets/Plugin/TOol/TextureProcessSettings.asset";

    [Header("源文件体积上限")]
    [Tooltip("磁盘上的贴图源文件超过这个体积才会触发压缩。\n" +
             "不要填大于 5 的值：交付打包工具把 5MB 作为硬性告警线（PACKAGING_RULES 规则 28），" +
             "这里放宽只会让超标贴图一路走到交付报告里才被发现。")]
    public float maxSourceMegabytes = 5f;

    [Header("缩放边界")]
    [Tooltip("缩放后允许的最小边长，防止把小图压成几个像素。")]
    public int minDimension = 64;

    [Tooltip("源图长宽都是二的幂时，只在 2048→1024→512 这样的对折阶梯上取尺寸，" +
             "保证缩放后仍是二的幂。交付打包工具会把非二的幂贴图记为问题项，所以默认开启。\n" +
             "关掉会改用连续二分（尺寸更贴近阈值、画质损失更小），但结果通常不是二的幂。")]
    public bool preservePowerOfTwo = true;

    [Tooltip("重新编码 JPG 时使用的质量（1-100）。PNG 是无损格式，不受这一项影响。")]
    [Range(1, 100)]
    public int jpgQuality = 90;

    [Tooltip("二分搜索最大迭代次数。每一次迭代都要完整编码一遍图片，" +
             "次数越多越接近\"刚好达标的最大尺寸\"，但耗时也越长。12 次足够覆盖 8K 到 64 像素。")]
    public int maxSearchIterations = 12;

    [Header("TGA 重新编码")]
    [Tooltip("勾选后写回 TGA 时使用 RLE 行程压缩（体积更小，所有主流 DCC 软件都能读）；" +
             "取消勾选则写未压缩 TGA（兼容性最好，但文件大得多，通常压不到阈值以下）。")]
    public bool tgaUseRunLengthEncoding = true;

    [Header("TGA 转 PNG")]
    [Tooltip("转换成功后删除原 .tga 文件。会把原来的 .tga.meta 改名成 .png.meta 来保住 GUID，" +
             "所以材质引用不会断。取消勾选则两个文件并存，需要你自己处理引用。")]
    public bool deleteTgaAfterConvert = true;

    [Header("亮度写入 Alpha（旋翼/光晕）")]
    [Tooltip("亮度 = max(R,G,B)。低于此值的像素 Alpha 直接置 0，并清掉 RGB，" +
             "用来砍掉黑底周边的半透明影子。0 = 只把纯黑切透明；调高会切掉更多暗边。")]
    [Range(0, 255)]
    public int luminanceAlphaCutoff = 24;

    [Tooltip("勾选后，把「阈值 ~ 255」的亮度重新映射到 Alpha 0~255，" +
             "软边从阈值处重新起算，避免刚过阈值的像素仍带着一层灰雾。")]
    public bool luminanceAlphaRemapAboveCutoff = true;

    [Tooltip("勾选后，保留的像素 RGB 改写成灰度（等于亮度）。" +
             "彩色源图在半透明边缘容易显得过分多彩时打开；需要保留旋翼本身颜色时关掉。")]
    public bool luminanceAlphaWriteGrayscaleRgb = false;

    [Header("导入期自动执行的操作")]
    [Tooltip("填写操作的 Id（见贴图处理窗口里每个操作后面标注的 Id）。" +
             "留空表示导入时什么都不自动做，全靠窗口里手动触发。")]
    public List<string> importAutoOperationIds = new List<string> { "shrink_source_file" };

    [Header("导入期 Importer 参数")]
    [Tooltip("FBX 导入时把材质来源设为 External（外部 .mat 由编辑器生成）。")]
    public bool modelUseExternalMaterials = true;

    [Tooltip("FBX 导入时剔除 DCC 软件带出来的灯光与摄像机节点。")]
    public bool modelStripLightsAndCameras = true;

    [Tooltip("贴图导入时关闭 Read/Write，避免运行时常驻一份可读写的 CPU 端副本。")]
    public bool textureDisableReadWrite = true;

    [Header("不介入的目录")]
    [Tooltip("这些路径前缀下的资产完全不被本插件的导入回调处理。\n" +
             "默认排除 Assets/Art —— 那是 Retinar 打包工具生成的交付工作副本目录，" +
             "它对 FBX 的 materialLocation 有自己的硬性要求（必须 InPrefab），" +
             "两边都去设置会互相覆盖并导致打包终止。")]
    public List<string> excludedPathPrefixes = new List<string> { "Assets/Art/" };

    /// <summary>
    /// 判断某个资产是否落在"不介入"的目录里。
    ///
    /// 这个开关存在的原因（改动前务必读懂，否则会复现一个已经修好的打包终止问题）：
    ///   Retinar 打包工具会把 FBX 复制到 Assets/Art/&lt;模型名&gt;/Model/ 作为交付工作副本，
    ///   并按它自己的规范把 materialLocation 设为 InPrefab，然后调 SaveAndReimport()。
    ///   而 SaveAndReimport 会触发本插件的 OnPreprocessModel，如果这里不加判断，
    ///   就会把它改回 External；External 意味着 Unity 在 Model/ 旁边生成 Materials/ 和
    ///   &lt;FBX名&gt;.fbm 两个目录，正好撞上打包工具"Model 目录只允许放模型文件"的校验，
    ///   打包终止。
    ///   分界线很清晰：Assets/Art 是打包工具的产物区，归它管；
    ///   其它目录是艺术家的导入区，归本插件管（材质来源外部、编辑器生成）。
    /// </summary>
    public bool IsExcludedPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || excludedPathPrefixes == null)
        {
            return false;
        }

        string normalized = assetPath.Replace("\\", "/");
        foreach (string prefix in excludedPathPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) &&
                normalized.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static TextureProcessSettings assetInstance;
    private static TextureProcessSettings fallbackInstance;
    private static bool fallbackWarningLogged;

    public long MaxSourceBytes
    {
        get { return (long)(maxSourceMegabytes * 1024f * 1024f); }
    }

    /// <summary>
    /// 只读取、绝不创建资产。给 AssetPostprocessor 回调这类"不能碰 AssetDatabase 写操作"
    /// 的地方用；找不到配置资产时返回一份纯内存的默认值，保证流程不会因为缺配置而中断。
    /// </summary>
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
                Debug.LogWarning("[TextureProcessSettings] 工程里还没有配置资产，本次使用内存里的默认值。" +
                    "打开 Tools/贴图处理工具 窗口会自动创建 " + DefaultAssetPath + "，创建后即可修改阈值。");
            }

            return fallbackInstance;
        }
    }

    /// <summary>
    /// 读取配置资产，不存在则创建。只能由窗口、菜单这类用户主动触发的入口调用，
    /// 不要在导入回调里调用（AssetDatabase 写操作会引发嵌套导入）。
    /// </summary>
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

        // 兜底：插件被挪到别的目录、或者配置资产被人为改名/移动过，都从这里找回来。
        // 这样就不会重现"只是移动了文件位置，工具就不工作了"这类问题。
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
        // 在 Inspector 里手滑填了 0 或负数会让二分搜索死循环 / 除零，这里直接夹住。
        maxSourceMegabytes = Mathf.Max(0.05f, maxSourceMegabytes);
        minDimension = Mathf.Clamp(minDimension, 1, 8192);
        maxSearchIterations = Mathf.Clamp(maxSearchIterations, 1, 32);
        luminanceAlphaCutoff = Mathf.Clamp(luminanceAlphaCutoff, 0, 255);
    }
}
