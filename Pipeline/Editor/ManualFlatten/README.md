# Pipeline / ManualFlatten — 人工④唯一调度入口

不搬 `RetinarBatchModelBuilder` 内核。自动④在 `PipelineRunner`，不进本夹。

操作者：总面板 **[④]「打开平铺面板」** 或 `Tools > Retinar > 批量汇总`。人工跑完整④，**不**自动⑤⑥。选中解包后的根文件夹时，只按根 Prefab 拆行。

```text
ManualFlatten/
├─ Orchestration/   选中、确认弹窗、模型先③、ToolFlattenApi.Run
└─ Plan/            GltfPackageFiles.Scan → FlattenPlan，禁止 PipelineJobContext
```

管线：已有 ctx → `FromContext` → `Run(plan)`。人工不建 ctx。相对 URI 选 B：仍要平铺才继续，取消/叉号中止。
