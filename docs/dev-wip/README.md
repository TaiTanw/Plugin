# 开发者须知

操作者流程不在本层：[操作者须知](../operator/README.md) · 总目：[docs/](../README.md)  
宿主 Unity 2022.3 Built-in。插件 Git：`Assets/Plugin`。推荐标签 **v1.6.2**。

下文是现约。历史切片在文末归档，不覆盖本页。

---

## 硬约束

1. 插件 1 / 2 不按类型认识中间层。编排外的工具自管路径与配置。
2. 不要把 Plan 并进 Orchestration。管线不要跳过 [1]。管线面板不进人工平铺入口。
3. 编排可读管线 SO；**执行内核不得 Load 管线 SO。**
4. **[2] 是全局导入设置**，不是步骤闸，也不是「开③才能开④」。
5. 平铺：同一数据类 **两份 SO**（人工 `TOol/ConfigData/Manual`、管线 `Pipeline/ConfigData`）。关联靠路径 + 缺则创建，不是 ObjectField。
6. 工作根拆开：管线三根在 `PipelineStepSettings`；人工三根在 `BatchFbxImportSettings`。默认同名文件夹，互不跟随。导入跳过 Art **钉死** `Assets/Art`，不跟可改的④根走。
7. **贴图 `AllowMasterBatch`（手写，不是自动探测「无分支」）**  
   `false`：禁止进 L1 / 管线⑤ masterBatch / 导入自动；仅 L2 精准可勾。  
   **Evaluate 对适用池无条件 NeedsWork 的 Op 必须为 false。**  
   现网仅 `bake_luminance_to_alpha`。材质 / 模型接口无此字段。加 Op 见 [⑤ Op](./04_implementation/op-recognition-and-extend.md)。  
   跟材质 Fade：精准面板该 Op 下勾选，本机 EditorPrefs（`TOol.ManualOp.Texture.bake_luminance_to_alpha.FollowMaterialsToFade`），**默认关**，不进贴图 SO。调用材质 Op **内部** `ApplyTargetSurface`，不是 `RunMasterBatch`。④ 拷材质不跟此开关。见 [风险说明](./02_structure/risks.md)。
8. L1 扫描不读管线 SO。管线无扫描入口。⑤ = `RunMasterBatch`（`triggeredByImport: false`），不读导入 exclude。
9. **Quiet**：面板跟步骤 SO；CLI **强制** true（≠ 退出编辑器）。  
   **清空导入区 / 交付根**：同 SO 另两字段，不绑 Quiet；CLI 跟 SO。先清 Art，挂起导入、删完再一次 Refresh。
10. OBJ 轴向是 Binding 上的人工勾选，不进 ctx，不自动勾。④ Finish 才转。

---

## 三层

```text
Pipeline/     步骤、Runner、CLI、ctx、人工④调度（ManualFlatten）
TOol/         [1] 入库、[2] 导入设置、③ Prefab、⑤ Op；④ 内核物理在此
Retinar…/     ⑥ RetinarAbApi.Build → name_android|ios.assetbundle（产品夹不分平台）
```

```text
面板 / CLI → PipelineOptions.FromSettings → PipelineRunner
  [1] ToolImportApi → ②.5 ctx（仅自动）→ ③ ToolPrefabApi
  → ④ FromContext → Run(plan) → ⑤ RunMasterBatch → ⑥ Build
人工④：人选定 Branch → FlattenManualPlanFactory → Run(plan)，不 Build ctx，不接⑤⑥
```

内核只认 `FlattenPlan`。事实在 ctx；人工决定（轴向、Branch）在 Binding / 按钮，不进 ctx。

**连锁：** 关③则④⑤关；关④则⑤关。面板/CLI 点跑 **一定入库**（底层 API 仍可关 `RunImport`）。⑥ 不绑③；无 Prefab 则 BadArgs。

| 码 | 停后面？ |
|---:|---|
| 10 / 20 / 30 / **40** | 停（40 = ④硬失败，含 glTF 缺伴生） |
| **41** leftover `.fbm` / **42** 贴图身份 | ⑤⑥继续 |
| 50 ⑤硬失败 | ⑥继续 |
| 60 ⑥全失败 | 停 |

OBJ 缺 `.mtl`/贴图不进 40。空槽只观察。首个非 0 退出码保留。

---

## 配置（谁读哪份）

| 用途 | 资产 | 谁用 |
|---|---|---|
| 步骤、Quiet、清空、三根工作路径、⑤纳哪些大类 | `Pipeline/ConfigData/PipelineStepSettings` | 管线 / CLI |
| [2] 导入期总闸 | `ImportPipelineSettings` | 全局 Processor；面板「全局导入设置（2）」 |
| ④ 分类 / 清本次 Art 单元 / 碰撞体 | 两份 `FlattenOperationSettings` | 各读各的；开跑冻 policy |
| ⑤ 压图 / 刷白 / 烤材质 | 两份 `*ProcessSettings`（Pipeline vs TOol） | 管线⑤ vs 人工总面板；**L2 勾选仍是本机 Prefs** |
| 材质细节 | 目前仍共用一份 Material SO | 未拆人工/管线实例 |
| 人工三根路径 | `BatchFbxImportSettings` | 选择器「手动端路径」；管线不读 |
| L1 扫描夹 / 纳入大类 | EditorPrefs | 仅人工总面板 |
| ⑥ 路径 / 是否 UP | `RetinarExportSettings`（一份） | 人工⑥与管线⑥共用 |

---

## 分册（现约）

| | 路径 |
|---|---|
| 战略（短） | [01_requirements/strategy.md](./01_requirements/strategy.md) |
| 三条「自动」 | [01_requirements/tech-and-ops.md](./01_requirements/tech-and-ops.md) |
| 结构 | [02_structure/overview.md](./02_structure/overview.md) |
| 风险（通道混用、Op 隐式调用；硬约束仍看本页） | [02_structure/risks.md](./02_structure/risks.md) |
| 待办 | [03_open-items/backlog.md](./03_open-items/backlog.md) |
| Runner / 窄口 / 退出码 | [04_implementation/pipeline-flow.md](./04_implementation/pipeline-flow.md) |
| 相位入参 | [04_implementation/pipeline-phase-io.md](./04_implementation/pipeline-phase-io.md) |
| ctx vs Binding | [04_implementation/pipeline-job-context.md](./04_implementation/pipeline-job-context.md) |
| ④ B/B′ 能力 | [04_implementation/pipeline-flatten-capabilities.md](./04_implementation/pipeline-flatten-capabilities.md) |
| ⑤ 加 Op | [04_implementation/op-recognition-and-extend.md](./04_implementation/op-recognition-and-extend.md) |
| ⑥ 文件名 | [04_implementation/d1-ab-only.md](./04_implementation/d1-ab-only.md) |
| CLI | [04_implementation/cli-getting-started.md](./04_implementation/cli-getting-started.md) |
| 冒烟口径 | [04_implementation/smoke-and-results.md](./04_implementation/smoke-and-results.md) |
| 日志 | [05_dev-log/timeline.md](./05_dev-log/timeline.md) |

旧入口：[CLI_AUTOMATION_DEV.md](../CLI_AUTOMATION_DEV.md)

---

## 归档（勿当现约）

[D24 步骤 1–14](./03_open-items/d24-flatten-steps.md) · [D24 边界稿](./03_open-items/d24-boundary-plan.md) · [④ 审计快照](./04_implementation/flatten-core-audit.md) · [D23 切片](./04_implementation/d23-slice-report.md) · [D13 洋红](./03_open-items/d13-glb-magenta.md) · [D6 file: 依赖](./04_implementation/d6-unitygltf-docker.md)

当前队列里 **41 / 42 / Extract 调试暂放**（缺真样）。不拆 3518 行目录、不重写⑥、本仓不接 License/Docker。
