# TOol（资源处理插件）

Unity Editor 插件：导入期 **设置自动** + 导入后 **后处理自动**（贴图压缩、模型顶点色等）；另提供 **批量 FBX 入库**。

- **当前版本：** v1.3.7（面板三层 L1/L2/L3；Evaluate / 仅扫描见 v1.3.6）
- **菜单：** `Tools > 资源处理总面板`（共用批量路径 + 总/分项批量；精准进子面板，高级进 L3）；`Tools > 批量FBX导入`
- **结构说明（目录 / 类 / 扩展方式）：** [ARCHITECTURE.md](./ARCHITECTURE.md)（含 **配置归属**、**Generated 中间资产**、面板分层）
- **中间资产（③ Prefab）：** [`Editor/Generated/Prefab/`](./Editor/Generated/Prefab/)；菜单 `Tools > 自动化预设体（选中模型）`
- **配置资产：** `ConfigData/TextureProcessSettings.asset`、`ConfigData/ModelProcessSettings.asset`、`ConfigData/BatchFbxImportSettings.asset`（后三者里「Art 相关前缀」默认相同但不共享；高级操作见各 L3「子处理配置」说明）
- **与打包插件关系：** 自动流默认不碰 `Assets/Art/`；交付打包见 `../RetinarBatchBuilder_Share/`；批量入库不自动建 Prefab，交付名仍以人工 Prefab 名为准
