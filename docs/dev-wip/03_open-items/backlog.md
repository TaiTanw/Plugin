# 待处理事项 · 模糊项 · 风险

返回 [总目录](../README.md)

进行中的项留在上面各节。确认结束后请改到 [01_requirements/strategy.md](../01_requirements/strategy.md)，并把该行移到文末 **[历史结束事务](#历史结束事务)**（文档定位可留）。

---

## A. 待开发（已拍板方向）


| ID  | 事项                                   | 优先级 | 状态                                                                                   |
| --- | ------------------------------------ | --- | ------------------------------------------------------------------------------------ |
| D5  | CLI：`PipelineCli` + `-executeMethod`；最小参数与退出码表 | P1  | **第一刀已写** `PipelineCli.Run`（`-source` / 可选 `-materialId`）→ [cli-getting-started](../04_implementation/cli-getting-started.md)；无头跑通后固化表 |
| D16 | ⑤ 结构化结果：窄口/Runner 映射 `PostProcessFailed(50)` | P1  | CLI/面板共用；现状 `RunMasterBatch` 仅 string、Runner 不 Fail → 见 [pipeline-flow](../04_implementation/pipeline-flow.md) §4 |
| D17 | ④成功后同步本次 Art 单元到⑤（`PostProcessFolderPaths`） | — | **已完成**：Runner ④后写 Art 单元根；见 [pipeline-flow](../04_implementation/pipeline-flow.md) |
| D13 | GLB 通道 AB 安卓洋红（材质/Shader）            | —   | **已完成** → [d13-glb-magenta](./d13-glb-magenta.md)                                    |
| D12 | ⑤ 模型扩展名 / L3 识别只读展示 | — | **已完成**：默认 .fbx/.glb/.gltf；L3 只读识别说明；见 **G** |
| D9  | materialId UX：选源时填入默认名；清除源时一并清 Id    | —   | **已完成**：Pipeline 选源/清除同步；见 **F**                                                     |
| D10 | materialId → 列表（多文件/多夹）；同夹多模型加文件名后缀  | P2  | 评估见 **F**；`SourceBindings` 已预备、**Runner 未消费**（CLI 多源依赖此项） |
| D11 | 成功后可选清理 Incoming（缓存）；**默认不删 Art**    | P2  | 与 Quiet 无关；见 **F**                                                                   |
| D14 | 模型自带动画/音效 vs Pack 入口                 | P2  | 评估见 **I**；**暂不新开管线**                                                                 |
| D15 | ⑤ 扫 Art 范围：单单元路径 vs 大根；Text 标记「已处理」  | P3  | **低优**；见 **J**；暂不实现                                                                  |
| D18 | 单文件②同目标路径「复用」：源已变仍不覆盖（静默用旧文件） | P2  | **隐患成立**；见 **K**；策略待选：覆盖 / Conflict 失败 / 内容哈希 / 版本后缀 |
| D19 | FBX 刷白后被重导冲掉（⑤贴图批 / ⑥ AB） | P1  | 补偿走**中间层代跑 L1 总批量**（preserve + ⑥后重刷白再打 AB）；**不**在导入钩子里打 Art。见 **L** |
| D20 | Art `Prefab/` 夹「看起来」未刷顶点色 | P3  | **可选**。色在 `Model/*.FBX` 子资产；编辑器预览常已白。不挡 CLI。见 **M** |


---



## K. 单文件导入同路径复用（D18）

> 2026-08-27 核对：`ToolImportApi.ImportSingleModel` 对工程外源按 `Import根/三层夹名/原文件名` 落盘；**目标已存在则复用、不 `File.Copy` 覆盖**。

| 问 | 结论 |
|---|---|
| 算不算判重失败？ | **否**——有意幂等，同路径再导一次当命中 |
| 算不算内容缓存加速？ | **否**——只按路径复用，**不比哈希/mtime** |
| 隐患 | 同三层名 + 同文件名、源已替换 → **静默旧资产**；批量 FBX 面板更严（夹已存在 Skip/Conflict） |
| 产品方向（待拍板） | CLI/服务器：覆盖更新 / BadArgs·Conflict / 后缀消歧；与 D11 清 Incoming 可配合 |

**现状可接受场景：** 同一任务反复跑同一源路径。  
**不可接受场景：** 同名新版本必须进工程。

---



## L. FBX 顶点刷白「⑤后 OK、⑥后/扫描又非白」（D19）

> 歼31：`Assets/Art/歼31-yy3d_3d/Model/fbx.FBX`  
> ⑤末 `[顶点色诊断] OK 非白=0`；⑥ AB 后 L1 扫描又报 `33/38 非全白`。

| 项 | 说明 |
|---|---|
| **不是** | 通道 1 exclude 挡了⑤；也不是「刷白没写上」（⑤后诊断已证明全白） |
| **是** | 色写在 ModelImporter 导入结果上；**⑥ `BuildAssetBundles` 会依赖重导**，从 FBX 二进制重建 Mesh → 白丢（⑤内贴图批标脏也可同类冲掉） |
| 时序 | ⑤（通道 3 代跑 L1）贴图→材质→模型→诊断 OK → ⑥ AB → 工程 Mesh 又黄 → 扫描 BAD；若未重打 AB，**交付 AB 也可能已是黄** |
| 一刀（正确层） | ⑤模型批前 preserve + colors/colors32；⑥后若仍 BAD → **再调同一 `RunMasterBatch` 只跑模型** → 再打 AB。全部是中间层代跑手动内核 |
| **不要** | 在 `OnPostprocessModel` 对 Art 刷白来「抗⑥重导」——那是把通道 3 做成通道 1，违反规则 33。导入期自动流继续整段跳过 Art |
| 验收 | ⑤后诊断 OK；⑥后应再出现诊断；若曾冲掉应有「重刷白并重打 AB」且第二次诊断 OK；再扫 L1 应 Skip |
| 非管线 | UnityGLTF 只读当前 `mesh.colors`；Export 在⑤前 = 验旧状态。导出黄 ≠ 这份 AB 黄 |

GLB 刷白仍暂放。通道定义见 [tech-and-ops](../01_requirements/tech-and-ops.md)「Art 目录边界」。

---



## M. Art Prefab 夹未刷顶点色（D20 · 可选）

> 2026-08-27：`Assets/Art/歼31-yy3d_3d/Prefab` 跑完管线后仍「未刷顶点色」；编辑器观感无明显问题。

| 项 | 说明 |
|---|---|
| 色写在哪 | Mesh 顶点色在 **`Art/<名>/Model/*.FBX`** 子资产上，**不在** `.prefab` 文件里 |
| 为何 Prefab 夹像没刷 | 对 Prefab 夹做「模型扫描」若只看 `.prefab`、未跟依赖到 FBX，会空或误判；Project 里点 Prefab 也看不到 Mesh.colors |
| 编辑器为何像正常 | Prefab 实例引用同一份 FBX Mesh；诊断/Scene 看的是 Model 上已白的 Mesh |
| 顽固点 | ⑥ 重导、UnityGLTF 另存 glb、看错夹，都可能再显得「Prefab 没白」 |
| 本阶段 | **不挡 CLI**。要对齐观感：扫 `Model/` 或 Prefab 的 GetDependencies；不要对 Prefab 夹单独写顶点色 |

---



## J. ⑤ Art 扫描范围 / Text 标记（D15 · 低优基本评估）

> 与「④后写本次 Art 单元给⑤」同一问题域；大根约定当前够用。详见拍板记录；**暂不实现**。


| 问                   | 结论                   |
| ------------------- | -------------------- |
| 当前⑤是否整棵 Assets/Art？ | **多数是**（L1 种子/习惯路径）  |
| Text 已处理标记？         | **暂不需要**；优先路径收窄（D15） |


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

1. **B.CLI** 参数格式（自定义 flag / 环境变量 / 临时 SO）— D5 第一刀先死 `-source`；全表拍板后再扩
2. License：Personal vs Pro；多机互踢策略（错误码 70 预留，见 D5）
3. Unity 精确版本号（现网打 AB 头为 `2022.3.54f1c1`）
4. iOS on Linux 失败时的拆分构建方案（基建）
5. ⑥ **部分** AB 失败时退出码：仍 0（当前 `PartialOk`）还是非 0 — 随 D5 错误码表拍板

---



## C. 已知风险（实现时注意）


| 风险                  | 说明                                                                     |
| ------------------- | ---------------------------------------------------------------------- |
| ⑥ 输入源               | 未平铺时应用**任意 Prefab**（近直通），勿写死必须 Art                                     |
| ④ 后打 AB             | 开④时 Runner **已**改用平铺返回的 Art Prefab                                     |
| Legacy 大函数拆 options | 易影响现有「导出全部/选中」菜单回归                                                     |
| ⑤ + GLB 内嵌          | 压图空跑 ≠ 合规；面板需提示                                                        |
| 总面板命名               | 勿与「资源处理总面板」混淆                                                          |
| URP 落差 / GLB 洋红     | 见 [d13-glb-magenta](./d13-glb-magenta.md)。主因 Shader，不是空 AB             |
| Art 通道混淆            | **导入期自动流不碰 Art** ≠ **中间层⑤/L1 不碰 Art**。⑤是代跑面板手动总批量。见 tech-and-ops「三条通道」、规则 33 |
| 贴图抽出                | 已延后；勿当洋红 blocker。ggdddd 贴图仍嵌在 `Model/glb.glb`                          |
| 单文件②路径复用（D18）   | 目标已存在不覆盖 → 同名新源可能静默旧文件；见 **K**                                      |


---



## E. 低优先级 / 与当前总目标弱相关


| ID  | 事项                                      | 说明                               |
| --- | --------------------------------------- | -------------------------------- |
| L1  | L2 子面板数据：继续 Prefs，或抽「数据资源来源」SO          | 操作参数仍来自 L3；L2 只是临时范围+勾选。**非本迭代** |
| L2  | Shared 根下扁平脚本迁入 Switches/BatchPath/… 子夹 | 纯目录卫生，保留 .meta                   |


---



## F. 需求评估：materialId 默认名 / 列表 / Quiet / 缓存清理

> 2026-08-26 评估；**D9 已落地**；D10 接口预备、多选 UI/Runner 暂缓。



### 1. 「清除」后 materialId 还在——现状 → **D9 已修**

选源（拖入/浏览/改路径）写入默认 materialId（三层夹名）；「清除」同时清源与 Id。  
实现：`PipelineWindow.SetSourcePath` + `PipelineMaterialId.SuggestDefault`。手改 Id 后若再换源，会按新源重写默认名。

### 2. 导入时写入默认名 + 能否少一次空判断？


| 点   | 结论                                                                                                   |
| --- | ---------------------------------------------------------------------------------------------------- |
| 合理性 | **高**：默认名=导入夹名（三层规则）；用户可改成业务 Id                                                                      |
| 空判断 | 面板保证「始终有字符串」后，**UI 路径**可少分支；`ResolvePrefabBaseName` 里 `IsNullOrWhiteSpace` **建议保留**（CLI/API/旧调用仍可能空） |
| 省逻辑 | 省的是产品语义分叉（空=算名 / 非空=覆盖），不是省一行 `if`；内核保留防御判断更稳                                                        |




### 3. 未来 materialId 列表 + 同夹多 FBX/GLB 加文件名后缀


| 点      | 结论                                                                                                  |
| ------ | --------------------------------------------------------------------------------------------------- |
| 合理性    | **高**，与批量入库消歧一致                                                                                     |
| 建议位置   | **P2 / D10**                                                                                        |
| 渠道     | 网页选中 → **单夹/单任务**；人工面板 → **可选多文件**——合理，勿过早揉进网页契约                                                    |
| 预备（本步） | PipelineMaterialId.BuildSourceBindings + PipelineOptions.SourceBindings 已就位；**多选 UI / Runner 消费暂缓** |




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
已完成        D13：见 [d13-glb-magenta](./d13-glb-magenta.md)
文档已整理    对外接口分块 A/B → pipeline-flow + cli-getting-started（无代码）
紧接着 P1     D5 CLI 写 PipelineCli；并行/随后 D16（⑤→50）、D17（④→⑤路径）；**D19 歼31 再验刷白**
已完成        D9 materialId 选源/清除同步
已完成        D12 模型扩展名 + L3 识别只读
P2            D10 列表（接口已预备）；D11 清 Incoming；D14 Pack/音效入口；**D18 单文件同路径复用策略**
不要做        Quiet=退出；退出默认删 Art；为 Pack 另开一套 ②③⑥
```

---



## G. ⑤ 资源处理：后缀 / 未知夹 / 子面板风险



### 行为（现状）

⑤ 总批量 = 在 L1 **批量路径下递归扫文件** → 用 **扩展名** 过滤：


| 侧   | 认什么后缀                                                       |
| --- | ----------------------------------------------------------- |
| 贴图  | `TextureCodecRegistry`（Codec 扩展名）；L3 只读展示 |
| 模型  | `supportedExtensions` 默认 `.fbx` / `.glb` / `.gltf`；L3 只读，改 SO |
| 材质  | Unity `t:Material`（.mat）；Op 再按 Shader 名过滤；无需后缀表 |


- **未知文件夹名**：一般**不用担心**——只要夹在扫描根之下且文件后缀命中，就会收到。  
- **未知/未注册后缀**：会**静默跳过**（不是报「找不到夹」）。  
- 平铺 `Unknown/`：那是④分类后缀表的事，与⑤ Codec/扩展名列表是两套。



### 风险 / D12 收口


| 风险              | 说明                                                                                                    |
| --------------- | ----------------------------------------------------------------------------------------------------- |
| GLB/管线 vs 模型 Op | **已缓**：默认与 Ensure 含 `.glb`/`.gltf`；旧 SO 打开 L3/加载会追加 |
| 子面板无「后缀编辑」      | **只读展示已做**（L3 `ResourceRecognitionGui`）；改后缀仍在 SO/Codec |
| Art 排除前缀        | excludedPathPrefixes 默认含 Assets/Art/——**只拦导入自动**，不拦⑤总批量。L1 默认路径本就是 Art（见 [d13](./d13-glb-magenta.md)） |


**D12 已落地：** 模型扩展名对齐管线；三侧 L3 只读「资源识别」；不做专用后缀编辑器、不与④平铺后缀表合并。

---



## H. GLB 洋红（D13）— 已归档

> **D13 已归类完成。** 调研 / 拍板 / 第一刀细节见：
> **[d13-glb-magenta.md](./d13-glb-magenta.md)**


| 项   | 状态                                     |
| --- | -------------------------------------- |
| 根因  | Art .mat 挂 UnityGLTF PBRGraph → APP 洋红 |
| 修复  | ⑤ Material：烤到可配目标 Shader（默认 Standard）  |
| 工程  | Op / L1 总批量 / L2 精准 / L3 高级 / ④⑤默认开    |
| 验收  | ggdddd 安卓已能亮                           |
| 残余  | 完整槽表、URP Lit 若需、D15 → 低优               |


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
| D9     | materialId 选源默认名 / 清除同步           | `PipelineMaterialId` + 面板；D10 绑定列表仅预备                                                  |
| D12    | ⑤ 模型扩展名 + L3 识别只读展示             | `ModelProcessSettings` + `ResourceRecognitionGui` |
| D13    | GLB 洋红 / 交付 Shader 规范化            | [d13-glb-magenta](./d13-glb-magenta.md)；Material L1/L2/L3；ggdddd APP 验通                |
| （无 ID） | 平铺分类面板去掉「添加根 BoxCollider」         | `AddBoxCollider` 默认 false；旧 Prefs 可能仍为 true                                            |




### 退化（现网为准，本阶段不改）


| ID        | 事项                                         | 说明                                      |
| --------- | ------------------------------------------ | --------------------------------------- |
| D7        | 与 APP 书面确认 `main` 名、LZ4、`.assetbundle` 文件名 | **可退化**（现网取包）；LZ4 已采用                   |
| D1 契约 1/2 | AB 文件名 / 包内 main                           | 保持 `name.assetbundle` + 平台夹；不强制改 `main` |


自动线**默认不做**（代码暂留，不当开放事务）：

- 导出确认/完成弹窗  
- 全套 00–06 Deliverables（日常走成品直达 / 管线⑥）  
- SafeZone 硬阻断（自动线可关）

> 注：Converter **④⑤ 默认开**（可关），已不在「默认不做」列。



### 取消 / 明确不做


| 事项                 | 说明        |
| ------------------ | --------- |
| 整包平铺迁插件 2          | 本阶段明确不做   |
| Quiet = 退出编辑器      | 禁止        |
| 退出默认删 `Assets/Art` | 禁止        |
| 为 Pack 另开一套 ②③⑥    | 禁止（见 D14） |


