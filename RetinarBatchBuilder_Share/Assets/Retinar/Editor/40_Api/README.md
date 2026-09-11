# 40_Api — 插件 1 对外窄口

供 `Plugin/Pipeline` 调用。④ 已迁插件 2（`ToolFlattenApi`）。本夹只留 ⑥。

菜单「平铺到 Art」仍挂在 `01_RetinarMenu`，转调插件 2 `RetinarFlattenScheduler`。

| 类 | 步骤 | 说明 |
|---|---|---|
| `RetinarAbApi` | ⑥ | **Build**：仅双端 AB（可选 UP）；**不跑**规范化门禁 |
| `RetinarExportSettings` / `RetinarAbBuildOptions` | ⑥ | 交付根 / 是否 UP |
