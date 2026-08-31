# Plugin / Pipeline — 流程编排中间层

位置：`Assets/Plugin/Pipeline/`

**职责：** 是否跑导入/处理/输出三区、quiet、错误码字符串汇总；**只依赖**插件 1 / 2 对外窄口。  
**不负责：** 压图参数、门禁细则、菜单人工交付。  
**不主动调用「设置自动」**（那是导入期自动流，靠 Unity 回调，默认不碰 Art）。  
**处理区连锁：** ③开才能④，④开才能⑤。⑤ = 代调 L1「执行全部」同一 `RunMasterBatch`，返回 `ToolPostProcessResult`（FailedCount→50）。

```text
Pipeline/
├─ ConfigData/PipelineStepSettings.asset   # 总步骤 SO（② 等）
└─ Editor/
   ├─ PipelineStepSettings.cs
   ├─ PipelineMaterialId.cs                # D9 默认 Id；D10 多源绑定预备
   ├─ PipelineOptions / Result / ErrorCodes
   ├─ PipelineRunner.cs                    # (A/B) 编排内核
   ├─ PipelineWindow.cs                    # (A) Tools > 自动化管线总面板
   └─ PipelineCli.cs                       # (B) -executeMethod PipelineCli.Run
```

对外分块说明：`docs/dev-wip/04_implementation/pipeline-flow.md` · CLI：`cli-getting-started.md`。

| 配置层 | 存哪 | 管什么 |
|---|---|---|
| 总调度步骤 | **PipelineStepSettings SO** | 要不要②③④⑤⑥、Quiet（⑥只表示是否导出） |
| 导出产物/路径 | **RetinarExportSettings SO** | 交付根、AB 根、是否 UP、是否拷 AB 到交付夹 |
| 资源自动细节 | **资源总面板 EditorPrefs + L3 SO** | 设置自动/后处理自动、Op、压缩参数 |

**materialId：** 面板选源自动填默认名、清除同步清空（D9）。多文件绑定见 `PipelineMaterialId.BuildSourceBindings` / `Options.SourceBindings`（D10 预备，Runner 未消费）。

| 步骤 | 窄口 |
|---|---|
| ② | `ToolImportApi.ImportSingleModel` |
| ③ | `ToolPrefabApi` |
| ④ | `RetinarFlattenApi` |
| ⑤ | `ToolPostProcessApi`（Converter 默认开；贴图→材质→模型） |
| ⑥ | `RetinarAbApi.BuildAbOnly` |
