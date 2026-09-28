# Pipeline / ManualFlatten — 人工④唯一调度入口

不搬 `RetinarBatchModelBuilder` 内核。自动④在 `PipelineRunner`，不进本夹。

操作者：总面板 **[④]「打开平铺面板」** 或 `Tools > 手动操作栏 > 步骤 > [④] 平铺 > 手动平铺（逐模型策略）`。手动跑完整④，**不**自动⑤⑥。选中解包后的根文件夹时，只按根 Prefab 拆行。

```text
ManualFlatten/
├─ Orchestration/   选中、模型先③、手动 SO 快照、ToolFlattenApi.Run
└─ Plan/            手动入口创建 FlattenPlan，禁止 PipelineJobContext
```

自动管线：已有 ctx → `FromContext` → `Run(plan)`，使用管线 SO 快照。手动端从选中资源建 plan，使用手动 SO 快照，不建 ctx。两个入口都在④为 Prefab 中的每个模型生成 `ModelUnits`：有需保留相对路径的文件时整组复制，其余模型按分类平铺。当前格式扫描器支持 glTF；OBJ 的 `.mtl` 继续由现有分类复制逻辑跟拷。
