# D1 核对：⑥ BuildAbOnly（仅双端 AB）

返回 [实现流程](./pipeline-flow.md) · [待办](../03_open-items/backlog.md)

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
| 契约 1 文件名 | **退化/现网**：保持 `name.assetbundle` + 平台文件夹（APP 已按此取包） |
| 契约 2 包内 main | **退化/现网**：不强制改 `main` |

---

## 契约 1 / 2（已确认可退化）

以现网 APP 为准，本阶段**不改**命名与包内主资源名。若日后 V1.2 要统一，再开专项。

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

## 相关产品规则

- **⑤ 依赖④**  
- Prefab 在 `Assets/Incoming/...` 下命名：用**导入夹名**，不对 `Assets` 再向上三层  
