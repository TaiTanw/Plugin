using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 这个类原来叫 FBXImportProcessor，改名是为了让职责直接写在名字上：
// 它和 TextureImportSettingsProcessor 是完全对称的两个类，都只做一件事——
// 在导入【之前】设置 Importer 参数。
//
// 职责边界（务必保持）：
//   全程只读写 (ModelImporter)assetImporter 这一个引用本身，绝不使用 File / Directory
//   之类的 API 去动磁盘上的源文件。源文件层面的操作一律走 Operations/ 层，
//   由 TextureSourceFileProcessor 在导入结束后统一调度。
//   在这里做文件操作就是把两种职责混在一起，以后出问题分不清是"导入参数不对"
//   还是"文件本身被改坏了"。
//
// 关于 materialLocation 和目录边界（这里和打包插件曾经互相打架，改动前必读）：
//   这里设的是 External——材质作为独立的 .mat 由编辑器生成，方便后续统一替换和管理，
//   这是艺术家把 FBX 导入工程时想要的行为。
//
//   但 Retinar 打包工具（RetinarBatchModelBuilder.ApplyModelImportSettings）对它复制到
//   Assets/Art/<模型名>/Model/ 的交付工作副本有相反的硬性要求：必须是 InPrefab
//   （PACKAGING_RULES.md 规则 20/21，已于 2026-07-20 完成导入回归验收）。原因是
//   External 会让目标工程首次导入 UnityPackage 时自动生成 Materials/ 和 <FBX名>.fbm 两个
//   目录，撞上"Model 目录只允许放模型文件"的校验，直接导致打包终止。
//
//   之前两边都不设边界，于是：打包工具设 InPrefab -> SaveAndReimport -> 触发本回调改回
//   External -> 生成伴生目录 -> 校验失败终止。最终生效哪个取决于时序，极难排查。
//
//   现在按目录划清职责，不再有交集：
//     Assets/Art/**   打包工具的产物区，本插件完全不介入（见 settings.excludedPathPrefixes）
//     其它目录        艺术家的导入区，本插件负责设成 External
//   如果以后打包工具换了产物目录，改配置资产里的 excludedPathPrefixes 就行，不用改代码。
// =====================================================================================
public class ModelImportSettingsProcessor : AssetPostprocessor
{
    // OnPreprocessModel 是 Unity 在导入 .fbx/.obj 等模型文件之前自动调用的回调，
    // 不需要手动注册——只要这个类继承 AssetPostprocessor 并放在 Editor 目录下就行。
    private void OnPreprocessModel()
    {
        if (!AssetProcessSwitch.IsEnabled)
        {
            return;
        }

        // 只处理 .fbx，避免影响 .obj / .blend 等其它模型格式的默认导入行为。
        if (Path.GetExtension(assetPath).ToLowerInvariant() != ".fbx")
        {
            return;
        }

        TextureProcessSettings settings = TextureProcessSettings.Current;

        // 打包工具的产物区不介入。这一句就是上面那段说明里"划清职责"的落点，
        // 删掉它会立刻复现"打包到一半终止"的问题。
        if (settings.IsExcludedPath(assetPath))
        {
            return;
        }

        var importer = (ModelImporter)assetImporter;

        if (settings.modelUseExternalMaterials)
        {
            importer.materialLocation = ModelImporterMaterialLocation.External;

            // 必须显式指定按【材质名】命名，不能用 Unity 在 External 模式下的默认值
            // BasedOnTextureName（按贴图名命名）。踩过的坑：
            //   BasedOnTextureName 是拿材质用到的贴图名去生成/查找外部 .mat。这就要求
            //   "每个材质都有贴图，且贴图各不相同"。一旦某个材质没有贴图、或者两个材质
            //   共用同一张贴图，就会少生成 .mat——模型上对应的材质槽解析不到外部材质，
            //   在编辑器里直接显示成紫色。本工程这个 FBX 就是这样：里面有 4 个材质，
            //   却只生成了 3 个 .mat。
            //   BasedOnMaterialName 直接用 FBX 里的材质名，和材质是严格一对一的，
            //   不依赖贴图，多少个材质就是多少个 .mat。打包工具那边用的也是这一项。
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        }

        if (settings.modelStripLightsAndCameras)
        {
            // 模型文件常常带着 DCC 软件导出的灯光、相机节点，游戏里用不上，
            // 关掉可以避免多余的场景对象和潜在的设置冲突。
            importer.importLights = false;
            importer.importCameras = false;
        }

        Debug.Log("[ModelImportSettingsProcessor] 已按开关设置导入模型: " + assetPath);
    }
}
