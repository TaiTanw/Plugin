# Generated / Flatten — ④ 平铺到 Art

对齐 ③ [`../Prefab/`](../Prefab/)：`Config` / `Layout` / `Service`；对外窄口 [`../../Shared/Api/ToolFlattenApi.cs`](../../Shared/Api/ToolFlattenApi.cs)。

> **接管期归属（2026-09-09）：** 文件已迁到插件 2，不代表职责拆分完成。因下列窄口必须消费 `PipelineJobContext`，当前把 `ToolFlattenApi`、`FlattenBuildService` 与七步组合**暂归中间层平铺能力**，物理目录暂不再搬。ctx 只在②.5构建一次，各步不得重新探测。长期是整体留中间层，还是只把无 ctx 执行叶下沉，待查封回归后再拍。实施顺序见 [`d24-boundary-plan.md`](../../../../docs/dev-wip/03_open-items/d24-boundary-plan.md)。

> **配置与人工入口（2026-09-10）：** 分类、清夹和碰撞体统一来自 `FlattenOperationSettings` SO，并在开跑前冻结为 `FlattenOperationPolicy`。人工默认资产位于 `Assets/Plugin/TOol/ConfigData/Manual/`，管线固定资产位于 `Assets/Plugin/Pipeline/ConfigData/`；同一数据类、不同实例。人工面板按资产所在目录判定归属：人工目录可编辑，管线目录和未归类目录只读。面板提供“普通平铺（B）”和“原子迁移（B′）”两个**完整相位**入口，不开放七步乱序。

当前中间层组合分步，**现网分支仍读 ctx**：

```text
TryBegin(prefab, ctx, request, out work)
ShouldRelocateAtomic(ctx) ? RelocateAtomic(work) : SplitDependencies(work)
ApplyImportAndExtract(work, ctx)     // 内部看 ImporterKind
Remap(work)
CopyRendererMaterials(work)
TryFinish(work)
```

| 分支 | 读什么 | 不读什么 |
|---|---|---|
| B vs B′ | `ctx.HasExternalUris` | 后缀是不是 `.gltf` |
| E 是否跑 | `ctx.ImporterKind == ModelImporter`（ctx 空=菜单仍跑） | 步骤 SO |
| 清单元夹 / 轴向 | `ToolFlattenRequest`（人给 / 编排目的） | ctx |

`RetinarFlattenOptions` 仍是内核闸，由 `FlattenBuildService.CreateOptions` 从 ctx+request 生成。编排不要自己拼 Options。

## 模糊点（本刀未拍，先挂口）

1. **缺伴生已收口（D26-2，2026-09-10）。** ctx 已有 typed `MissingUris`；Runner 在 Begin 前映射 `FlattenFailed(40)`，不解析 Warning 文案。B′ 内核也会拒绝已知缺件，并要求所有执行时输入都生成精确目标；不再以“拷到任一文件”算成功。
2. **`MaterialForm` 未进分支。** 查封里「另存 .mat = 开了④ × 内嵌材质」。现网 C 对 B/B′ 都跑。要不要按 `HasStandaloneMat` 跳过 C，未拍。
3. **工作单类型名。** `RetinarFlattenWork` / `RetinarFlattenOptions` 仍是旧名，与插件 2 目录不一致。改名会碰菜单与内核，下一刀再做。
4. **Art 根双份常量。** `FlattenBuildSettings.ArtRoot` 与 `RetinarPaths.ArtRoot` 必须同字面量。⑥ 仍读后者。
5. **菜单 FBX 直平铺。** `CreateNormalizedPrefab`（SafeZone）与管线 Prefab 平铺仍同内核类。管线禁止调 `FlattenPaths`。要不要独立菜单类，未拍。
6. **物理目录与临时归属不一致。** ③ `ToolPrefabApi` 不读 ctx；④现网接 `PipelineJobContext`。2026-09-09 暂把这层视为中间层能力，不为目录纯净强拆；但不要把 ctx 下沉到更底层 partial，也不要让各分步重复 Build。长期边界见 D24-R2。
7. **E 对 ScriptedImporter 改为直接跳过。** 以前仍进 Extract 再 Warning/continue。行为应等价，若 gltf 单元里混有 FBX 依赖则不再对那份 FBX 跑 Extract——现网 B′ 原子树通常没有 ModelImporter。若发现混包，再改成「扫 Art 单元里实际有的 Importer」而不是只看主文件 ctx。
8. **人工 Prefab 多 glTF 包暂拒绝。** 原子迁移只接受恰好一个 `.gltf` 主依赖，避免沿用“取第一个模型”的不确定行为。要支持多包 Prefab，需先定义一个 Prefab 对多份 plan 的输出和命名契约。
