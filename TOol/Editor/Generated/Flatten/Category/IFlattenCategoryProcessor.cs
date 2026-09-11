// =====================================================================================
// 平铺大类处理器：一种资源大类 = 一个实现。
//
// 约定：
//   Id = 该单元在 Art/<名>/ 下的根文件夹名（也是注册表唯一键）。
//   输出子路径写在实现类 const 里，禁止用反射拼夹名。
//   面板可改「是否参与筛选」和后缀；输出路径只读。
// =====================================================================================

/// <summary>平铺资源大类处理器。</summary>
public interface IFlattenCategoryProcessor
{
    string Id { get; }

    string DisplayName { get; }

    int Order { get; }

    string[] DefaultSuffixes { get; }

    /// <summary>只读：该单元会写出的相对路径（相对 Art/&lt;名&gt;/）。</summary>
    string[] OutputFolderHints { get; }

    /// <summary>后缀命中且允许认领时返回 true。Packages/ 等不可拷贝的资源应返回 false。</summary>
    bool Matches(string assetPath, FlattenCategorySettings settings);

    /// <summary>返回 Art/&lt;名&gt;/ 下的相对目录，例如 Model 或 image/Texture。</summary>
    string ResolveRelativeFolder(string assetPath);
}
