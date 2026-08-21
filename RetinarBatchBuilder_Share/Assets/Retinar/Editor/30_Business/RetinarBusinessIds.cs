// =====================================================================================
// 门禁 / 输出的语义 Id。字面量可随文件夹改名策略留在 RetinarPaths；
// 这里的 Id 一旦写进业务 SO 就不要改含义。
// =====================================================================================

/// <summary>验收门禁语义 Id。</summary>
public static class RetinarGateIds
{
    public const string RootIdentity = "root_identity";
    public const string SafeZoneBounds = "safezone_bounds";
    public const string BoxColliderAlign = "box_collider_align";
    public const string ModelFolderClean = "model_folder_clean";
    public const string ExternalDependencies = "external_dependencies";
}

/// <summary>交付输出语义 Id。FolderName 见 <see cref="RetinarPaths"/>。</summary>
public static class RetinarDeliverableIds
{
    public const string RuntimeRequirements = "runtime_requirements";
    public const string SourceArchive = "source_archive";
    public const string UnityPackage = "unity_package";
    public const string AssetBundles = "asset_bundles";
    public const string Docs = "docs";
}
