# Plugin / Pipeline — 编排中间层

操作者：[操作者须知](../docs/operator/README.md)。现约：[开发者须知](../docs/dev-wip/README.md)。

**负责：** 要不要跑 [1]/[2]/③④⑤⑥、Quiet、清空、错误码；主流程只调窄口。  
**不负责：** Op 内部；搬文件在④内核。人工④调度在 `Editor/ManualFlatten/`。

Runner 仍有 D19 诊断/补偿，尚非纯编排。

```text
Pipeline/
├─ ConfigData/
│  ├─ PipelineStepSettings.asset      # ③④⑤⑥、Quiet、清空、三根路径、⑤大类
│  ├─ ImportPipelineSettings.asset    # [2] 导入期总闸
│  ├─ FlattenOperationSettings.asset  # ④ 管线平铺
│  └─ *ProcessSettings.asset          # ⑤ 管线侧副本
└─ Editor/
   ├─ PipelineRunner / Options / Cli / Window
   ├─ PipelineJobContext.cs           # 仅自动 ②.5
   ├─ ManualFlatten/                  # 人工④：Orchestration / Plan
   └─ PipelineFlattenQuality.cs       # 41 / 42
```

| 步骤 | 窄口 |
|---|---|
| [1] | `ToolImportApi.ImportSingleModel` |
| ③ | `ToolPrefabApi` |
| ④ | `FromContext` → `Run(plan)` |
| ⑤ | `ToolPostProcessApi.RunMasterBatch` |
| ⑥ | `RetinarAbApi.Build` |

面板/CLI 一定入库。Quiet：面板跟 SO，CLI 强制 true。清空：跟 SO，不绑 Quiet。  
Pack（v1.6.5）：`ToolImportApi` 预处理后按信封内根 Prefab 展开多行。面板唯一入口是 [1] 预览「浏览…」一个 `.unitypackage`。灰框多选与选择器不收 pack。
