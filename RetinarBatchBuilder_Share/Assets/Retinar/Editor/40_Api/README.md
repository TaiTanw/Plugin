# 40_Api — 插件 1 对外窄口

供 `Plugin/Pipeline` 调用。菜单仍走 `01_RetinarMenu` → Scheduler。

| 类 | 步骤 | 说明 |
|---|---|---|
| `RetinarFlattenApi` | ④ | 路径列表平铺（quiet 无弹窗） |
| `RetinarAbApi` | ⑥ | **BuildAbOnly**：仅 Android/iOS AB，无 UP、无确认框 |

人工「规范化导出 / 成品直通」菜单保留，不经本夹也可继续用。
