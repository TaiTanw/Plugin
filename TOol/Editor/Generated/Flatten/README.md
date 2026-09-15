# Generated / Flatten — ④ 平铺到 Art

对齐 ③ [`../Prefab/`](../Prefab/)：`Config` / `Layout` / `Service`；对外窄口 [`../../Shared/Api/ToolFlattenApi.cs`](../../Shared/Api/ToolFlattenApi.cs)。

> **接管期归属（2026-09-14）：** 文件在插件 2，编排暂归中间层。目标是内核只收 `FlattenPlan`，不再读 ctx。旧三份 partial 对照删除，禁止先清空。⑥ 不随④重写。见 [`d24-boundary-plan.md` R3](../../../../docs/dev-wip/03_open-items/d24-boundary-plan.md#r3-flatten-split)。

> **配置与人工入口（2026-09-10）：** 分类、清夹和碰撞体统一来自 `FlattenOperationSettings` SO，并在开跑前冻结为 `FlattenOperationPolicy`。人工默认资产位于 `Assets/Plugin/TOol/ConfigData/Manual/`，管线固定资产位于 `Assets/Plugin/Pipeline/ConfigData/`；同一数据类、不同实例。人工面板按资产所在目录判定归属：人工目录可编辑，管线目录和未归类目录只读。面板提供“普通平铺（B）”和“原子迁移（B′）”两个**完整相位**入口，不开放七步乱序。

**步骤 1–3 已落地：** 管线④ `FromContext` → `Run(plan)`；多行只按下标配对。人工仍走下面分步（步骤 4 再切）：

```text
Run(plan)  // 管线已走；人工步骤 4
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

`RetinarFlattenOptions` 仍是内核闸。`Run(plan)` 走 `CreateOptionsFromPlan`；分步窄口仍走 `CreateOptions(ctx, request)`。编排不要自己拼 Options。

## 已收口与剩余差距（2026-09-14）

1. **glTF 缺伴生已收口（D26-2）。** ctx 已有 typed `MissingUris`；Runner 在 Begin 前映射 `FlattenFailed(40)` 并停止整趟④，不解析 Warning 文案。B′ 内核也会拒绝已知缺件。**OBJ 缺 `.mtl`/贴图不进该闸**（① 只 Warning）。
2. **`MaterialForm` 未进分支。** 旧草案的「开④ × 内嵌材质」不是当前契约；C 对 B/B′ 都跑。以后若要省略 C，须另做引用与贴图回归，不在透明修复中顺手改变。
3. **工作单类型名。** `RetinarFlattenWork` / `RetinarFlattenOptions` 仍是旧名，与插件 2 目录不一致。改名会碰菜单与内核，下一刀再做。
4. **Art 根双份常量。** `FlattenBuildSettings.ArtRoot` 与 `RetinarPaths.ArtRoot` 必须同字面量。⑥ 仍读后者。
5. **直接选 FBX 的人工普通平铺。** `CreateNormalizedPrefab`（SafeZone）与管线 Prefab 平铺仍同内核类。管线禁止调 `FlattenPaths`。要不要独立菜单类，未拍。
6. **物理目录与临时归属不一致。** ③ `ToolPrefabApi` 不读 ctx；④现网接 `PipelineJobContext`。2026-09-14 确认：**目录暂时保留**，待 R3 拆文件后再搬家/下沉。不要把 ctx 下沉到更底层 partial，也不要让各分步重复 Build。
7. **E 对 ScriptedImporter 改为直接跳过。** 以前仍进 Extract 再 Warning/continue。行为应等价，若 gltf 单元里混有 FBX 依赖则不再对那份 FBX 跑 Extract——现网 B′ 原子树通常没有 ModelImporter。若发现混包，再改成「扫 Art 单元里实际有的 Importer」而不是只看主文件 ctx。
8. **人工 Prefab 多 glTF 包暂拒绝。** 原子迁移只接受恰好一个 `.gltf` 主依赖，避免沿用“取第一个模型”的不确定行为。要支持多包 Prefab，需先定义一个 Prefab 对多份 plan 的输出和命名契约。

人工两按钮都跑完整④，不追加⑤⑥；这是已确认产品行为，不再列作风险。PipelineRunner 与 ManualFlattenService 目前分别组合顺序，尚无唯一共享相位 Runner。直接选 FBX 的 SafeZone 与管线 Prefab 路径仍须区分。B′“原子”保证相对包结构，失败不提供事务回滚。

④ Finish 仍写 AB 名/variant。2026-09-14 用户同意迁到⑥构建清单管理；仓内已无读标签，步骤第 8 步可做。

**2026-09-15：** R1b 基本核对完成；按 [d24-flatten-steps](../../../../docs/dev-wip/03_open-items/d24-flatten-steps.md) 从第 1 步按序换口。D25-4 不是全工程搜图，是 Extract 后再导入按短名挂兄弟单元；修法等第 9 步结果对象后再评估第 11 步。
