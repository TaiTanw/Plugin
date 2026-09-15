# 功能实现：流程与代码结构（对外接口）

返回 [总目录](../README.md) · [CLI 入口](./cli-getting-started.md) · [待办](../03_open-items/backlog.md)

> 总流程对外面分两块：**(A) 插件内中间层编排**、**(B) CLI 使用入口**。  
> 二者共用同一套窄口与 `PipelineRunner`；CLI **不是**第二套管线。

---

## 0. 两块入口（总览）

```text
┌─────────────────────────────────────────────────────────────┐
│  (A) 插件内 · 中间层调度编排                                    │
│      PipelineWindow / 将来其它 Editor 调用                      │
│           ↓                                                    │
│      PipelineOptions → PipelineRunner.Run → PipelineResult     │
│           ↓ 主流程调窄口                                           │
│      ② ToolImportApi │ ③ ToolPrefabApi │ ④ ToolFlattenApi      │
│      ⑤ ToolPostProcessApi │ ⑥ RetinarAbApi                     │
└─────────────────────────────────────────────────────────────┘
                              ↑ 同一 Runner
┌─────────────────────────────────────────────────────────────┐
│  (B) CLI 工具使用入口（D5 已验收 PipelineCli.Run）               │
│      Unity.exe -batchmode -executeMethod PipelineCli.Run …     │
│           ↓ 解析 argv → 填 Options → Runner → Exit(ExitCode)   │
│      （见 cli-getting-started.md）                             │
└─────────────────────────────────────────────────────────────┘
```

| 块 | 谁用 | 现状 | 文档 |
|---|---|---|---|
| **A 中间层** | 总面板、将来 Editor/测试 | **已可用** | 本文 §1–§4 |
| **B CLI** | 无头 CI / 本机脚本 | **D5 已验收** `PipelineCli.Run` | [cli-getting-started](./cli-getting-started.md) |

---

## 1. 目标流程（产品 · 三区）

```text
导入区    源文件
          1 入库（无勾选：拷入 BatchFbxImportSettings 导入根 + ImportAsset）
          2 总自动化处理（勾选 = ResourceProcessSwitches.MasterEnabled；分项在资源总面板）
处理区    ③ Prefab → ④ 平铺 Art → ⑤ 总批量（须开前一步才能开后一步）
输出区    ⑥ 是否导出（产物/路径读导出 SO）
```

面板三格与 SO 连锁：关③则④⑤关；关④则⑤关。`ApplyTo` / Runner 同样约束（防手组 Options）。⑥ 不绑在③上；关③且未预填 Prefab 时⑥会 BadArgs。

gltf / 多文件包：同一编排；② 后建 ctx，**仅④**经 ToolFlattenApi → FlattenBuildService 消费（B′），Bridge 已删。③ 不读 ctx。→ [pipeline-job-context](./pipeline-job-context.md) · [d23 报告](./d23-slice-report.md)。

人工④另有两个完整相位入口：“普通平铺（B）”与“原子迁移（B′）”。它们只显式选择内部互斥分支，均继续 E?/D/C/Finish；内部拆步只服务代码结构、中间层组合和测试，不开放为任意顺序的产品按钮。

---

## 2. A steps to APIs

**(A) 中间层 · 步骤 → 对外窄口**

自动管线是 **本地变量接力**，不靠修改 L1 Prefs 传本趟路径。插件 1 的平铺菜单仍会薄转发插件 2，不能泛称两插件无互调。每步窄口返回路径（或结果对象），Runner 收下再传入下一步。\
各相位入参 / 返回值 / 关步行为 → 专文 [pipeline-phase-io](./pipeline-phase-io.md)（2026-09-03 已按 D18/D23/Bindings 更新）。

```text
SourcePath
  → ② ImportSingleModel(out assetPath)     → options.ModelPaths
  → ③ BuildPrefabs(ModelPaths)             → 局部 prefabPaths
  → ④ Begin→B|B′→E→D→C→Finish   → 覆盖 prefabPaths = Art Prefab
                                           → D17 PostProcessFolderPaths = Art 单元根
  → ⑤ RunMasterBatch → ToolPostProcessResult（FailedCount + Report）
  → ⑥ Build(prefabPaths, abOpt)            → AB 文件列表
```

| 步 | 窄口（对外） | 实现落点 | 编排消费方式（入 → 出） |
|---|---|---|---|
| 1 入库 / ② | `ToolImportApi.ImportSingleModel` | TOol `Shared/Api` | 入 `SourcePath`；出 `assetModelPath` → `ModelPaths`。总面板跑管线时始终入库 |
| 2 总闸 | 不调窄口 | `ResourceProcessSwitches.MasterEnabled` | 决定用户设置自动 / 后处理自动是否生效；不替代 L1 分项。配置导入根内的模型安全基线不受此闸控制 |
| ③ | `ToolPrefabApi.BuildPrefabs` | → `PrefabBuildService` | 入 `ModelPaths` + `MaterialId`；出 `List` Prefab 路径 |
| ④ | `ToolFlattenApi` 能力方法 | TOol `Generated/Flatten`（职责暂归中间层） | 入 ③ 的 Prefab + **ctx** + 含 `FlattenOperationPolicy` 的 request；编排按行 Begin→B\|B′→E→D→C→Finish；出 Art Prefab覆盖⑥；D17 推 Art 单元给⑤ |
| ⑤ | `ToolPostProcessApi.RunMasterBatch` | → L1 总批量 | 入 D17 的 `PostProcessFolderPaths`；出 `ToolPostProcessResult`（FailedCount 复用三层 Summary；细节在 Report） |
| ⑥ | `RetinarAbApi.Build` | Retinar `40_Api` | 入当前 `prefabPaths` + `AbBuildOptions`（从导出 SO 填：根目录 / 是否 UP / 是否拷交付）；步骤 SO 只提供 `RunAb` |

⑤ 不把路径交给⑥：⑥ 仍用④改写后的 Prefab 列表。⑤ 只改 Art 里 Mesh/贴图/材质；⑥ 打那份 Prefab 的依赖。

**D16 失败口径：** `FailedCount > 0`（NeedsWork 之后 Execute Failed，有一条即算）→ `PostProcessFailed(50)`。Skip / NotApplicable / 未命中 / 未勾选大类 / 仅取消进度条 **不算**。报告字符串不解析。⑤ 失败后⑥ **仍跑**；若⑥再全失败则码被 60 覆盖。无 codec / 加载不到各 Op 口径不齐，**保持现状**（低优 **D21**）。

### 编排层文件（已建）

```text
Assets/Plugin/Pipeline/
├─ ConfigData/
│  ├─ PipelineStepSettings.asset           # 总步骤开关 SO
│  └─ FlattenOperationSettings.asset       # ④ 管线平铺细节 SO
└─ Editor/
   ├─ PipelineOptions / Result / ErrorCodes
   ├─ PipelineStepSettings.cs
   ├─ PipelineMaterialId.cs               # D9；SuggestBindingsForSelection
   ├─ PipelineSourceAccept.cs             # 批量输出到编排
   ├─ PipelineRunner.cs                   # ★ 自动化面板/CLI 共用；人工④另有编排服务
   └─ PipelineWindow.cs                   # (A) 人机入口
```

**边界目标：** 自动管线经窄口读取执行结果，不直接读取 Op 内部状态，也不走 Legacy 大菜单路径。当前 Runner 尚有 D19 诊断/补偿直接调用，不应宣称已完全纯化。\
**不做：** 主动调「设置自动」。导入由 Unity 回调触发；Art 模型硬跳过与贴图/后处理排除分别管理，Incoming 基线例外已落地。\
**⑤ 不是导入自动：** `ToolPostProcessApi.RunMasterBatch` = L1「按批量路径执行全部」的同一内核。中间层只是代点这个按钮；`triggeredByImport: false`，**不读** `excludedPathPrefixes`。

开④+⑤时：④成功后写入本次 Art 单元到 `PostProcessFolderPaths`（D17），再调⑤。顶点刷白 **不是**管线必要门禁（D19 已降级）；Runner 当前仍有⑥后重刷白+重打 AB 的遗留补偿；不能保证重导后的 Mesh 始终全白，也不作为 CLI 门禁。需白顶点 GLB 见 backlog **L**。

### Options → Runner 要点

| 字段 | 作用 |
|---|---|
| `SourcePath` | 单文件：工程外磁盘或 `Assets/…` |
| `RunImport/Prefab/Flatten/PostProcess/Ab` | 步骤开关（来自步骤 SO；`RunAb` = 是否导出） |
| `AbBuildOptions` | ⑥ 产物与路径：`ApplyTo` 从导出 SO 只读填入（不写回） |
| `ExportUnityPackage` | 从导出 SO 的快照，便于日志；执行以 `AbBuildOptions` 为准 |
| `MaterialId` | 覆盖 Prefab 三层命名 |
| `PostProcessFolderPaths` | ⑤扫描根；开④时 D17 写入本次 Art 单元。null → L1 Prefs（编排不改这份 Prefs） |
| `FlattenPolicy` | 从固定管线平铺 SO 冻结的本趟快照；分类、清单元、碰撞体。运行中不再读取平铺 EditorPrefs |
| `Quiet` | 禁 Dialog；**≠** 进程退出 |
| `SourceBindings` | Runner 主输入；空则合成一行 |

详细单文件/⑤约定见 [smoke-and-results](./smoke-and-results.md)。

---

## 3. (A) 错误码（与 CLI 退出码对齐；D5 已冻）

| 码 | 常量 | 含义 | Runner 现状 |
|---|---|---|---|
| 0 | `Ok` | 成功 | ✓ |
| 10 | `BadArgs` | 参数/路径 | ✓ |
| 20 | `ImportFailed` | ② | ✓ |
| 30 | `PrefabFailed` | ③ | ✓ |
| 40 | `FlattenFailed` | ④ | ✓ |
| 50 | `PostProcessFailed` | ⑤ | ✓（FailedCount&gt;0；细节在报告） |
| 60 | `AbFailed` | ⑥ 全失败 | ✓（部分成功仍 Ok） |
| 70 | `LicenseOrEnv` | License/环境 | **从未赋值** |
| 80 | `Other` | 其它 | 预留 |

---

## 4. (A) 窄口就绪度（相对 CLI / 编排）

| 窄口 | 可被 Runner 调用 | CLI 可直接依赖 | 缺口 |
|---|---|---|---|
| `ToolImportApi` | ✓ 单文件 | ✓ | 批量非主入口 |
| `ToolPrefabApi` | ✓ | ✓ | 多 materialId 靠 D10 |
| `ToolFlattenApi` | ✓ 接 ctx | ✓ | glTF `MissingUris` → Begin 前 Fail(40) 整趟停；OBJ 不进该闸 |
| `ToolPostProcessApi` | ✓ 返回 **`ToolPostProcessResult`** | ✓ | FailedCount→50（D16 已做） |
| `RetinarAbApi` | ✓ | ✓ | **D5 已冻**：`PartialOk` 仍 0；全失败 60 |
| `PipelineRunner` | ✓（面板已用） | ✓ | — |
| `PipelineCli` | ✓ `-source` / `-materialId` | ✓ | **D5 已验收**；扩 flag 见 B.CLI |

④→⑤：开⑤依赖开④（④依赖③）；④成功后 **已**把本次 Art 单元写入 `PostProcessFolderPaths`（D17）。⑤ `FailedCount>0` → 50（D16）。

---

## 5. (B) CLI · 指针

结构、命令、已冻退出码 → **[cli-getting-started.md](./cli-getting-started.md)**。  
实现顺序：A 已基本完成 → **D5 已验收**（参数/退出码已冻）。D16 ⑤结果码 **已做**。

---

## 6. 建议实现顺序（状态）

1. ~~1/2 窄口 + PipelineRunner~~ **已做**  
2. ~~D3 总面板 + D2 单文件~~ **已做**  
3. ~~实机验证 / D1 / D4~~ **已做**  
4. ~~对外接口文档分块（A/B）~~ **本文 + CLI 文（结构整理，无代码）**  
5. ~~**D5 CLI**~~ **已验收** `PipelineCli.Run`（2026-09-02 无头 `exit=0`；契约已冻）  
6. ~~**D16**（⑤结果码）~~ **已做** · ~~D17 ④→⑤ Art 单元路径~~ **已做** · ~~D19 刷白门禁~~ **已降级**  

结果形态：当前 `PipelineResult` + 字符串 Messages；`StepResult` 仍延后（CLI 要稳定按步失败时再加，见 smoke 文）。

2026-09-14 补充：⑤类型纳入仍有 EditorPrefs 默认，MaterialProcessSettings 仍为人工/管线共用 SO，尚未完成配置隔离。ToolPostProcessResult 当前只有 FailedCount、Canceled、Report；空配置/无目标等有文字提示，但缺少统一 typed 跳过原因。用户倾向合法跳过并明确提示，前提是窄口结果边界足够；不足时先评估中间层排错层，不让 Runner 直接探查 Op，不解析 Report 控制流程。见 D26-6。
