# 各相位：入参 · 返回值 · 编排接力

返回 [总目录](../README.md) · [流程总览](./pipeline-flow.md) · [冒烟 / StepResult](./smoke-and-results.md) · [导入 ctx D23](./pipeline-job-context.md)

> 对照 `PipelineRunner` 与五个窄口。  
> 2026-09-03：D10-1/2 已落（父目录磁盘扫 ID2；Runner 按行；无文件夹 ctx）。

编号别混：

| 口头 | 是什么 | 不是什么 |
|---|---|---|
| **1 入库** | 导入区拷入 Incoming + `ImportAsset`（代码里曾标 ② / D2） | 不是 ctx |
| **导入区 2** | 总闸 `MasterEnabled`（导入期自动流） | 不是 2.5 |
| **2.5 ctx** | 1 成功后 `PipelineJobContext.Build`，事实层 | 不是步骤开关、不是导入区 2 |
| **D10 的 1/2/3** | Incoming 跟 ID2；Runner 读 Bindings；Pack 填表 | 与上面格子号无关 |

---

## 1. 怎么传（已拍板）

自动化管线通过窄口与返回值接力，不靠改 L1 Prefs 传本次任务。插件 1 的人工平铺菜单仍会薄转发插件 2，不是全仓无互调。

```text
总面板 / 批量选择回填 / CLI（Pack 解包尚未实现）
    → 填一份 PipelineOptions（单行 SourcePath 或 Bindings 列表）
    → PipelineRunner.Run(options)     ← 自动化面板/CLI 共用编排
         本地变量接力
         各相位经窄口；④按多个能力方法组合
    → PipelineResult（ExitCode + Messages + PrefabOutputs / AbOutputs）
```

| 层 | 职责 |
|---|---|
| 窄口 | 单步能力；**不知道**自己被面板还是 CLI 调 |
| Runner | 收下返回值，写入本地变量或 Options，再传入下一步 |
| `PipelineResult` | 给面板日志 / CLI 退出码；**不是**步间总线 |
| L1 Prefs | 给人点「执行全部」；编排⑤走 `PostProcessFolderPaths`（D17），**不写** Prefs |
| 批量面板筛选后缀 | **只方便人勾选收集**；不缩小内核识别表；**不影响 CLI** |

`StepResult` **未做**。步间仍是 `bool`+`out`、`List<string>`、`int`+`out`、`ToolPostProcessResult`、`RetinarAbBuildResult`。

| 来源 | 描述 |
|---|---|
| `PipelineJobContext` | **事实**（2.5）：后缀、Importer、外 URI、伴生、typed `MissingUris`、Warnings |
| `PipelineOptions` / 步骤 SO | **目的**：开不开 ③④⑤⑥ |
| `FlattenOperationPolicy` | **④操作快照**：来自人工或管线 `FlattenOperationSettings` SO；分类、清夹、碰撞体 |

③ **不读** ctx。④ **读 ctx**：`ToolFlattenApi` 用 `HasExternalUris` 选 B/B′、用 `ImporterKind` 决定是否跑 E。轴向不在 ctx，走 `ToolFlattenRequest.ConvertZUpToYUp`（绑定行）。

---

## 2. 人机多文件 vs CLI 单文件 vs Pack

| 入口 | 填单 | 入库性格 |
|---|---|---|
| **编排总面板** | 可拖入单/多文件，或点「打开批量选择器」后由批量面板「输出到编排」回填路径+ID2 表 | 运行时仍走 Runner 的 1 入库（覆盖槽），**不**走批量 Conflict |
| **批量专属面板** | 多文件夹、按文件一条；可筛后缀。两个按钮：「执行导入」= 只入库；「输出到编排」= 不拷贝 | 人单独点「执行导入」时仍 Conflict。给编排当选择器时 **只收集、不执行拷贝** |
| **CLI** | 仍只一个 `-source`（可加一个 `-materialId`） | 与总面板同一套 1 入库。人工已经指定了那一个文件 |
| **CLI + Pack** | argv 仍一个压缩包路径 | 解包后 **在进程内** 填 Bindings（多行），再按多文件思路循环 1。不是多个 `-source` |

批量面板上的后缀勾选：**不代表**没勾的格式内核不认识。CLI 丢来 `.glb` 仍按内核全表识别。勾选只减少「人这次想看见哪些文件」。

### 默认 ID2（未手填 / 无工单名时）

- 同父目录磁盘上 **只有一个** 内核格式文件（`.fbx/.glb/.gltf/.obj`，**非递归**，不数 .bin/贴图）：ID2 缺省 = **三层夹名**。
- 同父目录磁盘上 **还有其它** 内核文件：ID2 缺省 = **三层 + `_` + 文件全名**（含扩展名）。三层 Warning/不足时同样加全名。
- 工单 / 面板手填的 ID2 **覆盖** 上述缺省。Pack 多预制体：每条用清单/工单 ID2，不要用三层猜。
- 扫盘只看后缀白名单，**不读文件体**，无需文件夹 ctx。

### 编排接受接口（A 已做）

```text
PipelineSourceAccept.SendToOrchestration(IList<PipelineSourceBinding> bindings)
    → PipelineWindow.AcceptBindings(bindings)   // 打开总面板并整表替换
```

`PipelineSourceBinding`：`SourcePath` + `MaterialId`（ID2）。**没有行号字段。** 第几行由 Runner 循环承担；导入窄口 / ctx 仍是单文件事实，不带列表下标。

Conflict 只约束批量「执行导入」，不拦「输出到编排」。

---

## 3. 1 识别 vs 2.5 事实（扩展名谁定）

**一份内核白名单**（现网写在 `ToolImportApi`：`.fbx` `.glb` `.gltf` `.obj`）。面板展示「当前可支持」读这一份。

| | **1 入库** | **2.5 ctx** |
|---|---|---|
| 问什么 | 这个后缀 **能不能进 Incoming** | 已经进工程的文件 **是什么形态** |
| 现网 | 不认识 → 不拷，20 | `Build(工程内路径)`：按后缀选探测（gltf 才 `Scan` URI） |
| 加新格式 | 1 必须能拷 + Import | 2.5 必须会归类或标 Unknown + Warning；否则④当普通 FBX 拆夹 |

所以：**产品上的「支持哪些模型」由 1 与 2.5 共同加行**——1 负责「收不收」，2.5 负责「收下之后怎么讲事实」。不是两份互相打架的表；2.5 **不能**单独放宽 1 没认的后缀（没入库就没有工程内路径可 Build）。

⑤ `ModelProcessSettings.supportedExtensions` 是**设置自动 / 后处理自动 / ⑤ 收集**共用的「认谁」表，**不是**入库白名单。不要拿⑤的勾选当 CLI 识别表。反过来，1 认 `.obj` 不代表 ⑤ 会处理它——两份表要各自加行。

### 1 要不要「先解析 ctx」？

**不要把 ctx 当 1 的入场券。** 1 只看：后缀在白名单、文件存在、（Pack）路径安全/无脚本。拷完 `ImportAsset` 之后才有 Importer，2.5 才有意义。

现网唯一例外：`.gltf` 拷主文件时 1 **已经**用 `GltfPackageFiles.Scan` 跟拷伴生。这是「为了拷全包」，不是 2.5。2.5 会再 Scan 一次，用来决定④走不走 B′。探测核共用 `Scan()`，职责仍两截：1=带文件进来，2.5=告诉④能不能拆。

收集阶段（批量选择 / Pack 列目录）只用 **1 的后缀表** 找出候选文件，**不**跑 ctx。

---

## 4. Runner 里实际接力的变量

### 现网（D10-2：Runner 读 Bindings）

```text
Bindings = SourceBindings
  （空且仅有 SourcePath → 合成 1 行；空 ID2 → SuggestDefault 扫父目录）

foreach (source, id2, axis):
        ↓ 1 入库：夹名 = id2（D18 只清 Incoming/<id2>/）
        ModelPaths.Add；binding.CloneWith 保留轴向等行配置
        JobContexts.Add(Build(该份工程内路径))  // 2.5；无文件夹 ctx
再 foreach (已入库模型与绑定行):
        ↓ ③ BuildPrefabs([该份], 该行 id2)
局部 prefabPaths（N 个）
        ↓ ④  按行 Begin → B|B′ → E → D → C → Finish  （ToolFlattenApi 接该行 ctx）
            覆盖 prefabPaths = Art Prefab 列表
            D17 → PostProcessFolderPaths（各 Art/<ID2>/）
        ↓ ⑤⑥ 现网已能吃列表
```

CLI 仍一个 `-source`。Pack 未做。

### 已定、尚未写（D10-3 Pack）

---

## 5. 各相位：窄口签名 × Runner 怎么用

产品格子：导入区 **1 入库 + 2 总闸**；**2.5** 不是格子。处理区 **③④⑤**；输出区 **⑥**。错误码 20 仍表示 1 失败。

### 1 入库 `ToolImportApi.ImportSingleModel`

```text
bool ImportSingleModel(string sourcePath, string incomingFolderName, out string assetModelPath, out string message)
```

`incomingFolderName` 可空：空则三层夹名；非空则 `SanitizeFolderName` 后作为 Incoming 子夹（D18 只清该夹）。

| | 现网 |
|---|---|
| **入** | 每行 `binding.SourcePath`；夹名跟 `binding.MaterialId`（ID2）。表空则 `SourcePath`+`MaterialId` 合成一行 |
| **出** | 逐行 `ModelPaths.Add` |
| **失败** | `false` → `ImportFailed(20)`，整单停 |
| **重名** | 工程外：D18 清 `Incoming/<该行 ID2>/` 再拷。已在 Assets：跳过、不清夹 |
| **关 1** | 只接受已在 Assets 的路径；工程外 → 10 |
| **给谁** | ③ 的该行模型；然后该行 2.5 |
| **识别** | 白名单后缀；gltf 跟拷伴生。筛选后缀不进本窄口 |

总面板跑管线时始终入库。CLI 跟 SO 的 `runImport`。设置自动不是本窄口返回值。

### 2 总闸（无窄口）

| | 现网 |
|---|---|
| **入** | `ResourceProcessSwitches.MasterEnabled` |
| **出** | 无路径。只决定导入期 `Is*Effective` |
| **给谁** | 不传给③④⑤⑥、也不传给 2.5 |

### 2.5 ctx（无窄口；每行 `Build`）

```text
PipelineJobContext.Build(工程内主路径)
```

| | 现网 |
|---|---|
| **入** | **每一份** 入库成功的模型 |
| **出** | `JobContexts` 与 `ModelPaths` 对齐；`JobContext` = 第一份 |
| **失败** | Build 本身不改退出码；普通 Warning 仅展示。glTF `MissingUris` 由④在 Begin 前映射为 40 并停止整趟。OBJ 缺件不进此项 |
| **给谁** | 仅④经 `ToolFlattenApi` → `RetinarFlattenOptions`（B′/E）；清 Art 来自 request/policy，不来自 ctx。③⑤⑥不读 |

无文件夹 ctx：父目录几个内核文件只用于建议 ID2，不进本类型。

### ③ `ToolPrefabApi.BuildPrefabs`

```text
List<string> BuildPrefabs(IList<string> sourceModelPaths, string materialId = null)
```

| | 现网 |
|---|---|
| **入** | **每行** `BuildPrefabs([该模型], 该行 ID2)` |
| **出** | Prefab 路径 → 局部 `prefabPaths` + `result.PrefabOutputs` |
| **失败** | 该行空列表 → 30，整单停 |
| **给谁** | ④；关④时⑥打 Incoming Prefab |

一个 `materialId` 罩 N 个模型时内核会追加 stem，**那不是工单多 ID2**。Runner 已改为逐条调用，避免踩这条。

### ④ `ToolFlattenApi` 能力组合

管线④不再调 `FlattenPaths`。按行：

```text
request = ForPipeline(options.FlattenPolicy)
request.ConvertZUpToYUp = binding.ConvertZUpToYUp
TryBegin(prefab, ctx, request, out work)      // 0 清单元夹 + A 写 Prefab
ShouldRelocateAtomic(ctx) ? RelocateAtomic(work) : SplitDependencies(work)
ApplyImportAndExtract(work, ctx)              // E；非 ModelImporter 跳过
Remap(work)                                   // D
CopyRendererMaterials(work)                   // C
TryFinish(work)                               // 自愈 / 空壳 / 动画 / AB 名
```

`FlattenPaths` 仍给菜单（含 FBX 直平铺那条 `CreateNormalizedPrefab`）。**管线禁止调它。**

| | 现网 |
|---|---|
| **入** | **按行** ③ 的该 Prefab + ctx + request：管线 SO policy 提供分类/清夹/碰撞体；`HasExternalUris` → B′；绑定行 `ConvertZUpToYUp` → Finish 里叠 −90°X |
| **出** | 各行 `work.PrefabPath` 拼成列表，**覆盖** 局部 `prefabPaths` |
| **旁路** | 各 Art 单元根 → `PostProcessFolderPaths`（⑤开且该字段仍空） |
| **失败** | glTF typed `MissingUris` 非空，或 Begin/B/B′/Finish 返回 false / Prefab 路径空 → 40 **整趟停**（⑤⑥不跑；已写出 Art 不回滚）。OBJ 缺件不进 MissingUris。E/D/C 为 void，尚无统一分步结果 |
| **给谁** | ⑥ 用 Art Prefab；⑤ 用单元根 |

gltf 整包：②/1 已入库伴生；④ 禁止按后缀拆相对 URI。不是新相位。

人工面板也调用同一套完整④组合，但提供两个显式入口选择 B 或 B′。人工点击时从选中的模型或 Prefab 依赖构建一次 ctx；并非把 B/B′ 暴露成跑完即停的裸步骤。

### ⑤ `ToolPostProcessApi.RunMasterBatch`

签名与失败口径不变：`FailedCount>0` → 50，**⑥ 仍跑**。入参仍是 D17 的 Art 单元夹，不是 Prefab 列表。

### ⑥ `RetinarAbApi.Build`

入参仍是当前 `prefabPaths`（开④则为 Art Prefab）。部分成功仍 0；全失败 60。D5 已冻。

---

## 6. `PipelineOptions`：步间数据 vs 开关

| 字段 | 现网 |
|---|---|
| `SourcePath` | Bindings 空时的单行来源；有表时同步为第一行（兼容日志/CLI） |
| `SourceBindings` | **Runner 主输入**（source + ID2 + ConvertZUpToYUp） |
| `MaterialId` | 单行 ID2；多行以 Bindings 各行为准（同步为第一行） |
| `RunImport/…` | 开不开步 |
| `FlattenPolicy` | `PipelineStepSettings.ApplyTo` 从固定管线平铺 SO 生成；运行中的分类、清夹、碰撞体快照 |
| `ModelPaths` | 1 写出、③ 按行读 |
| `PrefabPaths` | 关③时预填⑥ |
| `PostProcessFolderPaths` | ④→⑤；多单元累加各 `Art/<ID2>/` |
| `JobContext` | 第一份 2.5 |
| `JobContexts` | 每份模型一份；④ 按下标映射 |
| `AbBuildOptions` / `Quiet` | ⑥ / ④⑥ |

---

## 7. `PipelineResult`：对外，不是步间

| 字段 | 谁写 |
|---|---|
| `ExitCode` | Runner（10/20/30/40/50/60）；插件 1/2 不引用错误码类 |
| `PrefabOutputs` | ③ 当时的列表。④ 覆盖 **不会** 回写这里。⑥ 用局部 `prefabPaths` |
| `AbOutputs` | ⑥ |
| `Messages` | 各步日志 |

---

## 8. 关步 / 失败后还跑谁

```text
1 失败 → 停（20）
③ 失败 → 停（30）
④ 失败 → 停（40）
⑤ 硬失败 → 记 50，⑥ 仍跑
⑥ 全失败 → 60
```

连锁不变：关③则④⑤关；关④则⑤关。⑥ 不绑③；关③且没预填 Prefab 时⑥ → 10。

---

## 9. 开发切面（1、2 先做；3 Pack 后做）

| 顺序 | 做 | 不做（本刀） |
|---|---|---|
| **D10-1** | **已做**：1 入库夹名跟 ID2；缺省名扫父目录磁盘（仅一个内核文件→三层，还有其它或 Warning→三层+全名） | 改批量 Conflict；改⑤扩展表 |
| **D10-2** | **已做**：Runner 先完成各行1/2.5，再逐行③；④按行ctx | CLI 多个 `-source` |
| 面板展示 | 内核白名单展示为「当前可支持格式」 | 勾选筛掉的格式从 CLI 消失 |
| **D10-3** | Pack 解包填 Bindings | 新开一条 ②③⑥ |

2026-09-14 补充：⑤类型纳入仍有 EditorPrefs 默认，MaterialProcessSettings 仍为人工/管线共用 SO，尚未完成配置隔离。ToolPostProcessResult 当前只有 FailedCount、Canceled、Report；空配置/无目标等有文字提示，但缺少统一 typed 跳过原因。用户倾向合法跳过并明确提示，前提是窄口结果边界足够；不足时先评估中间层排错层，不让 Runner 直接探查 Op，不解析 Report 控制流程。见 D26-6。
