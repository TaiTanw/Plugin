# TOol / Editor / Generated

当前物理存放③生成与④平铺能力。操作者入口：[插件根 README](../../../README.md)。

| 模块 | 产物与职责 |
|---|---|
| [Prefab](./Prefab/) | ③ IncomingPrefab；编排经 ToolPrefabApi |
| [Flatten](./Flatten/) | ④ Art；`FromContext` 译 ctx，`Run(plan)` 不读 ctx。步骤 1–14 已落地，未迁目录 |

④约 3,518 行仍是三份 partial。⑤对既有资源的原地修改在 Texture/Material/Model，不放进 Generated。

总体数据流：[结构总览](../../../docs/dev-wip/02_structure/overview.md)。
