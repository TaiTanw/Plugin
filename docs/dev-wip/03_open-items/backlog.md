# 当前待办

现约：[开发者须知](../README.md)。本页只保留队列。历史切片不堆在这里。

## A. Open items

调试暂放（2026-09-18）：闸代码已在；缺能咬住退出码的失败真样。不接着造样本、不改闸。

测试1112（2026-09-22，**v1.6.8**）：同单元同名两套图的身份警告已关，清空重导 `exit=0`。见 [flatten-42-worklog](../04_implementation/flatten-42-worklog.md)。下表 1–3 仍是失败形态，本样没有顶上。

| 顺序 | ID | 问题 | 状态 |
|---|---|---|---|
| 1 | D24-R1b | 单元外 `.fbm` 依赖应 `exit=41` 且⑤⑥继续 | **调试暂放** |
| 2 | D25-2 B | FBX 内嵌抽到单元外 `.fbm` 时 E 口才该 Extract | **调试暂放** |
| 3 | D25-4 | 同名贴图串兄弟单元应 `exit=42`、保留原引用 | **调试暂放** |
| 4 | D26-6 | 无 Op 时 typed 告警、⑤不阻断⑥ | 代码已收 |

不要拆 3518 行目录、不要重写⑥、不要在本仓接 License/Docker。坏行跳过、稳定行 ID、⑥改名：**停放**。

## F. Pack 识别（第一刀已收）

最简：pack **不当一批**。预处理进信封后，只按包内 **根 Prefab** 各拆一行，后面与现网多行批相同。Pack 的 ID2 **只**加在管线导入根上：`导入根/<packID2>/`。④ 入盘仍是 `Art/<根Prefab名>/`，⑥ 跟该文件名。无根 Prefab → **[1] 失败（20）**。

**入口（第一刀）：** 只在 **管线总面板 [1] 预览** 用「浏览…」选 **一个** `.unitypackage`（或路径框贴一个文件）。选择器不收 pack。拖入区仍只认模型（见操作者须知）。人工 Project 拖包 / 人工④：选中解包后的**根文件夹**再点④（与管线同一套根 Prefab 规则）。CLI 同源 Runner：`-source` 可为 `.unitypackage`（不当文件夹批量）。

**预处理（已拍）：** 不解 `ImportPackage`。Assets 外解 tar → **删除** `.cs`/`.dll` 及其 `.meta` → 带其余 `.meta` 落到调用方传入的那一个导入根。不双看人工根。模块：`TOol/Editor/Shared/UnityPackage/`；编排只调 `ToolImportApi`（**不要**把 pack 加进 `GetSupportedModelExtensions`）。

**展开（已拍）：** 信封内找 **根** Prefab（包内没有其它 Prefab 依赖它）→ 各拆一行（`MaterialId`=文件 stem）。被套在里面的子 Prefab 文件仍在信封里，**不单独开一批**。无根 Prefab → **20**。③：已是 Prefab 原样交。④⑥ 不改。

| 单元 | 做什么 | 第一刀 |
|---|---|---|
| **F1** | `Shared/UnityPackage`：解 tar、删脚本/dll+meta、改写到信封相对尾巴 | **通过** |
| **F2** | `ToolImportApi` 分派 pack → 清并写入 `导入根/<packID2>/`，一次 Refresh | **通过** |
| **F3** | 总面板浏览单文件加 `.unitypackage`；表上只允许 **一条** pack。选择器不加 | **通过** |
| **F3b** | 只收根 Prefab，无则 20；子 Prefab 不拆行。展开后走现网 ③④⑤⑥ | **通过** |
| **F4** | 人工拖入工程、点原址 pack 根④ | **通过** |
| **F5** | ③ 对已是 Prefab 原样交；不用 packID2 当 Prefab 名 | **通过** |
| **F-CLI** | `-source` 一个 `.unitypackage`，走同一套 Runner | **通过** |
| **F6** | ④ 现状 `Art/<Prefab名>/` | **不改** |
| **F7** | 不为 pack 升 `AssetUnitFolder` | **不升级** |
| **F8** | 占用最小（包内贴图只留一份） | **降优先** |
| **F9** | 同名 Prefab 抢 `Art/<名>/`：管线默认先清空该槽再写，**后一行覆盖先一行**。不另做 Conflict | **暂放（不另做）** |

不做：CLI 文件夹批量；选择器认 pack；pack 当第二套管线；④ 用 packID2 当 Art 信封；Plan 并进 Orchestration。

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
