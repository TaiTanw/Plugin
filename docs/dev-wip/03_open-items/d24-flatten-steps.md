# d24-flatten-steps

返回 [待办](./backlog.md#a-open-items) · [D24 边界](./d24-boundary-plan.md) · [总目录](../README.md)

> 2026-09-15。④ 收口的最小可验证步骤。不含① 外置贴图、⑤、面板强制入库、退出码覆盖、重写⑥、D19。

## 已拍板（本页）

- 多行对齐：只用**下标**。禁止 `contexts[0]` / 第一份 `JobContext` 套到后续行。能配对的行照铺；**循环结束后**若数量不等或有未配对行 → Fail。不回滚已写出的 Art。
- **稳定行 ID 本目标不做。** 现网 `PipelineSourceBinding` 没有行主键；`materialId` 会重名，不能当键。
- 手动：人用按钮选定 Branch；**禁止** `PipelineJobContext.Build`。点错 B 默认**放行**。若在正式平铺前用已有 `GltfPackageFiles.Scan` 扫到相对 URI → **只提示，不拦截**。
- 仓内⑥ 不依赖 Prefab Importer 上的 bundle 名 → **第 8 步可做**。
- **第 5 步：基本核对完成**（2026-09-15）。缺件 30/40 已实跑；同名跨单元已按代码记清「现在会串」。未逐套勾目录树/源 hash 的格子不挡从第 1 步开工；也**不能**凭此宣称 Extract/搜图/去标签已验证。
- 开发顺序见下节。后面若干步（尤其 10–14）要等前一步**真正落地后再评估**，不是表填完就一次开完。

## 开发顺序

按编号从前向后做。**当前步完全顺利，才进入下一步。** 遇到卡点停下，不跳号「先做后面容易的」。

| 步 | 何时开 | 卡点时 |
|---|---|---|
| 1 | 已落地；假 plan 可 `Run` | — |
| 2 | 已落地；管线 `FromContext` → `Run` | — |
| 3 | 已落地；按下标配对，禁止 `ctx[0]` | — |
| 4 | 现在可开 | 手动断 ctx、轻扫描只提示 |
| 6 | 第 2、4 步 `Run(plan)` 接通后 | 冒烟失败则先修口，不改 Extract |
| 7、8 | 可与 1–4 穿插，仍须前一步顺利 | 8 仓内已核无读标签 |
| 9 | 第 1 步类型落地之后填字段 | 没有结果对象，11/14 的闸接不上 |
| 10–14 | **等前一步实际开发后再评估** | 例：11 需要 9；14 需要 9；10 要看 9 里能否看见双 Extract |

不要把「第 5 步基本核对」理解成 10–14 已解锁施工。基本核对只允许**从前面换口**；改 Extract / 绑图 / 删内核 / 升质量闸，仍看当时前一步产出。

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

### 4. 手动组 plan，禁止 ctx；轻扫描只提示

按钮 → `plan.Branch`。B′ sidecar 用 `GltfPackageFiles.Scan`（①/2.5 已用的正则扫 `"uri"`），不是 `JobContext.Build`（后者还 Load 主资产、Importer、材质形态、OBJ 轴向）。

选中 `.gltf`：直接 `Scan`。选中 Prefab：`GetDependencies` 找到 `.gltf` 再 `Scan`（仍不是 ctx）。FBX/OBJ：Scan 不适用，无提示。

点「普通平铺」且 Scan 到相对 URI：**提示后仍平铺**（可能拆坏相对路径，人责）。

验收：两按钮完整④；B′ 缺 `.bin` 仍失败；B 打外 URI glTF 有提示且不因 ctx 被拒。

### 5. R1b 对照表（行为改动闸）

**状态：基本核对完成（2026-09-15）。** 换口（1–4、7、8）可以按序开工。改搬文件/Extract/绑图算法前，仍应用本表做改前/改后对比；未勾的格子改算法时补记，不把第 5 步重开成阻断。

| 样例 | 要确认的具体事实 | 核对 |
|---|---|---|
| FBX 外置贴图 | Art 树（Model/Texture/Material/Prefab）；Prefab 依赖是否都在本单元；源 FBX/源材质 hash 不变 | 未逐套勾；改 E/搜图前补 |
| FBX 内嵌贴图 | Extract 后本单元有独立图；不是仍挂导入区 `.fbm`；源 hash 不变 | 未逐套勾；改 E 前补 |
| OBJ+MTL | 仅当源 `.obj` 里有 `mtllib` 且旁边有 `.mtl`。没有 `.mtl` 的 OBJ（或现有全是 FBX/glTF）此格标「无样例」 | 可标无样例，不挡换口 |
| 缺件 glTF | ① Warning 仍拷 `.gltf`。缺 `.bin`：主资产不是 GO，③ **exit=30**，④ 的 40 走不到。要看到 40：能出 Prefab、缺的是图不是 buffer | **已实跑。** 删 `.bin` 的 `、dddd.gltf`：缺 25 URI，`exit=30`。保留 `.bin` 删 4 张 jpg（`…FBX.gltf`，ID2=测试）：③ 成功、`missingUris=4`、④ **exit=40** |
| GLB | 整文件在 Model/；C 后有独立 `.mat` | 未逐套勾 |
| 完整外 URI glTF | `Art/<名>/<名>/` 相对树仍能解析；`.bin`/外图都在；exit=0 | 未逐套勾（有过整包入库日志，未当对照页） |
| 自包含 glTF（全 `data:`） | JSON 里贴图/buffer 是 `data:`；走 B 不走 B′；不当缺件 | 代码口径已核；未单独立跑页 |
| 同名跨单元贴图 | 第 5 步只**记录现状**（现网会串）；第 11 步才改 | **代码已核清会串**（见第 11 步）。未做两单元同名实跑页 |
| OBJ 轴向开/关 | 开：内容节点 −90°X；关：与源一致；重跑不叠第二层壳 | 先前已落地；本轮未再跑 |
| 源保护 | 复制失败：源 Prefab/材质 hash、路径、GUID 不变 | 静态查封已有；故障注入未做 |

每条另记：管线退出码；人工 B / B′；Art Prefab 路径。

### 6. 换口冒烟

自动一条 + 手动 B + 手动 B′。对照第 5 步已有格子；未勾的只确认「没崩、仍出 Art Prefab、源 hash 不变、管线不进 SafeZone」。

### 7. SafeZone 移出 `Run(plan)`

窗口「直接选 FBX + 普通平铺」时，把模型缩进约 0.8 立方体并移到 `(0, 0.15, 0)`。改的是 Prefab 上模型节点的缩放/位移，不是拷文件。管线④不走这条。

和 OBJ 轴向同类（交付姿态）。本步只从④内核拆出。是否单开「⑤之后、⑥之前」姿态层（轴向 + 可选 SafeZone）未拍板。

验收：管线 FBX→③→④ 无 SafeZone 位移。

### 8. 去掉④写/清 `assetBundleName`

七步 Finish 与 SafeZone 两处都停写。⑥ 继续 `AssetBundleBuild[]`。

验收：仓内无再写标签；⑥ 仍出双端 AB。

仓内已确认无读取依赖，本步可做。

### 9. 七步结构化结果

现网 E/D/C 是 `void`，残留 `.fbm`、映射失败多半只打 Console，Finish 仍可能成功。本步让 `FlattenRowResult` 带上：哪一步失败、拷了多少、还剩哪些外部 `.fbm`、本单元未绑上的贴图槽（若已能看见）。编排读对象，不解析日志。

**本步不改变**算不算失败（缺 sidecar 仍用现网 `HasMissingSidecars`→40）。升闸（残留 `.fbm`、本单元缺合法贴图）等第 11 / 14 步拿着对象再评估。

验收：缺文件 / 残留 `.fbm` 在结果对象里能看到。

**为何 11 需要 9：** Runner 的 Fail（`Fail(40)` + `return null`，停⑤⑥）结构已有。Extract/Remap 目前是 `void`，贴图绑错/本单元没有合法图**送不到**编排。`HasMissingSidecars` 是磁盘 URI，不能拿来当 D25-4 闸。没有第 9 步，第 11 步即使改了查找，也只能继续 Warning 或白膜。

### 10. Extract 只留一个所有者

E 与 Finish 自愈不再各 Extract 一次。不改 Extract 算法。

**等第 9 步能看见 Extract 次数/残留后再评估是否开。** 未核对：自愈是否仍要第二次 remap（可拷文件，不可再 Extract）。

验收（若开）：同一样例重导次数下降；贴图字节/顶点色对照表。

### 11. 本单元贴图身份（D25-4）

**依赖第 9 步**（结果通道），以及当时对 Fail/Warning 的产品拍板。不要在第 9 步之前改绑图算法。

不是「把 `FindAssets` 限定到本 Art 单元」——现网 `ResolveExistingOrDefaultTexturePath` **已经**只在本包四个贴图出口按文件名找。C# 没有全工程 `t:Texture` 搜索。backlog 旧句「`RemapModelImporterTexturesToArtFolder` 按文件名全工程解析」不准确。

**会串的原因：** FBX 槽位短名（`hull` / `hull.jpg`）工程内可重名。B 前半段已是「Incoming Prefab `GetDependencies` → 拷贝 → `objectMap` 按对象/GUID 再绑」。Extract 后再 `SaveAndReimport`，对 **Art 里的 FBX** 重新 `GetDependencies`；Unity 按短名挂上**兄弟单元**已有资产。随后 remap 把这条路径当成本单元源，按文件名 `CopyAsset` / `AddRemap`。

**要改的（开第 11 步时）：** 依赖表只承认本 `Art/<名>/` 下的路径；`AddRemap` 的目标必须是迁之前引用图里的副本，或本轮 Extract **写进本单元文件夹** 的文件。不要把兄弟 Art 的同名文件当源。B′ 相对 URI 树不要改成这条。内嵌贴图在 Incoming 往往没有独立文件（D25-2 B），只能对本单元 FBX Extract 到本单元再绑抽出的文件。

**同名覆盖是另一条链，不要和借图混成一步修：**

| | 覆盖 / 复用 | 跨单元按名借图 |
|---|---|---|
| 冲突 | 同一条目标路径已有文件 | 两条路径、短名相同 |
| 现网 | `CopyAssetToExactPath` 目标存在则**不覆盖、用旧的**；① 整夹删再拷 | `GetDependencies` 已指向兄弟单元 |
| 清夹 | 管线④默认只删本次 `Art/<名>/` | **清不掉**兄弟单元，挡不住借图 |
| 纠缠 | 现网 remap 会把兄弟文件**拷进**本单元，借图落成「本地旧文件」 | 下次只认本单元路径也可能认到赃像素 |

人工平铺默认不清夹，本单元旧同名文件会留下。

**Fail 结构：** 编排层已可行（与缺 sidecar 相同停法）。第 11 步若走 Fail，须用第 9 步的类型化事实，且多半发生在 Begin/拷贝**之后**（本行 Art 半成品、不回滚）。

未拍板：本单元找不到合法贴图是 **Fail**、白膜，还是 Warning。三者不是换皮：Fail 停⑤⑥并改退出码；Warning / 白膜继续打 AB。

验收（若开）：两单元同名图不串；不把兄弟文件拷进本单元。

### 12. 删死代码与菜单旧壳

`RemapCopiedAssets`、未读 Emission 常量、`OpenDeliverablesFolder` 转发；确认无调用的旧 `FlattenSourcePaths`。

**等换口稳定、调用方不再走旧壳后再评估。** 未核对：仓外反射。

### 13. 删除重复七步编排

Runner / 手动只调 `Run(plan)`。**等第 2、4、6 步实际接通后再评估。**

### 14. `.fbm` 质量闸

**需要第 9 步**能看见 leftover 之后再决定。未拍板；不在换口阶段升闸。

---

现在从第 4 步按序开发。第 5 步基本核对只给换口放行；10–14 仍「前一步落地后再评估」。
