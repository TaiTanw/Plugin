using UnityEditor;

// =====================================================================================
// 导入期贴图后处理自动已关闭。本类保留为空钩子。
// =====================================================================================
public class TextureSourceFileProcessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
    }
}
