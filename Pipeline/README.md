# Plugin / Pipeline — 流程编排中间层

位置：`Assets/Plugin/Pipeline/`

**职责：** 是否跑 ②③④⑤⑥、quiet、错误码字符串汇总；**只依赖**插件 1 / 2 对外窄口。  
**不负责：** 压图参数、门禁细则、菜单人工交付；**不主动调用**「设置自动」（靠导入回调）。

```text
Pipeline/
├─ ConfigData/PipelineStepSettings.asset   # 总步骤 SO（② 等）
└─ Editor/
   ├─ PipelineStepSettings.cs
   ├─ PipelineOptions / Result / ErrorCodes
   ├─ PipelineRunner.cs
   └─ PipelineWindow.cs                    # Tools > 自动化管线总面板
```

| 配置层 | 存哪 | 管什么 |
|---|---|---|
| 总调度步骤 | **PipelineStepSettings SO** | 要不要②③④⑤⑥、是否同步 L1 路径 |
| 资源自动细节 | **资源总面板 EditorPrefs + L3 SO** | 设置自动/后处理自动、Op、压缩参数 |

| 步骤 | 窄口 |
|---|---|
| ② | `ToolImportApi.ImportSingleModel` |
| ③ | `ToolPrefabApi` |
| ④ | `RetinarFlattenApi` |
| ⑤ | `ToolPostProcessApi`（Converter 默认开；贴图→材质→模型） |
| ⑥ | `RetinarAbApi.BuildAbOnly` |
