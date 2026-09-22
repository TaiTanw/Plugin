# ④ 身份闸（42）收尾

现约：[开发者须知](../README.md) · [④ 能力](./pipeline-flatten-capabilities.md)。  
本串随 **v1.6.8** 提交（基线曾是 v1.6.5）。下文是刀序与文件范围。不是现约，不覆盖操作者流程。

样包：`测试1112.unitypackage`（信封 `Eye_Eyeball`，根 Prefab `Character01` + `Eye_Eyeball`）。

---

## 先读

**42 不是单一 bug。** 编排把本趟所有贴图身份警告收成一个质量码；有一条就 `exit=42`，⑤⑥照跑。所以 Console 仍报 42 **不等于上一刀没干活**——常见是关了上一闸、下一闸亮了。

本串从 pack 剥 Missing Script 起，目的是让④能写完 Art，再按「同名不一定同一张图」把副本落到不互盖的路径，并用 Begin 指纹认账。**没有**做：同槽别名、F8 占用表、Incoming 相对整树（B′）、短名兄弟搜索、放宽指纹。

| | 状态 |
|---|---|
| 提交 / 上推 | **v1.6.8**（GitHub `origin`、Gitea `team`） |
| 剥 miss → ④ 能交 Art | 已落地；睫状肌类 40 已过 |
| 同名两套图并存（落点） | 已落地；磁盘上 `Texture/` 与 `<名>.fbm/` 字节可不同 |
| 相同字节的套层副本入账 | 已落地（TrustMatching） |
| FBX 抽出后搬到套层再入账 | 已落地（伴生后 `RegisterExtracted`） |
| 眼睛 leftover **41**（本样） | **本趟已关**（`leftoverFbm=0`） |
| Character01 当前 **42** | **本样已关**（清空重导，`exit=0`，`textureIdentityWarnings=0`） |
| `Child_TouFa.dds` → Unknown | 分类后缀未含 dds，**不是 42** |
| ⑥ 后顶点色被冲再刷 | **不是 42**，未动 |

**收尾：** 本样到此为止，已随 **v1.6.8** 发布。操作者会看见的落点、Unknown、空槽、⑥ 后顶点色日志已写入 [操作者须知](../../operator/README.md)。重绑规则写入 [④ 能力](./pipeline-flatten-capabilities.md)。失败形态三行（单元外 `.fbm`→41、内嵌才 Extract、跨单元短名→42）仍在 [backlog](../03_open-items/backlog.md)，本样没有顶上。

---

## 刀序（实际开工，不是设想）

顺序即施工顺序。每一刀只关当时那条闸。

### 0. 剥 Missing Script（④ 前置，不是 42 本身）

pack 预处理丢掉 `.cs` 后 Prefab YAML 仍挂脚本 → `SaveAsPrefabAsset` **40**，身份闸够不到。

- 内核 policy 布尔 `StripMissingScripts`；管线 `CreatePolicy` **强制 true**；人工 SO 默认 false。
- Begin：Load → 剥 miss → **再**清本次 `Art/<名>/` → 标准夹 → 存 Art Prefab。清夹时 `AssetUnitFolder` 可跳过 Refresh（LoadPrefabContents 持有期间）。
- 内核不 `if (pack)`、不 Load 管线 SO。

实测：测试1112 ④ 写出 `Assets/Art/Character01` 与 `Eye_Eyeball`，⑥ 打出 AB。

### 1. 删 SyncNewer

⑤ 在④后、管线默认清本次 Art。拷贝不再按体积/时间戳跳过；**同名已占坑则不覆盖**（先到先得）。

为落点刀清路：否则大图会被⑤压过的小图或先入的平铺文件挡住。

### 2. B 落点（还原引用的存放）

产品锁定：**套层，不按文件名压平两张不同的图。**

- `.fbm` 父夹永远套一层：`image/Texture/<名>.fbm/`。
- 非 `.fbm`：仅当分类根文件已被占用、且来源父夹名不是分类叶名时才套一层。
- E：本趟已有本单元副本则 **不 Extract**（仍无副本、只有内嵌时才抽）。
- 伴生搬移、动画拷贝、自愈补拷 **同一** `ResolveDestAssetPath`。

实测：`Incoming Texture/Child_Face.jpg`（674KB）与 `Child.fbm/Child_Face.jpg`（941KB）在 Art 各占其位；眼睛 jpeg 同理。**42 未消失**：账本当时还不认套层 GUID。

### 3. TrustMatching A

扫描本单元 `t:Texture`，指纹与 Begin `sourceFingerprints` **字节相同** 才写入 `trustedGuids`。**不改** `copies[source]`（那是同槽别名，已否决）。指纹不同的同名图不得混认。

实测：相同字节对（如部分 `zhengtai_d` / `3d66`）不再报；**不同字节**的 `zhengtai_d_2` 仍 42。眼睛仍可能 41（抽出图不在 Capture 表里）。

### 4. 伴生后再 `RegisterExtracted`

FBX 重导冒出的 `Model/<名>.fbm/` 搬到 `image/Texture/<名>.fbm/` 后，把「搬之前 Dest 夹里没有、搬之后出现」的文件记为 **本趟抽出**，加入模型贴图表 + `trustedGuids`。Capture 从未见过的抽出图也能入账。

实测（最新一趟 Console）：

- 两行 `leftoverFbm=0`，眼睛 **41 关掉**。
- 材质槽 `zhengtai_d_2` 那类 BindKnownTexture 42 **关掉**。
- 新闸：`Child.fbx` ×6 条 `RemapKnownModelTextures`（路径 / 文件名 / 无后缀各一遍，实为 Face+Body 两对）。

### 5. 最新一趟核对（仍 42，未改代码）

Console facts：

| 单元 | copied | extract | leftoverFbm | unboundSlots | textureIdentityWarnings |
|---|---:|---:|---:|---:|---:|
| Character01 | 44 | 0 | 0 | 6 | **6** |
| Eye_Eyeball | 52 | 0 | 0 | 2 | **0** |

6 条全是 `Child.fbx` 的 Face + Body。每张图报三遍：依赖全路径、带后缀文件名、无后缀名。`unboundSlots=6` 与这 6 条是同一批，槽位没清。眼睛 `unboundSlots=2` 是空槽，不算 42。`Child_TouFa.dds`、⑥ 后顶点色被冲再刷，都不是这闸。

`PreferUniqueFbmIfAmbiguous` **已经在这趟生效**，不是没编进编辑器。同一份 `Child.fbx` 的 `AddRemap` 8 条 = `3d66`、`3d66.jpg`、`Child_Face_M`、`Child_Face_M.jpg`，各绑 Texture 与 Texture2D。`Child.fbx.meta` 绑的是套层 GUID（`3d66` → `606f5860…`，`Child_Face_M` → `62789e1d…`），不是平铺那张。Face / Body **没有**进 external map。

跑完后的 Art（第二次伴生整理已清掉 `Model/Child.fbm`）：

| 文件 | 平铺 | 套层 `image/Texture/Child.fbm/` |
|---|---|---|
| `Child_Face.jpg` | `a5fbffe9…`，`Material/Child_Face.mat` | `1c629c22…`，`Material/Materials/Child_Face.mat` |
| `Child_Body.jpg` | `21a7ba74…`，`Material/Child_Body.mat` | `69ac91b1…`，`Material/Materials/Child_Body.mat` |

警告里的依赖**已经是**套层路径（`…/Child.fbm/Child_Face.jpg`、Body 同理）。`RemapKnownModelTextures` 丢掉这条路径，只拿文件名叫 `ResolveForModel`。返回空才警告。Face_M / `3d66` 是「恰好一个套层」所以收住了；Face / Body 在重绑那一刻的本模型账本**不是**这个形状，所以 `nested` 不是 1。跑完再看磁盘，多出来的那条路径已经不在。

### 6. 本单元依赖按路径绑（本样已关）

`RemapKnownModelTextures`：`GetDependencies` 里已经在本单元的贴图，用 `SelectUniqueCandidate` 只在这些路径里定一张（恰好一个 `.fbm` 就用它，一张平铺加一张套层也收成套层）。账本 `ResolveForModel` 只在本单元依赖里没有这个名字时才用。同名两条以上都在 `.fbm`：警告，不改投，也不回退到账本猜一张。不注册新来源，不改材质上的平铺引用。

清空旧 Art / Incoming 后重导测试1112：`exit=0`。Child.fbx 的 AddRemap 从上一趟 8 条增到 16 条（Face/Body 的带后缀与无后缀各绑 Texture 与 Texture2D）。facts：Character01 `textureIdentityWarnings=0` `leftoverFbm=0` `unboundSlots=6`；眼睛 `textureIdentityWarnings=0` `unboundSlots=2`。`unboundSlots` 是空槽，不算 42。

重导后的 GUID（与上一趟不同，因为清过再写）：

| 引用 | GUID | 落点 |
|---|---|---|
| `Child.fbx` 的 `Child_Face` / `Child_Face.jpg` | `ec193649…` | `image/Texture/Child.fbm/Child_Face.jpg` |
| `Child.fbx` 的 `Child_Body` / `Child_Body.jpg` | `7d6e7531…` | `image/Texture/Child.fbm/Child_Body.jpg` |
| `Material/Child_Face.mat` 主贴图 | `3852cf1e…` | 平铺 `image/Texture/Child_Face.jpg` |
| `Material/Child_Body.mat` 主贴图 | `7be9533b…` | 平铺 `image/Texture/Child_Body.jpg` |

`Child_TouFa.dds` → Unknown、⑥ 后顶点色被冲再刷白并重打 AB，仍不是这闸，未动。

编辑器测试：`ModelImporter_BindsInUnitFbmDependency_WhenLedgerHasAnotherSameName`、`ModelImporter_DoesNotGuess_WhenTwoInUnitFbmDependenciesShareAName`。本环境未跑 Unity 测试。

---

## 未做 / 停放（本串明确否决或未排）

- 同槽别名（改 `copies[source]` 指向另一来源）
- F8 占用表
- B′ Incoming 相对整树、短名兄弟搜索
- 放宽指纹（不同字节当同一张）
- 分类加 `dds`、⑥ 顶点色冲刷、信封 ID2=`Users_16028_Desktop`

---

## 改动范围（相对 `Assets/Plugin`，v1.6.8）

路径以插件仓为根。外部审阅可按组打开，不必通读 3000 行内核。

### 剥 Missing Script + Begin 顺序

| 文件 | 改什么 |
|---|---|
| `TOol/Editor/Generated/Flatten/Config/FlattenOperationSettings.cs` | policy / SO：`StripMissingScripts`；管线 CreatePolicy 强制 true |
| `TOol/Editor/Generated/Flatten/FlattenOperationSettingsGui.cs` | 仅人工面板勾选 |
| `TOol/Editor/Generated/Flatten/Config/RetinarFlattenOptions.cs` | 内核 Options 布尔 |
| `TOol/Editor/Generated/Flatten/Service/FlattenReferenceAudit.cs` | `StripMissingScripts` |
| `TOol/Editor/Generated/Flatten/Service/FlattenBuildService.cs` | 从 plan 冻 Options；Begin 顺序 |
| `TOol/Editor/Generated/Flatten/Service/RetinarBatchModelBuilder.cs` | Begin：剥 miss → 清夹 → 存 Prefab |
| `TOol/Editor/Shared/AssetUnitFolder.cs` | 清夹可跳 Refresh |
| `TOol/Editor/Generated/Flatten/Service/FlattenMissingScriptStripTests.cs` | **新** |
| `TOol/Editor/Generated/Flatten/Config/FlattenOperationSettingsTests.cs` | 管线强制 / 人工默认 |
| `TOol/Editor/Generated/Flatten/Config/FlattenPlanTests.cs` | policy 快照 |
| `Pipeline/Editor/ManualFlatten/Plan/FlattenManualPlanFactoryTests.cs` | 人工默认 |
| `docs/operator/README.md` | 操作者：管线剥 miss |

### 删 SyncNewer + B 落点

| 文件 | 改什么 |
|---|---|
| `TOol/Editor/Generated/Flatten/Service/RetinarBatchModelBuilder.cs` | `CopyAssetToExactPath` 占坑不覆盖；拷贝循环走落点；伴生后 `RegisterExtracted` |
| `TOol/Editor/Generated/Flatten/Service/FlattenCopyRunner.cs` | `ResolveDestAssetPath` / `AppendSourceDisambiguation` |
| `TOol/Editor/Generated/Flatten/Service/FlattenCopyRunnerTests.cs` | **新** |
| `TOol/Editor/Generated/Flatten/Service/FlattenAnimationClipRemapper.cs` | 动画拷贝同一落点 |
| `TOol/Editor/Generated/Flatten/Service/RetinarBatchModelBuilder.AssetResolution.cs` | E 跳过 Extract；自愈落点；E 后 TrustMatching / RegisterExtracted |

### 身份账本（TrustMatching + 抽出登记）

| 文件 | 改什么 |
|---|---|
| `TOol/Editor/Generated/Flatten/Service/FlattenTextureIdentity.cs` | `TrustMatchingUnitTextures`、`RegisterExtracted`、`DestFbmFolder`、套层歧义时优先唯一 `.fbm` |
| `TOol/Editor/Generated/Flatten/Service/FlattenTextureIdentityTests.cs` | 指纹冻结、不写 copies、伴生 Dest 入账 |

### 现约指针（一行级）

| 文件 | 改什么 |
|---|---|
| `docs/dev-wip/04_implementation/pipeline-flatten-capabilities.md` | B 落点；本单元路径重绑；本样 `exit=0` 与 backlog 三行分开 |
| `docs/dev-wip/README.md` | 管线④固定剥 miss；分册改为收尾 |
| `docs/operator/README.md` | 平铺写完之后：两张同名图、Unknown、空槽、⑥ 后顶点色 |
| `docs/dev-wip/02_structure/overview.md` | 本样已关 / 失败真样仍暂放 |
| `docs/dev-wip/03_open-items/backlog.md` | 测试1112 不顶上 1–3 |
| `docs/dev-wip/05_dev-log/timeline.md` | 未提交一句，不加假提交 |
| `TOol/Editor/Generated/Flatten/README.md` | 重绑规则指针 |

### 第 6 刀

| 文件 | 改什么 |
|---|---|
| `TOol/Editor/Generated/Flatten/Service/RetinarBatchModelBuilder.TextureBinding.cs` | 本单元依赖按路径绑；两条套层不猜 |
| `TOol/Editor/Generated/Flatten/Service/FlattenTextureIdentityTests.cs` | 上面两条编辑器测试 |

`FlattenPlanTests` 里 Begin 失败文案断言可能仍偏旧（英文 no-renderers），与 42 无关，审阅可忽略。
