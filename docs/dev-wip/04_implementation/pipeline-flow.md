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
│           ↓ 只调窄口                                           │
│      ② ToolImportApi │ ③ ToolPrefabApi │ ④ RetinarFlattenApi   │
│      ⑤ ToolPostProcessApi │ ⑥ RetinarAbApi                     │
└─────────────────────────────────────────────────────────────┘
                              ↑ 同一 Runner
┌─────────────────────────────────────────────────────────────┐
│  (B) CLI 工具使用入口（D5 第一刀已写 PipelineCli.Run）           │
│      Unity.exe -batchmode -executeMethod PipelineCli.Run …     │
│           ↓ 解析 argv → 填 Options → Runner → Exit(ExitCode)   │
│      （见 cli-getting-started.md）                             │
└─────────────────────────────────────────────────────────────┘
```

| 块 | 谁用 | 现状 | 文档 |
|---|---|---|---|
| **A 中间层** | 总面板、将来 Editor/测试 | **已可用** | 本文 §1–§4 |
| **B CLI** | 无头 CI / 本机脚本 | **第一刀已写** `PipelineCli.Run` | [cli-getting-started](./cli-getting-started.md) |

---

## 1. 目标流程（产品）

```text
基线（默认）：
  ② 导入 → ③ Prefab → ⑥ 仅 Android/iOS AB（quiet）

可选（面板勾选 / 将来 CLI flag）：
  ④ 平铺 Art（含 remap）
  ⑤ 压图 / 顶点白 / 材质规范化
  ⑥+ UnityPackage、门禁、全套 Deliverables
```

---

## 2. (A) 中间层 · 步骤 → 对外窄口

| 步 | 窄口（对外） | 实现落点 | 编排消费方式 |
|---|---|---|---|
| ② | `ToolImportApi.ImportSingleModel` | TOol `Shared/Api` | `SourcePath` + `RunImport` |
| ③ | `ToolPrefabApi.BuildPrefabs` | → `PrefabBuildService` | `ModelPaths` + `MaterialId` |
| ④ | `RetinarFlattenApi.FlattenPaths` | Retinar `40_Api` | 返回 Art Prefab → ⑥改打这些 |
| ⑤ | `ToolPostProcessApi.RunMasterBatch` | → L1 总批量 | `PostProcessFolderPaths` 或 L1 Store |
| ⑥ | `RetinarAbApi.Build` / `BuildAbOnly` | Retinar `40_Api` | `AbBuildOptions` / ExportSettings |

### 编排层文件（已建）

```text
Assets/Plugin/Pipeline/
├─ ConfigData/PipelineStepSettings.asset   # 总步骤开关 SO
└─ Editor/
   ├─ PipelineOptions / Result / ErrorCodes
   ├─ PipelineStepSettings.cs
   ├─ PipelineMaterialId.cs               # D9；D10 SourceBindings 预备
   ├─ PipelineRunner.cs                   # ★ 唯一编排内核
   └─ PipelineWindow.cs                   # (A) 人机入口
```

**约定：** 编排 **只**依赖上述窄口；不碰 Window / Op 内部 / Legacy 大菜单路径。  
**不做：** 主动调「设置自动」（那是导入期自动流，靠 Unity 回调，且默认不碰 Art）。  
**⑤ 不是导入自动：** `ToolPostProcessApi.RunMasterBatch` = L1「按批量路径执行全部」的同一内核。中间层只是代点这个按钮；`triggeredByImport: false`，**不读** `excludedPathPrefixes`。

开④+⑤时：④成功后写入本次 Art 单元到 `PostProcessFolderPaths`（D17），再调⑤。⑥后若顶点色被重导冲掉，仍用同一口只跑模型再重打 AB（D19）——同属中间层代跑手动内核。

### Options → Runner 要点

| 字段 | 作用 |
|---|---|
| `SourcePath` | 单文件：工程外磁盘或 `Assets/…` |
| `RunImport/Prefab/Flatten/PostProcess/Ab` | 步骤开关（默认来自 SO） |
| `MaterialId` | 覆盖 Prefab 三层命名 |
| `PostProcessFolderPaths` | ⑤扫描根；null → L1 Prefs |
| `Quiet` | 禁 Dialog；**≠** 进程退出 |
| `SourceBindings` | D10 预备；**Runner 未消费** |

详细单文件/⑤约定见 [smoke-and-results](./smoke-and-results.md)。

---

## 3. (A) 错误码（与 CLI 退出码对齐草案）

| 码 | 常量 | 含义 | Runner 现状 |
|---|---|---|---|
| 0 | `Ok` | 成功 | ✓ |
| 10 | `BadArgs` | 参数/路径 | ✓ |
| 20 | `ImportFailed` | ② | ✓ |
| 30 | `PrefabFailed` | ③ | ✓ |
| 40 | `FlattenFailed` | ④ | ✓ |
| 50 | `PostProcessFailed` | ⑤ | **未映射**（只打 Info，见 D16） |
| 60 | `AbFailed` | ⑥ 全失败 | ✓（部分成功仍 Ok） |
| 70 | `LicenseOrEnv` | License/环境 | **从未赋值** |
| 80 | `Other` | 其它 | 预留 |

---

## 4. (A) 窄口就绪度（相对 CLI / 编排）

| 窄口 | 可被 Runner 调用 | CLI 可直接依赖 | 缺口 |
|---|---|---|---|
| `ToolImportApi` | ✓ 单文件 | ✓ | 批量非主入口 |
| `ToolPrefabApi` | ✓ | ✓ | 多 materialId 靠 D10 |
| `RetinarFlattenApi` | ✓ + Art 路径 out | ✓ | — |
| `ToolPostProcessApi` | ✓ 返回 **string** | 弱 | 无 Ok/失败码 → **D16** |
| `RetinarAbApi` | ✓ | ✓ | 部分失败策略见 D5 表 |
| `PipelineRunner` | ✓（面板已用） | ✓ | — |
| `PipelineCli` | ✓ `-source` / `-materialId` | ✓ | 无头验收；全 flag 表仍见 B.CLI |

④→⑤：开⑤依赖开④；④成功后 **已**把本次 Art 单元写入 `PostProcessFolderPaths`（D17）。

---

## 5. (B) CLI · 指针

结构、第一刀命令、退出码 → **[cli-getting-started.md](./cli-getting-started.md)**。  
实现顺序：A 已基本完成 → **D5 第一刀已写** → 无头验收 → D16 ⑤结果码。

---

## 6. 建议实现顺序（状态）

1. ~~1/2 窄口 + PipelineRunner~~ **已做**  
2. ~~D3 总面板 + D2 单文件~~ **已做**  
3. ~~实机验证 / D1 / D4~~ **已做**  
4. ~~对外接口文档分块（A/B）~~ **本文 + CLI 文（结构整理，无代码）**  
5. ~~**D5 CLI 第一刀**~~ **已写** `PipelineCli.Run`（待无头验收）  
6. **D16**（⑤结果码）· ~~D17 ④→⑤ Art 单元路径~~ **已做**  

结果形态：当前 `PipelineResult` + 字符串 Messages；`StepResult` 仍延后（CLI 要稳定按步失败时再加，见 smoke 文）。
