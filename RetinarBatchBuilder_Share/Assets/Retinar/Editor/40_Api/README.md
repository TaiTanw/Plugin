# 40_Api — 插件 1 对外窄口

供 `Plugin/Pipeline` 调用。菜单仍走 `01_RetinarMenu` → Scheduler。

| 类 | 步骤 | 说明 |
|---|---|---|
| `RetinarFlattenApi` | ④ | 路径列表平铺（quiet）；可选 `RetinarFlattenOptions.SkipDependencySplit` |
| `RetinarFlattenOptions` | ④ | 执行闸；不读 Pipeline ctx |
| `RetinarAbApi` | ⑥ | **Build**：仅双端 AB（可选 UP）；**不跑**规范化门禁 |

人工「规范化导出 / 成品直通」菜单保留，不经本夹也可继续用。
