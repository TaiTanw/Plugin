# D1 核对：⑥ AB 输出契约

返回 [实现流程](./pipeline-flow.md) · [待办](../03_open-items/backlog.md)

> D1命名/压缩契约于2026-08-26收口；2026-09-14核对当前API与默认配置。旧BuildAbOnly/菜单对照已不代表当前入口。

## 已锁定（本阶段）

| 项 | 当前结论 |
|---|---|
| API | RetinarAbApi.Build，参数为Prefab列表与RetinarAbBuildOptions |
| 默认产物 | Android + iOS AB；UnityPackage按导出SO可选 |
| 配置 | RetinarExportSettings提供路径/UP/拷贝设置；Android+iOS与LZ4当前仍在代码中固定；总步骤SO的RunAb只决定是否执行⑥ |
| 输入 | 开④用其返回Art Prefab；关④用③或预填Prefab列表 |
| 构建 | 显式AssetBundleBuild[]；不靠扫描全部已打标签的资产决定本次输入 |
| 压缩 | ChunkBasedCompression（LZ4） |
| 路径 | 默认AssetBundles/{Android,iOS}；按配置拷至Deliverables/名称/03_assetbundles；UP在02_unity |
| 文件名 | name.assetbundle + 平台目录；未改为{id}_android/ios.assetbundle |
| 包内main | 不强制改main，继续现网取包方式 |
| Quiet | 不弹确认，不等于退出编辑器；CLI负责退出进程 |
| 不做 | 旧规范化业务门禁、全套00–06、runtime/xlsx/贴图报告已删除 |

当前正常SO默认②③④⑤⑥全开；“②③⑥”是可裁剪最小线，不是当前默认。

## 契约 1 / 2（已确认可退化）

文件名/包内main继续现网契约。工单提出{id}_android.assetbundle + {id}_ios.assetbundle是历史评估，未获APP改取包确认，不改默认；上传层改名仍是备选。历史工单证据见[日志§2](../05_dev-log/timeline.md#2-issue-274)，本次未查询远程新状态。

④AB标签归属迁移与文件名重开不是一件事。2026-09-14用户同意取消④提前写标签，转由⑥构建清单管理名称；先核对旧菜单/外部工具依赖，不借此改变APP文件名契约或新增Importer写入。

## ④ + ⑥：如何找到 Art 资源？

③Prefab列表 → ④七步返回Art Prefab列表 → ⑥Build。Runner传真实返回路径，不根据名字猜Art路径；⑤只改该单元资产，不另提供⑥输入。

## materialId（可选）含义

Runner逐行使用binding的MaterialId：②工程外入库夹名、③Prefab名，④Art单元/⑥AB名称再跟随Prefab名；空ID按现行建议命名规则生成。它还不是跨任务唯一ID，也没有自动获得工单平台后缀格式。

## 与旧菜单的关系 / 输出目录

RetinarDirectPackage、RetinarPackageScheduler、ExportArtPrefabPaths及旧规范化/成品直达菜单已删除，不再维护三条出包路径。当前日常出包使用管线⑥ / RetinarAbApi.Build。交付目录保留02_unity/03_assetbundles，不再写01_source或全套报告。

## 碰撞体 / 缩放 / 门禁

⑥不负责平铺变换。④碰撞体由本入口SO快照决定，默认关闭；直接选FBX的人工普通平铺走SafeZone，管线③Prefab走另一条空壳路径。旧业务门禁已删除，不存在“传skipGates才能禁用”的现行开关。

需要已加工Prefab直接出包时，可由API传入已验收列表并禁用不需要的前置步骤；不要因此宣称完整管线无需④⑤或等同于人工④按钮。
