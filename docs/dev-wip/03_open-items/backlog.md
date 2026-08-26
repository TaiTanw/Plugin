# 待处理事项 · 模糊项 · 风险

返回 [总目录](../README.md)

进行中的项留在上面各节。确认结束后请改到 [01_requirements/strategy.md](../01_requirements/strategy.md)，并把该行移到文末 **[历史结束事务](#历史结束事务)**（文档定位可留）。

---

## A. 待开发（已拍板方向）


| ID  | 事项                                   | 优先级 | 状态                                                                                   |
| --- | ------------------------------------ | --- | ------------------------------------------------------------------------------------ |
| D5  | `-executeMethod` 参数与错误码表固化           | P1  | 概念/第一刀见 [cli-getting-started](../04_implementation/cli-getting-started.md)；**下一步开工** |
| D13 | GLB 通道 AB 安卓洋红（材质/Shader） | P0 | **已拍板**：⑤ Material 层 + 方案 A；见 **H** |
| D12 | ⑤ 模型扩展名默认仅 `.fbx`；管线已含 GLB 时 Op 可能空跑 | P1  | 评估见 **G**                                                                            |
| D9  | materialId UX：选源时填入默认名；清除源时一并清 Id    | P1  | 评估见 **F**                                                                            |
| D10 | materialId → 列表（多文件/多夹）；同夹多模型加文件名后缀  | P2  | 评估见 **F**                                                                            |
| D11 | 成功后可选清理 Incoming（缓存）；**默认不删 Art**    | P2  | 与 Quiet 无关；见 **F**                                                                   |
| D14 | 模型自带动画/音效 vs Pack 入口                 | P2  | 评估见 **I**；**暂不新开管线**                                                                 |


---

## B. 仍模糊 / 未拍板

### 工程

1. `productName` 是否改为 Plugin2022
2. 宿主是否将来 submodule
3. ModleEvent 与 Plugin2022 两份 Plugin 长期如何对齐

### 产品 / 流程

1. 素材库 V1.2 是否要跑全套 Art ④⑤门禁，还是永远只要双端 AB
2. 嵌套 GLB Prefab：禁止 vs 警告
3. Prefab 落盘用「纯三层名」还是「三层名/stem」子夹（实现已用扁平行 `{名}.prefab`）
4. 门禁 Profile / SO 何时接线
5. Converter 工程用 **专用 URP** 还是双管线（与 D13 相关）

### CLI / 运维

1. 参数格式（自定义 flag / 环境变量 / 临时 SO）
2. License：Personal vs Pro；多机互踢策略
3. Unity 精确版本号（现网打 AB 头为 `2022.3.54f1c1`）
4. iOS on Linux 失败时的拆分构建方案（基建）

---

## C. 已知风险（实现时注意）


| 风险                  | 说明                                            |
| ------------------- | --------------------------------------------- |
| ⑥ 输入源               | 未平铺时应用**任意 Prefab**（近直通），勿写死必须 Art            |
| ④ 后打 AB             | 开④时 Runner **已**改用平铺返回的 Art Prefab            |
| Legacy 大函数拆 options | 易影响现有「导出全部/选中」菜单回归                            |
| ⑤ + GLB 内嵌          | 压图空跑 ≠ 合规；面板需提示                               |
| 总面板命名               | 勿与「资源处理总面板」混淆                                 |
| URP 落差 / GLB 洋红     | 见 **H**（D13）。主因 Shader，不是空 AB                 |
| Art 通道混淆            | **导入自动跳过 Art** ≠ **⑤ 不碰 Art**。后者默认打 Art。见 tech-and-ops「Art 目录边界」、规则 33 |
| 贴图抽出                | 已延后；勿当洋红 blocker。ggdddd 贴图仍嵌在 `Model/glb.glb` |


---

## E. 低优先级 / 与当前总目标弱相关


| ID  | 事项                                      | 说明                               |
| --- | --------------------------------------- | -------------------------------- |
| L1  | L2 子面板数据：继续 Prefs，或抽「数据资源来源」SO          | 操作参数仍来自 L3；L2 只是临时范围+勾选。**非本迭代** |
| L2  | Shared 根下扁平脚本迁入 Switches/BatchPath/… 子夹 | 纯目录卫生，保留 .meta                   |


---

## F. 需求评估：materialId 默认名 / 列表 / Quiet / 缓存清理

> 2026-08-26 评估；**建议位置**见上表 D9–D11。不立刻改代码，除非先做 D9 小 UX。

### 1. 「清除」后 materialId 还在——现状

面板「清除」**只清 `sourcePath`**，不清 `materialId` 文本。  
所以不是传参缓存错乱，是 **UI 未同步**。期望：选源/导入时写入默认名；清源时清 Id（或灰显只读展示默认名）。

### 2. 导入时写入默认名 + 能否少一次空判断？


| 点   | 结论                                                                                                   |
| --- | ---------------------------------------------------------------------------------------------------- |
| 合理性 | **高**：默认名=导入夹名（三层规则）；用户可改成业务 Id                                                                      |
| 空判断 | 面板保证「始终有字符串」后，**UI 路径**可少分支；`ResolvePrefabBaseName` 里 `IsNullOrWhiteSpace` **建议保留**（CLI/API/旧调用仍可能空） |
| 省逻辑 | 省的是产品语义分叉（空=算名 / 非空=覆盖），不是省一行 `if`；内核保留防御判断更稳                                                        |


### 3. 未来 materialId 列表 + 同夹多 FBX/GLB 加文件名后缀


| 点    | 结论                                               |
| ---- | ------------------------------------------------ |
| 合理性  | **高**，与批量入库消歧一致                                  |
| 建议位置 | **P2 / D10**                                     |
| 渠道   | 网页选中 → **单夹/单任务**；人工面板 → **可选多文件**——合理，勿过早揉进网页契约 |


### 4. Quiet ≠ 退出编辑器


| Quiet 现在               | 不是                   |
| ---------------------- | -------------------- |
| 禁止 `DisplayDialog` 确认框 | 不会 `-quit`、不会关 Unity |


无头 Converter 才是进程退出；与面板 Quiet 勾选是两层事（D5）。

### 5. 退出时删 Incoming / Art「当缓存」？


| 目录                 | 建议                                    |
| ------------------ | ------------------------------------- |
| `Assets/Incoming*` | 可作为**任务缓存**：成功出 AB 后**可选**清理；失败保留便于排错 |
| `Assets/Art/**`    | **不要**默认当缓存删——交付/人工归档区；误删成本高          |
| `IncomingPrefab`   | 中间产物，可与 Incoming 一并可选清                |


与 Quiet **解耦**：清理应是独立选项（如 `cleanupScratchOnSuccess`），不要绑在 Quiet 上。

### 建议实施顺序（在总排期中的位置）

```text
进行中 P0     D13：Material 层已接线；ggdddd 烤 Standard + APP 验洋红
紧接着 P1     D5 CLI；④后同步 Art 单元路径给⑤（加固）
紧接着可插    D9 materialId 默认写入/清除同步
P1 并行       D12 ⑤ 扩展名
P2            D10 列表；D11 清 Incoming；D14 Pack/音效入口
不要做        Quiet=退出；退出默认删 Art；为 Pack 另开一套 ②③⑥
```

---

## G. ⑤ 资源处理：后缀 / 未知夹 / 子面板风险

### 行为（现状）

⑤ 总批量 = 在 L1 **批量路径下递归扫文件** → 用 **扩展名** 过滤：


| 侧   | 认什么后缀                                                       |
| --- | ----------------------------------------------------------- |
| 贴图  | `TextureCodecRegistry`（实现了 Codec 的扩展名，如 png/jpg/tga…）       |
| 模型  | `ModelProcessSettings.supportedExtensions`（**默认只有 `.fbx`**） |


- **未知文件夹名**：一般**不用担心**——只要夹在扫描根之下且文件后缀命中，就会收到。  
- **未知/未注册后缀**：会**静默跳过**（不是报「找不到夹」）。  
- 平铺 `Unknown/`：那是④分类后缀表的事，与⑤ Codec/扩展名列表是两套。

### 风险（建议代办 D12）


| 风险              | 说明                                                           |
| --------------- | ------------------------------------------------------------ |
| GLB/管线 vs 模型 Op | 自动化已导 GLB，但模型 L3 默认扩展名无 `.glb` → ⑤「刷顶点白」等对 GLB **可能 0 命中**   |
| 子面板无「后缀编辑」      | L2/L3 不提供像平铺分类那样的后缀 UI；贴图靠 Codec 代码、模型靠 SO 列表——**易漏配**       |
| Art 排除前缀 | `excludedPathPrefixes` 默认含 `Assets/Art/`——**只拦导入自动**，不拦⑤总批量。L1 默认路径本就是 Art（见 H 归属修订） |


**建议位置：** P1 **D12**——核对并扩展 `ModelProcessSettings.supportedExtensions`（至少 `.glb`/`.gltf` 若⑤要对它们生效）；文档写清「⑤ 按后缀收，不按文件夹名」。平铺分类面板后缀编辑保留；**不要**与⑤混成同一套 UI（除非以后做统一 Profile）。

---

## H. GLB 样例 + 安卓洋红（D13）

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
| `excludedPathPrefixes`（默认 `Assets/Art/`） | **只拦** 设置自动 / 后处理自动（Importer / delayCall），避免导入区钩子改交付产物 |
| 编排时序 | Runner：④ 成功 → ⑤ → ⑥；且 `RunPostProcess = runFlatten && runPostProcess`（无④则⑤锁关） |
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
| **L1 路径与单任务 Art 夹** | ②后 `SyncFolderToL1` 写的是**导入夹**；开④后⑤应对 **本次 Art 单元**（或保持 `Assets/Art` 大根）。编排在④成功后应写入 `PostProcessFolderPaths` / L1 |
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

## I. 动画 / 音效 / 要不要认 Pack（D14）

> 结论：**不必为 Pack 新开 ②③⑥ 管线。**

### 模型文件自己能带什么


| 载体           | 动画                                                                               | 音效                     |
| ------------ | -------------------------------------------------------------------------------- | ---------------------- |
| **GLB/glTF** | **能**。标准 glTF 动画轨；UnityGLTF 作成 `.glb` **子资源**（ggdddd 已有 Take）。不是独立 `.anim`，除非再抽取 | **基本不能**               |
| **FBX**      | **能**。内嵌 Clip；现网平铺可抽到 `Art/.../Animation/` 并绑 Controller                         | **基本不能**               |
| 独立文件         | `.anim` / `.controller`                                                          | `.wav` `.mp3` `.ogg` 等 |


平铺分类**已经**有 `Animation/`、`Audio/`：前提是 Prefab **引用到的**依赖。

- 动画：优先模型内嵌 → 需要时再抽 Clip（GLB 线 ggdddd **尚未**抽到 `Animation/`，Controller 为空）。  
- 音效：几乎总是旁路文件，靠 Prefab 引用带进包。  
- 「Pack」只在源是文件夹/压缩包且尚未挂到一个 Prefab 时，才需要多文件入库识别；识别后仍走同一套 ③④⑥。


| 事                                  | 放哪              |
| ---------------------------------- | --------------- |
| GLB 内嵌动画 → 独立 Clip + 非空 Controller | 现有④增强；样例 ggdddd |
| 旁路音效                               | 现有 `Audio/`     |
| 认 zip/文件夹 Pack                     | **P2 / D14**    |
| 新开一条「Pack 管线」                      | **现在不要**        |


---

## 历史结束事务

> 已做 / 退化 / 明确不做。文档链接保留备查。不要从这里再拉回「待开发」，除非需求翻案。

### 已做


| ID     | 事项                                | 收口说明                                                                                   |
| ------ | --------------------------------- | -------------------------------------------------------------------------------------- |
| 接口+中间层 | 1/2 窄口 + `Plugin/Pipeline` Runner | 编排内核                                                                                   |
| D3     | 自动化管线总面板                          | `Tools > 自动化管线总面板`                                                                     |
| D2     | Runner：单文件导入、写 L1 路径、字符串结果；⑤默认关   | StepResult 延后                                                                          |
| D1     | ⑥ 契约核对（命名/压缩/main）                | 契约 1/2 **退化已锁**；文档 [d1-ab-only](../04_implementation/d1-ab-only.md)                    |
| D4     | ② 路径入库进管线；收集认 `.glb`              | 真 `-batchmode` 留给 D5                                                                   |
| D8     | 直通与 BuildAbOnly 合并 Options        | `RetinarExportSettings` + `RetinarAbApi.Build`                                         |
| D6     | UnityGLTF 去本机 `file:`，改 git 依赖    | [d6-unitygltf-docker](../04_implementation/d6-unitygltf-docker.md)；本机拉包若未做，属环境验证不是开放功能 |
| L3     | Shared 对外 Facade                  | **已建** `Shared/Api/`                                                                   |
| （无 ID） | 平铺分类面板去掉「添加根 BoxCollider」         | `AddBoxCollider` 默认 false；旧 Prefs 可能仍为 true                                            |


### 退化（现网为准，本阶段不改）


| ID        | 事项                                         | 说明                                      |
| --------- | ------------------------------------------ | --------------------------------------- |
| D7        | 与 APP 书面确认 `main` 名、LZ4、`.assetbundle` 文件名 | **可退化**（现网取包）；LZ4 已采用                   |
| D1 契约 1/2 | AB 文件名 / 包内 main                           | 保持 `name.assetbundle` + 平台夹；不强制改 `main` |


自动线**默认不做**（代码暂留，不当开放事务）：

- 导出确认/完成弹窗  
- Converter 默认④⑤  
- 全套 00–06 Deliverables  
- SafeZone 硬阻断（自动线可关）

### 取消 / 明确不做


| 事项                 | 说明        |
| ------------------ | --------- |
| 整包平铺迁插件 2          | 本阶段明确不做   |
| Quiet = 退出编辑器      | 禁止        |
| 退出默认删 `Assets/Art` | 禁止        |
| 为 Pack 另开一套 ②③⑥    | 禁止（见 D14） |


