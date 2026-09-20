# D1：⑥ AB

现约入口：[开发者须知](../README.md)。

| 项 | 口径 |
|---|---|
| API | `RetinarAbApi.Build(Prefab[], RetinarAbBuildOptions)` |
| 产物 | Android+iOS；UP 按导出 SO 可选 |
| 文件名 | `{stem}_android.assetbundle` / `{stem}_ios.assetbundle` |
| 路径 | `AssetBundles/` 平铺，不分平台夹；暂存 `Library/RetinarAbBuild/{android\|ios}` |
| 构建 | 显式 `AssetBundleBuild[]`；④ 不写 Importer AB 标签 |
| 压缩 | LZ4，平台钉在代码 |
| 成功 | 双端都成功才 0；任一端失败 60 |
| 不做 | 旧门禁、全套报告、按已打标签资产扫全库 |

开④：打④返回的 Art Prefab。关④：打③或预填列表。⑤不另提供⑥输入。`materialId` 会重名，不是跨任务主键。
