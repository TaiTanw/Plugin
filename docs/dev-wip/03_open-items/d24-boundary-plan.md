# D24：平铺黑盒接管与 ctx 边界

返回 [当前待办](./backlog.md#a-open-items) · [④能力查封](../04_implementation/pipeline-flatten-capabilities.md) · [ctx](../04_implementation/pipeline-job-context.md)

> 状态：**目录迁移、P0 静态查封、源资产保护、配置 SO 化与两个人工相位入口已完成；职责拆分未完成。** 因④当前必须消费 ctx，平铺编排内核先归中间层；不为追求目录纯净强拆。完整查封见 [flatten-core-audit](../04_implementation/flatten-core-audit.md)。

## 1. 当前结论

1. `RetinarBatchModelBuilder.cs`（2280 行）、`AssetResolution.cs`（1014 行）、`AtomicRelocate.cs`（224 行）已经从插件 1 搬到插件 2 `Generated/Flatten/Service/`，合计 3,518 行。静态调用图和方法归类已经完成，但真实模型回归尚未覆盖全部风险；**文件在哪里不等于谁真正拥有。**
2. 中间层已经能按 Begin / B / B′ / E / D / C / Finish 调用；`ToolFlattenApi` 和 `FlattenBuildService` 直接接收 `PipelineJobContext`。本轮把这部分**按职责暂归中间层**，所以直接读 ctx 暂不视为阻断缺陷；真正的问题是物理目录和命名仍像插件 2，边界没有写清。
3. B 与 B′ 仍只是互斥选路；成功验收已单独补上：ctx 的 typed `MissingUris` 在 Begin 前触发 `FlattenFailed(40)`，B′ 也要求所有必需输入生成精确目标。
4. ④仍以完整相位交付。2026-09-10 已增加“普通平铺（B）”与“原子迁移（B′）”两个手动相位按钮；它们选择分支但都会继续完成 E?/D/C/Finish，不是七步任意执行。
5. 插件 1 的目标边界是**只负责输出文件格式与 AB**，不再拥有资源平铺、Importer、Prefab、材质或 Art 内容变换。

## 2. 目标分层

| 层 | 应负责 | 不应负责 |
|---|---|---|
| 中间层 `Pipeline/`（含暂归的④编排内核） | ②.5 构建一次 ctx；按行对齐；校验缺失；选择 B/B′；决定是否调用 E；按序组合七步；聚合结果/退出码 | 每步重新 Build ctx；把 Pipeline SO 或菜单状态散进执行叶 |
| 插件 2 `TOol/` | 继续承载已存在的资源执行能力；后续只在边界清楚、测试覆盖后下沉不需要 ctx 的执行叶 | 为了支持独立按钮而重复扫描源；擅自决定整趟分支或退出码 |
| 插件 1 `Retinar.../` | AB 平台、压缩、bundle 名/variant、输出目录与文件格式 | 改 Art、Importer、Prefab、材质、轴向、伴生文件、引用映射 |
| 菜单适配层 | 为 FBX SafeZone 入口组装菜单默认请求 | 假装拥有管线 ctx；复用 `FlattenPaths` 进入管线④ |

目标调用：

```text
PipelineRunner
  → PipelineJobContext.Build(primaryAssetPath)             // 只一次
  → ValidateContext(ctx)                                  // 缺必需伴生先失败
  → BuildFlattenDecision(ctx, binding, pipelineOptions)   // 中间层内决定
  → Begin(ctx, request)
  → ctx.HasExternalUris ? RelocateAtomic(work) : SplitDependencies(work)
  → if (ctx.ImporterKind == ModelImporter) ApplyImportAndExtract(work)
  → Remap(work) → CopyRendererMaterials(work) → Finish(work)
  → FlattenPhaseResult
```

若后续确认要把无 ctx 的执行叶下沉，再引入 `ToolFlattenPlan`（名字可再定）。它只应是中间层生成的执行快照：

| 字段 | 来源 | 用途 |
|---|---|---|
| `Branch` | `ctx.HasExternalUris` | B / B′ 互斥 |
| `PrimaryAssetPath` / `SidecarPaths` | ctx | B′ 原子白名单 |
| `ApplyModelImportAndExtract` | `ctx.ImporterKind` | 是否调用 E |
| `ClearDestinationArtFolder` | Pipeline request | 管线覆盖写；菜单默认 false |
| `ConvertZUpToYUp` | binding/request | 人给的轴向决定，不进 ctx |
| 分类开关/后缀、`AddBoxCollider` | `FlattenOperationSettings` SO | 人工与管线同数据类、不同实例；运行前冻结 |

当前已先实现 `FlattenOperationPolicy` 配置快照，不要求为了形式分层立刻生成完整 plan。管线 ctx 仍只在②.5构建一次；人工操作因没有②.5，会在点击时对选中的原始模型/Prefab 依赖构建一次 ctx，之后整趟复用，步骤内不重复探测。

## 3. 按序接管黑盒

### R1 静态查封（P0，已完成）

- 已画出两个入口的调用图：管线 Prefab 平铺、菜单 FBX SafeZone。
- 已把 3,518 行逐方法标成：管线④、菜单专用、两者共用内核、历史死码、疑似⑥格式副作用。
- 固定最小回归集：FBX 外贴图、FBX 内嵌贴图、OBJ+MTL、GLB、完整 glTF+sidecar、缺 sidecar glTF、同名跨单元贴图、OBJ 轴向开/关。
- 记录每个样例的 Art 树、Prefab 依赖、Importer 关键字段、退出码。没有这份基线前，不再用批量脚本切大文件。

### R2 固定临时归属与 SO 快照（P1，本刀已完成）

- 本轮先把 `ToolFlattenApi`、`FlattenBuildService` 与七步组合标成中间层能力，即使文件暂在 `TOol/Generated/Flatten`。
- ctx 仍只在②.5 Build 一次；各分步不得自己重建。
- ctx 新增 typed `MissingUris`；中间层直接据此 Fail，不解析 `Warnings` 文案。
- 人工与管线使用同一个 `FlattenOperationSettings` 数据类，但分别位于 `TOol/ConfigData/Manual` 与 `Pipeline/ConfigData`；面板只可编辑自身目录的 SO，拖入另一来源时只读。
- 分类、清夹、碰撞体在运行前冻结为 `FlattenOperationPolicy`，沿 request/work 传递；平铺内核不再读取 `EditorPrefs`。
- 等黑盒方法归类和回归完成后，再决定：整体移入 `Pipeline/`，或由中间层生成 plan、仅把无 ctx 执行叶留在插件 2。
- 在最终归属拍板前，不做第二次目录搬迁和大规模改名。

### R3 再按能力拆文件（P1）

建议拆的是实现文件，不是产品按钮：

| 文件/能力 | 从黑盒中抽什么 | 验收 |
|---|---|---|
| `FlattenBeginStep` | 清单元夹、写 Art Prefab、建立 work/map | 失败不产生可继续 work |
| `FlattenSplitStep` | B 分类拷贝、OBJ `.mtl` 跟拷 | 不处理外 URI 包 |
| `FlattenAtomicStep` | B′ 主文件+sidecar 原子树 | 清单完整才成功 |
| `FlattenImportStep` | E Art ModelImporter + Extract | 只由 plan 开闸 |
| `FlattenRemapStep` | D 引用重映射 | 不跨单元查找 |
| `FlattenMaterialStep` | C Renderer `.mat` 与贴图副本 | 明确是否允许双份贴图 |
| `FlattenFinishStep` | 自愈、动画、空壳、轴向、碰撞盒 | AB 格式副作用移出后仍保持 Prefab 结果 |
| `MenuFbxFlattenAdapter` | `CreateNormalizedPrefab` / SafeZone | 与管线入口物理隔离 |

每抽一项就跑对应回归，不做“一次性重写 3,518 行”。原类在调用方清零后再删/改名。

### R3b 两个人工相位入口（P1，已实现）

| 按钮 | 选择的分支 | 输入与保护 |
|---|---|---|
| 普通平铺（B） | 按 SO 分类规则拆依赖 | 接受 Prefab 或 `.fbx/.obj/.glb/.gltf`；检测到外部相对 URI 时拒绝，提示改用 B′ |
| 原子迁移（B′） | 主 `.gltf` + sidecar 保持相对树 | 接受 `.gltf`，或恰好依赖一个 `.gltf` 包的 Prefab；无外 URI、缺伴生、多模型包均拒绝 |

直接选择非 FBX 模型时，人工服务先调用③生成 Prefab，再执行完整④。FBX 普通平铺保留既有 SafeZone 入口。两个按钮都执行 Begin→B/B′→E?→D→C→Finish；没有开放 Begin/B/D 等任意乱序按钮，因此不需要跨域保存 work/checkpoint。

### R4 最后收插件 1 输出格式（P1）

必须先把“格式”写成白名单：

- 允许：Android/iOS 平台选择、LZ4 等压缩、bundle 名和 variant、输出文件名、输出目录。
- 待拍：`assetBundleName` 是④ Finish 提前写，还是⑥ `RetinarAbApi` 在构建前写。若插件 1 只负责格式，建议迁到⑥入口附近，并由构建结束清理/恢复，避免平铺相位夹带出包策略。
- 禁止：插件 1 再写 Art Importer、Extract、Remap、Prefab Transform、材质、贴图、sidecar。

## 4. 本轮增减清单

### 已增加

- typed `MissingUris` 与日志计数；缺件已接④失败闸。
- `FlattenOperationSettings`：同数据类的人工/管线两份 SO。
- `FlattenOperationPolicy`：分类、清夹、碰撞体的一次性快照；管线日志记录来源与完整分类值。
- 普通平铺/原子迁移两个手动相位按钮及错误保护。

### 仍需要增加

- “声明数/找到数”的完整可核验信息。
- `FlattenPhaseResult`；结果至少含失败步骤、原因、产物 Prefab、拷贝计数。`ToolFlattenPlan` 仅在确认下沉执行叶时增加。
- ctx / binding / prefab 的稳定行 ID，禁止列表不足时回退第一项。
- 上述最小回归集和黑盒方法归类表。
- 插件 1 输出格式白名单。

### 需要删除或收窄

- 插件 2 内通过 Warning 字符串判断缺伴生。
- 管线调用 `FlattenPaths` 的可能性；它只留菜单兼容，后续迁入独立 adapter。
- 菜单 FBX SafeZone 与管线 Prefab 平铺在同一巨型类中的混居。
- `FlattenBuildSettings.ArtRoot` / `RetinarPaths.ArtRoot` 双常量。
- ④ Finish 中不属于平铺的 AB 格式副作用。
- `Retinar*` 类型名和注释；仅在行为测试覆盖后机械改名。

### 本轮明确不增加

- Begin/B/B′/E/D/C/Finish 的菜单或 CLI 独立按钮。
- 每个分步自行扫描并重建 ctx。
- 为缺 ctx 再造一套后缀猜测分支。
- 在拆分同时顺手改变材质、Extract、自愈或 SafeZone 业务规则。

## 5. 完成定义

D24 结构收口只有同时满足以下条件才算完成：

1. ctx 只在②.5构建一次；当前消费 ctx 的平铺编排代码已明确归中间层，任何执行叶和人工入口都不重复探测。
2. 管线④只有一个完整入口，B/B′ 内部互斥；菜单 SafeZone 是独立适配入口。
3. 缺必需 sidecar 不再静默成功；首个失败码不被后续步骤覆盖。
4. 3,518 行均已归类，管线主路径已按能力拆开，未归类代码不得直接删除。
5. 插件 1 只剩白名单内的⑥输出格式/AB 能力。
6. 回归集通过，且两入口的差异有明确文档，不依赖“当前刚好能跑”。
