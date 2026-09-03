# 40_Api — 插件 1 对外窄口

供 `Plugin/Pipeline` 调用。菜单仍走 `01_RetinarMenu` → Scheduler。

| 类 | 步骤 | 说明 |
|---|---|---|
| `RetinarFlattenApi` | ④ | 管线按能力组合（Begin / B\|B′ / E / D / C / Finish）；`FlattenPaths` 仅菜单兼容 |
| `RetinarFlattenWork` | ④ | 单份工作单，源→副本表在此，不进 ctx |
| `RetinarFlattenOptions` | ④ | 执行闸；不读 Pipeline ctx |
| `RetinarAbApi` | ⑥ | **Build**：仅双端 AB（可选 UP）；**不跑**规范化门禁 |

人工菜单只剩平铺两项，出包走管线⑥，不经本夹也可继续用。
