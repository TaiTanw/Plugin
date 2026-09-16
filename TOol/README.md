# TOol（资源处理插件）

操作者先看插件根 [README · 操作者入口](../README.md)。

当前包括②导入与设置、③ Prefab、⑤贴图/材质/模型 Op，并**物理承载**④平铺内核。消费 ctx 的编排在 Pipeline，不等于内核已拆文件。

| 入口 | 菜单 | 说明 |
|---|---|---|
| 入库选择器 | `Tools > 批量选择器` | 与总面板 **[1]** 同一窗；可「输出到编排」 |
| 资源处理 L1 | `Tools > 资源处理总面板` | 与管线 **⑤** 同一 `RunMasterBatch`；默认扫 `Assets/Art` |
| 人工④ | 总面板 [④] 或 `Tools > Retinar > 批量汇总` | 普通/原子两按钮都跑完整④，**不**接⑤⑥ |

- 配置：Texture / Material / Model / BatchFbxImport Settings。人工 `FlattenOperationSettings` 在 `ConfigData/Manual`，管线实例在 `Pipeline/ConfigData`。
- ⑤透明修复：`NormalizeDeliverableShaderOperation`，不扩模型 ctx。Material SO 仍共用。
- Art 模型 Processor **硬跳过**；贴图/后处理另按排除表。Incoming 模型安全基线不受总闸控制。显式⑤可以改 Art。
- ④内核：[`Editor/Generated/Flatten/README.md`](./Editor/Generated/Flatten/README.md)

结构：[overview](../docs/dev-wip/02_structure/overview.md) · [ARCHITECTURE](./ARCHITECTURE.md) · [Op 扩展](../docs/dev-wip/04_implementation/op-recognition-and-extend.md)
