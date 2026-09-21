# Shared / Api — 插件 2 对外窄口

供 `Plugin/Pipeline` 与其它编排调用。**不要**从编排层直接碰 Window / Op 内部类。

| 类 | 步骤 | 说明 |
|---|---|---|
| `ToolImportApi` | 1 入库 | **单文件** `ImportSingleModel`；夹名非空=ID2。`.unitypackage` 解到 `导入根/<ID2>/`（须编排传入导入根）。模型表仍是 `.fbx/.glb/.gltf/.obj`。`.gltf` 跟拷 URI 伴生；`.obj` 跟拷 `.mtl` 与贴图。批量仍 `ExecuteBatch` |
| `ToolFlattenApi` | ④ | `FromContext(ctx, request, prefab)` → `Run(plan)`；人工自己组 plan，不建 ctx。步骤 13 已撤公开分步口；旧 `FlattenPaths` 在步骤 12 删除 |
| `ToolPrefabApi` | ③ | 模型 → `Generated/Prefab`；**已是 Prefab 则原样交**（不按 packID2 改名） |
| `ToolPostProcessApi` | ⑤ | → L1 子流程（总批量）；返回 `ToolPostProcessResult`（FailedCount + Report） |

⑤显式范围覆盖L1路径，null才回落；类型纳入未覆盖时仍读EditorPrefs。Op参数来自各资源SO，Material仍人工/管线共用。ToolPostProcessResult只有FailedCount/Canceled/Report，未统一typed跳过原因；编排不解析报告或读取Op内部。

④虽物理在插件2，消费ctx的API/服务/编排暂按职责归中间层。自动每模型②.5一次；人工仅轻扫描伴生事实并组 plan，禁止另建 ctx。内核七步只由 `FlattenBuildService.Run` 调用，步骤 13 已撤公开分步转发，未迁移目录。详见[整体结构](../../../../docs/dev-wip/02_structure/overview.md)。
