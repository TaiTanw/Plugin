// =====================================================================================
// 40_Api — AB/交付构建选项（管线⑥与成品直通共用）
// =====================================================================================

/// <summary>
/// 出包选项。默认对齐现网 Deliverables / AssetBundles；管线默认不打 UnityPackage。
/// </summary>
public sealed class RetinarAbBuildOptions
{
    /// <summary>工程根下交付目录名或相对路径（默认 Deliverables）。</summary>
    public string DeliverableRoot = RetinarPaths.DeliverableRoot;

    /// <summary>工程根下 BuildPipeline 原始 AB 输出目录（默认 AssetBundles）。</summary>
    public string AssetBundleRoot = RetinarPaths.AssetBundleRoot;

    /// <summary>是否额外导出 UnityPackage 到 Deliverables/&lt;名&gt;/02_unity。</summary>
    public bool ExportUnityPackage;

    /// <summary>是否把打好的 AB 拷到 Deliverables/…/03_assetbundles。</summary>
    public bool CopyAbToDeliverables = true;

    /// <summary>禁止确认弹窗（直通菜单可关）。</summary>
    public bool Quiet = true;

    public string NormalizedDeliverableRoot
    {
        get
        {
            string r = string.IsNullOrWhiteSpace(DeliverableRoot)
                ? RetinarPaths.DeliverableRoot
                : DeliverableRoot.Trim().Replace("\\", "/").TrimEnd('/');
            return string.IsNullOrEmpty(r) ? RetinarPaths.DeliverableRoot : r;
        }
    }

    public string NormalizedAssetBundleRoot
    {
        get
        {
            string r = string.IsNullOrWhiteSpace(AssetBundleRoot)
                ? RetinarPaths.AssetBundleRoot
                : AssetBundleRoot.Trim().Replace("\\", "/").TrimEnd('/');
            return string.IsNullOrEmpty(r) ? RetinarPaths.AssetBundleRoot : r;
        }
    }

    public static RetinarAbBuildOptions CreateDefaultAbOnly()
    {
        return new RetinarAbBuildOptions
        {
            ExportUnityPackage = false,
            CopyAbToDeliverables = true,
            Quiet = true
        };
    }

    public static RetinarAbBuildOptions FromExportSettings(
        RetinarExportSettings settings,
        bool? exportUnityPackageOverride = null,
        bool? quietOverride = null)
    {
        var opt = CreateDefaultAbOnly();
        if (settings != null)
        {
            opt.DeliverableRoot = settings.deliverableRoot;
            opt.AssetBundleRoot = settings.assetBundleRoot;
            opt.ExportUnityPackage = settings.exportUnityPackage;
            opt.CopyAbToDeliverables = settings.copyAbToDeliverables;
        }

        if (exportUnityPackageOverride.HasValue)
        {
            opt.ExportUnityPackage = exportUnityPackageOverride.Value;
        }

        if (quietOverride.HasValue)
        {
            opt.Quiet = quietOverride.Value;
        }

        return opt;
    }
}
