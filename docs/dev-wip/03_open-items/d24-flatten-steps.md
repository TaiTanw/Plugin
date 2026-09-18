# d24-flatten-steps

返回 [待办](./backlog.md#a-open-items) · [D24 边界](./d24-boundary-plan.md) · [总目录](../README.md)

> 2026-09-16。④ 收口步骤 **1–14 已落地并上推 `23b3567`**。不含重写⑥、D19；D26-5 入库入口一致性与 D26-4 首错保留已于 2026-09-16 另行收口。① FBX 外置图跟拷（D25-2 A）已随本轮落地。操作者入口：[插件根 README](../../../README.md)。

## 已拍板（本页）

- 多行对齐：只用**下标**。禁止 `contexts[0]` / 第一份 `JobContext` 套到后续行。能配对的行照铺；**循环结束后**若数量不等或有未配对行 → Fail。不回滚已写出的 Art。
- **稳定行 ID 本目标不做。** 现网 `PipelineSourceBinding` 没有行主键；`materialId` 会重名，不能当键。
- 手动：人用按钮选定 Branch；**禁止** `PipelineJobContext.Build`。点错 B 会确认：点「仍要平铺」才继续；**取消或关闭对话框中止**。
- 仓内⑥ 不依赖 Prefab Importer 上的 bundle 名 → **第 8 步可做**。
- **第 5 步：基本核对完成**（2026-09-15）。缺件 30/40 已实跑；同名跨单元已按代码记清「现在会串」。未逐套勾目录树/源 hash 的格子不挡从第 1 步开工；也**不能**凭此宣称 Extract/搜图/去标签已验证。
- **第 11 步：2026-09-16 报错但不卡。** 找不到本次合法贴图 → 内核 Warning + `TextureIdentityWarnings`，`row.Ok` 仍 true，槽位引用不清；编排 `Fail(42)`，⑤⑥继续。原有 glTF 缺伴生 30/40 规则不改。
- **第 14 步：2026-09-16 leftover `.fbm` 报错但不卡。** 内核 `Ok` 仍 true；编排 `Fail(41)`，⑤⑥继续。空槽仍只观察。
- **编排入口用 `[数字]` 标注**，与资源处理总面板留存的自动化参数对应。`[1]` 框末打开批量选择器；`[④]` 后打开人工平铺面板；`[⑤]` 打开资源处理总面板。`Tools/批量选择器` 与 `[1]` 同一窗（旧菜单名「批量FBX导入」已改名）。`Tools/资源处理/` 子菜单已撤。

## 开发顺序

按编号从前向后做。**当前步完全顺利，才进入下一步。** 遇到卡点停下，不跳号「先做后面容易的」。


| 步     | 何时开                                       | 卡点时                 |
| ----- | ----------------------------------------- | ------------------- |
| 1     | 已落地；假 plan 可 `Run`                        | —                   |
| 2     | 已落地；管线 `FromContext` → `Run`              | —                   |
| 3     | 已落地；按下标配对，禁止 `ctx[0]`                     | —                   |
| 4     | 已落地；手动 Scan 组 plan，不建 ctx                 | —                   |
| 6     | 路径锁已落地；编辑器三条待人跑                           | 冒烟失败则先修口，不改 Extract |
| 7、8   | 已落地；人工 FBX 不再 SafeZone；④ 不写 AB 标签         | —                   |
| 9     | 已落地；结果对象带拷贝数 / Extract 次数 / 残留 .fbm / 未绑槽 | 14 已拍 41 |
| 10    | 已落地；Extract 只归 E，自愈不抽                     | —                   |
| 11    | 已落地；**Fail(42) 不停⑤⑥**；保留原引用 | `FBX`/`FBX2` 重名专项已放行（本单元 guid，零串兄弟） |
| 12    | 已清理旧入口、SafeZone 专用链与死成员；独立编译及 32 项 EditMode 通过 | 少量真实 CLI 结果见本节 |
| 13    | 已落地；公开七步转发已撤，ctx 只在 FromContext | 未迁目录 |
| 14    | **已拍 Fail(41) 不停⑤⑥**（2026-09-16）。内核 `Ok` 仍 true | 无 leftover>0 真实样例；码已加 |


不要把「第 5 步基本核对」理解成 Extract/搜图已验完。1–14 已按序落地；41/42 报错但不卡（对比 40 整趟停）。

## 步骤

### 1. 冻结 `FlattenPlan` + `FlattenRowResult`，七步原样转调 — **已落地（2026-09-15）**

`ToolFlattenApi.Run(plan)` → `FlattenBuildService.Run`：Begin→B|B′→E?→D→C→Finish。不改搬文件算法。管线/人工仍走分步窄口（步骤 2 / 4 再切）。

验收：`FlattenPlanTests` 组假 plan 调通（无效路径停在 Begin；`MissingUris` 在 Begin 前失败；Options 不读 ctx）。

### 2. 管线 `FromContext(ctx, request)` → `Run(plan)` — **已落地（2026-09-15）**

`PipelineRunner.FlattenPerPrefab`：`ToolFlattenApi.FromContext` → `Run(plan)`。操作层不再收 ctx。缺件仍 `FlattenFailed(40)` 整趟停。

验收：现有 3 个 `MissingUris` 测试仍过；`FlattenPlanTests.FromContext_ThenRun_MissingUrisFailBeforeBegin`。

### 3. 多 ctx 循环：禁止回退，结束后报错 — **已落地（2026-09-15）**

只铺 `i < prefabPaths.Count && i < contexts.Count` 的配对行。循环结束后若数量不等 → `FlattenFailed(40)`，文案写明两长度。不回滚已写出的 Art。不再用 `contexts[0]` / `JobContext` 填缺。

验收：`PipelineFlattenAlignmentTests`（两 Prefab 一行 ctx 只配对 1 且 mismatch）。

未做：给 Binding 加 GUID。那是「稳定 ID」，本目标不做。

### 4. 手动组 plan，禁止 ctx；轻扫描只提示 — **已落地（2026-09-15）**

按钮 → `plan.Branch`。B′ sidecar 用 `GltfPackageFiles.Scan`，不是 `JobContext.Build`。选中 `.gltf` 直接 Scan；Prefab 用 `GetDependencies` 找 `.gltf` 再 Scan。FBX/OBJ 不 Scan、无提示。点「普通平铺」且有相对 URI：弹窗确认（`FlattenManualPrompt.ConfirmSplitWithRelativeUris`）；**仍要平铺**才继续，**取消/叉号中止**，不建 Prefab、不跑④。

调度/计划分目录（中间层 `Pipeline/Editor/ManualFlatten/`：`Orchestration` + `Plan`），未搬内核。管线总面板 `[④]`「打开平铺面板」进同一 `FlattenWindow`（人工 SO）；管线运行仍读管线平铺 SO。

验收：`FlattenManualPlanFactoryTests`（Split 不拒外 URI；B′ 缺 `.bin` 失败；完整 sidecar 组 Relocate plan）。

### 5. R1b 对照表（行为改动闸）

**状态：基本核对完成（2026-09-15）。** 换口 1–4、7、8 已落地。改搬文件/Extract/绑图算法前，仍应用本表做改前/改后对比；未勾的格子改算法时补记，不把第 5 步重开成阻断。


| 样例                  | 要确认的具体事实                                                                                       | 核对                                                                                                                             |
| ------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| FBX 外置贴图            | Art 树（Model/Texture/Material/Prefab）；Prefab 依赖是否都在本单元；源 FBX/源材质 hash 不变                        | 未逐套勾；改 E/搜图前补                                                                                                                  |
| FBX 内嵌贴图            | Extract 后本单元有独立图；不是仍挂导入区 `.fbm`；源 hash 不变                                                      | 未逐套勾；改 E 前补                                                                                                                    |
| OBJ+MTL             | 仅当源 `.obj` 里有 `mtllib` 且旁边有 `.mtl`。没有 `.mtl` 的 OBJ（或现有全是 FBX/glTF）此格标「无样例」                     | 可标无样例，不挡换口                                                                                                                     |
| 缺件 glTF             | ① Warning 仍拷 `.gltf`。缺 `.bin`：主资产不是 GO，③ **exit=30**，④ 的 40 走不到。要看到 40：能出 Prefab、缺的是图不是 buffer | **已实跑。** 删 `.bin` 的 `、dddd.gltf`：缺 25 URI，`exit=30`。保留 `.bin` 删 4 张 jpg（`…FBX.gltf`，ID2=测试）：③ 成功、`missingUris=4`、④ **exit=40** |
| GLB                 | 整文件在 Model/；C 后有独立 `.mat`                                                                      | 未逐套勾                                                                                                                           |
| 完整外 URI glTF        | `Art/<名>/<名>/` 相对树仍能解析；`.bin`/外图都在；exit=0                                                      | 未逐套勾（有过整包入库日志，未当对照页）                                                                                                           |
| 自包含 glTF（全 `data:`） | JSON 里贴图/buffer 是 `data:`；走 B 不走 B′；不当缺件                                                       | 代码口径已核；未单独立跑页                                                                                                                  |
| 同名跨单元贴图             | 第 5 步只**记录现状**（现网会串）；第 11 步才改                                                                  | **代码已核清会串**（见第 11 步）。未做两单元同名实跑页                                                                                                |
| OBJ 轴向开/关           | 开：内容节点 −90°X；关：与源一致；重跑不叠第二层壳                                                                   | 先前已落地；本轮未再跑                                                                                                                    |
| 源保护                 | 复制失败：源 Prefab/材质 hash、路径、GUID 不变                                                               | 静态查封已有；故障注入未做                                                                                                                  |


每条另记：管线退出码；人工 B / B′；Art Prefab 路径。

### 6. 换口冒烟 — **仓内路径锁已落地（2026-09-15）**

自动一条 + 手动 B + 手动 B′。对照第 5 步已有格子；未勾的只确认「没崩、仍出 Art Prefab、源 hash 不变、管线不进 SafeZone」。

仓内已锁：`PipelineRunner` / `FlattenBuildService.Run` / 人工 `ManualFlattenOrchestration` 均不出现 `FlattenPaths`、`CreateNormalizedPrefab`。验收测试：`FlattenSmokePathTests`。

**编辑器仍须人跑：** 管线一条（任意能出 Prefab 的源）、手动 B、手动 B′；确认出 Art Prefab、源 hash 不变。本环境不能代跑 Unity 窗口。人工直接选 FBX 的姿态已与管线对齐（步骤 7），不再缩 0.8。

### 7. 去掉人工 FBX 的 SafeZone 分流 — **已落地（2026-09-15）**

不是「把 SafeZone 抽成窗口适配以后再用」。产品不需要 0.8 立方体缩放。人工直接选 `.fbx` 与 `.obj` 相同：先③ `ToolPrefabApi.BuildPrefabs`，再 `Run(plan)`。`CreateNormalizedPrefab` / `FlattenSourcePaths` 无产品调用，已在第 12 步删除。姿态层（⑤.5）未拍板，不得把这条创建链搬过去。

验收：`FlattenSmokePathTests.ManualOrchestration_DoesNotCallFlattenPaths`。管线与人工 FBX 都是空壳 Finish，无 SafeZone 位移。

### 8. 去掉④写/清 `assetBundleName` — **已落地（2026-09-15）**

Finish 与死代码 SafeZone 创建链都停写、停清。⑥ 继续 `AssetBundleBuild[]`。`GeneratedAsset.BundleFileName` 仍是内存字段（步骤 12 再看）。

验收：`FlattenSmokePathTests.FlattenKernel_DoesNotWriteImporterBundleNames`。仓内无再写 Prefab Importer 标签。

仓内已确认无读取依赖。编辑器⑥双端 AB 请人补跑。

### 9. 七步结构化结果 — **已落地（2026-09-15）**

现网 E/D/C 是 `void`，残留 `.fbm`、映射失败多半只打 Console，Finish 仍可能成功。本步让 `FlattenRowResult` 带上：哪一步失败、拷了多少、还剩哪些外部 `.fbm`、本单元未绑上的贴图槽（若已能看见）。编排读对象，不解析日志。

**本步不改变**算不算失败（缺 sidecar 仍用现网 `HasMissingSidecars`→40）。残留 `.fbm` 是否 Fail 见第 14 步（现为 41、不停⑤⑥）。本单元缺合法贴图见第 11 步（现为 42、不停⑤⑥）。

`FlattenRowResult` 增加 `CopiedDependencyCount`、`ExtractTexturesCallCount`、`LeftoverExternalFbm`、`UnboundTextureSlots`（`_MainTex` 空，或贴图路径不在本 Art 单元）。`Run(plan)` 在 Begin 之后填这些字段。**不改** Fail 闸。Console 打 `[Flatten] facts …`。

验收：Begin 失败时计数字段为 0（`FlattenPlanTests`）；真跑一行可在日志看到 copied / extractTextures / leftoverFbm / unboundSlots。

**为何 11 需要 9：** Extract/Remap 原为 `void`，本单元贴图异常缺少返回通道。第 11 步通过 `FlattenRowResult.TextureIdentityWarnings` 返回警告；内核 `Ok` 不变。编排 `PipelineFlattenQuality` 记 `FlattenTextureIdentity(42)`，不 `return`。`HasMissingSidecars` 是磁盘 URI 缺件，保持 `FlattenFailed(40)` 整趟停，不能与贴图身份混用。

### 10. Extract 只留一个所有者 — **已落地（2026-09-15）**

E（`FlattenApplyImportAndExtract`）是 `ExtractTextures` 的唯一产品口。Finish `TryHeal` 只补拷 + remap，不再调 `ExtractAndBind`。死代码 SafeZone 创建链在自愈前自己抽一次，避免那条链零 Extract。

不改 Extract 算法（仍：有外部 `.fbm` 才 `ExtractTextures`；无则 remap 后可能重导）。`FlattenRowResult.ExtractTexturesCallCount` 记实际 `ExtractTextures` 次数。

验收：`FlattenSmokePathTests.FinishHeal_DoesNotOwnExtract`。CLI 日志 `extractTextures=` 与 `ExtractTextures 已调用` 条数一致，且自愈日志为「自愈不 Extract」。

2026-09-15 CLI（`materialId` 分单元）：直18 `glb.glb` → `extractTextures=0`（ScriptedImporter，E 跳过）；歼15 GLTF → `copied=26 extractTextures=0`；OBJ/FBX 走了 E 口但无外部 `.fbm`，`ExtractTextures` 次数仍为 0。四趟 `exit=0`，自愈均「不 Extract」。

### 11. 本单元贴图身份（D25-4）— Fail(42) 不停⑤⑥，保留原引用

**依赖第 9 步**（结果通道）。**2026-09-16：** 找不到本次合法贴图 → 内核 Warning + 列表，`row.Ok` 仍 true，批量继续；不清空、不解除原引用；编排 `Fail(42)` 且不 `return`（与 leftover 41、⑤ 的 50 同型）。不得因此宣称“最终依赖已完全隔离”。不是把原有 glTF 缺必需 sidecar 也降为 Warning，也不是 40 整趟停。

不要在第 9 步之前改绑图算法。第 10 步不改查找，只收 Extract 所有者。

不是「把 `FindAssets` 限定到本 Art 单元」——现网 `ResolveExistingOrDefaultTexturePath` **已经**只在本包四个贴图出口按文件名找。C# 没有全工程 `t:Texture` 搜索。backlog 旧句「`RemapModelImporterTexturesToArtFolder` 按文件名全工程解析」不准确。

**会串的原因：** FBX 槽位短名（`hull` / `hull.jpg`）工程内可重名。B 前半段已是「Incoming Prefab `GetDependencies` → 拷贝 → `objectMap` 按对象/GUID 再绑」。Extract 后再 `SaveAndReimport`，对 **Art 里的 FBX** 重新 `GetDependencies`；Unity 按短名挂上**兄弟单元**已有资产。随后 remap 把这条路径当成本单元源，按文件名 `CopyAsset` / `AddRemap`。

**要改的（开第 11 步时）：** 依赖表只承认本 `Art/<名>/` 下的路径；`AddRemap` 的目标必须是迁之前引用图里的副本，或本轮 Extract **写进本单元文件夹** 的文件。不要把兄弟 Art 的同名文件当源。B′ 相对 URI 树不要改成这条。内嵌贴图在 Incoming 往往没有独立文件（D25-2 B），只能对本单元 FBX Extract 到本单元再绑抽出的文件。

**同名覆盖是另一条链，不要和借图混成一步修：**


|     | 覆盖 / 复用                                         | 跨单元按名借图                   |
| --- | ----------------------------------------------- | ------------------------- |
| 冲突  | 同一条目标路径已有文件                                     | 两条路径、短名相同                 |
| 现网  | `CopyAssetToExactPath` 目标存在则**不覆盖、用旧的**；① 整夹删再拷 | `GetDependencies` 已指向兄弟单元 |
| 清夹  | 管线④默认只删本次 `Art/<名>/`                            | **清不掉**兄弟单元，挡不住借图         |
| 纠缠  | 现网 remap 会把兄弟文件**拷进**本单元，借图落成「本地旧文件」            | 下次只认本单元路径也可能认到赃像素         |


人工平铺默认不清夹，本单元旧同名文件会留下。

**本次实现边界：**

- `FlattenTextureIdentity`：Begin 写入前记录源依赖；B 登记实际副本，按 GUID 跟随本轮路径移动；E 登记本轮 Extract 新增/内容改变的文件。账本放在 `RetinarFlattenWork`，不读 ctx、不使用跨行静态状态。
- `RetinarBatchModelBuilder.TextureBinding`：只绑定账本认可的文件。模型短名只在该模型已知来源内匹配；`hull.jpg` 不猜成 `hull.png`，无后缀且有多个候选时不猜。
- **退化的是“按名字猜图并补拷”的兜底，不是整段 Finish。** E、D、C 和 Finish 均限制普通平铺的贴图来源。Finish 不再从重导依赖里补拷陌生贴图；其他材质/动画等引用整理保留。
- 人工不清夹时，旧同名文件不能仅因路径在本单元就成为合法副本。当前用内容一致性核对副本；同名覆盖策略本身不改。Extract 未改变的旧文件也不能凭目录扫描获得本轮身份。
- B′ 相对 URI 树保持现状，不套这条按模型身份的规则。
- `FlattenRowResult.TextureIdentityWarnings` 返回警告，`Ok` 不因此变 false。日志含单元、模型/材质槽、贴图路径。空槽和容器内嵌图不直接按“缺合法独立贴图”报警；第 9 步 `UnboundTextureSlots` 仍只是观察数据，不用于 Fail。管线编排另记 **42**。

**自动验收（2026-09-15）：27/27 通过，0 失败、0 跳过。** Unity 2022.3.54f1c1 EditMode：`FlattenTextureIdentityTests` 9 项 + `FlattenPlanTests` / `FlattenSmokePathTests` / `FlattenManualPlanFactoryTests` 18 项。包含实际 OBJ+MTL 引用的副本绑定、保留错误 Importer 引用、B→D→C 材质链、同名不同后缀/多候选、旧同名拒绝、Extract 文件来源证据和源材质保护。测试使用专用临时目录，已清理；没有导入或打包真实飞机样例。

结果：项目根 `Logs/d24-step11-tests.xml`；日志：`Logs/d24-step11-tests.log`。**真实 FBX/OBJ/GLB/glTF 外观、真实内嵌 FBX Extract 和双单元批量仍需验收**，不以测试或日志数量代替视觉核对。警告保留引用意味着仍可有跨单元依赖；本次不是最终交付隔离完成。用户随后反馈常规测试暂无异常，允许第 12 步开工；第 12 步结果见下节。

### 12. 删死代码与菜单旧壳 — 已清理（2026-09-15）

前提：用户第 11 步常规测试暂无发现异常，但未确认触发重名贴图；仓外依赖已确认无必要保留。只清理无产品调用的旧链，不调整搬文件/命名/后缀/警告规则。

- 删除 `ToolFlattenApi.FlattenPaths` → `RetinarFlattenApi.FlattenPaths` → `FlattenSourcePaths` / `CreateNormalizedPrefab` 旧链，连同只服务旧链的 `CreatePackagedAdjustedPrefab`、选中项/弹窗、SafeZone 0.8 缩放、旧源图搜集、旧材质/动画创建等助手。
- 删除零调用 `RemapCopiedAssets`、未读 Emission/旧材质常量、内核里的 `OpenDeliverablesFolder` 转发。现用菜单仍走 `RetinarEditorUtil.OpenDeliverablesFolder`，功能不删除。
- `GeneratedAsset` 仅保留收尾需要的单元名、路径和 Prefab；删除未读的 `BundleFileName` 等旧导出字段。
- **保留：** 七步方法、菜单可用性校验、碰撞盒/Bounds、空壳/轴向、现用动画整理、E Extract、Finish 引用整理、步骤 11 贴图身份。`Run(plan)` 与分步兼容窄口仍在，不顺手清掉整个 API。
- 使用 C# 语法节点定位删除边界；未执行整段正则切函数。相关脚本保留在原目录，未整包迁移。

**验证：** 独立编译通过；Unity EditMode **32/32 通过**（9 贴图身份 + 7 plan + 8 路径/删除边界 + 8 人工计划）。日志与结果位于项目根 `Logs/d24-step12-tests.log` / `.xml`。首轮 CLI 测试实例因项目已被打开而未能启动；用户关闭编辑器后复跑通过，不记作代码回归失败。

**真实 CLI（仅两份，不全量导入）：**

| 样例 | 新测试单元 | 结果 |
|---|---|---|
| 歼6 `3d/fbx.FBX` + 外置 `j6_11.jpg` | `D24S12_J6_FBX_20260915` | B；copied=2、extractTextures=0、leftoverFbm=0、身份警告=0；⑤失败0；Android/iOS AB 均生成；exit=0 |
| 歼15 `GLTF/…FBX.gltf` + 相对文件 | `D24S12_J15_GLTF_20260915` | B′；copied=26、extractTextures=0、leftoverFbm=0；Art glTF URI 核对无缺失；⑤失败0；Android/iOS AB 均生成；exit=0 |

日志：项目根 `Logs/d24-step12-fbx.log`、`Logs/d24-step12-gltf.log`。歼6触发现有⑥后顶点色补刷/重打分支，最终成功；不是本步新增行为。两组输入共核对 28 个源模型/伴生文件 SHA-256，执行前后均一致。测试未覆盖真实内嵌 FBX Extract，也不据此宣称真实重名专项或移动端视觉验收完成。

测试产物保留在对应 `Assets/Incoming/<测试单元>`、`Assets/IncomingPrefab/<测试单元>.prefab`、`Assets/Art/<测试单元>` 及 `Deliverables/<测试单元>`，不覆盖既有同名单元。TU 原图已更新为五页目录/文件与全流程展示，不新增截图。第 12 步完成。

### 13. 删除重复七步编排 — **已落地（2026-09-16）**

先核对，再删无人调用的转发，不把「再删一遍步骤 12 已删编排」当任务，不迁目录，不改七步算法。

**核对（删前）：**

| 层 | 七步/选中转发 | 仓内产品调用 |
|---|---|---|
| `PipelineRunner` / `ManualFlattenOrchestration` | 无 | 只 `FromContext` → `Run(plan)` |
| `ToolFlattenApi` | `TryBegin`…`TryFinish`、`Should*`、`FlattenSelectedToArt` | 无（仅自身定义） |
| `FlattenBuildService` | 同上七步包装；`ApplyImportAndExtract(work, ctx)` 仍读 ctx | 无；`Run` 已直接调内核 |
| `RetinarFlattenApi` | 七步包装 + `FlattenSelectedToArt` | 无 |
| `RetinarFlattenScheduler` / 菜单 | 选中项 | `ManualFlattenService` → `ManualFlattenOrchestration` → `Run(plan)` |
| `RetinarBatchModelBuilder` 七步 | 实现 | **`FlattenBuildService.Run` 与贴图身份测试** |

**ctx 转换只在一处：** `FlattenBuildService.FromContext`（`HasExternalUris` → Branch，`ImporterKind` → 是否 E，sidecar / MissingUris 抄进 plan）。`CreateOptions(ctx, request)` 只转调 FromContext，供测试。`Run(plan)` 不读 ctx。

**本步删除：** `ToolFlattenApi` 与 `FlattenBuildService` 上的公开七步方法；`ToolFlattenApi` 上无调用方的 `Should*` / `HasMissingSidecars` / `FlattenSelectedToArt`；整个 `RetinarFlattenApi`。`ShouldRelocateAtomic` / `ShouldApplyArtModelImporter` 收成 `FromContext` 私有。

**保留：** 内核七步、`Run` / `FromContext`、`CreateOptions*`、`HasMissingSidecars`（探针测试）、菜单 `RetinarFlattenScheduler`、`ManualFlattenService` 薄转发、物理目录。

验收：`FlattenSmokePathTests.Step13_DuplicateSevenStepPublicOrchestrationRemoved`。未迁目录。第 14 步质量闸已落地（41）。

### 14. `.fbm` 质量闸 — **2026-09-16 Fail(41) 不停⑤⑥**

第 14 步**不是**再改搬文件、Extract 或搜图，也**不是**第 13 步漏掉的拆类。它只回答：Finish 后 Prefab 仍依赖本单元外 `*.fbm/` 贴图时，算不算失败。

**用户决定：** leftover **报错但不卡**。内核仍 Warning + `LeftoverExternalFbm`，`row.Ok` 仍 true（不走 `FlattenFailed(40)` + `return null`）。编排 `PipelineFlattenQuality.Apply` → `Fail(41)`，④循环继续、⑤⑥照跑。CLI `exit=41`（本趟已跑完后续步）。同趟若⑤硬失败则升为 **50**；⑥全失败仍 **60**。同一行若同时有身份警告，**41 优先于 42**。空槽 / OBJ 缺 `.mtl` / glTF 缺 URI 不捆进本 `if`。

旧文档「B 质量闸」即此事。真正扫依赖在 **Finish 自愈之后**。这不是「`.fbm` 抽不出来」：图往往还在，只是引用仍挂在单元外缓存夹上。

Prefab `GetDependencies` 中的贴图：不在本次 `Art/<名>/` 下，且某一级目录名以 `.fbm` 结尾。本单元内不算。⑥ 已无出包前 Extract 兜底。近期真实 CLI 均为 `leftoverFbm=0`，故现网样例仍会 `exit=0`。

**不要和这些闸混：** glTF 缺 URI 已是 Fail(40) 整趟停；空槽只观察；① 缺伴生 Warning 仍导入；无外部 `.fbm` 则跳过 Extract（D25-2 B）；OBJ 缺 `.mtl` 不进 40；D19 顶点色无关。步骤 11 身份是 **42**，同型但不共用一个 if。

验收：`PipelineFlattenQualityTests`；`FlattenSmokePathTests.Step14_QualityCodesDoNotReturnFromFlatten`。

---

## 编排入口与菜单（随本轮提交，不占 1–14）

管线总面板步骤用方括号标注：`[1]` 入库 → `[2]` 总闸 → `[③]` Prefab → `[④]` 平铺 → `[⑤]` 总批量 → `[⑥]` 导出。

| 格 | 勾选 | 打开子面板 |
|---|---|---|
| `[1]` | 无 | 区域框**结尾**「打开批量选择器」 |
| `[2]` | **开头**：总自动化处理 | 无。分项设置自动 / 后处理自动留在资源处理总面板 |
| `[③]` | 开头 | 无。ID2 在本框 |
| `[④]` | 开头 | 「打开平铺面板」= 人工 B/B′（人工 SO）；本框折叠是管线平铺 SO |
| `[⑤]` | 开头 | 「打开资源处理总面板」。该面板标题写明：同时留存自动化参数；管线用上述编号对应。`[⑤]` 跑「执行全部」内核 |
| `[⑥]` | 开头 | 导出 SO |

**菜单**

- `Tools/批量选择器` = `BatchFbxImportWindow`，与 `[1]` 按钮同一窗。旧名 `Tools/批量FBX导入` 已改名，**窗未删**（仍有收集、Conflict、「执行导入」、「输出到编排」）。
- `Tools/资源处理/` 子菜单已撤。「诊断选中模型顶点色」只留 `ModelVertexColorDiagnose` API（管线⑤/⑥日志）；人机扫描走资源总面板「按批量路径仅扫描模型」。`Tools/资源处理总面板` 保留。

**① 旁路（D25-2 A）**

`ToolImportApi.CopyFbxSidecarsBeside` + `FbxPackageFiles.Scan`：跟拷 FBX 同目录图、`Texture/` 等夹、`*.fbm`、FBX 内相对路径。缺件 Warning 仍导入。**（B）** 无外部 `.fbm` 时仍跳过 `ExtractTextures`，未改。

---

第 1–14 步已落地。第 14 步 **Fail(41) 不停⑤⑥**；第 11 步身份 **Fail(42) 不停⑤⑥**。glTF 缺件仍 40 整趟停。
