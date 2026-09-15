# TOol / Editor / Generated

当前物理存放③生成与④平铺能力，不再是“仅中间资产、绝不写Art”的旧定义。

| 模块 | 产物与职责 |
|---|---|
| [Prefab](./Prefab/) | ③生成IncomingPrefab；编排经ToolPrefabApi调用PrefabBuildService |
| [Flatten](./Flatten/) | ④生成Art交付副本；ToolFlattenApi、FlattenBuildService直接消费ctx，暂按职责归中间层 |

④约3,518行遗产已迁到此处，但仍是三份partial，七步拆文件与回归未完成；人工普通/原子按钮都跑完整④。⑤对既有资源的原地修改仍位于Texture/Material/Model，不放进Generated。

总体数据流与临时边界见[结构总览](../../../docs/dev-wip/02_structure/overview.md)。
