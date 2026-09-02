# 各相位：入参 · 返回值 · 编排接力

返回 [总目录](../README.md) · [流程总览](./pipeline-flow.md) · [冒烟 / StepResult](./smoke-and-results.md) · [导入 ctx D23](./pipeline-job-context.md)

> 2026-08-31 讨论过「编排怎么消费窄口返回值、是否用返回值当下一步入参」。当时收进 [pipeline-flow §2](./pipeline-flow.md#2-a-steps-to-apis) 一张压缩表。  
> **本文是专文：** 对照现网 `PipelineRunner` 与五个窄口签名，把接力变量、关步行为、谁不传给谁写全。不改代码。

---

## 1. 怎么传（已拍板）

插件 1 / 2 **不互相调**，也不靠改 L1 Prefs 传本次任务数据。

```text
面板 / CLI
    → 填一份 PipelineOptions
    → PipelineRunner.Run(options)     ← 唯一编排
         本地变量接力（SourcePath / ModelPaths / prefabPaths / PostProcessFolderPaths）
         每步只调一个窄口
    → PipelineResult（ExitCode + Messages + PrefabOutputs / AbOutputs）
```

| 层 | 职责 |
|---|---|
| 窄口 | 单步能力；返回路径或结果对象；**不知道**自己被面板还是 CLI 调 |
| Runner | 收下返回值，写入本地变量或 Options 字段，再传入下一步 |
| `PipelineResult` | 给面板日志 / CLI 退出码；**不是**步间总线 |
| L1 Prefs | 给人点「执行全部」用；编排⑤走 `PostProcessFolderPaths`（D17），**不写**那份 Prefs |

`StepResult` **未做**（见 [smoke-and-results §4](./smoke-and-results.md#4-stepresult)）。步间仍是各自形态：`bool`+`out`、`List<string>`、`int`+`out`、`ToolPostProcessResult`、`RetinarAbBuildResult`。

处理区另有两份**职责必须分开**的数据（D23；上表路径接力未改，③ 仍不吃 ctx）→ [pipeline-job-context](./pipeline-job-context.md) · [d23 报告 §2](./d23-slice-report.md)：

| 来源 | 描述 |
|---|---|
| `PipelineJobContext` | **事实**：文件归类（能否拆 URI、伴生、Importer、Warnings） |
| `PipelineOptions` / 步骤 SO | **目的**：本趟开不开 ③④⑤⑥ |

---

## 2. Runner 里实际接力的变量

`Run()` 内不是每步读同一份「阶段结果对象」，而是这几份：

```text
options.SourcePath
        ↓ ②
options.ModelPaths          ← 工程内模型（③ 入参）
        ↓ ③
局部 prefabPaths            ← Incoming Prefab；开④后被 Art Prefab 覆盖
        ↓ ④
options.PostProcessFolderPaths  ← Art 单元根（⑤ 入参，D17）
局部 prefabPaths（已覆盖）      ← ⑥ 入参
        ↓ ⑤
（无路径交给⑥；只改 Art 上已有资产 + 记 FailedCount）
        ↓ ⑥
result.AbOutputs
```

④ 成功后 **覆盖** `prefabPaths`：后面⑥打的是 Art 下那份 Prefab，不是 Incoming 那份。

⑤ 与⑥ **分叉**：⑤改 `Art/.../Model` 上的 Mesh/贴图/材质；⑥仍用④给出的 Prefab 路径去打依赖。所以⑤的返回值 **不** 作为⑥的入参。

---

## 3. 各相位：窄口签名 × Runner 怎么用

产品格子：导入区 **1 入库 + 2 总闸**；处理区 **③④⑤**（须开前一步才能开后一步）；输出区 **⑥**。  
代码步号仍是 ②③④⑤⑥（1 入库 = ②）。

### 1 入库 / ② `ToolImportApi.ImportSingleModel`

```text
bool ImportSingleModel(string sourcePath, out string assetModelPath, out string message)
```

| | 现网 |
|---|---|
| **入** | `options.SourcePath`（工程外磁盘或 `Assets/…`） |
| **出** | `true` → `assetModelPath` 写入 `options.ModelPaths`；`message` 进日志 |
| **失败** | `false` → `ImportFailed(20)`，整趟停 |
| **关②** | 只接受已在 Assets 内的路径；工程外 → `BadArgs(10)` |
| **给谁** | ③ 读 `ModelPaths` |

总面板跑管线时始终入库（无勾选）。CLI 跟 SO 的 `runImport`。  
设置自动 **不是** 本窄口的返回值：靠 `MasterEnabled` + Unity `ImportAsset` 回调，编排不另调。

### 2 总闸（无窄口）

| | 现网 |
|---|---|
| **入** | `ResourceProcessSwitches.MasterEnabled`（Prefs，面板「2」勾选） |
| **出** | 无路径。只决定导入期 `Is*Effective` |
| **给谁** | 不传给③④⑤⑥ |

### ③ `ToolPrefabApi.BuildPrefabs`

```text
List<string> BuildPrefabs(IList<string> sourceModelPaths, string materialId = null)
```

| | 现网 |
|---|---|
| **入** | `options.ModelPaths` + `options.MaterialId`（可空，空则三层夹名） |
| **出** | 写出的 Prefab 路径 → 局部 `prefabPaths` + `result.PrefabOutputs` |
| **失败** | 空列表 → `PrefabFailed(30)`，停 |
| **关③** | 不调窄口。若还开⑥且 `options.PrefabPaths` 空 → `BadArgs(10)` |
| **给谁** | ④ 与⑥（关④时⑥直接打这份 Incoming Prefab） |

### ④ `RetinarFlattenApi.FlattenPaths`

```text
int FlattenPaths(IList<string> sourcePaths, bool quiet, out List<string> artPrefabPaths)
```

| | 现网 |
|---|---|
| **入** | ③ 的 `prefabPaths` + `options.Quiet` |
| **出** | 成功条数 `n`；`artPrefabPaths` **覆盖** 局部 `prefabPaths` |
| **旁路** | 从 Art Prefab 推出单元根 → `options.PostProcessFolderPaths`（仅当⑤开且该字段仍空） |
| **失败** | `n<=0` 或 Art 列表空 → `FlattenFailed(40)`，停 |
| **关④ / ③未开** | 强制 `RunFlatten=false`（④依赖③） |
| **给谁** | ⑥ 用覆盖后的 Art Prefab；⑤ 用单元根，**不是** Prefab 列表本身 |

gltf 多文件包：同一窄口；按后缀拆文件会拆坏 URI。改模式是 D23（ctx 选「禁止拆外 URI」），**不是**新相位、也不是改本表入参类型。

### ⑤ `ToolPostProcessApi.RunMasterBatch`

```text
ToolPostProcessResult RunMasterBatch(
    IList<string> folders = null,
    bool? includeTexture = null, bool? includeModel = null, bool? includeMaterial = null)
```

返回对象：`FailedCount`、`Canceled`、`Report`；`HasHardFailure` = `FailedCount > 0`。

| | 现网 |
|---|---|
| **入** | `options.PostProcessFolderPaths`（开④时 D17 写入）。`null` 才会去读 L1 批量路径——编排开④时不应落到这条 |
| **出** | 副作用写 Art 资产；结果对象只进日志 + 退出码 |
| **失败** | `FailedCount>0` → `PostProcessFailed(50)`；**⑥ 仍跑**。Skip / 未命中 / 未勾选大类 / 仅取消进度条不算 |
| **关⑤ / ④未开** | 强制 `RunPostProcess=false`（⑤依赖④） |
| **给谁** | **不给⑥路径**。⑥ 仍用④覆盖后的 `prefabPaths` |

编排主路径三个 `include*` 传默认 `null`（跟 L1 勾选）。⑥ 后遗留「重刷白」会再调一次且只开模型——**不是**门禁（D19 已降级），验收不看。

### ⑥ `RetinarAbApi.Build`

```text
RetinarAbBuildResult Build(IList<string> prefabPaths, RetinarAbBuildOptions options)
```

返回对象：`OkNames`、`BuiltBundleFiles`、`FailLines`；`PartialOk` = 至少打出一份。

| | 现网 |
|---|---|
| **入** | 当前 `prefabPaths`（开④则为 Art Prefab）+ `AbBuildOptions`（从导出 SO 填：根目录 / 是否 UP / 是否拷交付） |
| **出** | `BuiltBundleFiles` → `result.AbOutputs` |
| **失败** | 全部失败（`!PartialOk`）→ `AbFailed(60)`。部分成功仍 Ok |
| **关⑥** | 不调。关③且未预填 Prefab 时若仍开⑥ → `BadArgs` |
| **给谁** | 交付物；无「下一步」 |

步骤 SO 只提供 `RunAb`（要不要导出）。产物种类/路径不在步骤 SO。

---

## 4. `PipelineOptions`：哪些是步间数据，哪些是开关

| 字段 | 角色 |
|---|---|
| `SourcePath` | ② 入参 |
| `RunImport/Prefab/Flatten/PostProcess/Ab` | 开不开步；面板③④⑤连锁 |
| `ModelPaths` | ② 写出、③ 读入；也可预填已导入模型 |
| `PrefabPaths` | 关③时给⑥的预填；开③时 Runner 用局部列表，不靠这个字段往下传 |
| `PostProcessFolderPaths` | ④→⑤ 的 Art 单元；不是窄口返回值，是 Runner 从 Art Prefab **推出来的** |
| `MaterialId` | 只给③ |
| `AbBuildOptions` / `ExportUnityPackage` | 只给⑥ |
| `Quiet` | ④⑥ 禁 Dialog |
| `SourceBindings` | D10 预备；**Runner 未消费** |

拟定中的 `PipelineJobContext` **不是**新相位。② 后 `Build`，挂 Options；**当前仅④**经 `PipelineFlattenBridge` 消费。③ 不读 ctx（`MainAssetOk` 失败仍走 PrefabFailed 30）。→ [pipeline-job-context](./pipeline-job-context.md) · [d23 报告 §2](./d23-slice-report.md)。

---

## 5. `PipelineResult`：编排对外，不是步间

| 字段 | 谁写 |
|---|---|
| `ExitCode` | Runner 映射（10/20/30/40/50/60）；插件 1/2 **不引用** `PipelineErrorCodes` |
| `PrefabOutputs` | ③ 的列表（④ 覆盖发生在局部变量，这份列表是否同步 Art 以代码为准：现网 `AddRange` 在覆盖前） |
| `AbOutputs` | ⑥ |
| `Messages` | 各步日志字符串 |

现网注意：`PrefabOutputs` 在③后写入，④覆盖 `prefabPaths` **不会**回写 `PrefabOutputs`。⑥ 用的是局部 `prefabPaths`。以后若面板要展示「实际出包 Prefab」，不要误读 `PrefabOutputs`。

---

## 6. 关步 / 失败后还跑谁

```text
② 失败 → 停（20）
③ 失败 → 停（30）
④ 失败 → 停（40）
⑤ 硬失败 → 记 50，⑥ 仍跑
⑥ 全失败 → 60（可覆盖仍为 Ok 时的 50）
```

连锁（面板 + `ApplyTo` + Runner 防手组）：关③则④⑤关；关④则⑤关。⑥ 不绑③，但关③且没预填 Prefab 时⑥会 BadArgs。
