# Shared / Api — 插件 2 对外窄口

供 `Plugin/Pipeline` 与其它编排调用。**不要**从编排层直接碰 Window / Op 内部类。

| 类 | 步骤 | 说明 |
|---|---|---|
| `ToolImportApi` | ② | **单文件** `ImportSingleModel`；批量仍 `ExecuteBatch` |
| `ToolPrefabApi` | ③ | → `Generated/Prefab` |
| `ToolPostProcessApi` | ⑤ | → L1 子流程（总批量）；编排默认不跑 |

⑤ 具体 Op 集合与压缩参数仍在 L3 SO；本口只触发「按 L1 配置跑」。
