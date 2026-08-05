using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// 子面板与总面板共用的「手动执行时包含哪些 Operation」勾选状态。
// 按资源域 + operation Id 存 EditorPrefs；缺省为勾选（与原先窗口默认 true 对齐）。
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
