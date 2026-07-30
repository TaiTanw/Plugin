using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界（很重要，务必保持）：
//   这个类只负责在 FBX 导入【之前】设置 ModelImporter 上的各项参数，
//   全程只读写 importer 这一个引用本身，绝不使用 File/Directory 之类的 API
//   去直接改动磁盘上的源文件。源文件层面的操作（比如压缩、缩放、改字节内容）
//   一律放到专门处理"源文件"的脚本里，在这个类里做就是职责混在一起，以后很难排查
//   到底是导入设置的问题还是文件本身被改坏了。
//
// 因为本工具"只会用在专门导出文件的工程里"，所以这里不做额外的白名单路径判断，
// 直接在 On 方法里处理，用 SwitchManagerWindow.switchValue 这个总开关来控制是否生效。
// =====================================================================================
public class FBXImportProcessor : AssetPostprocessor
{
    // OnPreprocessModel 是 Unity 在导入 .fbx/.obj 等模型文件之前自动调用的回调，
    // 不需要手动注册——只要这个类继承 AssetPostprocessor 并放在 Editor 文件夹下就行。
    private void OnPreprocessModel()
    {
        // 总开关关闭时，完全不介入导入流程，行为等同于没装这个脚本。
        if (!SwitchManagerWindow.switchValue)
        {
            return;
        }

        // 只处理 .fbx，避免影响到 .obj / .blend 等其它模型格式的默认导入行为。
        if (Path.GetExtension(assetPath).ToLowerInvariant() != ".fbx")
        {
            return;
        }

        // assetImporter 是 AssetPostprocessor 基类提供的、指向"当前正在导入的这个资产"
        // 的导入器引用；这里只是把它转成 ModelImporter 类型来读写它自己的属性。
        ModelImporter importer = (ModelImporter)assetImporter;

        // 材质放在外部 .mat 文件里，而不是内嵌在 Prefab/Model 资产内部，
        // 方便后续统一管理、替换材质。
        importer.materialLocation = ModelImporterMaterialLocation.External;

        // importLights / importCameras：模型文件里常常会带 DCC 软件导出的灯光、相机节点，
        // 这些在游戏里通常用不上，关掉可以避免多余的场景对象和潜在的设置冲突。
        importer.importLights = false;
        importer.importCameras = false;

        Debug.Log("[FBXImportProcessor] 已按开关设置导入模型: " + assetPath);
    }
}
