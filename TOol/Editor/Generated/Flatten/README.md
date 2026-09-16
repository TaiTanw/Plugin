# Generated / Flatten — ④ 平铺到 Art

对齐 ③ [`../Prefab/`](../Prefab/)：`Config` / `Layout` / `Service`；对外窄口 [`../../Shared/Api/ToolFlattenApi.cs`](../../Shared/Api/ToolFlattenApi.cs)。

> **接管期归属（2026-09-16）：** 文件在插件 2，编排暂归中间层。现用内核入口已是 `Run(FlattenPlan)`；ctx 转换只在 `FromContext`（`CreateOptions` 转调它）。第 13 步已撤公开分步转发；⑥ 不随④重写。见 [`d24-boundary-plan.md` R3](../../../../docs/dev-wip/03_open-items/d24-boundary-plan.md#r3-flatten-split)。

> **配置与人工入口（2026-09-10）：** 分类、清夹和碰撞体统一来自 `FlattenOperationSettings` SO，并在开跑前冻结为 `FlattenOperationPolicy`。人工默认资产位于 `Assets/Plugin/TOol/ConfigData/Manual/`，管线固定资产位于 `Assets/Plugin/Pipeline/ConfigData/`；同一数据类、不同实例。人工面板按资产所在目录判定归属：人工目录可编辑，管线目录和未归类目录只读。面板提供“普通平铺（B）”和“原子迁移（B′）”两个**完整相位**入口，不开放七步乱序。

**步骤 1–14 已落地：** 管线与人工④都进 `ToolFlattenApi.Run(plan)`。人工不建 ctx；直接选 FBX 也先③再 Run。Finish 不写 AB 标签。公开七步转发已撤。leftover **41**、身份 **42** 由编排 Fail 且不停⑤⑥。内核仍在本目录 Service（未整包搬迁）。

```text
Run(plan)
  Begin → B|B′ → E? → D → C → Finish
```

产品与菜单都进 `Run(plan)`，不开放七步乱序。`RetinarFlattenApi` 已删。

| 分支 | 读什么 | 不读什么 |
|---|---|---|
| B vs B′ | `ctx.HasExternalUris`（经 FromContext 写入 plan.Branch） | 后缀是不是 `.gltf` |
| E 是否跑 | `ctx.ImporterKind == ModelImporter`（ctx 空=菜单仍跑；写入 plan.ApplyArtModelImporter） | 步骤 SO |
| 清单元夹 / 轴向 | `ToolFlattenRequest`（人给 / 编排目的） | ctx |

`RetinarFlattenOptions` 仍是内核闸。`Run(plan)` 走 `CreateOptionsFromPlan`。测试可用 `CreateOptions(ctx, request)`（内部仍 FromContext）。编排不要自己拼 Options。

## 已收口与剩余差距（2026-09-14）

1. **glTF 缺伴生已收口（D26-2）。** ctx 已有 typed `MissingUris`；Runner 在 Begin 前映射 `FlattenFailed(40)` 并停止整趟④，不解析 Warning 文案。B′ 内核也会拒绝已知缺件。**OBJ 缺 `.mtl`/贴图不进该闸**（① 只 Warning）。
2. **`MaterialForm` 未进分支。** 旧草案的「开④ × 内嵌材质」不是当前契约；C 对 B/B′ 都跑。以后若要省略 C，须另做引用与贴图回归，不在透明修复中顺手改变。
3. **工作单类型名。** `RetinarFlattenWork` / `RetinarFlattenOptions` 仍是旧名，与插件 2 目录不一致。改名会碰菜单与内核，下一刀再做。
4. **Art 根双份常量。** `FlattenBuildSettings.ArtRoot` 与 `RetinarPaths.ArtRoot` 必须同字面量。⑥ 仍读后者。
5. **SafeZone 创建链已删除（步骤 12）。** `FlattenPaths` / `FlattenSourcePaths` / `CreateNormalizedPrefab` 及旧链专用助手不再保留。人工 FBX 与 OBJ 一样先③再 `Run(plan)`。共用 Bounds、碰撞盒、轴向、动画整理仍在。
6. **物理目录与临时归属不一致。** ③ `ToolPrefabApi` 不读 ctx；④ 公开口是 `FromContext` → `Run(plan)`。**目录暂时保留**，待 R3 拆文件后再搬家/下沉。不要把 ctx 下沉到更底层 partial。
7. **E 对 ScriptedImporter 改为直接跳过。** 以前仍进 Extract 再 Warning/continue。行为应等价，若 gltf 单元里混有 FBX 依赖则不再对那份 FBX 跑 Extract——现网 B′ 原子树通常没有 ModelImporter。若发现混包，再改成「扫 Art 单元里实际有的 Importer」而不是只看主文件 ctx。
8. **人工 Prefab 多 glTF 包暂拒绝。** 原子迁移只接受恰好一个 `.gltf` 主依赖，避免沿用“取第一个模型”的不确定行为。要支持多包 Prefab，需先定义一个 Prefab 对多份 plan 的输出和命名契约。

人工两按钮都跑完整④，不追加⑤⑥；这是已确认产品行为，不再列作风险。PipelineRunner 与人工调度都进 `Run(plan)`。B′“原子”保证相对包结构，失败不提供事务回滚。

④ Finish **不再**写 AB 名/variant（步骤 8）。⑥ 用 `AssetBundleBuild[]`。`FlattenRowResult` 步骤 9 已带拷贝数 / 残留 `.fbm` / 未绑槽。

**2026-09-15：** 第 10 步已落地且 CLI 核对。第 11 步 **Fail(42) 不停⑤⑥，保留原引用，不清空槽**：`FlattenTextureIdentity` 保存迁移前来源、副本 GUID 和 Extract 产物证据；`RetinarBatchModelBuilder.TextureBinding` 负责已知贴图绑定。普通平铺的 E/D/C/Finish 不再按同名猜图补拷；B′ 相对 URI 树不改。警告进入 `FlattenRowResult.TextureIdentityWarnings`，内核 `Ok` 不变；编排记 42。第 14 步 leftover `.fbm` 记 **41**，同样不停⑤⑥。原有缺 sidecar 失败规则仍是 40 整趟停。自动测试及真实样例验收状态见 [步骤页](../../../../docs/dev-wip/03_open-items/d24-flatten-steps.md)。
