# Plugin / Pipeline — 流程编排中间层

位置：`Assets/Plugin/Pipeline/`

操作者先看插件根 [README · 操作者入口](../README.md)。本文是编排层地图。

**职责：** 要不要跑 [1]/[2]/③④⑤⑥、quiet、错误码；主流程只调窄口。Runner 仍有 D19 顶点色诊断/补偿，尚非纯编排。  
**不负责：** 压图/材质 Op 内部；搬文件在插件 2 内核。人工④唯一调度入口在 `Editor/ManualFlatten/`。  
**处理区连锁：** ③开才能④，④开才能⑤。⑤ = 代调 L1「执行全部」，`FailedCount>0` → **50**（⑥仍跑）。leftover `.fbm` → **41**；贴图身份 → **42**（均不停⑤⑥）。glTF 缺伴生 → **40** 整趟停。

```text
Tools > 自动化管线总面板
  [1] 入库 → [2] 导入期 → [③] Prefab → [④] 平铺 → [⑤] 总批量 → [⑥] 导出
CLI: Unity.exe -executeMethod PipelineCli.Run -source …
人工④: 手动操作栏原子/平铺 → ManualFlattenOrchestration → Run(plan)（不接⑤⑥）
```

```text
Pipeline/
├─ ConfigData/
│  ├─ PipelineStepSettings.asset           # 总步骤 SO（含三根路径）
│  ├─ ImportPipelineSettings.asset         # [2] 导入期总开关
│  ├─ FlattenOperationSettings.asset       # ④ 管线专用平铺 SO
│  └─ *ProcessSettings.asset               # ⑤ 管线侧资源处理副本
└─ Editor/
   ├─ PipelineStepSettings.cs
   ├─ PipelineMaterialId.cs                # ID2；SuggestBindingsForSelection
   ├─ PipelineSourceAccept.cs              # 批量 → 编排
   ├─ PipelineOptions / Result / ErrorCodes
   ├─ PipelineFlattenQuality.cs            # 41 leftover / 42 身份（Fail 不 return）
   ├─ ManualFlatten/                       # 人工④唯一调度：Orchestration / Plan
   ├─ PipelineJobContext.cs                # 仅自动管线 ②.5
   ├─ PipelineRunner.cs
   ├─ PipelineWindow.cs                    # Tools > 自动化管线总面板
   └─ PipelineCli.cs                       # -executeMethod PipelineCli.Run
```

对外：[`pipeline-flow.md`](../docs/dev-wip/04_implementation/pipeline-flow.md) · CLI：[`cli-getting-started.md`](../docs/dev-wip/04_implementation/cli-getting-started.md)

| 配置层 | 存哪 | 管什么 |
|---|---|---|
| 总调度步骤 | **PipelineStepSettings SO** | 要不要③④⑤⑥、Quiet、三根工作路径 |
| 导入期 | **ImportPipelineSettings SO** | OnPreprocess 总开关；高级残留 delayCall Prefs |
| ④ 管线平铺细节 | **Pipeline/ConfigData 下 FlattenOperationSettings** | 分类、清本次 Art 单元、根碰撞体；开跑前冻成 policy |
| 导出产物/路径 | **RetinarExportSettings SO** | 交付根、AB 根、是否 UP |
| ⑤ 资源处理 | **Pipeline/ConfigData *ProcessSettings** | 压图/顶点/材质；导入期字段已迁走 |

人工平铺用同一数据类，资产在 `TOol/ConfigData/Manual/`。两份互不覆盖。面板只编辑自身目录。

**ID2：** 选源自动填。批量「输出到编排」→ `AcceptBindings`。Runner 读 `SourceBindings`，先逐行入库+②.5，再逐行③，④把该行 ctx 译成 plan 再 `Run`。空表则 `SourcePath`+`MaterialId` 合成一行。**同一物理路径不能在一张表里出现两次。**

| 步骤 | 窄口 |
|---|---|
| [1] / ② | `ToolImportApi.ImportSingleModel` |
| ③ | `ToolPrefabApi` |
| ④ | `ToolFlattenApi.FromContext` → `Run(plan)` |
| ⑤ | `ToolPostProcessApi.RunMasterBatch` |
| ⑥ | `RetinarAbApi.Build` |

差距：总面板强制 `RunImport=true`，CLI 跟 SO（D26-5）；⑤类型纳入仍读 EditorPrefs；后错覆盖首错（D26-4）。见 [overview](../docs/dev-wip/02_structure/overview.md)。
