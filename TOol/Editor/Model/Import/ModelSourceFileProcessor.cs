using UnityEditor;
using UnityEngine;

// =====================================================================================
// 导入期后处理自动已关闭。本类保留为空钩子，避免旧引用丢失。
// 刷白 / 压图：资源面板手动或管线⑤（triggeredByImport=false）。
// =====================================================================================
public class ModelSourceFileProcessor : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject root)
    {
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
    }
}
