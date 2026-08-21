// =====================================================================================
// 业务验收门禁扩展点（对齐插件 2 ITextureAssetOperation）。
//
// 本期不接线：Legacy 仍硬编码 ValidatePrefabSpatialPlacement /
// ValidateModelFoldersAreClean / ValidateExternalDependencies。
// 总面板 + 反射注册表以后再做。新增门禁时：
//   1) 实现本接口，Id 用 RetinarGateIds 常量；
//   2) 语义 Id 发布后不得改含义；
//   3) 是否启用写在 RetinarBusinessProfile.enabledGateIds。
// 门禁只检查、可阻断出包，不得改 Prefab。套壳 / SafeZone 缩放属于平铺内核。
// =====================================================================================

/// <summary>单资产业务验收。未勾选则导出跳过该项。</summary>
public interface IRetinarAcceptanceGate
{
    /// <summary>冻结语义键，写入业务 SO。发布后不得改含义。</summary>
    string Id { get; }

    string DisplayName { get; }

    int Order { get; }

    /// <summary>通过返回 null 或空串；失败返回写入 diagnostics 的原因。</summary>
    string Validate(string prefabPath, string assetFolder, string assetName);
}
