// =====================================================================================
// 交付输出槽扩展点。夹名写在实现类 const（可改字面量），语义 Id 冻结。
// 本期不接线：导出仍按 Legacy / RetinarDeliverableIo 写 00–06。
// =====================================================================================

/// <summary>一种 Deliverables 产出。未勾选则不写盘。</summary>
public interface IRetinarDeliverableOutput
{
    /// <summary>冻结语义键，例如 unity_package。不得把语义改成别的产物。</summary>
    string Id { get; }

    /// <summary>磁盘文件夹名，须与实现类 const / RetinarPaths 一致，例如 02_unity。</summary>
    string FolderName { get; }

    string DisplayName { get; }

    int Order { get; }
}
