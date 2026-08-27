# D13 · GLB 安卓洋红 / 交付 Shader（归档）

> 从 backlog **H** 抽出；**D13 已归类完成**（工程结构 + ggdddd 安卓验通）。  
> 残余：完整槽表、纯 URP 若仍洋红换 Lit、D15 单单元路径 —— 低优 / 遇到再说。

返回 [backlog](./backlog.md) · [总目录](../README.md)

## 原 H 全文

> 2026-08-26：编辑器内总流程已用 `Assets/Art/ggdddd` 验收。交付 AB 约 19.1MB，**不是空包**。


| 项        | 事实                                                                                                     |
| -------- | ------------------------------------------------------------------------------------------------------ |
| 通道       | GLB → ③ Prefab → ④ Art → ⑥ 双端 AB                                                                       |
| 材质球      | 已拆成独立 `.mat`（`Material/Mat_ggdddd_ID**.mat`）                                                           |
| 贴图       | **仍嵌在** `Model/glb.glb` 子资源                                                                            |
| 动画       | glb 内有 Take；Art `Animation/` 空；Animator.Controller 为空（另见 D14）                                          |
| 真机       | **整模型完全洋红**（不是局部块/部分材质）                                                                                |
| 交付 AB    | `Deliverables/ggdddd/03_assetbundles/Android/ggdddd.assetbundle` 20081400 字节；UnityFS / `2022.3.54f1c1` |
| manifest | Class 21 Material / 28 Texture / 43 Mesh / 48 Shader 都在                                                |


### 是不是主要是 Shader？

**是。APP 不便打开材质查看时，「完全洋红、不是部分」已经够用。**


| 观感            | 更像                                                                                   |
| ------------- | ------------------------------------------------------------------------------------ |
| **整片纯洋红**（本次） | 所有 Renderer 共用的 Shader 在 Player 里不可用（Missing / 管线不对 / 变体全被剥）。引擎 Error Shader 就是这一种洋红 |
| 部分洋红、部分正常     | 只有部分材质 Shader 不对，或个别贴图/子网格坏了                                                         |
| 灰白/黑、形状还在     | Shader 还在，主要是贴图没解出来                                                                  |


ggdddd 的 18 个 `.mat` **全部**挂同一套 `PBRGraph`，所以会整模型一起洋红，和「包是空的」「少了几张图」对不上。


|        | ggdddd（GLB，真机全洋红）                              | Zhi18（FBX，同工程 AB）          |
| ------ | ---------------------------------------------- | -------------------------- |
| Shader | UnityGLTF `**PBRGraph**`（ShaderGraph，Packages） | 内置 **Standard**            |
| 转换工程   | Built-in（无 URP Asset）                          | 同左                         |
| APP 目标 | 工单 **URP**                                     | Standard 在 Built-in APP 能亮 |


Built-in 工程打进 AB 的 ShaderGraph 变体，URP Player 加载即洋红。编辑器有 UnityGLTF，所以本机看起来正常。

**次因（解释不了「完全洋红」）：** 贴图仍挂 GLB 子资源。抽 png 治的是图，**不能**当洋红第一刀。不必为了定性再改 APP。

### 修复方案

| # | 做法 | 治洋红？ | 代价 |
|---|---|---|---|
| **A 已拍板** | Art `.mat` 换到 APP 已有 Shader（先 Standard 验；目标名可配） | 是 | ⑤ Material Op + ConfigData；属性对照表 |
| B | Converter 工程改成 **专用 URP** 再打 AB（待办 B.8） | 有帮助 | 工程级；仍建议不要把 `PBRGraph` 打进 APP |
| C | APP 带上 UnityGLTF / Always Included `PBRGraph` | 能亮 | APP 绑死 Packages；不推荐当交付契约 |
| D | 只抽独立 png | **否** | 可后做 |


UnityGLTF 自带的 `ShaderConverters` 主要是 **往 glTF 导**，不是交付用的「PBRGraph → URP Lit」。

验收不必进 APP 材质面板：Converter 侧先把 ggdddd 材质换成现网能亮的 Shader（与 Zhi18 同款或 APP 的 URP Lit）再打一份 AB。若不再整片洋红，定性成立；若变成灰模，再补贴图映射/抽图。

### 和④平铺的关系（架构）

换材质**很像**平铺的一部分（都是改 Art 里的 `.mat` + 保证 Prefab 引用正确），但**不要**再开「另一条平铺管线」。


| 做法 | 评价 |
|---|---|
| **并入⑤（修订推荐）** | 插件 2 Material/Shader 层；编排④后跑⑤；④⑤默认开，轻重靠子面板勾选 |
| ~~④ 可选后置步~~ | 旧推荐；与⑤体系重复，不再作主落点 |
| ⑥ 出包前再换 | 也能治 AB；Art 与交付易不一致 |
| **原地改 Art 已有 `.mat`** | **合理**：④已切断外引；Prefab 已指向 Art mat |
| 另存 mat 再改 Prefab 引用 | 多 GUID；一般不必 |
| 整条「Shader 平铺」新管线 | 过重 |


#### 逻辑入口：不要用「是否 GLB」


| 判据            | 评价                                                                                                             |
| ------------- | -------------------------------------------------------------------------------------------------------------- |
| `扩展名 == .glb` | **过窄且易漏**。问题本质是 **材质挂了 APP 没有的 Shader**，不是格式本身                                                                 |
| 其它格式会不会出现？    | **会**。任意导入器/插件材质（自定义 ShaderGraph、HDRP Lit、第三方包）只要进 Art 且 APP 无此 Shader，都会整片洋红。FBX 现网多是 Standard 所以「碰巧」没事       |
| **推荐入口**      | 平铺结束、Art mat 已落盘后：扫本包 `Material/*.mat`，若 `shader` **不属于交付白名单**（如 Standard / 配置的 URP Lit 名）→ 执行属性映射烘焙到目标 Shader |


伪流程（主路径在⑤，不在④拷贝循环里）：

```text
④ CreatePackagedAdjustedPrefab …（结构+引用收敛）
  → [可选碰撞体等仍可留④]
⑤ RunMasterBatch（贴图 → 材质Shader规范化 → 模型…）
  → ⑥ AB
```

开关：Pipeline 总步④⑤；Op 轻重 → L3「主面板批量包含」+ ConfigData。**不要**写进平铺分类后缀面板。

#### 「材质重映射」这个词？

仓内现有 **Remap** = 引用收敛（GUID/路径改到本包副本：贴图、Prefab、动画 PPtr）。  
本步是 **交付 Shader 规范化 / 材质烘焙**（换 Shader + 属性槽对照），**不是**同一类 Remap。文档/面板请分开叫，避免和 `RemapMaterialTexturesToArtFolder` 混谈。

#### 平铺会不会膨胀？要不要拆？

会有压力，但**现在不必拆第二条平铺管线**。先做**语义分层**（接口/类名分开即可，实现仍可暂住 Legacy）：

| 层 | 职责 | 例子 |
|---|---|---|
| ① 结构平铺 | 拷依赖、分类落 Art、切断外引 | Copy / Category / objectMap |
| ② 引用收敛 Remap | GUID/路径改到本包 | 贴图/材质/动画 PPtr Remap |
| ③ 交付规范化（可选） | 曾设想挂④末；**修订**：Shader 烤进⑤，碰撞体可仍留④ |

③ 继续堆「APP 契约后置」可以；不要把 ③ 的逻辑揉进 ① 的拷贝循环。

---

### 归插件 1 还是插件 2？（评估修订 2026-08-26）

> 上一版把「⑤ 扫不到 Art」当成主顾虑，**核对后纠正**：那是把 **导入期自动** 与 **⑤ 总批量** 混在一起了。

#### 事实核对

| 点 | 实际代码 |
|---|---|
| L1 批量路径 | `ResourceBatchFolderStore` 默认种子 **`Assets/Art`**；总面板天生对着交付区 |
| ⑤ `RunMasterBatch` | 按批量路径 `FindAssets` 收集；**不读** `excludedPathPrefixes` |
| `excludedPathPrefixes`（默认 `Assets/Art/`） | **只拦导入期自动流**（Importer / delayCall）。L1「执行全部」与中间层⑤ **不读** 该列表 |
| 中间层⑤ | `PipelineRunner` 代调 `ToolPostProcessApi.RunMasterBatch` = 同一手动内核，不是 `AssetPostprocessor` |
| 编排时序 | Runner：④ 成功 → 写 Art 单元到 `PostProcessFolderPaths`（D17）→ ⑤ → ⑥ |
| 插件 2 是否已有业务配置 | **有**。`TOol/ConfigData` 下 Texture/Model/Batch SO；压图参数等已是业务刚需 |
| E.L1 | L2 范围/勾选 vs L3 SO 操作细则——「意向轻重」与「规则参数」可拆的钩子（低优先，方向对） |

因此：**「并入⑤、由编排保证④后执行」在架构上成立**；先前「必须留插件 1 才碰得了 Art」不成立。

#### ④⑤ 默认开启（产品意向）

| 现状 | 目标 |
|---|---|
| `PipelineStepSettings`：`runFlatten` / `runPostProcess` **默认 false**；战略写「④⑤ 可选默认关」 | Converter 线：**④⑤ 默认开**；轻重在子面板 / L3「主面板批量包含」勾选 |

合理：自动线要交付可用 AB，平铺 + Art 后处理应是默认路径；「不做压图 / 不烤 Shader」用子流程关 Op，而不是整步关掉⑤。

实现时需同步改：`PipelineStepSettings` 默认值、总面板文案、strategy「可选默认关」、历史「Converter 默认④⑤ 可退化」→ 改为 **默认开、可关**。

#### 并入⑤后仍要补的缝（不是归属问题）

| 缝 | 说明 |
|---|---|
| **收集器没有 Material 层** | 现⑤只有贴图（`t:Texture2D`）+ 模型。Shader 规范化扫 `.mat` → 需 **新层**（Material/Shader Op + Collector），挂进 `RunMasterBatch` |
| **L1 路径与单任务 Art 夹** | ②后 `SyncFolderToL1` 写的是**导入夹**；开④后 Runner **已**把本次 Art 单元写入 `PostProcessFolderPaths`（D17） |
| **内嵌贴图** | GLB 贴图仍在容器内时，贴图 Op 仍可能 0 命中；与 Shader 烤 **正交** |
| **目标 Shader 名** | 仍属 APP 契约；放 `ConfigData` 的 MaterialProcessSettings（或等价）合理；L3=规则，主批量勾选=这轮要不要跑 |

### 已拍板（2026-08-26）

| 项 | 决定 |
|---|---|
| 归属 | **⑤ 资源处理**（插件 2）；入口 = 资源总面板 / `RunMasterBatch` |
| 是否执行 | **SO**（`ConfigData`：目标 Shader、白名单等）+ L3「主面板批量包含」勾选的 Op Id |
| 修复方案 | **A**：Art 内不合规 `.mat` **换到 APP 已有 Shader**（先 Standard 验；目标名可配） |
| ④ 职责 | 结构平铺 + 引用收敛；**不做** Shader 烤 |
| 导入后处理自动 | 对未平铺资源 = **不保证成功的附属功能**，默认关；交付靠④后⑤总批量 |

### 总面板如何「认识」有哪些资源操作？（扩展方式）

**两层，不要混：**

| 层 | 是否反射 | 现状 |
|---|---|---|
| **资源大类**（贴图 / 材质 / 模型） | **否** | 总面板 **写死**三块 + Prefs `MasterBatchIncludeTexture/Material/Model`。交付顺序：总批量「贴图 → 材质 → 模型」 |
| 大类内的具体 Op | **是** | `TextureOperationRegistry` / `ModelOperationRegistry` / `MaterialOperationRegistry` 扫实现了 `I*AssetOperation` 且无参构造的类型；L3 用 Id 勾选进 `masterBatchOperationIds` / `importAutoOperationIds` |

加一个**贴图/模型已有大类下的新 Op**（如再压一种图）：

```text
新建 class XxxOperation : ITextureAssetOperation（无参 ctor）
  → 反射自动进 Registry.All
  → L3「操作集合」勾进 masterBatchOperationIds
  → 总批量下次就会跑（无需改总面板按钮）
```

加一个**全新资源大类**（本步：材质 / Shader 规范化）：

```text
1. IMaterialAssetOperation + MaterialOperationRegistry（反射）
2. MaterialTargetCollector（扫 .mat）+ Runner
3. ConfigData/MaterialProcessSettings.asset（目标 Shader、白名单、masterBatchOperationIds）
4. 总面板：新 DrawResourceBlock("材质") + MasterBatchIncludeMaterial Prefs
5. ResourcePostProcessService.RunMasterBatch 接入该层（建议顺序：贴图 → 材质 → 模型）
6. Pipeline ⑤ 仍调同一 RunMasterBatch 口
```

**反射只扩展「某大类里有哪些 Op」；「总面板有几个大类按钮」要显式接线。** 这是当前架构，不是疏漏。

### 仍未拍板 / 可控风险

| 项 | 状态 |
|---|---|
| APP 目标 Shader 最终名 | **先 Standard**（对齐 FBX 能亮）。URP APP 若仍洋红 → 再改 `Universal Render Pipeline/Lit` |
| ④⑤ Pipeline 默认开 | **已落地**：`PipelineStepSettings` `.cs` 默认 true；`.asset` 已为 1；strategy / tech-and-ops 已改 |
| ④后 L1/`PostProcessFolderPaths` 是否改成本次 Art 单元 | **未实现**；约定直指 Art（大根）；两插件对齐说明见 tech-and-ops |
| 属性槽对照表完整度 | **第一刀**：baseColor→`_MainTex`/`_Color` + metallic/gloss(+normal/occlusion/emission 有则搬)；完整表后补 |
| GLB 内嵌贴图⑤压图 0 命中 | **已知、正交**；不挡 Shader 烤定性 |
| ShaderGraph→Standard 在纯 URP APP 仍洋红 | **遇到再说**（改目标 Shader 名即可） |

### 第一刀进度（工程结构）

| 项 | 状态 |
|---|---|
| Material 层（Op/Registry/Collector/Runner/SO） | **已做** |
| `RunMasterBatch` 贴图→材质→模型 + L1 纳入开关 | **已做** |
| `NormalizeDeliverableShaderOperation`（PBRGraph→Standard） | **已做** |
| Pipeline ④⑤ 默认开 | **已做** |
| tech-and-ops ④→⑤ Art 路径对齐说明 | **已做** |
| ggdddd Art `.mat` 实跑 + 重打 AB + APP 验 | **待本机**：工程正被 Editor 占用，batchmode 无法并行；请在已开工程跑菜单后打 AB |
| ShaderGraph→Standard 在 URP APP 上仍洋红 | **若发生**：说明 APP 吃不下 Standard → 换 URP Lit，不是方案 A 失败 |

### 第一刀

菜单或临时 Op：只扫 `Art/ggdddd/Material`，不合规 → Standard 原地写回 → 打 AB。通过后按上表接进⑤ Material 层。

---


### 收口补充（2026-08-27）

| 项 | 状态 |
|---|---|
| Material L2 精准面板 | **已做**（范围/勾选/结果 = EditorPrefs） |
| Material L3 高级设置 | **已做**；L1 可直接打开 |
| ggdddd APP | 安卓已能亮 |
| 独立 Tools/资源处理 菜单 | 已撤；走总面板 / L2 |

