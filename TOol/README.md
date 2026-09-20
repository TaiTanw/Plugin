# TOol（插件 2）

操作者：[操作者须知](../docs/operator/README.md)。现约：[开发者须知](../docs/dev-wip/README.md)。

② 导入与设置、③ Prefab、⑤ Op；**物理承载**④内核。消费 ctx 的编排在 Pipeline。

| 入口 | 菜单 |
|---|---|
| 入库选择器 | `手动操作栏 > 步骤 > [1]`（与管线「打开批量选择器」同一窗） |
| [2] | `Tools > 全局导入设置（2）` |
| 人工④ | `手动操作栏 > [④]`（完整④，不接⑤⑥） |
| 人工⑤ | `手动操作栏 > [⑤]`（总面板 / 精准） |

- 人工平铺 SO：`ConfigData/Manual`。管线实例在 `Pipeline/ConfigData`。
- 贴图 `AllowMasterBatch=false` 不得进 L1/⑤/导入自动。
- Art 模型 Processor 硬跳过 `Assets/Art`。显式⑤可以改 Art。
- ④：[`Editor/Generated/Flatten/README.md`](./Editor/Generated/Flatten/README.md)

结构：[overview](../docs/dev-wip/02_structure/overview.md) · [ARCHITECTURE](./ARCHITECTURE.md) · [Op](../docs/dev-wip/04_implementation/op-recognition-and-extend.md)
