# D24：平铺黑盒接管与 ctx 边界

返回 [当前待办](./backlog.md#a-open-items) · [④能力查封](../04_implementation/pipeline-flatten-capabilities.md) · [ctx](../04_implementation/pipeline-job-context.md)

> 状态：**目录迁移、P0 静态查封、源资产保护、配置 SO 化与两个人工相位入口已完成；职责拆分未完成。** 实施步骤单页：[d24-flatten-steps](./d24-flatten-steps.md)。

## 1. 当前结论

1. `RetinarBatchModelBuilder.cs`（2280 行）、`AssetResolution.cs`（1014 行）、`AtomicRelocate.cs`（224 行）已经从插件 1 搬到插件 2 `Generated/Flatten/Service/`，合计 3,518 行。静态调用图和方法归类已经完成，但真实模型回归尚未覆盖全部风险；**文件在哪里不等于谁真正拥有。**
2. 中间层已经能按 Begin / B / B′ / E / D / C / Finish 调用；`ToolFlattenApi` 和 `FlattenBuildService` 直接接收 `PipelineJobContext`。本轮把这部分**按职责暂归中间层**，所以直接读 ctx 暂不视为阻断缺陷；真正的问题是物理目录和命名仍像插件 2，边界没有写清。
3. B 与 B′ 仍只是互斥选路。glTF typed `MissingUris` 在 Begin 前触发 `FlattenFailed(40)` **并停止整趟④**（⑤⑥不跑；已写出的前几行 Art 不回滚）。B′ 另要求所有必需输入生成精确目标。**OBJ 缺 `.mtl`/贴图不进 `MissingUris`，不是漏修 D26-2。**
4. ④仍以完整相位交付。2026-09-10 已增加“普通平铺（B）”与“原子迁移（B′）”两个手动相位按钮；它们选择分支但都会继续完成 E?/D/C/Finish，不是七步任意执行。
5. 插件 1 的目标边界是**只负责输出文件格式与 AB**，不再拥有资源平铺、Importer、Prefab、材质或 Art 内容变换。
6. **物理目录暂时保留**（2026-09-14）：文件继续放在 `TOol/Generated/Flatten`，编排暂归中间层；等 R3 按文件拆完再评估搬家/下沉，不做第二次目录搬迁。
7. **B 质量闸**（拷贝表非 null 即成功、残留外部 `.fbm` 只 Warning 仍进⑥）当前只记风险，R3 拆步后再评估是否加失败码。

## 2. 目标分层

| 层 | 应负责 | 不应负责 |
|---|---|---|
| 中间层 `Pipeline/`（含暂归的④编排内核） | ②.5 构建一次 ctx；按行对齐；校验缺失；选择 B/B′；决定是否调用 E；按序组合七步；聚合结果/退出码 | 每步重新 Build ctx；把 Pipeline SO 或菜单状态散进执行叶 |
| 插件 2 `TOol/` | 继续承载已存在的资源执行能力；后续只在边界清楚、测试覆盖后下沉不需要 ctx 的执行叶 | 为了支持独立按钮而重复扫描源；擅自决定整趟分支或退出码 |
| 插件 1 `Retinar.../` | AB 平台、压缩、bundle 名/variant、输出目录与文件格式 | 改 Art、Importer、Prefab、材质、轴向、伴生文件、引用映射 |
| 人工入口适配层 | 点击时为模型/Prefab依赖建立自身ctx；普通/原子完整相位；直接选FBX另走SafeZone | 重建自动管线ctx；复用FlattenPaths进入管线④ |

目标调用：

```text
PipelineRunner
  → PipelineJobContext.Build(primaryAssetPath)             // 每模型每趟一次
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

当前已先实现 `FlattenOperationPolicy` 配置快照。**2026-09-14：** `FlattenPlan` 改为 R3 本刀要落地的类型，不再等「确认下沉」才做。管线 ctx 仍只在②.5构建一次；人工点击时构建一次，步骤内不重复探测。内核切走 ctx 后，编排继续译 plan。

## 3. 按序接管黑盒

### R1 静态查封（P0，已完成）

- 已画出两个入口的调用图：管线 Prefab 平铺、菜单 FBX SafeZone。
- 已把 3,518 行逐方法标成：管线④、菜单专用、两者共用内核、历史死码、疑似⑥格式副作用。
- 固定最小回归集：FBX 外贴图、FBX 内嵌贴图、OBJ+MTL、GLB、完整 glTF+sidecar、缺 sidecar glTF、同名跨单元贴图、OBJ 轴向开/关。
- 记录每个样例的 Art 树、Prefab 依赖、Importer 关键字段、退出码。没有这份基线前，不再用批量脚本切大文件。

### R2 固定临时归属与 SO 快照（P1，本刀已完成）

- 本轮先把 `ToolFlattenApi`、`FlattenBuildService` 与七步组合标成中间层能力，即使文件暂在 `TOol/Generated/Flatten`。
- 自动每模型②.5一次ctx；人工点击时建立自身ctx，分步均不得重建。
- ctx 新增 typed `MissingUris`；中间层直接据此 Fail，不解析 `Warnings` 文案。
- 人工与管线使用同一个 `FlattenOperationSettings` 数据类，但分别位于 `TOol/ConfigData/Manual` 与 `Pipeline/ConfigData`；面板只可编辑自身目录的 SO，拖入另一来源时只读。
- 分类、清夹、碰撞体在运行前冻结为 `FlattenOperationPolicy`，沿 request/work 传递；平铺内核不再读取 `EditorPrefs`。
- 等黑盒方法归类和回归完成后，再决定：整体移入 `Pipeline/`，或由中间层生成 plan、仅把无 ctx 执行叶留在插件 2。
- 在最终归属拍板前，不做第二次目录搬迁和大规模改名。

### r3-flatten-split

**R3 · ④拆分管理（2026-09-14 评估）。** 本刀只管理④。⑥已有独立口，不同期清空重写。先冻结调用契约，再换实现；禁止先删 3,518 行再从零补行为。

#### 三问收口

| 问 | 结论 | 为何不进本刀 |
|---|---|---|
| ① 是否承担 ctx.Build？要不要扩字段、由①+2.5共同构建？ | **否。** 2.5 已在同一 `for` 里紧跟每次成功的①（`ImportOne` → `AttachOneContext`）。ImporterKind / MainAssetOk / MaterialForm 必须等 `ImportAsset` 之后才能观测。① 已有的 sidecar Scan 只是日志，2.5 对 glTF 再扫一次；以后可把 Scan 结果交给 2.5，那是复用不是扩字段。 | 扩 ctx 解决不了无头「一批里坏一件卡死」。那是 Runner 粒度，见下表停放。 |
| 无头不能在①人工排错，直接报错会卡住同批其它资源？ | **属编排策略，不是④内核。** 现网① `ImportOne` 失败已 `return false` 停整批；④ `HasMissingSidecars` 同样 `Fail(40)` 停整趟。人工④（`ManualFlattenService`）已按选中项继续。要改成「坏行跳过、好行继续」只改 PipelineRunner，且与 2026-09-14「缺件整趟停」冲突，须另拍。 | 内核按**一行**返回成功/失败即可。整趟停或跳行由适配层决定。 |
| ④⑥从头清空、抽象管线/窗口口、内部不再依赖旧代码？ | **④：可以新实现，但必须先冻结口、旧代码留到回归切开。⑥：不要动。** `RetinarAbApi.Build(prefabPaths, options)` 已是管线口；窗口只编 `RetinarExportSettings`，没有第二套打 AB 内核。④ Finish 写 `assetBundleName` 是⑥不需要的副作用（D24-R4）。 | 清空旧④会一次丢掉 SafeZone、`.fbm` 自愈、OBJ `.mtl` 跟拷、Extract、跨单元搜图等未完全回归的行为。 |

#### 目标调用（内核不再读 ctx）

```text
自动：②.5 ctx → FromContext → Run(plan)
手动：按钮 Branch + 选中项 + 人工 SO → 组 plan（禁止 Build ctx）→ 同一 Run(plan)
```

管线适配层：ctx + binding + 管线 SO → plan；现网仍整趟停。  
窗口适配层：人已判定 B/B′，不要用 ctx 再推断或否决分支。sidecar 是 B′ 操作输入，走 plan 或操作层包扫描，不走 `PipelineJobContext`。FBX SafeZone 仍只挂窗口。  
内核：不引用 `PipelineJobContext` / `PipelineResult` / `PipelineRunner`；不写 AB 标签。

`FlattenPlan` 最小字段（与 §2 表一致，本刀要落地类型，不再「以后再说」）：

- `Branch`（B / B′）
- `PrimaryAssetPath` / `SidecarPaths` / `MissingUris`（调用方填好）
- `ApplyModelImportAndExtract`
- `ClearDestinationArtFolder` / `ConvertZUpToYUp` / `FlattenOperationPolicy`

现网按钮**不是**内核口。目标：**产品两个按钮，内核一个 `Run(plan)`，`plan.Branch` 在手动侧等于人所选，在自动侧由 ctx 填写。** 不要在手动路径再 Build ctx 来「纠正」按钮。点错 B 导致拆坏相对 URI，是人责或可选的包扫描提示，不是接回 `PipelineJobContext` 的理由。FBX SafeZone 仍只挂窗口适配层。

#### 现网还会绊住④换血的耦合（清空前必须有替代）

| 耦合 | 谁 | 若直接删旧④ |
|---|---|---|
| 管线组合七步 + 缺件闸 | `PipelineRunner.FlattenPerPrefab` → `ToolFlattenApi` | ④整段停 |
| 人工两按钮 | `FlattenWindow` / 菜单 → `ManualFlattenService` → 同七步；FBX 另走 `FlattenPaths` / SafeZone | 人工④停 |
| Art 交付 Importer | `ModelImporterProfiles.ApplyArtDelivery`，入口是④ E | Art 模型设置回退 |
| Art 根字面量 | `FlattenBuildSettings.ArtRoot` 与 `RetinarPaths.ArtRoot` 必须同文；⑤扫单元、⑥ UP 按 `Art/<名>/` 切前缀 | ⑤⑥找错夹 |
| 运行时依赖白名单 | ④分类拷贝与⑥ UP 各有一份相同前缀表 | 漏拷或 UP 漏收 |
| ⑥ 二次构建 | Runner 在 D19 补偿里再调一次 `RetinarAbApi.Build` | 与④无关，换④时勿顺手改 |
| 测试 | `FlattenOperationSettingsTests`、`PipelineGltfUriProbeTests` 经 `FlattenBuildService.CreateOptions(ctx)` | 断 ctx 后要改测 plan |

仓内⑥调用方只有 `PipelineRunner`（含 D19 再打）。没有独立「窗口打 AB」第二内核。

#### 切开顺序

1. **R1b 基本核对（2026-09-15 已做）。** 缺件 30/40 实跑；同名跨单元代码记清会串。未逐套 Art 树/hash 不挡换口，改 Extract/绑图前补记。细则：[步骤单页](./d24-flatten-steps.md)。
2. 按步骤 **从前向后**：当前步完全顺利才进入下一步；卡点停下。落地 `FlattenPlan` + `FlattenRowResult`；`ToolFlattenApi` 改为只收 plan（编排继续译 ctx）。
3. 新实现可按能力分文件（下表仍是模块名，不是「从旧类剪切」）。旧 `RetinarBatchModelBuilder` 三份 partial **先留作对照**，调用方切走后再删。
4. Finish 不再写 AB 标签（步骤第 8 步 / D24-R4）。仓内已无读标签。
5. ⑥、① ctx、跳行策略、OBJ 升闸、B 质量闸、D25-4：**停放**到对应步骤；第 11 步需要第 9 步结果对象后再评估。

能力模块（新实现内部，产品仍两个完整按钮）：

| 模块 | 做什么 | 验收 |
|---|---|---|
| Begin | 清单元夹、写 Art Prefab、建立 work | 失败不产生可继续 work |
| B | 分类拷贝、OBJ `.mtl` 跟拷 | 不处理外 URI 包 |
| B′ | 主文件+sidecar 原子树 | 清单完整才成功 |
| E | Art ModelImporter + Extract | 只由 plan 开闸 |
| D | 引用重映射 | 只绑本单元路径（现网 D25-4：Extract 后再导入按短名挂兄弟单元；不是 FindAssets 全工程搜） |
| C | Renderer `.mat` | 是否双份贴图写进 plan/文档，不顺手改 |
| Finish | 自愈、动画、空壳、轴向、碰撞盒 | **不写** AB 名 |
| FbxSafeZoneAdapter | 仅窗口直接选 FBX | 管线禁止进入 |

### R3b 两个人工相位入口（P1，已实现）

| 按钮 | 选择的分支 | 输入与保护 |
|---|---|---|
| 普通平铺（B） | 按 SO 分类规则拆依赖 | 接受 Prefab 或 `.fbx/.obj/.glb/.gltf`；点错打到外 URI 时 **提示后仍平铺**（步骤第 4 步；人责） |
| 原子迁移（B′） | 主 `.gltf` + sidecar 保持相对树 | 接受 `.gltf`，或恰好依赖一个 `.gltf` 包的 Prefab；无外 URI、缺伴生、多模型包均拒绝 |

直接选择非 FBX 模型时，人工服务先调用③生成 Prefab，再执行完整④。FBX 普通平铺保留既有 SafeZone 入口。两个按钮都执行 Begin→B/B′→E?→D→C→Finish；没有开放 Begin/B/D 等任意乱序按钮，因此不需要跨域保存 work/checkpoint。

### R4 最后收插件 1 输出格式（P1）

必须先把“格式”写成白名单：

- 允许：Android/iOS 平台选择、LZ4 等压缩、bundle 名和 variant、输出文件名、输出目录。
- **2026-09-14用户同意：** 取消④提前写AB标签，由⑥显式构建清单管理名称；旧菜单避免再读 Importer 标签。⑥已有 `AssetBundleBuild[]`。
- **仓内核对（同日）：** `Assets/Plugin` 下没有任何业务读取 `assetBundleName`。④ 只写/清标签；⑥ 构建时自己填 `AssetBundleBuild`。`ClearDuplicateBundleNames` 是写侧善后。仓外/APP/旧工程是否还按标签打 AB **本仓证不了**，删写入前仍须产品确认仓外。
- 禁止：插件 1 再写 Art Importer、Extract、Remap、Prefab Transform、材质、贴图、sidecar。

### kernel-contents

3518 行里「看起来不像平铺」的东西分三类。不能整段清空的原因：有的其实就是把模型变成可交付 Art Prefab。

| 类 | 做什么 | 能否移出/删除 |
|---|---|---|
| **平铺本体** | Begin 清单元+写 Art Prefab；B 按类拷依赖、OBJ 跟 `.mtl`；B′ 整树；E 写 Art Importer + Extract 内嵌图；D 引用重映射；C 独立 `.mat`；Finish 里的空壳/轴向、动画曲线改绑、按 SO 的碰撞盒、`.fbm` 自愈 | **留下。** 空壳、Extract、自愈、轴向不是⑥，是交付 Prefab 能站住的结构。删了 Art 会缺图、相对 URI 裂、或模型躺着 |
| **窗口专用，不是管线④** | FBX SafeZone：缩进 0.8 立方体、移到 `(0,0.15,0)`、旁路搜父目录贴图、另建 Controller | **移出**到窗口适配，不要进 `Run(plan)`。管线 Prefab 不走这条。产品若还要「直接选 FBX」就保留适配，不是内核 |
| **⑥ 格式副作用** | 写/清 `assetBundleName`/`variant`；`GeneratedAsset.BundleFileName` | **移出或删除。** ⑥ 不依赖这些标签 |
| **死/兼容壳** | `OpenDeliverablesFolder`（菜单已走 `RetinarEditorUtil`）；`RemapCopiedAssets` 零调用；未读的 Emission 常量；菜单对话框/`FlattenSourcePaths` 旧批处理（现网按钮走 `ManualFlattenService`） | **可删**，先确认仓外无反射调用 |
| **重复承载** | 运行时依赖白名单与⑥ UP 各一份；ArtRoot 双常量；E 与 Finish 都会 Extract | 不能先各删一份；须定单一所有者再收 |

E 与 Finish 双 Extract、Extract 后再按短名借图（D25-4）、残留 `.fbm` 只 Warning：是平铺质量债，不是「非平铺功能」。移出⑥时不要把 Extract/自愈一起扔掉。同名覆盖（目标路径已有则复用）与借图是两条链。

### r1b-skip

D24-R1b **基本核对已完成**（2026-09-15），不再用「完全没跑」挡换口。仍**不能**凭感觉删空壳/Extract/自愈/SafeZone，也不能宣称 D25-4 / `.fbm` / 去标签已验证。没有逐套样例树和源 hash 时，改这些算法只能事后从损坏 Art 反查；源保护护栏也未做故障注入。10–14 等前一步落地后再评估（11 需要 9）。

## 4. 本轮增减清单

### 已增加

- typed `MissingUris` 与日志计数；**glTF** 缺件已接④失败闸（整趟停）。OBJ 不在此列。
- `FlattenOperationSettings`：同数据类的人工/管线两份 SO。
- `FlattenOperationPolicy`：分类、清夹、碰撞体的一次性快照；管线日志记录来源与完整分类值。
- 普通平铺/原子迁移两个手动相位按钮及错误保护。

### 仍需要增加

- “声明数/找到数”的完整可核验信息。
- `FlattenPlan` + `FlattenRowResult`（本刀要落地；编排译 ctx，内核不再收 `PipelineJobContext`）。结果含失败步骤、原因、产物 Prefab。
- ctx / binding / prefab 的稳定行 ID，禁止列表不足时回退第一项。
- 最小真实回归集的执行结果；黑盒方法归类表已完成，不重复列为待增加。
- 插件 1 输出格式白名单。

### 需要删除或收窄

- 缺件已改 typed MissingUris（glTF），只核查字符串判断残留，不重复列 D26-2 为未开发。OBJ 是否升闸与 B 质量闸一并在 R3 后评估，本轮不加码。
- 管线调用 `FlattenPaths` 的可能性；它只留菜单兼容，后续迁入独立 adapter。
- 菜单 FBX SafeZone 与管线 Prefab 平铺在同一巨型类中的混居。
- `FlattenBuildSettings.ArtRoot` / `RetinarPaths.ArtRoot` 双常量。
- ④ Finish 中不属于平铺的 AB 格式副作用。
- `Retinar*` 类型名和注释；仅在行为测试覆盖后机械改名。

### 本轮明确不增加

- 先清空旧④再从零手写；同期重写⑥。
- 让① `Build` ctx，或为无头排错扩 ctx 字段。
- 把「整趟停 / 坏行跳过」写进平铺内核。
- Begin/B/B′/E/D/C/Finish 的菜单或 CLI 独立按钮。
- 每个分步自行扫描并重建 ctx。
- 为缺 ctx 再造一套后缀猜测分支。
- 在拆分同时顺手改变材质、Extract、自愈或 SafeZone 业务规则。

## 5. 完成定义

D24 结构收口只有同时满足以下条件才算完成：

1. 自动每模型②.5一次 ctx；人工点击时建立自身 ctx，此后分步不重扫。编排译成 plan；**内核不再接收 `PipelineJobContext`。**
2. 管线④只有一个完整入口，B/B′ 内部互斥；菜单 SafeZone 是独立适配入口。
3. 缺必需 sidecar 不再静默成功。内核按行返回失败；整趟停仍是当前 Runner 行为，是否改跳行另拍，不挡④换口。
4. 旧 3,518 行在新实现切开并经 R1b 对照前不得删除。
5. 插件 1 只剩白名单内的⑥输出格式/AB 能力（④不再写 AB 标签）。
6. 回归集通过，且两入口的差异有明确文档，不依赖“当前刚好能跑”。
