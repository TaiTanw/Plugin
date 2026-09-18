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
| 路径 | 默认 `AssetBundles/` 根下平铺，不再分 Android/iOS 夹；按配置拷至 Deliverables；UP 仍在 02_unity。Unity 构建暂存在 `Library/RetinarAbBuild/{android\|ios}` |
| 文件名 | `{stem}_android.assetbundle` / `{stem}_ios.assetbundle` |
| 包内main | 不强制改main，继续现网取包方式 |
| Quiet | 不弹确认，不等于退出编辑器；CLI负责退出进程 |
| 不做 | 旧规范化业务门禁、全套00–06、runtime/xlsx/贴图报告已删除 |

当前正常SO默认②③④⑤⑥全开；“②③⑥”是可裁剪最小线，不是当前默认。

## 契约 1 / 2（v1.6.0 已改文件名）

自 **v1.6.0** 起产品默认即 `{stem}_android.assetbundle` / `{stem}_ios.assetbundle`，产品夹不再分平台。包内 `main` 仍不强制改。无回退到旧平台子夹。APP 若仍按 `AssetBundles/{Android,iOS}/name.assetbundle` 取包需改路径。历史工单见[日志§2](../05_dev-log/timeline.md#2-issue-274)。

④ 不再提前写 AB 标签；⑥ 构建清单管理名称。

## ④ + ⑥：如何找到 Art 资源？

③Prefab列表 → ④七步返回Art Prefab列表 → ⑥Build。Runner传真实返回路径，不根据名字猜Art路径；⑤只改该单元资产，不另提供⑥输入。

## materialId（可选）含义

Runner逐行使用binding的MaterialId：②工程外入库夹名、③Prefab名，④Art单元/⑥AB名称再跟随Prefab名；空ID按现行建议命名规则生成。它还不是跨任务唯一ID，也没有自动获得工单平台后缀格式。

## 与旧菜单的关系 / 输出目录

RetinarDirectPackage、RetinarPackageScheduler、ExportArtPrefabPaths及旧规范化/成品直达菜单已删除，不再维护三条出包路径。当前日常出包使用管线⑥ / RetinarAbApi.Build。交付目录保留02_unity/03_assetbundles，不再写01_source或全套报告。

## 碰撞体 / 缩放 / 门禁

⑥不负责平铺变换。④碰撞体由本入口SO快照决定，默认关闭；人工直接选FBX与管线③Prefab同一套空壳路径，不再SafeZone缩放。旧业务门禁已删除，不存在“传skipGates才能禁用”的现行开关。

需要已加工Prefab直接出包时，可由API传入已验收列表并禁用不需要的前置步骤；不要因此宣称完整管线无需④⑤或等同于人工④按钮。
