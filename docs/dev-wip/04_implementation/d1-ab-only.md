# D1 核对：⑥ BuildAbOnly（仅双端 AB）

返回 [实现流程](./pipeline-flow.md) · [待办](../03_open-items/backlog.md)

> **状态：已收口（2026-08-26）。** 文档保留备查。  
> 实机样例：`Assets/Art/ggdddd`（GLB 通道出双端 AB）。安卓洋红不属本项，见待办 D13。  
> **2026-09-02：** 工单文件名仍未改代码。意向 = **夹名不变**（`Android`/`iOS`），夹内改为 `{id}_android.assetbundle` + `{id}_ios.assetbundle`。真正成本是重开契约 1（APP 取包）。#274 在 [YuWu 14305](http://swm-server.local:3000/Admin/prd-docs/issues/274#issuecomment-14305) 之后只移出 milestone 49，无 APP 书面改名。未改 APP 不切默认。见 [开发日志 §2](../05_dev-log/timeline.md#2-issue-274)。

## 已锁定（本阶段）

| 项 | 结论 |
|---|---|
| 产物 | **仅** Android + iOS AssetBundle |
| 输入 | 未开④：③ Prefab；**开④：平铺返回的 Art Prefab** |
| Quiet | 无确认框；Console 日志 |
| 不做 | UnityPackage、全套 Deliverables 文档、SafeZone/门禁硬阻断 |
| 落盘 | `AssetBundles/{Android,iOS}` + 拷到 `Deliverables/<名>/03_assetbundles` |
| API | `RetinarAbApi.BuildAbOnly` |
| 压缩 | **`ChunkBasedCompression`（LZ4）** |
| 契约 1 文件名 | **退化/现网** `name.assetbundle` + 平台夹。工单两文件名 = **评估未开发**（见文首 2026-09-02） |
| 契约 2 包内 main | **退化/现网**：不强制改 `main` |

---

## 契约 1 / 2（已确认可退化）

以现网 APP 为准，本阶段**不改**命名与包内主资源名。若日后 V1.2 要统一，再开专项。

插件改名范围小（`RetinarAbApi` 按平台循环已具备）；直通会跟着变；规范化导出旧路径不会自动变。契约 2（`main`）不要绑进文件名这一刀。上传层改名仍是备选（你在 #274 的回复已写过）。

---

## ④ + ⑥：如何找到 Art 资源？

**不要**靠人手在面板里拼 Art 路径字符串。

正确做法（已按此改 Runner）：

```text
③ 产出 IncomingPrefab/*.prefab
  → ④ Flatten 返回 Art/.../Prefab/*.prefab 列表（数据）
  → ⑥ 用该列表打 AB
```

字符串只出现在日志/调试里；编排链路用**平铺返回值**，不是猜 `Assets/Art/{?}/Prefab/{?}.prefab`。

---

## materialId（可选）含义

| 现状 | 目标（产品意图） |
|---|---|
| 只覆盖 **③ Prefab 文件名** | 希望成为整条链业务 Id：Prefab →（④ Art 名）→（⑥ AB 名） |

- **现在填 materialId**：③ 文件名用 Id；④ 一般跟 Prefab 名走，故常能间接影响 Art；⑥ 现网仍用资产名打 AB（契约 1 退化）。  
- **留空**：导入夹名（三层规则）→ Prefab 名（已修：不再出现 `Assets_Incoming_…`）。

若要把 materialId **强制**贯穿 AB 文件名，需另开需求（与契约 1 冲突时以 APP 为准）。

---

## 与「成品直通」的关系 / 输出目录

| 入口 | AB | UnityPackage | 改 Prefab / 门禁 |
|---|---|---|---|
| `RetinarAbApi.BuildAbOnly`（管线⑥） | ✓ | ✗ | ✗ |
| `RetinarDirectPackage`（菜单直通） | ✓（已共用 AbApi） | ✓ | ✗ |
| 规范化导出 `ExportArtPrefabPaths` | ✓ | ✓ + 全套 | ✓ 校验/可再规范化 |

**可合并：** AB 内核已共用；**D8 已做**：`RetinarExportSettings`（交付根/AB 根/UP）+ `RetinarAbBuildOptions`；直通菜单薄门面调 `RetinarAbApi.Build`；管线⑥勾选 UP +「导出路径设置」按钮。  

---

## 碰撞体 / 缩放 / 门禁在「只出 AB」上如何禁用？

**⑥ BuildAbOnly / 直通打 AB：这些在出包步骤里本来就不会跑**——不是传 `skipGates=true`，而是**走了另一条 API**（不调用 `ExportArtPrefabPaths`）。

| 能力 | 挂在哪 | 只出 AB（⑥） | 开④平铺时 | 规范化导出 |
|---|---|---|---|---|
| 碰撞体 | ④ 平铺末（Prefs `FlattenPostProcessSettings.AddBoxCollider`，默认开） | 不跑 | **会跑**（除非 Prefs 关） | 可能再规范化 |
| SafeZone 缩放 | ④ 内核（FBX 入口更明显；外来 Prefab 多套壳不缩放） | 不跑 | **可能改 Prefab** | 可能再跑 |
| 门禁/SafeZone 校验阻断 | 规范化 `PartitionAssetsThatPassValidation` | **不跑** | 不跑 | **会跑** |
| `IRetinarAcceptanceGate` | 未接线 | — | — | Legacy 硬编码校验 |

结论：管线默认 ③→⑥、④关 → **碰撞体/缩放/门禁都不介入**。  
开④ → 碰撞体/缩放属**平铺副作用**，与⑥无关；若只要干净 AB，保持④关，或关 `AddBoxCollider` Prefs。  
