# 当前待办

现约：[开发者须知](../README.md)。本页只保留队列。历史切片不堆在这里。

## A. Open items

调试暂放（2026-09-18）：闸代码已在；缺能咬住退出码的真样。不接着造样本、不改闸。

| 顺序 | ID | 问题 | 状态 |
|---|---|---|---|
| 1 | D24-R1b | 单元外 `.fbm` 依赖应 `exit=41` 且⑤⑥继续 | **调试暂放** |
| 2 | D25-2 B | FBX 内嵌抽到单元外 `.fbm` 时 E 口才该 Extract | **调试暂放** |
| 3 | D25-4 | 同名贴图串兄弟单元应 `exit=42`、保留原引用 | **调试暂放** |
| 4 | D26-6 | 无 Op 时 typed 告警、⑤不阻断⑥ | 代码已收 |

不要拆 3518 行目录、不要重写⑥、不要在本仓接 License/Docker。坏行跳过、稳定行 ID、⑥改名：**停放**。

```text
②.5 只构建 ctx（人工禁止）
  → 编排译成 FlattenPlan（内核不读 ctx）
  → Begin → B|B′ → E? → D → C → Finish
  → 40 整趟停；41 / 42 报错不卡
```

## OBJ vs glTF 缺伴生

| | glTF | OBJ |
|---|---|---|
| 入库 | 缺 `.bin`/外图 → Warning，仍拷已有 | 缺 `.mtl`/贴图 → Warning，仍导入（常白膜） |
| ctx | typed `MissingUris` | **不扫** OBJ 包；`MissingUris` 空 |
| ④ | 非空 → **40** 整趟停 | 不进 40；缺件仍可能成功 |

`.glb` / `.fbx` 同样不填 `MissingUris`。整趟停无磁盘回滚。

## B. Unresolved

工程名 / submodule / 两宿主对齐；素材库是否永远只要双端 AB；嵌套 GLB Prefab 禁止 vs 警告；Converter 是否专用 URP。  
CLI 扩参、License 70、iOS-on-Linux：D5 已冻本入口不扩；属另开项或基建。

## C. Known risks

- 关④时⑥可打任意 Prefab，勿写死必须 Art。
- ⑤ 压独立文件；GLB 内嵌图空跑 ≠ 合规。
- 导入期不碰 Art ≠ ⑤/L1 不碰 Art。
- ④ 代码在 `TOol/Generated/Flatten`，拆文件前不二次搬家。

## E. Low priority

L2 勾选继续 Prefs；Shared 扁平脚本迁夹（目录卫生）。
