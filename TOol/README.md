# TOol（资源处理插件）

Unity Editor 插件：导入期 **设置自动** + 导入后 **后处理自动**（贴图压缩、模型顶点色等）；另提供 **批量 FBX 入库**。

- **当前版本：** v1.3.6（Evaluate / 仅扫描；全流程基线见 v1.3.5）
- **菜单：** `Tools > 资源处理总面板`（含总开关、仅扫描，默认开启，EditorPrefs）；`Tools > 批量FBX导入`（外部 FBX 入库）
- **结构说明（目录 / 类 / 扩展方式）：** [ARCHITECTURE.md](./ARCHITECTURE.md)
- **配置资产：** `ConfigData/TextureProcessSettings.asset`、`ConfigData/ModelProcessSettings.asset`、`ConfigData/BatchFbxImportSettings.asset`
- **与打包插件关系：** 自动流默认不碰 `Assets/Art/`；交付打包见 `../RetinarBatchBuilder_Share/`；批量入库不自动建 Prefab，交付名仍以人工 Prefab 名为准
