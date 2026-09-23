# D1：⑥ AB

现约入口：[开发者须知](../README.md)。

| 项 | 口径 |
|---|---|
| API | `RetinarAbApi.Build(Prefab[], RetinarAbBuildOptions)` |
| 产物 | Android+iOS；UP 按导出 SO 可选 |
| 拷 AB 到交付夹 | 导出 SO `copyAbToDeliverables`，默认开。关掉只是不写 `Deliverables/<名>/03_assetbundles`。双端仍打到 AB 根。没有「不打 AB、只出其他项」 |
| 可选附加 | 同一份导出 SO，默认关：模型文件 → `01_source/Model`；资源信息表 → `06_docs/asset_info.xlsx`；UP → `02_unity`。双端构建成功后才写。构建失败则勾了也不写。人工与管线都走 `Build`，不另做面板闸 |
| 步骤 ⑥ | 管线关掉则不调用 `Build`。导出 SO 上其余勾选也不执行 |
| 模型文件 | 从该预设体 `GetDependencies` 留 `.fbx` `.obj` `.glb` `.gltf`。`.gltf` 再按相对 URI 跟拷伴生。不按 `Art/<名>/Model` 或 `Art/<名>/<名>` 找。`.obj` 只拷容器，不修 `mtllib` |
| 资源信息表 | 拷仓库模板。身份样例、DCC「FBX 已交付」、版权段里的样例清单改为当前名或待填写/待补充。B17 Unity 版本；B25/B26 为 LOD0 三角面/顶点（同一 Mesh 只计一次）；B27 Renderer 个数；B28 材质个数；B30 贴图张数与最大导入尺寸；B32 动画 Clip 数；B33 Collider 数；B34 预设体路径。A 列标签不动。LOD1/LOD2、性能验收、文件清单不填 |
| 文件名 | `{stem}_android.assetbundle` / `{stem}_ios.assetbundle` |
| 路径 | `AssetBundles/` 平铺，不分平台夹；暂存 `Library/RetinarAbBuild/{android\|ios}` |
| 构建 | 显式 `AssetBundleBuild[]`；④ 不写 Importer AB 标签 |
| 压缩 | LZ4，平台钉在代码 |
| 成功 | 双端都成功才 0；任一端失败 60。模型拷贝或资源表失败只打日志，不进 FailLines，不把退出码改成 60 |
| 不做 | 旧门禁、全套报告、`00_runtime`、贴图体积报告、按已打标签资产扫全库。上述两档不是把已删的全套导出做成开关 |

开④：打④返回的 Art Prefab。关④：打③或预填列表。⑤不另提供⑥输入。`materialId` 会重名，不是跨任务主键。
