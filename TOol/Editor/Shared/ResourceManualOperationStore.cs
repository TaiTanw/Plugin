using System.Collections.Generic;
using UnityEditor;

// =====================================================================================
// L2 精准面板：手动执行时包含哪些 Operation（本机 EditorPrefs）。
// L1 总面板读 TOol/ConfigData.masterBatchOperationIds；管线⑤读 Pipeline/ConfigData 同名字段。互不读取。
// =====================================================================================
public static class ResourceManualOperationStore
{
    public const string DomainTexture = "Texture";
    public const string DomainModel = "Model";
    public const string DomainMaterial = "Material";

    private const string KeyPrefix = "TOol.ManualOp.";
    private const string LuminanceFollowFadeKey =
        KeyPrefix + "Texture.bake_luminance_to_alpha.FollowMaterialsToFade";

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

    /// <summary>精准面板「亮度写入 Alpha」附加项。本机 Prefs，默认关，不进 SO。</summary>
    public static bool IsLuminanceFollowMaterialsToFade()
    {
        return EditorPrefs.GetBool(LuminanceFollowFadeKey, false);
    }

    public static void SetLuminanceFollowMaterialsToFade(bool follow)
    {
        EditorPrefs.SetBool(LuminanceFollowFadeKey, follow);
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

    public static List<IMaterialAssetOperation> CollectSelectedMaterialOperations()
    {
        var result = new List<IMaterialAssetOperation>();
        foreach (IMaterialAssetOperation operation in MaterialOperationRegistry.All)
        {
            if (operation != null && IsSelected(DomainMaterial, operation.Id))
            {
                result.Add(operation);
            }
        }

        return result;
    }
}
