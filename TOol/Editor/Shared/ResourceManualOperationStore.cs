using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// L2 精准面板：手动执行时包含哪些 Operation（本机 EditorPrefs）。
// 主面板批量不读本 Store，改读 Settings.masterBatchOperationIds（L3 / SO）。
// =====================================================================================
public static class ResourceManualOperationStore
{
    public const string DomainTexture = "Texture";
    public const string DomainModel = "Model";

    private const string KeyPrefix = "TOol.ManualOp.";

    public static bool IsSelected(string domain, string operationId)
    {
        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(operationId))
        {
            return false;
        }

        return EditorPrefs.GetBool(KeyPrefix + domain + "." + operationId, true);
    }

    public static void SetSelected(string domain, string operationId, bool selected)
    {
        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(operationId))
        {
            return;
        }

        EditorPrefs.SetBool(KeyPrefix + domain + "." + operationId, selected);
    }

    public static List<ITextureAssetOperation> CollectSelectedTextureOperations()
    {
        var result = new List<ITextureAssetOperation>();
        foreach (ITextureAssetOperation operation in TextureOperationRegistry.All)
        {
            if (operation != null && IsSelected(DomainTexture, operation.Id))
            {
                result.Add(operation);
            }
        }

        return result;
    }

    public static List<IModelAssetOperation> CollectSelectedModelOperations()
    {
        var result = new List<IModelAssetOperation>();
        foreach (IModelAssetOperation operation in ModelOperationRegistry.All)
        {
            if (operation != null && IsSelected(DomainModel, operation.Id))
            {
                result.Add(operation);
            }
        }

        return result;
    }
}
