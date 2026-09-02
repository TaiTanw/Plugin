# 待处理事项 · 模糊项 · 风险

返回 [总目录](../README.md)

进行中的项留在上面各节。确认结束后请改到 [01_requirements/strategy.md](../01_requirements/strategy.md)，并把该行移到文末 **[已结束](#closed-history)**（文档定位可留）。

编辑器点链接只认**标题英文 slug**（与 GitHub 相同算法：小写、去标点、空格变 `-`）。空的 `<a id>`、中文标题锚点都会点不动。各节标题已改成 id 本身。

节：[A](#a-open-items) · [B](#b-unresolved) · [C](#c-known-risks) · [E](#e-low-priority) · [F](#d11-f) · [G](#g-op-recognition) · [H](#h-d13-archived) · [I](#d14-i) · [J](#d15-j) · [K](#d18-k) · [L](#d19-l) · [M](#d20-m) · [N](#d21-n) · [O](#d22-o) · [已结束](#closed-history)

---

## A. Open items

**A. 待开发（已拍板方向）**


| ID  | 事项                                      | 优先级 | 状态                                                                                               |
| --- | --------------------------------------- | --- | ------------------------------------------------------------------------------------------------ |
| D5  | CLI：无头验收后固化参数/退出码表                      | P1  | 第一刀已写 `PipelineCli.Run`；见 [cli-getting-started](../04_implementation/cli-getting-started.md)     |
| D10 | materialId → 列表（多文件/多夹）；同夹多模型加文件名后缀     | P2  | 评估见 [F](#d11-f)；`SourceBindings` 已预备、**Runner 未消费** |
| D11 | 成功后可选清理 Incoming（缓存）；**默认不删 Art**       | P2  | 与 Quiet 无关；见 [F](#d11-f) |
| D14 | 模型自带动画/音效 vs Pack 入口                    | P2  | 评估见 [I](#d14-i)；**暂不新开管线** |
| D15 | ⑤ 扫 Art 范围：单单元路径 vs 大根；Text 标记「已处理」     | P3  | **低优**；见 [J](#d15-j)；暂不实现 |
| D18 | 单文件②同路径复用；拟改为路径键+用户 ID 双重定位           | P2  | **隐患成立**。见 [K](#d18-k) |
| D23 | 导入 ctx + ④ B′ 原子搬迁                      | P2  | **编辑器实跑已通**。见 [d23-slice-report](../04_implementation/d23-slice-report.md) |
| D20 | Art `Prefab/` 夹「看起来」未刷顶点色               | P3  | **可选**。见 [M](#d20-m) |
| D21 | 无 codec / 加载不到：各 Op Skip vs Failed 口径不齐 | P3  | **低优**。见 [N](#d21-n) |
| D22 | `.gltf` 先封装成 GLB 再②（非 DCC 重导）           | P3  | **搁置，当前无开发必要**。见 [O](#d22-o) |


---



## d18-k

**K. Incoming 复用 / 双重定位（D18）** · 未拍板、未开发。现网事实见下「现网」；策略见「草稿」。

### 现网

`ImportSingleModel`：`Incoming/<三层名>/<原文件名>` 已存在则**复用、不覆盖**（伴生也不再拷）。③ Prefab 名 = `materialId` 或 Incoming 第一段。④ Art 夹 = Prefab **文件名**。不是内容缓存、不比哈希/mtime。gltf 更新会静默旧树 → [报告 4-1](../04_implementation/d23-slice-report.md#4-1)。

有意幂等 = 同槽再跑一次当命中。现网把「同路径」当成槽，所以**换内容不换路径**会用旧文件。

### 报告里已写、本稿仍成立（勿当新发现）

- ④⑥ **不读** Incoming 外层；只跟 Prefab 文件名。导入区区分 ≠ 交付区区分。
- 同用户 ID、Prefab 仍叫 `Chair.prefab` → Art/AB 仍一份。
- 覆盖必须 **清空内层再写**（gltf 旧 `.bin`/图）。
- 多一层夹必须改 `TryGetIncomingImportFolderName`（现网取第一段）。
- 人工批量面板本刀不对齐。

### CLI 在服务器上能拿到什么

工具只认 argv：`-source`、可选 `-materialId`。没有客户原机路径、没有上传 jobId，除非外壳写进这两个参数。

`-source` = **Unity 那台机器上的路径**（拷到工程旁 / 容器工作目录之后）。网页上传常见是固定落盘：`/work/in/model.gltf`、`/tmp/upload/a.gltf`。此时「路径+文件名+后缀」对每个客户都一样，**编码出来的父夹 ID 会撞**。客户磁盘上的 `D:\项目\三层\foo.gltf` 默认到不了 Unity。

要双重定位，外壳必须另给稳定键，例如：`-materialId`（用户名）+ `-sourceKey`/`-jobId`（任务键），或把 jobId 编进 `-source` 的目录（`/work/{jobId}/model.gltf`）。不要假设 `-source` 字符串等于客户侧唯一路径。

### 「内容哈希」是什么（和路径编码不是一回事）

| 键 | 输入 | 同路径换字节 | 不同路径、字节相同 | 服务器固定 `/tmp/a.gltf` |
|---|---|---|---|---|
| **路径指纹**（你草稿） | 规范化后的路径字符串 → SHA256 截断 | 槽不变 → 必须覆盖才更新 | 两槽 | **全员一槽**（除非路径含 jobId） |
| **内容哈希** | 文件字节（gltf 要 json+伴生整包） | 槽变了 | 一槽 | 按内容分槽，不靠路径 |
| **mtime/长度** | 时间戳/大小 | 多数能发现更新，可被同秒替换骗过 | 无关 | 同左 |

现网「不比哈希」= 没做后两行。路径指纹 **不是** 内容哈希，也不能替代三层当「人能看懂的名」。三层本就是手动端可见名，**没有唯一性**；当哈希用会误导。

短 ID：不要「16 进制 / 32 进制」混谈。常见是 **SHA256 的十六进制截断**（每字符 4 bit）。12～16 个 hex（48～64 bit）够文件夹名；8 个 hex 太短。Base32 更省长度，但实现成本高于截断 hex。不要用 Adler/GetHashCode。

### 草稿评估

```text
Incoming/<路径指纹>/
    <用户ID>/                 ← materialId，空则需规定默认（勿再假装三层唯一）
        文件（相对 URI 不变）
IncomingPrefab/<路径指纹>_<用户ID>.prefab
Art/<路径指纹>_<用户ID>/      ← ④ 读 Prefab 文件名，现网已如此
```

双重 ID 都对上 → Incoming 内层清空再写；Prefab `SaveAsPrefabAsset` 覆盖；Art 同名夹视为同槽覆盖。

**和上一稿「外层三层、Prefab 只用用户 ID」的差别：** 你现在把路径键编进 Prefab 名，交付区也会分开。这是上一稿选项 C，不是重复，是改交付槽定义。

| 问 | 结论 |
|---|---|
| ④ 读 Prefab 名当 Art 夹，流程不变？ | **编排不变**（仍 Flatten(Prefab) → Art 夹=stem → ⑥ 打这份）。**交付文件名变了**：AB 现网跟资产名（D1 退化）。APP 若按 `Chair.assetbundle` 取包，改成带指纹的名字要外壳/APP 一起认。 |
| 父夹用路径指纹、子夹用户 ID？ | 可以。指纹对人不可读，手动端 Incoming 会变「哈希树」。面板若仍要三层可见，CLI 与面板不要混用同一套外层规则。 |
| 只编码 `-source`？ | 服务器固定落盘则指纹无意义。优先 jobId/sourceKey；没有则退化为「用户 ID 单键 + 覆盖」（回到 SKU 槽）。 |
| gltf | 指纹若按主文件路径，伴生仍靠相对 URI 拷进同一内层；覆盖仍要整包清空。 |

**建议默认（未拍）：** 服务器键 = `sourceKey`（jobId，外壳给）+ `materialId`。本机面板可继续三层当 **显示名**，不要当唯一键。同双键 → 清空覆盖。不要在 Unity 里对上传临时路径做路径哈希当全局唯一。

---

## d19-l

**L. FBX 顶点色 / 导出 GLB（D19 · 已降级）**

> **不再作为自动化管线门禁。** 交付 AB 不以「工程 Mesh 全白」为必要。  
> 顶点色写在 `ModelImporter` 导入结果上；⑥ `BuildAssetBundles` 或贴图批标脏重导会从 FBX 二进制重建 Mesh，白会被冲掉。这主要在 **用 UnityGLTF 把当前 Mesh 导出成 GLB** 时露出来（Export 读 `mesh.colors`）。AB 黄/不黄与菜单导出 GLB 不是同一验法。

**若需要白顶点的 GLB：** 不要靠管线⑥后再导。

1. 资源处理总面板（或模型子面板）对 `Art/<名>/Model/*.FBX` **手动**「顶点色设为全白」。
2. **不要**接着做会重导该 FBX 的事：不要打 AB、不要无保护 `SaveAndReimport` / Extract、不要再跑会标脏 Model 的贴图批。
3. 在 Mesh 仍白时，用 UnityGLTF **导出 Prefab→GLB**。

管线里若仍留⑥后诊断/重刷白，视为遗留补偿，**不验收、不挡 CLI**。禁止把刷白塞进 `OnPostprocessModel` 打 Art（规则 33）。GLB 源文件刷白⑤仍 Skip。

---



## d20-m

**M. Art Prefab 夹未刷顶点色（D20 · 可选）**

> 2026-08-27：`Assets/Art/歼31-yy3d_3d/Prefab` 跑完管线后仍「未刷顶点色」；编辑器观感无明显问题。


| 项              | 说明                                                                                |
| -------------- | --------------------------------------------------------------------------------- |
| 色写在哪           | Mesh 顶点色在 `Art/<名>/Model/*.FBX` 子资产上，**不在** `.prefab` 文件里                         |
| 为何 Prefab 夹像没刷 | 对 Prefab 夹做「模型扫描」若只看 `.prefab`、未跟依赖到 FBX，会空或误判；Project 里点 Prefab 也看不到 Mesh.colors |
| 编辑器为何像正常       | Prefab 实例引用同一份 FBX Mesh；诊断/Scene 看的是 Model 上已白的 Mesh                              |
| 顽固点            | ⑥ 重导、UnityGLTF 另存 glb、看错夹，都可能再显得「Prefab 没白」                                       |
| 本阶段            | **不挡 CLI**。要对齐观感：扫 `Model/` 或 Prefab 的 GetDependencies；不要对 Prefab 夹单独写顶点色         |


---



## d15-j

**J. ⑤ Art 扫描范围 / Text 标记（D15 · 低优基本评估）**

> 与「④后写本次 Art 单元给⑤」同一问题域；大根约定当前够用。详见拍板记录；**暂不实现**。


| 问                   | 结论                   |
| ------------------- | -------------------- |
| 当前⑤是否整棵 Assets/Art？ | **多数是**（L1 种子/习惯路径）  |
| Text 已处理标记？         | **暂不需要**；优先路径收窄（D15） |


---



## d21-n

**N. 无 codec / 加载不到：Op 口径不齐（D21 · 低优）**

> D16 只认 Execute `Failed`。真解码失败（有 codec、`TryDecode` 失败）已是 Failed。灰项是「没有编解码器 / 资产加载不到」——各 Op 态度不同，**保持现状，不趁 D16 改 Op**。


| Op                 | 无 codec / 加载不到时                                                                                  | 会不会 50 |
| ------------------ | ------------------------------------------------------------------------------------------------ | ------ |
| 压图 Shrink、亮度→Alpha | Evaluate 扩展名 NotApplicable；Execute 里 `codec==null` 为 **Skip**                                    | 多数否    |
| TGA→PNG            | Execute 缺 codec 为 **Failed**                                                                     | 是      |
| 刷白                 | Evaluate 空 Mesh / 非 ModelImporter 为 **Skip**；Execute 空加载为 **Failed**（总批量先 Evaluate，常进不了 Execute） | 多数否    |


**风险：** 敏感度两头偏。Shrink 缺 codec 静默 Skip，CLI 仍 0，可能漏报；TGA 缺 codec 直接 50，可能偏严。以后若要统一，只改这些 Execute/Evaluate 返回值，不改 D16、不解析报告字符串。

**不做：** 本项不挡 CLI / D16。

---



## d22-o

**O. `.gltf` 先封装 GLB 再②（D22 · 已评未实现）**

> **产品（已改）：** `.gltf` **可直接入库**（② 整包 + ④ B′）。封装成 GLB **不开发**（D22 搁置）。  
> **边界未变：** 编辑器 **不**承担 DCC（改拓扑、重打材质、轴向/单位、Unity 场景再 Export）。源侧 / gltf-pipeline 转 GLB 仍可用、不是必须。  
> 现状总览 → [d23 报告](../04_implementation/d23-slice-report.md)（总目录 **4j**）。



### 现网


| 点                              | 现状                                                                                    |
| ------------------------------ | ------------------------------------------------------------------------------------- |
| ② `ToolImportApi`              | 扩展名认 `.gltf`；**现网已整包拷**（JSON + Scan 到的相对 URI 伴生）。目标已存在则 **D18 复用、连伴生也不再拷**            |
| UnityGLTF                      | `ScriptedImporter` 同时注册 `glb`/`gltf`；菜单 **Export GLB** = 从 Scene/Prefab **重导**，不是入库打包 |
| `GLBBuilder.ConstructFromGLTF` | 注释写明 **Does not currently copy binary data**；**不能**当实现                                |
| ⑤ 模型 SO                        | 代码默认 `.gltf`；资产曾出现 `.gitf` 拼写，与②列表不是同一份                                               |


面板 / ② 日志已改为：**可整包入库，转 GLB 可选**（不再要求先封装）。禁止用 UnityGLTF 场景 Export 当入库。

### 预估实现（若做，只做这一刀）

挂在 `ToolImportApi.ImportSingleModel`（及批量若将来认 gltf）`File.Copy` **之前**：

```text
ext == .gltf
  → 解析 buffers[].uri / images[].uri（相对路径、同目录 .bin、data: URI）
  → 写成标准 GLB（JSON chunk + BIN chunk，4 字节对齐，URI 改为 bufferView）
  → 只把 stem.glb 拷进 Import 区 → ImportAsset
  → 日志：[②] 已将 .gltf 封装为 .glb 再导入（容器打包，非 Unity 重导出）
缺伴生 / Draco·meshopt·KTX2 等编辑器不打算解 → Fail，文案指向 DCC 或 gltf-pipeline
```

**禁止：** `GLTFSceneExporter` / 先 Import 再 Export；禁止④把 `.gltf` 与贴图拆到不同夹还当源文件维护相对 URI。

体量：常见「一 json + 一 bin + 若干 png」大约一个小工具类；完整 glTF 2.0 扩展面大，**超出则不做、让 DCC 出 GLB**。

### 风险


| 风险     | 说明                                                         |
| ------ | ---------------------------------------------------------- |
| 当成 DCC | 美术以为编辑器会「整理模型」；实际最多改容器                                     |
| 半截封装   | 只写 JSON chunk、不嵌 BIN → 比现在更难查                              |
| 落盘改名   | `foo.gltf` → `foo.glb`，D18 复用键变了；旧残缺 `.gltf` 可能仍占 Import 夹 |
| CLI    | `-source *.gltf` 无头不能弹窗，只能日志 + 非 0                         |
| ⑤      | 封装后与现网 GLB 相同：刷白仍 Skip；材质烤独立 `.mat`                        |


**现阶段：不开发 D22。** `.gltf` 可直接给 `-source`：② 整包拷 + ④ 原子搬迁。源侧转 GLB 仍可用、不是必须。见 [pipeline-job-context](../04_implementation/pipeline-job-context.md)。

---



## B. Unresolved

**B. 仍模糊 / 未拍板**



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



## C. Known risks

**C. 已知风险（实现时注意）**


| 风险                   | 说明                                                                                                         |
| -------------------- | ---------------------------------------------------------------------------------------------------------- |
| ⑥ 输入源                | 未平铺时应用**任意 Prefab**（近直通），勿写死必须 Art                                                                         |
| ④ 后打 AB              | 开④时 Runner **已**改用平铺返回的 Art Prefab                                                                         |
| Legacy 大函数拆 options  | 易影响现有「导出全部/选中」菜单回归                                                                                         |
| ⑤ + GLB 内嵌           | 压图空跑 ≠ 合规；面板需提示                                                                                            |
| 总面板命名                | 勿与「资源处理总面板」混淆                                                                                              |
| URP 落差 / GLB 洋红      | 见 [d13-glb-magenta](./d13-glb-magenta.md)。主因 Shader，不是空 AB                                                 |
| Art 通道混淆             | **导入期自动流不碰 Art** ≠ **中间层⑤/L1 不碰 Art**。⑤是代跑面板手动总批量。见 tech-and-ops「三条通道」、规则 33                               |
| 贴图抽出                 | 已延后；勿当洋红 blocker。ggdddd 贴图仍嵌在 `Model/glb.glb`                                                              |
| 单文件②路径复用（D18）        | 目标已存在不覆盖 → 同名新源静默旧文件；gltf **伴生也不再拷**，B′ 会搬旧树。见 [K](#d18-k)、[d23 报告 §4](../04_implementation/d23-slice-report.md) |
| `.gltf`→GLB 再导入（D22） | **搁置、当前不开发。** `.gltf` 已可整包② + ④ B′。容器封装 ≠ DCC 重导；勿用场景 Export 当入库。若将来落盘改 `.glb` 仍与 D18 复用键交叉。见 [O](#d22-o) |


---



## E. Low priority

**E. 低优先级 / 与当前总目标弱相关**


| ID  | 事项                                      | 说明                               |
| --- | --------------------------------------- | -------------------------------- |
| L1  | L2 子面板数据：继续 Prefs，或抽「数据资源来源」SO          | 操作参数仍来自 L3；L2 只是临时范围+勾选。**非本迭代** |
| L2  | Shared 根下扁平脚本迁入 Switches/BatchPath/… 子夹 | 纯目录卫生，保留 .meta                   |


---



## d11-f

**F. 需求评估：materialId 默认名 / 列表 / Quiet / 缓存清理**

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
紧接着 P1     D5 CLI 无头验收后固化参数/退出码表
已完成        D9 / D12 / D16 / D17；D19 **已降级**（不挡管线；见 [L](#d19-l)）
P2            D10 列表（接口已预备）；D11 清 Incoming；D14 Pack/音效入口；**D18 单文件同路径复用策略**
P3 已评        D22 `.gltf`→GLB 再②（容器封装，非 DCC）；见 [O](#d22-o)
不要做        Quiet=退出；退出默认删 Art；为 Pack 另开一套 ②③⑥；用 Unity 场景 Export 当 glTF 入库
```

---



## G. Op recognition

**G. ⑤ 资源处理：后缀 / 未知夹 / 子面板风险**

> **完整归档（识别表 + 加 Op / 加后缀 / 加大类）：**  
> [04_implementation/op-recognition-and-extend.md](../04_implementation/op-recognition-and-extend.md)  
> 此前散落在本节、[d13](./d13-glb-magenta.md)「总面板如何认识操作」、Codec 注释。

**D12 已落地：** 模型默认 `.fbx/.glb/.gltf`；L3 只读「资源识别」；不做专用后缀编辑器、不与④平铺后缀表合并。未知夹一般无妨；未注册后缀静默跳过。无 codec 口径不齐见 **D21**。

---



## H. D13 archived

**H. GLB 洋红（D13）— 已归档**

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



## d14-i

**I. 动画 / 音效 / 要不要认 Pack（D14）**

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



## closed-history

**历史结束事务**

> 已做 / 退化 / 明确不做。文档链接保留备查。不要从这里再拉回「待开发」，除非需求翻案。



### 已做


| ID     | 事项                                   | 收口说明                                                                                                   |
| ------ | ------------------------------------ | ------------------------------------------------------------------------------------------------------ |
| 接口+中间层 | 1/2 窄口 + `Plugin/Pipeline` Runner    | 编排内核                                                                                                   |
| D3     | 自动化管线总面板                             | `Tools > 自动化管线总面板`                                                                                     |
| D2     | Runner：单文件导入、字符串结果；⑤后改默认开            | 曾含「写 L1 路径」（②.2），已删除；⑤ 靠 D17。StepResult 延后                                                             |
| （无 ID） | 删除②.2 导入后写 L1 批量路径                   | 编排不再改 `ResourceBatchFolderStore`；L1 Prefs 仅人手动批量；⑤ 仍用 D17 Art 单元                                       |
| D1     | ⑥ 契约核对（命名/压缩/main）                   | 契约 1/2 **退化已锁**；文档 [d1-ab-only](../04_implementation/d1-ab-only.md)                                    |
| D4     | ② 路径入库进管线；收集认 `.glb`                 | 真 `-batchmode` 留给 D5                                                                                   |
| D8     | 直通与 BuildAbOnly 合并 Options           | `RetinarExportSettings` + `RetinarAbApi.Build`                                                         |
| D6     | UnityGLTF 去本机 `file:`，改 git 依赖       | [d6-unitygltf-docker](../04_implementation/d6-unitygltf-docker.md)；本机拉包若未做，属环境验证不是开放功能                 |
| L3     | Shared 对外 Facade                     | **已建** `Shared/Api/`                                                                                   |
| D9     | materialId 选源默认名 / 清除同步              | `PipelineMaterialId` + 面板；D10 绑定列表仅预备                                                                  |
| D12    | ⑤ 模型扩展名 + L3 识别只读展示                  | `ModelProcessSettings` + `ResourceRecognitionGui`                                                      |
| D13    | GLB 洋红 / 交付 Shader 规范化               | [d13-glb-magenta](./d13-glb-magenta.md)；Material L1/L2/L3；ggdddd APP 验通                                |
| D16    | ⑤ `ToolPostProcessResult` + Fail(50) | 窄口返回 FailedCount（复用三层 Summary）+ Report；有一条 Execute Failed 即 50；Skip/未命中不算。⑤失败⑥仍跑。无 codec 口径不齐见 **D21** |
| D17    | ④成功后写本次 Art 单元到⑤扫描根                  | Runner 写 `PostProcessFolderPaths`；null 才回落 L1 Prefs；编排不改 Prefs                                         |
| D19    | 管线⑤⑥不以顶点刷白为门禁（降级）                    | FBX 白会被重导冲掉，主要在导出 GLB 露黄。需白：人工刷 + **不要**再触发导入导出后导 GLB。见 [L](#d19-l) |
| （无 ID） | 平铺分类面板去掉「添加根 BoxCollider」            | `AddBoxCollider` 默认 false；旧 Prefs 可能仍为 true                                                            |




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


| 事项                                 | 说明                                 |
| ---------------------------------- | ---------------------------------- |
| 整包平铺迁插件 2                          | 本阶段明确不做                            |
| Quiet = 退出编辑器                      | 禁止                                 |
| 退出默认删 `Assets/Art`                 | 禁止                                 |
| 为 Pack 另开一套 ②③⑥                    | 禁止（见 D14）                          |
| 用 UnityGLTF 场景 Export 当 `.gltf` 入库 | 禁止（DCC 重导）；入库最多做容器封装，见 [O](#d22-o) / D22 |


