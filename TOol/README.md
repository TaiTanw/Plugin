# TOol（资源处理插件）

Unity Editor 插件：导入期 **设置自动** + 导入后 **后处理自动**（贴图压缩、模型顶点色等）。

- **菜单：** `Tools > 资源处理总面板`（含总开关，默认开启，EditorPrefs）
- **结构说明（目录 / 类 / 扩展方式）：** [ARCHITECTURE.md](./ARCHITECTURE.md)
- **配置资产：** `ConfigData/TextureProcessSettings.asset`、`ConfigData/ModelProcessSettings.asset`
- **与打包插件关系：** 自动流默认不碰 `Assets/Art/`；交付打包见 `../RetinarBatchBuilder_Share/`
