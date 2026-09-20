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

## F. Pack 识别（开设，评估中）

目标：一个 `.unitypackage` 当多资源信封。不做 CLI 文件夹批量、不在 argv 铺 N 个 ID2。**不写操作者/开发者须知**。最简：结构仍走管线 [1]→③→④，不把 pack 加进选择器共用后缀表。

**评估（已拍）：** 管线（含点「运行管线」）与人工路径配置已拆开，可走不同根出同一结果。整根清空机器现状 **只在管线跑完** 跟步骤 SO（导入根+Prefab 根 / 交付根），现成 `TryClearRootContents`。因此 **不为 pack 升 `AssetUnitFolder` 信封删除器**；跑中槽位清仍是现网单段（`Incoming/<ID2>/`、`Art/<Prefab名>/`），pack 第一刀 Incoming 信封刚好是单段，够用。

**入口（已拍）：** 第一刀重点 **CLI `-source` 一个 pack**。编排面板 **单文件** 同源（同一 Runner）。选择器 **[1] 不支持 pack**（含「执行导入」）。人工不走 [1]、直接拖资源进工程再④：**下一步**。

**Assets 前预处理（已拍）：** 不解 `ImportPackage`。Assets 外解 tar → **删除** `.cs` / `.dll`（不改后缀）→ 带原 `.meta` 落到**调用方传入的那一个导入根**/`<ID2>/`。不管线+手动两套根做判断。引用靠 meta guid，不重绑。

**落点：** 编排只调 `ToolImportApi`（勿把 `.unitypackage` 加进 `GetSupportedModelExtensions`）。正文 **单开** `TOol/Editor/Shared/UnityPackage/`（解包/删除/改写路径），不要塞进 `ToolImportApi.cs`、选择器、`Pipeline/`、Retinar `20_Package`（那是⑥导出）。`Model/Import` 是 [2] Processor，不管 pack。

**核对（未拍）：** 是否同删 `.asmdef` / 原生插件 / `.js`；是否带文件夹 meta；包内 `.unity` 场景收不收；删除脚本后 Prefab 缺脚本是否只 Warning。

第一刀：④ **保持现状**。⑥ 不重写。

| 单元 | 做什么 | 第一刀 |
|---|---|---|
| **F1 识别+预处理** | 扫清单；删脚本/dll；路径只当信封内相对尾巴。不按原址 Art/Plugin 落地 | 开（模块 `Shared/UnityPackage`） |
| **F2 管线入库** | Api 分派 → 信封 `导入根/<ID2>/` → 一次 Refresh。选择器共用模型表不加 pack | 开 |
| **F3 一次一 pack** | CLI 与编排单文件各一包。选择器不收 pack，故无选择器批量 pack | 开 |
| **F4 人工拖入 / ④闸** | 不走 [1] 的拖入、以及点「原址 pack 根」人工④ | **下一步** |
| **F5 ③ 命名** | ID2 只当 Incoming 信封夹；Prefab 名跟内层 | 开（随 F2） |
| **F6 ④** | 现网逐 Prefab 平铺 | **保持现状** |
| **F7 清夹** | 整根清空 = 步骤 SO 现有勾选。不为 pack 改 `AssetUnitFolder` | **不升级** |
| **F8 占用最小** | 包内贴图/材质只留一份 | **降优先** |
| **F9 夹名重复** | 人工 Conflict vs 管线覆盖槽收束 | **暂放** |

不做：CLI 文件夹批量；选择器认 pack；pack 当第二套管线；Plan 并进 Orchestration；执行层认 `PipelineSourceBinding`。

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
