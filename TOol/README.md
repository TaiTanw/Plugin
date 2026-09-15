# TOol（资源处理插件）

当前包括②导入与设置、③ Prefab、⑤贴图/材质/模型操作，并物理承载④平铺遗产。消费 ctx 的④ API/服务/编排暂按职责归中间层，不等于黑盒拆分已经完成。

- 资源总入口：`Tools > 资源处理总面板`；精确操作进 L2，高级 SO 配置进 L3。
- 人工④：[平铺操作面板](./Editor/Generated/Flatten/README.md)，普通/原子两按钮都跑完整④；SO 同数据类、人工/管线不同实例。
- 配置：Texture/Material/Model/BatchFbxImport Settings；人工 FlattenOperationSettings 在 ConfigData/Manual，管线实例在 Pipeline/ConfigData。
- ⑤材质透明修复位于 NormalizeDeliverableShaderOperation，读取单个材质状态，不扩模型 ctx；Material SO 目前仍共用。
- Art 模型设置 Processor 硬跳过，贴图/后处理另按排除表；Incoming 模型安全基线不受总闸控制。显式⑤可以处理 Art，不能混称为导入自动。
- [当前整体结构与数据流](../docs/dev-wip/02_structure/overview.md) · [详细结构](./ARCHITECTURE.md) · [Op扩展](../docs/dev-wip/04_implementation/op-recognition-and-extend.md)

版本以插件根发布标签为准，不再把历史 v1.3.7 面板版本当当前代码版本。
