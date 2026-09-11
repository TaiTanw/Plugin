# Plugin / Pipeline — 流程编排中间层

位置：`Assets/Plugin/Pipeline/`

**职责：** 是否跑导入/处理/输出三区、quiet、错误码字符串汇总；**只依赖**插件 1 / 2 对外窄口。  
**不负责：** 压图参数、门禁细则、菜单人工交付。  
**不主动调用「设置自动」**（那是导入期自动流，靠 Unity 回调，默认不碰 Art）。  
**处理区连锁：** ③开才能④，④开才能⑤。⑤ = 代调 L1「执行全部」同一 `RunMasterBatch`，返回 `ToolPostProcessResult`（FailedCount→50）。

```text
Pipeline/
├─ ConfigData/
│  ├─ PipelineStepSettings.asset           # 总步骤 SO（② 等）
│  └─ FlattenOperationSettings.asset       # ④ 管线专用平铺 SO
└─ Editor/
   ├─ PipelineStepSettings.cs
   ├─ PipelineMaterialId.cs                # D9 默认 Id；SuggestBindingsForSelection（A）
   ├─ PipelineSourceAccept.cs              # 批量 → 编排（路径+ID2）
   ├─ PipelineOptions / Result / ErrorCodes
   ├─ PipelineRunner.cs                    # (A/B) 编排内核
   ├─ PipelineWindow.cs                    # (A) Tools > 自动化管线总面板
   └─ PipelineCli.cs                       # (B) -executeMethod PipelineCli.Run（D5 已验收）
```

对外分块说明：`docs/dev-wip/04_implementation/pipeline-flow.md` · CLI：`cli-getting-started.md`。

| 配置层 | 存哪 | 管什么 |
|---|---|---|
| 总调度步骤 | **PipelineStepSettings SO** | 要不要②③④⑤⑥、Quiet（⑥只表示是否导出） |
| ④ 管线平铺细节 | **Pipeline/ConfigData 下的 FlattenOperationSettings SO** | 分类、清本次 Art 单元、根碰撞体；运行前冻结为 policy |
| 导出产物/路径 | **RetinarExportSettings SO** | 交付根、AB 根、是否 UP、是否拷 AB 到交付夹 |
| 资源自动细节 | **资源总面板 EditorPrefs + L3 SO** | 设置自动/后处理自动、Op、压缩参数 |

人工平铺使用同一 `FlattenOperationSettings` 数据类，但资产位于 `TOol/ConfigData/Manual/`。两份资产互不覆盖；人工面板拖入管线目录资产时只读，管线面板只编辑管线目录资产。Runner 开跑前把管线资产冻结为 `FlattenOperationPolicy`，深层内核不再读取平铺 EditorPrefs。

**materialId / ID2：** 选源自动填（父目录仅一个内核文件→三层；还有其它或三层 Warning→三层+文件全名）。批量「输出到编排」走 `PipelineSourceAccept.SendToOrchestration` → 总面板 `AcceptBindings`。Runner 读 `SourceBindings` 按行 1→2.5→③（该行 ID2）；④ 按该行 ctx。空表则用 `SourcePath`+`MaterialId` 合成一行。

| 步骤 | 窄口 |
|---|---|
| ② / 1 入库 | `ToolImportApi.ImportSingleModel`（可选 Incoming 夹名 = ID2） |
| ③ | `ToolPrefabApi` |
| ④ | `ToolFlattenApi`（ctx 选 B/B′；`FlattenOperationPolicy` 给分类/清夹/碰撞体） |
| ⑤ | `ToolPostProcessApi`（Converter 默认开；贴图→材质→模型） |
| ⑥ | `RetinarAbApi.BuildAbOnly` |
