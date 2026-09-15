# Shared / Api — 插件 2 对外窄口

供 `Plugin/Pipeline` 与其它编排调用。**不要**从编排层直接碰 Window / Op 内部类。

| 类 | 步骤 | 说明 |
|---|---|---|
| `ToolImportApi` | 1 入库 | **单文件** `ImportSingleModel(source, incomingFolderName?, …)`；夹名非空=ID2。`.gltf` 跟拷 URI 伴生；`.obj` 跟拷 `.mtl` 与贴图。批量仍 `ExecuteBatch` |
| `ToolFlattenApi` | ④ | → `Generated/Flatten`。分步接 `PipelineJobContext`（B/B′、E）；轴向走 `ToolFlattenRequest` |
| `ToolPrefabApi` | ③ | → `Generated/Prefab` |
| `ToolPostProcessApi` | ⑤ | → L1 子流程（总批量）；返回 `ToolPostProcessResult`（FailedCount + Report） |

⑤显式范围覆盖L1路径，null才回落；类型纳入未覆盖时仍读EditorPrefs。Op参数来自各资源SO，Material仍人工/管线共用。ToolPostProcessResult只有FailedCount/Canceled/Report，未统一typed跳过原因；编排不解析报告或读取Op内部。

④虽物理在插件2，消费ctx的API/服务/编排暂按职责归中间层。人工点击建立自身ctx，自动每模型②.5一次，分步复用。详见[整体结构](../../../../docs/dev-wip/02_structure/overview.md)。
