# 当前整体结构（入口 · 数据流 · 类）

返回 [总目录](../README.md) · [已确认要求](../01_requirements/strategy.md) · [待办](../03_open-items/backlog.md)

> 核对日期：2026-09-16。操作者入口：[插件根 README](../../../README.md)。这是当前结构入口，取代旧图中“插件 1 仍拥有④/门禁/全套导出”的描述。

## 1. 宏观分工

```text
Assets/Plugin/                     Git 根
├─ Pipeline/                      自动化入口、总步骤 SO、Runner、ctx
│  └─ Editor/ManualFlatten/        人工④唯一调度入口（自动④仍在 Runner）
├─ TOol/                          插件 2：导入、③生成、⑤资源操作
│  └─ Editor/Generated/Flatten/    ④物理存放处；ctx 编排部分暂归中间层
└─ RetinarBatchBuilder_Share/      插件 1：⑥ AB/可选 UnityPackage、菜单适配
```

宿主 `Plugin2022` 为 Unity 2022.3。Incoming 是入库副本，IncomingPrefab 是③产物，Art 是④交付副本区；不是原始外部模型目录。②/④的清夹只针对本次受控单元，不能扩展成清整个根目录。

## 2. 自动化入口到输出

```text
PipelineWindow（总面板） ─┐
PipelineCli.Run（无头） ─┴→ PipelineOptions.FromSettings
                           → PipelineRunner.Run
② ToolImportApi → 导入路径与 binding.CloneWith 保留行配置
②.5 每模型 PipelineJobContext.Build → 本趟事实列表
③ ToolPrefabApi → IncomingPrefab 路径列表
④ FlattenPerPrefab → FromContext(ctx, request) → ToolFlattenApi.Run(plan)
⑤ ToolPostProcessApi.RunMasterBatch → ResourcePostProcessService → 各类 Op
⑥ RetinarAbApi.Build → 平台 AB / 可选 UP / 输出拷贝
```

Runner 先完成入库与 ctx 收集，再按行生成 Prefab，并非每导入一个就立即跑完全部相位。绑定承载 SourcePath、MaterialId、ConvertZUpToYUp；ctx 承载观测事实，不承载人工轴向决定。

当前差距：

- 操作者总面板与 CLI 的入库固定开启；`PipelineStepSettings` 不再保存 `runImport`。底层 API 仍可显式关闭 `PipelineOptions.RunImport`。
- Prefab/ctx 数量不足时管线④已 Fail（步骤 3）；人工④不建 ctx（步骤 4）。尚无稳定行 ID。
- ⑤失败记 50 但仍可继续⑥；后续失败保留消息但不覆盖首个非 0 退出码（D26-4 已修）。
- Runner 仍有 D19 顶点色诊断/补偿分支，不是只剩业务无关的窄口转发。

## 3. ④平铺：三层（目标）与现网差在哪

目标（2026-09-14，与「解析 ctx → 操作数据 → 操作层」一致）：

```text
译 plan                                          操作层（不认管线 / 窗口 / ctx）
────────────────────────────────────             ────────────────────────
自动：②.5 已有 ctx → FromContext(ctx, request)
手动：人已用按钮选定 Branch；适配层组 plan
      禁止再 Build PipelineJobContext
                                                 → Run(plan)
                                                 → Begin → B|B′ → E? → D → C → Finish
                                                 → FlattenRowResult
```

手动不是「不需要 sidecar 事实」，而是 **Branch 已由人判定，禁止再用管线 ctx 复核或代填。** sidecar 列表是 B′ 的操作输入：由 plan 携带，或操作层对主文件做与 ctx 无关的包扫描（`GltfPackageFiles`），类型上不得出现 `PipelineJobContext`。

自动与手动在操作层上只差 plan 内容。适配层还会差：哪份 SO、模型是否先③、失败粒度。这些不要写进操作层。

现网已有 `FlattenPlan`：管线 `FromContext`，人工 `FlattenManualPlanFactory`；两端都 `ToolFlattenApi.Run(plan)`。公开分步转发已在步骤 13 删除。内核三份 partial 不读 ctx。

| 位置/类 | 现网职责 |
|---|---|
| `ToolFlattenApi` | `Run(plan)` / `FromContext`。ctx 只在 FromContext 译成 plan |
| `FlattenBuildService` | plan → Options；`Run` 转调七步 |
| `ToolFlattenRequest` | 轴向、清夹、SO 快照（目的，不是事实） |
| `RetinarFlattenWork` | 本行源/目标路径、Options、拷贝表 |
| `RetinarBatchModelBuilder*.cs` | 遗产操作内核，3518 行三份 partial |
| `ManualFlattenOrchestration` / `FlattenWindow` | 按钮 → Branch；Scan 组 plan；模型先③ |
| `PipelineRunner.FlattenPerPrefab` | 已有 ctx → FromContext → Run |

### 相位顺序与数据

```text
ctx（主模型、Importer、外 URI、sidecar、MissingUris）
+ request（轴向、SO 快照等）
  → 缺件预检 → Begin → B 或 B′ → E? → D → C → Finish
                         └── Work.CopiedDependencies ──┘
```

| 步 | 输入与作用 | 当前成功语义/边界 |
|---|---|---|
| Begin | 依 SO 清本次 Art 单元、创建 Art Prefab、建立 work | 失败不能回退源 Prefab 继续写 |
| B | 按 SO 分类复制依赖，OBJ 补 MTL | 拷贝表非 null 则内核 `Ok`；Finish 后再扫的残留外部 `.fbm` 由编排 **Fail(41)**，不停⑤⑥ |
| B′ | 主模型+sidecar 保持相对树于 Art/名称/名称/ | typed MissingUris 在 Begin 前失败；复制后核验全部必需目标。“原子”指包结构整体迁移，不代表事务回滚 |
| E | 对 Art ModelImporter 设置并 Extract | ScriptedImporter 跳过；ctx 为 null 的兼容路径走旧 E |
| D | 按源→副本表重映射 | 与 B/B′共享路径表 |
| C | Renderer 材质独立化及贴图引用 | 两分支都执行；不是按 ctx.MaterialForm 开关 |
| Finish | 引用自愈、动画、空壳、轴向、碰撞体、Renderer 检查 | **不写** assetBundleName/variant（步骤 8） |

E/D/C 内核仍是 void；步骤 9 已把 copied / leftover / unbound / 身份警告收进 `FlattenRowResult`。普通分支的伴生、自愈、跨单元同名图等风险仍见 D24/D25；B′缺件修复不代表全部平铺问题已解决。

### 人工入口

`FlattenWindow` 可拖入平铺 SO；Retinar 菜单薄转发至 `RetinarFlattenScheduler → ManualFlattenService` → `ManualFlattenOrchestration`。

- 普通平铺按钮固定 B；Scan 到外 URI **只提示不拦截**。原子迁移固定 B′，只接受完整且唯一的外 URI glTF 包。
- 输入为已在 Assets 导入的模型/Prefab。直接选 `.fbx/.obj/.glb/.gltf` 都先③再 `Run(plan)`。
- 人工没有②.5；按钮给出 Branch 后 `FlattenManualPlanFactory` 组 plan，**不** `PipelineJobContext.Build`。调度在 `Pipeline/Editor/ManualFlatten/Orchestration`，组计划在 `Plan`。
- 人工 FBX **不再**走 SafeZone / `FlattenPaths`（步骤 7）；步骤 12 已删除旧创建链与专用助手。现用空壳、轴向、碰撞盒、动画和七步工具保留。
- 两按钮是完整④，不是只拷文件的裸 B/B′，也不自动追加⑤⑥。这已由用户确认。
- 自动与人工都进 `ToolFlattenApi.Run(plan)`。尚未合并为一个相位 Runner 类。

### 当前核心问题是否在④

拆④、冻结 plan、3518 行内核、B/B′、跨单元按名借图（D25-4）、B 残留 `.fbm`：**在④。** 步骤 1–14 已落地（41/42 质量闸在编排）。AB 标签写入已从④去掉（步骤 8）。目录未迁。施工记录见 [步骤单页](../03_open-items/d24-flatten-steps.md)。

不在④、拆④时不要顺手开：① FBX 外置贴图跟拷（D25-2 外置侧）、⑤ 跳过口径、⑥ 构建本身、D19 冲色。入库入口一致性与首错保留已在 D26-5/4 收口。行对齐：管线④步骤 3 已收口；人工步骤 4 不再用 ctx。属编排不是操作层。

## 4. SO 与运行状态

| 用途 | 当前来源 | 编辑/运行方式 |
|---|---|---|
| 管线步骤 | Pipeline/ConfigData/PipelineStepSettings.asset | 正常 SO 路径下②③④⑤⑥均开；依赖步骤由 ApplyTo 约束 |
| 管线平铺 | Pipeline/ConfigData/FlattenOperationSettings.asset | 固定实例；运行前冻结 Policy；默认清当前 Art 单元 |
| 人工平铺 | TOol/ConfigData/Manual/FlattenOperationSettings.asset | 可拖同数据类 SO；默认不清单元 |
| 两种平铺 | 同一个 FlattenOperationSettings 类 | 面板只编辑自身来源目录，跨来源/未知目录只读；只读不妨碍按快照执行。不是全局 Inspector 锁 |
| 材质操作 | TOol/ConfigData/MaterialProcessSettings.asset | 当前仍人工/管线共用，未拆来源实例、未做完整运行快照 |
| ⑤类型纳入/人工路径等 | ResourceProcessSwitches / ResourceBatchFolderStore 的 EditorPrefs | 仍有机器状态依赖，不能宣称全部 SO 化 |
| ⑥ | RetinarExportSettings → RetinarAbBuildOptions | 路径、UP和拷贝开关；平台双端与LZ4当前仍固定在代码中 |

分类、清夹、碰撞体已脱离平铺内核 EditorPrefs；两份平铺 SO 的碰撞体均默认关闭。其它单项配置 SO 化是已确认方向，不是已经全部完成。

## 5. ⑤从总批量到单个材质

```text
PipelineRunner（显式 Art 单元） / ResourceProcessWindow（人工范围）
 → ToolPostProcessApi / ResourcePostProcessService.RunMasterBatch
 → 贴图 → 材质 → 模型
材质：
 MaterialTargetCollector（独立 .mat）
 + MaterialProcessSettings
 → MaterialOperationRegistry.GetMasterBatchOperations
 → MaterialOperationRunner（Evaluate → Execute → 汇总）
 → NormalizeDeliverableShaderOperation
 → 保存材质资产 → ToolPostProcessResult
```

Pipeline 传范围时不改人工路径；传 null 才回落 L1 路径。类型纳入参数未显式提供时仍读 EditorPrefs，管线通常未覆盖这些参数。

材质 L2 `MaterialToolWindow` 提供范围与精确操作选择；L3 `MaterialAdvancedSettingsWindow` 编辑共用 SO。Collector 收集 .mat/文件夹下独立材质，不是任意选 Prefab 都自动递归处理其材质。Registry 反射发现同程序集、可无参构造的 `IMaterialAssetOperation`，按配置组装操作。

`MaterialOperationContext` 只带该材质 AssetPath、Settings、进度等执行信息；不是 PipelineJobContext。当前 `EnsureMasterBatchDefaults` 会为空的材质总批量列表补入规范化操作，不能把“材质列表清空”直接等同于“材质禁用”。

### 透明修复（插件 2 的单一 OP）

文件：`TOol/Editor/Material/Operations/NormalizeDeliverableShaderOperation.cs`。

1. 换 Shader 前读取 `MaterialSurfaceSnapshot`：Opaque / Cutout / Blend 与 cutoff，并保存贴图、颜色等映射所需数据。
2. 切目标 Shader并迁移属性。
3. 写回渲染模式、混合、ZWrite、队列、关键字；glTF BLEND → Standard Fade（Mode 2）。

不只凭颜色 alpha 猜透明，不新增模型级 ctx 字段。目标 Shader 名可配，但当前属性迁移以 Standard 为主，不能承诺换名就能正确支持任意 URP Shader。已是目标 Shader 的材质会 Evaluate Skip，因此旧 Opaque 坏副本需重建④⑤，单独重跑⑤不能恢复已丢失的源透明语义。

2026-09-10：7 个专项测试、连同此前相关测试共 35/35 通过（历史记录，非本轮重跑）。2026-09-11：用户确认 Art 玻璃正确，41 个材质为 37 Opaque + 4 Fade。2026-09-14：用户确认目标移动端 AB 验收完成，D13-R1 退出顶部队列。

## 6. ⑥插件 1 现存代码

| 位置/类 | 作用 |
|---|---|
| 40_Api/RetinarAbApi.cs | Build；显式 AssetBundleBuild[] 指定 Prefab，按平台构建 |
| 40_Api/RetinarAbBuildOptions.cs、RetinarExportSettings.cs | 构建选项、配置、结果相关类型 |
| 20_Package/RetinarDeliverableIo.cs | 输出目录与文件拷贝 |
| 00_RetinarPaths.cs、00_RetinarEditorUtil.cs | 路径/辅助 |
| 01_RetinarMenu.cs | 人工平铺菜单转发与打开交付目录；没有旧独立出包菜单 |

默认 Android/iOS AB，可选 UnityPackage；交付拷贝使用 02_unity / 03_assetbundles。RetinarDirectPackage、RetinarPackageScheduler、30_Business、ExportArtPrefabPaths 及旧门禁/全套报告代码已删除，不应再按旧结构图定位或要求恢复。

平台/压缩当前固定为Android+iOS/LZ4；导出SO还没有对应选择字段，不能把目标格式白名单当成已全部可配。

目标是仅负责输出格式；④ **已停写** Prefab Importer AB 标签（步骤 8 / D24-R4）。⑥ 用显式 `AssetBundleBuild[]`。不得用“旧⑥业务门禁已删”推导“④缺文件也应成功”。

## 7. 文档与验收范围

- 当前约定：[strategy](../01_requirements/strategy.md)；优先级：[backlog](../03_open-items/backlog.md)；④接管：[D24 计划](../03_open-items/d24-boundary-plan.md)。
- 原始源保护仍有效；受控 Incoming/Art 单元的明确重建不承诺旧副本 GUID 保持不变。
- 历史规则/报告保留追溯，不能覆盖当前明确确认，也不能把“曾建议”自动视作“已实现”。
- 文档随步骤更新；代码改动见 [步骤单页](../03_open-items/d24-flatten-steps.md)。`23b3567` 已上推。编辑器冒烟须人在 Unity 补跑。
