# 打包规则（当前口径）

版本：1.2  
最近同步：2026-09-14  

本文**只保留现行约定**。旧编号 1–41 的完整条文已删除，避免与现网并行。追溯查 `CHANGELOG.md` 与当时提交，不要把旧句当阻断。

当前已确认方向：[战略](../docs/dev-wip/01_requirements/strategy.md) · [结构](../docs/dev-wip/02_structure/overview.md) · [待办](../docs/dev-wip/03_open-items/backlog.md) · [回归](./REGRESSION_CHECKLIST.md)

## 仍有效

| 主题 | 现行 |
|---|---|
| 用户原始源 | 不得改用户工程外原文件及其 Importer。②/④ 可清空并重建**受控** Incoming/Art **本趟单元**（含该单元 `.meta`），不承诺保留旧 GUID/人工改过的副本 |
| Importer 分区 | Art 模型只由④ `ApplyArtDelivery`（InPrefab + Local）。Processor 硬跳过 Art。Incoming 安全基线不跟总闸；策略自动仍要总闸+分项 |
| ⑤ | 用户开了⑤时允许指定 OP 改交付副本。透明修复在插件 2 Material OP，不扩模型 ctx |
| C 另存 `.mat` | B/B′ 都执行；不以 `MaterialForm` 为闸 |
| 模型格式 | FBX/OBJ/GLB/glTF。B′ 另有相对 URI 整树。SafeZone 只用于人工直接选 FBX。碰撞体跟平铺 SO，默认关 |
| 缺伴生 | 旧⑥门禁已删。glTF `MissingUris` 非空 → ④ `FlattenFailed(40)`，**整趟停止**，不承诺其余行继续。OBJ 缺 `.mtl`/贴图**不进**该闸（见待办说明） |
| `.fbm` | 禁止改缓存文件。④ 自愈；无⑥兜底。B 残留外部 `.fbm` 仍可进 AB（B 质量闸待④拆分后评估） |
| 贴图体积/报告 | 旧 5MB 报告线、`01_source`、xlsx、runtime 文案已删。压图走插件 2 SO/OP |
| 顶点色 | D19：重导可冲色，不是 CLI/AB 必须全白 |
| AB 标签 | 现网④ Finish 仍写；已拍迁⑥清单，先核对外依赖（D24-R4） |
| InPrefab | 不是旧门禁遗物。External 会在目标工程自动生成 `Materials/` 与 `.fbm` 目录 |

## 明确不作现行要求

- 规范化导出 / 成品直达 / DirectPackage / 出包前三道校验
- 「未知依赖必须停⑥」「仅单资产清 bundle 名、其余继续」
- 完成弹窗、全套 00–06 交付夹、`texture_size_report`
- 平铺七步作为菜单/CLI 独立产品操作
