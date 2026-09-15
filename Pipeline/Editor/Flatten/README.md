# Pipeline / Flatten — ④ 中间层（调度 / 操作）

不搬 `RetinarBatchModelBuilder` 内核。这里只放编排与操作数据。

```text
Flatten/
├─ Orchestration/   调度：选中、提示、③（模型）、ToolFlattenApi.Run
└─ Operations/      操作：GltfPackageFiles.Scan → FlattenPlan，禁止 PipelineJobContext
```

管线自动④仍在 `PipelineRunner`（②.5 的 ctx 只在那里译成 plan）。人工④不建 ctx。
