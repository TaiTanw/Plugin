# 当前待办 · 决策口 · 风险

返回 [总目录](../README.md)

本页顶部的优先级表是**当前唯一待办入口**。完成项移到文末 [已结束](#closed-history)；长篇调查保留作归档证据，不再冒充进行中任务。

编辑器点链接只认**标题英文 slug**（与 GitHub 相同算法：小写、去标点、空格变 `-`）。空的 `<a id>`、中文标题锚点都会点不动。各节标题已改成 id 本身。

入口：[当前队列](#a-open-items) · [D24 当前拆分](./d24-boundary-plan.md) · [未拍板池](#b-unresolved) · [已知风险](#c-known-risks) · [低优先级](#e-low-priority) · [历史 D24](#d24-structure) · [已结束](#closed-history)

---

## A. Open items

**排序规则：P0 阻断正确交付或阻断继续拆分；P1 会静默产错或让两入口不一致；P2 为边界债务；P3 为延后项。每级从上到下执行。**

| 顺序 | ID | 严重度 | 类别 | 当前问题 | 下一验收 |
|---:|---|---|---|---|---|
| 1 | D24-R1b | P1 | 真实模型回归 | **2026-09-15 基本核对完成。** 缺件 30/40 已实跑；同名跨单元已代码记清会串。目录树/hash 未逐格勾，不挡换口 | ④ 按 [步骤页](./d24-flatten-steps.md) 从第 1 步按序做；改 Extract/绑图前补未勾格子 |
| 2 | D24-R4 | P1 | 插件 1 边界 | ④仍写 AB 标签；已同意迁⑥清单 | 步骤第 8 步；仓内已无读标签 |
| 3 | D25-2 | P1 | FBX 伴生 | 源旁独立贴图①未跟拷；内嵌须等④ Extract | 外置/内嵌分开验收；不并进第 11 步 |
| 4 | D25-4 | P1 | 引用隔离 | Extract 后再导入按短名挂兄弟单元；remap 把该路径当成本单元源 | **等步骤第 9 步**有结果对象再评估第 11 步；不是「FindAssets 限本单元」 |
| 5 | D26-4 | P1 | 退出码 | `Fail` 后错覆盖前错 | 保留首个失败码 |
| 6 | D26-5 | P1 | 入口一致性 | 面板强制入库，CLI 跟 SO | 两入口对同一 Options 一致，或写成显式差异 |
| 7 | D26-6 | P1 | ⑤ 跳过 | 无统一 typed 跳过原因 | 先核汇总契约；不让 Runner 读 Op 内部 |
| 8 | D26-7 | P2 | 行对齐 | 管线④已按下标配对、数量不等 Fail 40。人工仍 `contexts[0]`（步骤 4） | 手动断 ctx、轻扫描只提示 |
| 9 | D26-8 | P2 | 死接口 | `Received` 零订阅 | 接线或删除 |
| 10 | D10-3 / D11 / D14 | P2 | 产品能力 | Pack / 清 Incoming / 动画音效 | 不与 D24 同刀 |
| 11 | D24-9 / D15 / D21 | P3 | 清理 | 死自动层、扫描范围、Skip 口径 | P1 后处理 |


### 当前关键拆分

详见 [D24：平铺黑盒接管与 ctx 边界](./d24-boundary-plan.md)。一句话口径：

```text
②.5 仍只构建 ctx（紧跟①；人工点击时自建）
  → 编排把 ctx 译成 FlattenPlan（内核不再读 ctx）
  → 完整④：Begin → B|B′ → E? → D → C → Finish
  → 按行返回结果；整趟停/跳行留在 Runner
  → ⑥ 继续 RetinarAbApi，本刀不重写
```

管理细则：[D24 R3](./d24-boundary-plan.md#r3-flatten-split) · 步骤单页：[d24-flatten-steps](./d24-flatten-steps.md)。扩 ctx、① 建 ctx、坏行跳过、重写⑥、稳定行 ID：**停放**。

2026-09-10 已增加两个人工**完整相位**按钮：普通平铺固定走 B；原子迁移固定走 B′，面向带相对 URI 的 glTF。两者都继续执行 Begin→B/B′→E?→D→C→Finish，不开放七步任意乱序。直接选 `.gltf` 时先用③生成 Prefab；选 Prefab 做原子迁移时必须恰好依赖一个 glTF 包。菜单 FBX SafeZone 仍是兼容入口；管线不得用 `FlattenPaths` 代替④窄口。

---

## decisions-2026-09-15

- **第 5 步（R1b）基本核对完成。** 实跑：缺 `.bin` → ③ `exit=30`；留 `.bin` 缺 4 张 jpg → ③ 成功、④ `exit=40`。同名跨单元：现网会串（代码），第 11 步才改。未逐套目录树/hash 不挡从步骤第 1 步开工，也不能宣称 Extract/搜图已验证。
- **步骤 1–3 已落地（同日开工）：** `FlattenPlan`/`FlattenRowResult`、`ToolFlattenApi.Run`；管线 `FromContext`→`Run`；④ 多行只按下标配对，禁止 `contexts[0]`/`JobContext` 填缺。下一步是步骤 4（手动断 ctx）。
- **D25-4 机制更正：** 不是 C# 全工程搜贴图。B 前半段已是源引用→拷贝→objectMap；串在 Extract 后再对 Art FBX `GetDependencies` + 按短名 `AddRemap`。同名覆盖（同一目标路径不覆盖旧文件）是另一条链；现网 remap 会把兄弟文件拷进本单元，两条链会拧在一起。
- **Fail vs Warning：** 不是只换显示。Fail=`Fail(code)`+`return null`，停后续行和⑤⑥；Warning 不改退出码、继续打 AB。第 11 步 Fail/白膜/Warning 仍未拍板。

## decisions-2026-09-14

- ④AB标签：同意迁到⑥构建清单管理，先核对旧菜单与外部工具依赖。本轮只更新文档。
- ⑤NoOp：倾向跳过并明确提示，不让编排读取执行层内部。现有窄口无统一typed跳过原因，先核对汇总是否足够；不足时评估中间层排错层，新层方案尚未拍板。
- MaterialProcessSettings.EnsureMasterBatchDefaults会为空列表补规范化OP；“材质列表清空=禁用”不成立，纳入D26-6。
- 人工B/B′完整④已确认，不再列风险；④改为「plan 口 + 新实现、旧代码对照删除」，不是先清空 3518 行。
- **缺 sidecar：整趟④停、`FlattenFailed(40)`**（不承诺其余行继续）。
- **④文件在 TOol、编排暂归中间层**：只是暂时保留，待后续按步拆分后再搬家/下沉。
- **OBJ 缺 `.mtl`/贴图不进 `MissingUris` 闸**（见 [obj-vs-gltf](#obj-vs-gltf)）；不是漏修 D26-2，是探针未接入 ctx。
- **B 质量闸**（残留 `.fbm` 等）记为风险，④逐步拆分后再评估，本轮不加失败码。
- **历史文档按文件剔除**：已删 PACKAGING_RULES 旧编号正文、分享说明旧操作篇；后续同类文件继续单文件收缩，不把考古文堆回 backlog。
- **D13-R1 移动端验收完成**，退出顶部队列。
- **④拆分（同日续）：** ① 不承担 `ctx.Build`、不扩字段。无头「坏一件卡一批」是 Runner 粒度，与④换口分开。④ 可新实现，但先冻结 plan 口、旧内核留到 R1b 切开；**禁止先清空再写。⑥ 已有 `RetinarAbApi`，本刀不重写。** 详见 [D24 R3](./d24-boundary-plan.md#r3-flatten-split)。

## obj-vs-gltf

**OBJ vs glTF 缺伴生（2026-09-14 核对）。** 两套扫描都存在，**不是同一条④闸**。

| | glTF（`.gltf`） | OBJ（`.obj`） |
|---|---|---|
| ① 入库 | `GltfPackageFiles.Scan`：缺 `.bin`/外图 → Warning，仍拷已有文件 | `ObjPackageFiles.Scan`：缺 `.mtl`/贴图 → Warning，仍导入网格（常白膜） |
| ②.5 `JobContext.Build` | `PipelineGltfUriProbe` 写入 typed `MissingUris` | **不调用** `ObjPackageFiles.Scan`；`HasExternalUris=false`；`MissingUris` 保持空 |
| ④ | `HasMissingSidecars` → `FlattenFailed(40)`，**整趟④停**（`return null`），⑤⑥不跑 | 不进 40 闸。B 跟拷已解析到的 `.mtl`；缺件仍可能 `CopiedDependencies != null` 成功 |
| 当前产品口径 | 缺必需相对 URI = 硬失败 | 缺伴生仍可 `exit=0`；是否升闸待④拆文件后与 B 质量闸一并评估 |

`.glb` / `.fbx` 在 Build 里同样不填 `MissingUris`。FBX 源旁独立贴图另见 D25-2，不是本闸漏扫。

整趟停**没有磁盘回滚**：循环里已经成功的前几行 Art 会留在工程里。

## archived-analysis

**以下是历史调查与已完成切片的归档证据。** 当前执行顺序只看上方表；旧段落中的“下一刀”“本刀”均以当时日期为准。



## d18k

**K. Incoming 复用 / 双重定位（D18）** · 唯一定位**暂放**。管线重名：目标夹清空再写，不膨胀策略模块。**已开发（管线②④）。**

### 现网

管线② `ImportSingleModel`：工程外源 → **只删** `Incoming/<三层名>/` 再拷主文件+伴生（不扫整棵 Incoming）。源已在 Assets 则跳过拷贝、**不清夹**。③ Prefab 名 = `materialId` 或 Incoming 第一段。④ 管线 `ClearDestinationArtFolder`：**只删** `Art/<名>/` 再平铺（不扫整棵 Art）。菜单 Default 仍 Skip。批量导入面板仍 Skip/Conflict。不是内容缓存、不比哈希/mtime。唯一定位仍暂放。

有意幂等 = 同槽再跑一次当覆盖写新内容。换内容不换路径会更新该槽。

### 报告里已写、本稿仍成立（勿当新发现）

- ④⑥ 只跟 Prefab 文件名；覆盖须清空内层；多一层须改第一段启发式。



### 「内容哈希」是什么（和路径编码不是一回事）


| 键             | 输入                     | 同路径换字节           | 不同路径、字节相同 | 服务器固定 `/tmp/a.gltf`   |
| ------------- | ---------------------- | ---------------- | --------- | --------------------- |
| **路径指纹**（你草稿） | 规范化后的路径字符串 → SHA256 截断 | 槽不变 → 必须覆盖才更新    | 两槽        | **全员一槽**（除非路径含 jobId） |
| **内容哈希**      | 文件字节（gltf 要 json+伴生整包） | 槽变了              | 一槽        | 按内容分槽，不靠路径            |
| **mtime/长度**  | 时间戳/大小                 | 多数能发现更新，可被同秒替换骗过 | 无关        | 同左                    |


现网「不比哈希」= 没做后两行。路径指纹 **不是** 内容哈希，也不能替代三层当「人能看懂的名」。三层本就是手动端可见名，**没有唯一性**；当哈希用会误导。

短 ID：不要「16 进制 / 32 进制」混谈。常见是 **SHA256 的十六进制截断**（每字符 4 bit）。12～16 个 hex（48～64 bit）够文件夹名；8 个 hex 太短。Base32 更省长度，但实现成本高于截断 hex。不要用 Adler/GetHashCode。

### 网页任务时 `-source` 实际是什么

现网 CLI **不会**接到浏览器里的盘符。典型链路（V1.2 外壳，本仓未做）：

```text
网页拖入/选模型
  → 上传到 API/COS（对象键自定，常带 jobId）
  → 工人把对象下到 Unity 机器磁盘
  → Unity.exe … -source <这份本地路径> [-materialId <网页填的名>]
  → 退出码 + AB 文件回传
```


| 路径                                | 谁有                 | 会不会进 `-source`    |
| --------------------------------- | ------------------ | ----------------- |
| 用户电脑 `D:\项目\【m2222】歼15\fbx\a.FBX` | 仅浏览器；最多当「原始文件名」元数据 | **默认不会**          |
| COS 对象键 `jobs/{jobId}/a.gltf`     | 服务器                | 不会，除非先下载          |
| 下载后本地路径                           | Unity 工人决定         | **这就是** `-source` |


工人若写成 `/tmp/in/a.gltf`（每单覆盖同一文件），则三层、路径指纹**全员相同**。应写成 `/work/jobs/{jobId}/a.gltf`（或另传 `-sourceKey {jobId}`）。网页侧的 ID2 ≈ `-materialId`（展示名/SKU），没有则需规定默认。

### 草稿（本机可见名 + 指纹；Prefab 带双键）

```text
Incoming/<三层>_<路径指纹>/<ID2>/文件
IncomingPrefab/<ID2>_<路径指纹>.prefab
Art/<ID2>_<路径指纹>/          ← ④ 仍用 Prefab 文件名，编排不变
```

合理处：导入区还能看见三层；唯一性靠指纹；交付槽 = ID2+指纹（④⑥会跟着 Prefab 名走）。同双键 → 清空覆盖。③ 必须收到 ID2，勿靠 Incoming 第一段（第一段已是 `三层_指纹`）。夹名注意 80 字截断。

不合理处（服务器）：三层是从 `-source` 的父目录算的，网页任务里那是 `/work/jobs/xxx`，**不是**美术目录。指纹若哈希这份临时路径，没有 jobId 时会撞。本机面板：`-source` 仍是真实工程外路径，三层有意义。

**建议：** 网页用 `jobId` 当指纹输入（或直接当外层名），不要哈希 `/tmp/in/a.gltf`。本机可继续 `三层_指纹(本地路径)`。ID2 = 网页名称。AB 文件名会变成 `ID2_指纹.assetbundle`，回传按 job 目录取，不要假定还叫用户中文名。**本节草稿暂放，不挡下面重名策略。**

### 现网 Skip 怎么来的（写死，不是系统默认）

Unity `File.Copy` / `CopyAsset` **没有**「重名则跳过」的导入默认。现网是代码主动短路：


| 处                          | 行为                                                             |
| -------------------------- | -------------------------------------------------------------- |
| ② `ImportSingleModel`      | **管线已改**：先清本趟 `Incoming/<三层>/` 再拷。源已在 Assets 仍跳过、不清夹           |
| ② `CopyGltfSidecarsBeside` | 每个伴生 `if (!File.Exists) Copy`，已有则跳过（夹清空后通常打不中）                 |
| ④ B `CopyAssetToExactPath` | 目标已有资产 → **return 旧路径**（贴图另有 mtime 覆盖）。菜单仍走这条；管线④先清 `Art/<名>/` |
| ④ B′                       | `if (!File.Exists) File.Copy(..., false)`。管线④先清单元夹             |
| ③ Prefab                   | `SaveAsPrefabAsset` 同路径会覆盖，与上面不是同一套                            |


批量导入面板另有夹已存在 Skip/Conflict，不管线单文件。

### 重名：不要策略模块（夹级清空）

讨论的是 **整个导入夹 / Art 单元夹** 是否占用，不是按贴图逐文件精细策略。等价于人手删掉该夹再导。GUID 不必保：② 之后仍跑 ③④⑥，Prefab/Art/AB 都是新写。

因此 **不必** ctx 分发、不必三动作枚举、不必 SO。② 在 ctx 之前，若还要 Scan 再分支，收益薄。有外 URI 和单文件对「删掉目标夹再拷」结果一样。

管线已做：目标夹存在则 `AssetDatabase.DeleteAsset` 整夹，再按现网拷贝。② 清 `Incoming/<三层>/`；④ 清本次 `Art/<名>/`（辅助函数拒绝非「父夹/单段」）。菜单平铺不接，避免手点清 Art。与 D11「成功后可选清 Incoming 缓存」不是同一刀。

一小段删夹辅助函数即可，不要「文件策略模块」。哈希/逐文件以后再说。

### AB 改为 `{materialId}_android.assetbundle`

现网：`AssetBundles/{Android,iOS}/{stem}.assetbundle`，stem = Prefab 文件名小写；`assetBundleName` 同 stem。平台靠**文件夹**区分，文件名两端一样。D1 契约 1 已按此锁给 APP。

**2026-09-02 评估（未开发）：** 夹名仍 `Android`/`iOS`，夹内改为工单两个文件 `{id}_android.assetbundle` + `{id}_ios.assetbundle`。#274 在你回复（上传层可改名）之后只移出 milestone 49，无 APP 改名确认。见 [开发日志 §2](../05_dev-log/timeline.md#2-issue-274)。

改文件名：**中等、局部**。主改 `RetinarAbApi.BuildAndCopyAssetBundles`（已按平台循环）+ `BuildBundleFileName(assetName, platform)` + 交付拷贝。不必改②③④。填了 `materialId` 则 Prefab stem 已是 Id。旧槽 `name.assetbundle` 只删本趟平台夹里该文件，不扫整棵 `AssetBundles/`。

Unity 输出 `{assetBundleName}.{variant}` → 名用 `chair_android`、variant 仍 `assetbundle`。直通共用 AbApi 会一起变；规范化导出旧路径不会自动变。契约 2（`main`）不绑本刀。

真正成本是 **重开 D1 契约 1**。未改 APP 前不要切默认；上传层改名仍是备选。

---



## d19-l

**L. FBX 顶点色 / 导出 GLB（D19 · 已降级）**

> **不再作为自动化管线门禁。** 交付 AB 不以「工程 Mesh 全白」为必要。  
> 顶点色写在 `ModelImporter` 导入结果上；⑥ `BuildAssetBundles` 或贴图批标脏重导会从 FBX 二进制重建 Mesh，白会被冲掉。这主要在 **用 UnityGLTF 把当前 Mesh 导出成 GLB** 时露出来（Export 读 `mesh.colors`）。AB 黄/不黄与菜单导出 GLB 不是同一验法。

**若需要白顶点的 GLB：** 不要靠管线⑥后再导。

1. 资源处理总面板（或模型子面板）对 `Art/<名>/Model/*.FBX` **手动**「顶点色设为全白」。
2. **不要**接着做会重导该 FBX 的事：不要打 AB、不要无保护 `SaveAndReimport` / Extract、不要再跑会标脏 Model 的贴图批。
3. 在 Mesh 仍白时，用 UnityGLTF **导出 Prefab→GLB**。

管线里若仍留⑥后诊断/重刷白，视为遗留补偿，**不验收、不挡 CLI**。禁止把刷白塞进 `OnPostprocessModel` 打 Art。GLB 源文件刷白⑤仍 Skip。

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

**O.** `.gltf` **先封装 GLB 再②（D22 · 已评未实现）**

> **产品（已改）：** `.gltf` **可直接入库**（② 整包 + ④ B′）。封装成 GLB **不开发**（D22 搁置）。  
> **边界未变：** 编辑器 **不**承担 DCC（改拓扑、重打材质、轴向/单位、Unity 场景再 Export）。源侧 / gltf-pipeline 转 GLB 仍可用、不是必须。  
> 现状总览 → [d23 报告](../04_implementation/d23-slice-report.md)（总目录 **4j**）。



### 现网


| 点                              | 现状                                                                                    |
| ------------------------------ | ------------------------------------------------------------------------------------- |
| ② `ToolImportApi`              | 扩展名认 `.gltf`；**现网已整包拷**（JSON + Scan 到的相对 URI 伴生）。管线②先清本趟 Incoming 夹再拷（D18）            |
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

1. **B.CLI** 扩参（步骤 flag / 环境变量 / 临时 SO）— **D5 已冻**仅 `-source` + `-materialId`，步骤跟 SO。再扩须另开项
2. License：Personal vs Pro；多机互踢策略（**D5 已冻**：错误码 70 本入口不赋值）
3. Unity 精确版本号（现网打 AB 头为 `2022.3.54f1c1`）
4. iOS on Linux 失败时的拆分构建方案（基建）
5. ⑥ **部分** AB 失败改非 0 — **D5 已冻**：`PartialOk` 仍 0；全失败才 60。改码须另开项

---



## C. Known risks

**C. 已知风险（实现时注意）**


| 风险                   | 说明                                                                                                        |
| -------------------- | --------------------------------------------------------------------------------------------------------- |
| ⑥ 输入源                | 未平铺时应用**任意 Prefab**（近直通），勿写死必须 Art                                                                        |
| ④ 后打 AB              | 开④时 Runner **已**改用平铺返回的 Art Prefab                                                                        |
| Legacy 大函数拆 options  | 易影响现有「导出全部/选中」菜单回归                                                                                        |
| ⑤ + GLB 内嵌           | 压图空跑 ≠ 合规；面板需提示                                                                                           |
| 总面板命名                | 勿与「资源处理总面板」混淆                                                                                             |
| URP 落差 / GLB 洋红      | 见 [d13-glb-magenta](./d13-glb-magenta.md)。主因 Shader，不是空 AB。**D13-R1 含移动端已验收** |
| Art 通道混淆             | **导入期自动流不碰 Art** ≠ **中间层⑤/L1 不碰 Art**。⑤是代跑面板手动总批量。见 tech-and-ops「三条通道」、[PACKAGING_RULES 现行 Importer 分区](../../../RetinarBatchBuilder_Share/PACKAGING_RULES.md) |
| 贴图抽出                 | 已延后；勿当洋红 blocker。ggdddd 贴图仍嵌在 `Model/glb.glb`                                                             |
| 单文件②路径重名（D18）        | **管线已落**：② 只清 `Incoming/<三层>/`，④ 只清 `Art/<名>/`，再拷。菜单/批量面板仍 Skip。唯一定位暂放。见 [d18k](#d18k)                    |
| `.gltf`→GLB 再导入（D22） | **搁置、当前不开发。** `.gltf` 已可整包② + ④ B′。容器封装 ≠ DCC 重导；勿用场景 Export 当入库。若将来落盘改 `.glb` 仍与 D18 复用键交叉。见 [O](#d22-o) |
| B 残留外部 `.fbm` | 只 Warning，仍可进⑥。质量闸待④拆文件后评估，不提前加失败码 |
| OBJ 缺伴生 | `JobContext.Build` 对 `.obj` 不跑 `ObjPackageFiles.Scan`，`MissingUris` 空；① 只 Warning。与 glTF ④40 不是同一闸 |
| ④ 物理目录 | 代码在 `TOol/Generated/Flatten`，职责暂算中间层；拆步完成前不二次搬家 |


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


| 点      | 结论                                                                          |
| ------ | --------------------------------------------------------------------------- |
| 合理性    | **高**，与批量入库消歧一致                                                             |
| 建议位置   | **P2 / D10**                                                                |
| 渠道     | 网页选中 → **单夹/单任务**；人工面板 → **可选多文件**——合理，勿过早揉进网页契约                            |
| 预备（本步） | `SuggestDefault` 扫父目录磁盘；面板接表；Incoming 跟 ID2；**Runner 已按行循环**。Pack（D10-3）仍暂缓 |




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
已完成        **D5** 无头验收 + 参数/退出码按现状冻结
已完成        D9 / D12 / D16 / D17；D19 **已降级**（不挡管线；见 [L](#d19-l)）
P2            D10 列表（接口已预备）；D11 清 Incoming；D14 Pack/音效入口；**D18 管线夹级清空已落**（唯一定位暂放）
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



## d24-structure

**P. 结构收口（D24）历史归档 · 目录迁移与此前调查**

> **归档日期：** 2026-09-07。以下保留 2026-09-03 的调查、删除和目录迁移证据；不再作为当前执行表。
> 当前未完成的黑盒接管、ctx 边界与插件 1 输出格式白名单见 [d24-boundary-plan](./d24-boundary-plan.md)。



### 已拍板


| #   | 决定                                                                | 影响面                                       |
| --- | ----------------------------------------------------------------- | ----------------------------------------- |
| 1   | **导入区 Importer 参数唯一入口 = 插件 2** `OnPreprocessModel`。④ 只保留交付必须改的那几项 | 补回 `ModelImportSettingsProcessor`         |
| 2   | **相机 / 灯光在 ③ 之前用 Importer 标志剔除**，不在 Prefab 层级删组件                  | 标志必须早于 `PrefabBuildService`               |
| 3   | **④ 的「按后缀分类拷贝」抽成中间层 / 插件 2 的独立模块**；插件 1 只留交付专用步骤                  | **翻案**：旧「整包平铺迁插件 2 = 明确不做」作废              |
| 4   | **插件 1 门禁 / 遗产导出菜单 / DirectPackage 直接删除**（管线全部绕过，已是死路径）           | `Validate`*、`IRetinarAcceptanceGate`、遗产菜单 |
| 5   | 先开 P0（Importer 所有权 + 相机灯光 + OBJ 法线），P1 再拆 ④                       | 分刀，不一次翻                                   |




### 触发这次收口的实测（歼15-yy3d）

`Assets/Incoming/…_fbx.FBX/fbx.FBX.meta` 全是 Unity 出厂值 → **设置自动一次没跑过**：


| 字段                               | Incoming 现值 | 含义                      | 插件 1 在 Art 写  |
| -------------------------------- | ----------- | ----------------------- | ------------- |
| `materialLocation`               | `1`         | InPrefab（默认）            | `1`           |
| `materialName`                   | `0`         | BasedOnTextureName（默认）  | `1`           |
| `materialSearch`                 | `1`         | RecursiveUp（默认）         | `0` Local     |
| `importCameras` / `importLights` | `1` / `1`   | **未剔除**                 | `0` / `0`     |
| `normalImportMode`               | `0`         | Import（OBJ 应 Calculate） | `1` Calculate |


三重失效：钩子类**不在工程里**（仅存 `_backup_Plugin_legacy_copy`）；本机分项开关默认关；Art 被 `excludedPathPrefixes` 排除。

**Art 预设体仍有 10 个 Camera + Light 的原因是时序，不是标志没写：**

```text
② 入库   Incoming/fbx.FBX  importCameras=1（含 Camera001..010 + Light）
③ 建 Prefab  IncomingPrefab/*.prefab   ← 相机灯光在这里被烤成 GameObject
④ 平铺       拷这份 Prefab → Art/Prefab/
             才对 Art/Model/fbx.FBX 写 importCameras=0（已太晚）
```

`importCameras=0` 只影响 FBX 自己的层级；③ 存出的是独立 Prefab 资产，节点不会被回收。全仓**没有任何代码**从 Prefab 层级删 Camera/Light 组件。

### 现网所有权（2026-09-03 再拍：插件 1 只留 ⑥）


| 步        | 执行者                                                               | 归属       |
| -------- | ----------------------------------------------------------------- | -------- |
| 1 入库     | `ToolImportApi`                                                   | 插件 2     |
| 2.5 ctx  | `PipelineJobContext`                                              | 中间层      |
| ③ Prefab | `PrefabBuildService.TryBuildOnePrefab` → `Assets/IncomingPrefab/` | 插件 2     |
| ④ 平铺     | `ToolFlattenApi` → `FlattenBuildService`（内核 `RetinarBatchModelBuilder` 已在插件 2 `Generated/Flatten`） | **插件 2（D27）** |
| ⑤ 总批量    | `ResourcePostProcessService`                                      | 插件 2     |
| ⑥ AB     | `RetinarAbApi.Build`                                              | **插件 1 唯一允许留下的步** |


中间层→插件 1 现只剩：⑥ AB、`RetinarExportSettings`。④ 走 `ToolFlattenApi`。

**历史判断：D26 不挡 D27。** 步骤总闸（`PipelineStepSettings.runFlatten` 等）关掉某步仍 `exit=0` 是业务需要，不是谎报。D26-1/2 已于 2026-09-10 收口；D26-6 仍是「步开了但质量闸没咬」，不必与目录迁移绑定。

### 分刀清单

**P0 · 契约错位（本刀）**


| 项     | 内容                                                                                                             |
| ----- | -------------------------------------------------------------------------------------------------------------- |
| D24-1 | 补回 `ModelImportSettingsProcessor`，导入区 Importer 唯一入口                                                            |
| D24-2 | 剔灯剔相机 / OBJ 法线改为**基线**：读 SO 布尔、只受总闸约束，不受本机分项勾选影响                                                               |
| D24-3 | `modelUseExternalMaterials` **④ 拆分前保持关闭**——改导入区材质来源会改变 ④ 的输入（历史打架点，见 `RetinarBatchModelBuilder.cs` 599-605 注释） |
| D24-4 | SO 资产 `supportedExtensions` 缺 `.gltf`（迁移键已置位不会自动补）；补回                                                          |


**P0 · 2026-09-03 跑通管线后暴露（D24 验收回归）**


| 项     | 内容                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D25-1 | ~~OBJ 源/副本材质数 35 vs 111，三处引用改写被跳过~~ **2026-09-03 已改（方案 A）并验收通过**：`Art/<obj单元>/Model/` 下已有 `obj.mtl`，`exit=0`，三条 LogError 消失，模型带色。① `CopyObjSidecarsBeside` 把 `obj.mtl` 拷进 Incoming → Incoming 按 mtl 出 35 材质；④ 只按「Prefab 依赖 + 后缀分类」搬运，`.mtl` 既非 Unity 依赖、也不在 `ModelFlattenProcessor.DefaultSuffixes`(fbx/obj/glb/gltf) → Art 副本按 group 出 111 个默认白材质 → `MapSubAssetsBetweenCopies` 判非同源，Material 整类引用改写跳过。**曾评估按材质名配对（方案 C）并否决**：两侧材质名分别来自 `usemtl`(35 去重) 与 `g`(111)，只部分重合，配对不完整且会错配（同 `Plane_Zhi18` 那类静默坏引用）。落地：新增 `CopyObjMaterialLibrariesBesideCopiedModels`（读 `mtllib` 跟拷）；`FlattenModelCompanionFolders` 与 `ValidateModelFoldersAreClean` 放行 `.mtl`；规则 19/20/21 措辞同步 |
| D25-3 | `materialLocation=InPrefab` **不是遗留，删门禁时不得连坐。** 依据 `PACKAGING_RULES` 规则 20/21/37 + `RetinarBatchModelBuilder.cs:592-612`：External 会让目标工程首次导入 UnityPackage 时 Unity **自动**在 `Model/` 下生成 `Materials/` 与 `<FBX名>.fbm`。约束理由是 Unity 行为，与已删除的 `ValidateModelFoldersAreClean` 无关。**已写进规则 20 正文**。                                                                                                                                                                                                                                                                                                                                                            |


**P1 · ④ 分离**

> **2026-09-03 再拍并落地 D27：** ④ 进插件 2 `Generated/Flatten/`，插件 1 只留 ⑥。D25-2/4 仍挂 P1。


| 项      | 内容                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| D24-5  | **2026-09-03 已落地（不搬目录）。** `RetinarFlattenApi` 拆成 `TryBegin` / `SplitDependencies` / `RelocateAtomic` / `ApplyImportAndExtract` / `Remap` / `CopyRendererMaterials` / `TryFinish`；状态与源→副本表住 `RetinarFlattenWork`。`PipelineRunner.FlattenPerPrefab` 按行组合，B↔B′ 在编排里互斥。`FlattenPaths` 仍给菜单（含 FBX 直平铺）。实现未搬出 Retinar。 |
| D24-6  | **2026-09-03 已落地。** 两套赋值收进 `ModelImporterProfiles`。Processor **硬跳过** `RetinarPaths.ArtRoot`。交付档不读导入区 SO。 |
| D27    | **2026-09-03 已落地（目录+窄口）。** ④ 按 ③ Prefab 同款分类进插件 2 `Generated/Flatten/`（Config / Layout / Service / Category）。管线只调 `ToolFlattenApi`（接 `PipelineJobContext` + `ToolFlattenRequest`）。`PipelineFlattenBridge` 已删。内核类名仍是 `RetinarBatchModelBuilder`（未改逻辑）。菜单 FBX 直平铺随文件迁走，管线禁止调 `FlattenPaths`。模糊点见 `TOol/Editor/Generated/Flatten/README.md`。 |
| D25-2  | **不是放弃，也不是「导入区 FBX 就该白」。** 分两档：**(A) 源旁有独立贴图**——① 缺 `CopyFbxSidecarsBeside`，与 OBJ/gltf 不对等；规则 2 **不禁**跟拷（不改原始 Importer）。**(B) 贴图只嵌在 FBX 二进制**——Incoming 白是规则 36 两遍流程的预期，④ Extract 才出图；在 Incoming Extract 才真冲突规则 2/20/35。现网 Art 有时有图是 D25-4 跨单元按文件名借贴图，不是 (A) 已修好。挂 P1，不挡 D27。 |
| D25-4  | **FBX 单元跨单元借贴图（2026-09-15 更正）。** 不是 `FindAssets` 扫全工程。`ResolveExistingOrDefaultTexturePath` 已限本包出口。串图：Extract 后再导入按槽位短名挂兄弟 Art，`RemapModelImporterTexturesToArtFolder` 把 `GetDependencies` 里的外部路径当成本单元源去拷/`AddRemap`。`materialSearch=Local` 只管材质搜索。同名覆盖链（`CopyAssetToExactPath` 目标存在则复用）是另一回事。修法见 [步骤第 11 步](./d24-flatten-steps.md)，**依赖第 9 步**。 |
| D24-10 | **OBJ 轴向** 已落地（绑定行开关 + 内容节点 −90°X）。 |

**原插件 1 平铺 partial 现状（D27 后：文件已在插件 2 `Generated/Flatten/Service/`）**

⑥ 几乎不在这些文件里。`RetinarAbApi.cs` + `RetinarDeliverableIo` 才是 ⑥。`FlattenPaths` 菜单与管线④仍落在下面三个 partial：

| 文件 | 行数 | 实际职责 | ⑥ 是否读 |
| --- | --- | --- | --- |
| `RetinarBatchModelBuilder.cs` | 2253 | 菜单入口、`CreateNormalizedPrefab`（FBX 直平铺 / SafeZone，**管线不走**）、管线 ④ 七步实现（Begin/B/B′ 转发/E/D/C/Finish）、空壳+轴向、碰撞盒、动画、分类拷贝调用、OBJ `.mtl` 跟拷 | 否（只给 Prefab 写 `assetBundleName`） |
| `RetinarBatchModelBuilder.AssetResolution.cs` | 1006 | 菜单 FBX 的 `CollectSourceAssets`（管线 ④ 几乎不用）、`MoveAssetToExactPath`、④ 结束自愈 `TryHeal`、`ExtractAndBind`、顶点色快照重导、运行时依赖白名单 | 白名单注释自称 ⑥ 仍读；出包校验已删，现主要给 ④ 自愈 |
| `RetinarBatchModelBuilder.AtomicRelocate.cs` | 224 | 仅 B′ | 否 |
| `10_Flatten/*` | 分类拷贝注册表 | B 的 `FlattenCopyRunner` | 否 |

混居点：菜单 FBX 直平铺（SafeZone）与管线 Prefab 平铺（空壳）同文件。D27 第一刀应把前者与后者切开，避免搬家时把 SafeZone 带进管线。

**D24-10 轴向勘查（2026-09-03）**

同一份源模型，两种导出，Prefab 里的 Transform 是这样的：


|                      | Transform 数 | 旋转                                                            | 结果   |
| -------------------- | ----------- | ------------------------------------------------------------- | ---- |
| `Art/<fbx单元>/Prefab` | 139         | 根节点 `{-0.7071068, 0, -0, 0.7071068}` = **−90°X**，其余节点带各自源 TRS | 显示正确 |
| `Art/<obj单元>/Prefab` | 113         | **112 个** `{0,-0,-0,1}` **＋ 空壳 1 个** `{0,0,0,1}`**，全单位**      | 竖立   |


结论三条：

1. **目标修正量就是 −90°X**，而且「把它放在 Transform 节点上」正是 Unity 自己对 FBX 的做法（FBX 头里有 up-axis，Unity 读到就写这个旋转）。OBJ 格式没有 up-axis 字段，Unity 只能当 Y-up 读，于是不写任何补偿。
2. **不与规则 23 冲突。** 规则 23 锁的是**外壳根**必须 Identity；FBX 那份的交付 Prefab 内容节点本来就带着 −90°X 并且一直通过验收。OBJ 全单位意味着没有「根偏移＋子节点反向补偿」结构要保护，加一个 −90°X 不会拆掉任何东西。落点：`WrapIncomingPrefabInEmptyShell` 里的 content 节点（空壳下唯一那个子节点）。
3. **④ 天然幂等**：管线 ④ 是 `ClearDestinationArtFolder = true`，每趟重建 `Art/<名>/`，不存在「转过一次又转一次」。这点比放 ⑤ 强——⑤ 改 Mesh 子资产会和顶点色一样被 ⑥ 的重导冲掉（D19 同款），且无法判断是否已转过。

**2026-09-03 已落地。** 触发方式拍板为「绑定行显式开关」，落点如上：


| 件                                                      | 改动                                                                                                 |
| ------------------------------------------------------ | -------------------------------------------------------------------------------------------------- |
| `PipelineSourceBinding.ConvertZUpToYUp`                | 新字段，人给的输入，与 `MaterialId` 同级                                                                        |
| `PipelineObjAxisProbe`（新）                              | 只读 `.obj` 头部注释区（遇非 `#` 行即停），命中默认 Z-up 的导出器署名就返回该行。读盘失败一律静默                                         |
| `PipelineJobContext.ZUpExporterNote`                   | ②.5 记提示。**故意不进** `Warnings`——`warnings=0` 要继续表示「没发现问题」                                             |
| `PipelineFlattenBridge.ToFlattenOptions(ctx, binding)` | 加第二参。轴向不是观测事实，不塞 ctx，走独立入口                                                                         |
| `RetinarFlattenOptions.ConvertZUpToYUp`                | ④ 的入参                                                                                              |
| `WrapIncomingPrefabInEmptyShell(…, bool)`              | 内容节点 `ZUpToYUpRotation * localRot`；外壳根仍 Identity。排在 `AddOrUpdateBoxColliderInPrefab` 之前，碰撞盒按修正后姿态算 |
| 面板                                                     | 绑定行只对 `.obj` 显示勾选；命中署名时下方补一行灰字提示。嗅探在 `RefreshAxisHints` 里算一次，OnGUI 不读盘                             |
| 规则 23                                                  | 补两条子款：轴向修正是「保留源 TRS」的唯一例外；外壳根不放宽                                                                   |


**首轮验收发现的接线漏（已修）**：`PipelineRunner.NormalizeBindings` 用两参构造重建每一行、再于 `ImportFromBindings` 覆盖 `options.SourceBindings`，开关在入库阶段就被抹掉，④ 永远读到 false。绑定行在面板与 Runner 之间共重建三次（`ApplyBindings` / `CopyBindings` / `NormalizeBindings`），全是逐字段手抄，加字段必漏。已统一改走 `PipelineSourceBinding.CloneWith`，以后新增字段只改那一处。
另补一条负向日志：嗅到默认 Z-up 却没开修正时打 `④ [n] 未开轴向修正（该源导出器默认 Z-up）`——「没勾」与「开关没传到」在产物上完全一样（都是「没变化」），没这条只能靠翻 Prefab 里的四元数才能分辨。

**已知限制**：CLI（D5 契约冻结，仅 `-source`/`-materialId`）合成的绑定行拿不到这个开关，恒为 false。CLI 只收单源，本就不是主路径；要放开须先解冻 D5。

**为什么不自动判定**（ctx 测不出轴向）：

- ctx 现在只记录 AssetDatabase 能直接回答的**事实**（importer 种类 / 外部 URI / 材质形态）。轴向不是事实——OBJ 文件里根本没有这个字段。
- 能拿到的最强信号是文件头第 1 行 `# 3ds Max Wavefront OBJ Exporter v0.97b - (c)2007 guruware`。GuruWare 导出器默认按 Max 的 Z-up 写，但它带 Flip YZ 勾选，勾了就是 Y-up。所以 ctx 最多写 `ZUpLikely`（怀疑），写不了 `IsZUp`（事实）。让 ctx 开始存放怀疑会破坏它现有的性质。
- 猜错的代价不对称：猜错方向 = 交付一架躺着的飞机，且 AB 里看不出来、下游才发现；人工勾一下的代价只是多点一次。

**D26 · 2026-09-03 全仓扫查新增（D24/D25 之外）**

共性是**「静默成功」**：步开着、质量闸没咬，整趟仍 `exit=0`。这与「业务关掉某步」不是一回事——`PipelineStepSettings.runFlatten/runPostProcess/runAb` 关掉则该步不跑，0 表示「按配置跑完」，合法。以下为历史发现；D26-1/2 已于 2026-09-10 修复，D26-4/5/6 仍开放。

D26-2 现网已改（**仅 glTF 探针路径**）：`PipelineGltfUriProbe` 同时写 typed `MissingUris` 与展示 Warning；Runner 在 Begin 前直接映射 `FlattenFailed(40)` 并停止整趟④；B′ 还会拒绝已知缺件，并要求全部执行时输入生成精确目标。控制流不解析 Warning 文案。OBJ 的 `ObjPackageFiles.Scan` 只用于① Warning，不进 ctx，见 [obj-vs-gltf](#obj-vs-gltf)。


| 项     | 优先级    | 内容                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ----- | ------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D26-1 | ~~P0~~ | **2026-09-10 已修。** Art 仍硬跳过；配置导入根内的受支持模型始终应用安全基线，不受 `MasterEnabled` 或排除前缀影响；其它 Assets 路径保留原总闸/排除语义；用户偏好策略仍要求总闸与“模型·设置自动”同时开启。目录边界与基线/策略组合已有 19 个 EditMode 测试。 |
| D26-2 | ~~P0~~ | **2026-09-10 已修（glTF）。** ctx 增加 typed `MissingUris`；Runner 在④ Begin 前 Fail(40) 并停止整趟；B′ 拒绝缺件或执行时丢件。typed 控制流已有 3 个 EditMode 测试。**OBJ 缺 `.mtl`/贴图不进该闸。** |
| D26-3 | ~~P1~~ | `.obj` ~~不在~~ `supportedExtensions` ~~的 C# 默认与迁移里~~ **2026-09-03 已修。** 现网资产有 `.obj` 故未暴露，但代码默认是 `.fbx/.glb/.gltf`、`EnsureSupportedExtensionsDefaults` 只补 glb/gltf → 新工程或重建 SO 时，① 能入库 OBJ 而 ⑤ 收集 / 设置自动 / 后处理自动都不认（含 P0 刚加的 OBJ 法线基线，它自己就闸在 `IsSupportedModelExtension` 上）。落地：默认补 `.obj`；迁移改成每批一个键的 `MigrateOnce`，加 `TOol.ModelExt.ObjMigrated.v1`；tooltip 改成「与 ToolImportApi 白名单对齐，两处必须同步」                                                                                                                                               |
| D26-4 | P1     | `PipelineResult.Fail` **无条件覆盖退出码**（`PipelineResult.cs:21-27`），多步失败只剩最后一步的码。⑤ 设 50 后 ⑥ 再设 60，CLI 只看得到 60。应改成「已有非 0 则保留首个」                                                                                                                                                                                                                                                                                                                                                                                                                  |
| D26-5 | P1     | **面板强制** `RunImport=true`**，CLI 跟 SO 的** `runImport`（`PipelineWindow.cs:621-622` vs `PipelineCli.cs:32`）。CLI 关了 `runImport` 再给工程外 `-source`，报 `BadArgs(10)` 而非 `ImportFailed(20)`（`PipelineRunner.cs:346-350`）。同工程两套行为                                                                                                                                                                                                                                                                                                                    |
| D26-6 | P1     | **⑤ 三层** `masterBatchOperationIds` **全空时静默 Skip 且** `FailedCount=0`（`ResourcePostProcessService.cs:121-126,149-154,179-184`），`HasHardFailure=false` 不映射 50。`runPostProcess=true` 却整步空跑仍 `exit=0`。与 D24-9 同类但不同字段，一起定「空配置算不算失败」                                                                                                                                                                                                                                                                                                            |
| D26-7 | P2     | **④ 多 Prefab 时 ctx 下标不足回退** `contexts[0]`（`PipelineRunner.cs:453-456`）。长度不一致时后续行拿第一份 ctx 去映射 `SkipDependencySplit`/B′ sidecar，可能错走拆夹。**注意**：D24-10 新加的 binding 取用（同处）已用 `i < Count` 直接给 null 而非回退，两处口径不一，一并统一                                                                                                                                                                                                                                                                                                                             |
| D26-8 | P2     | `PipelineSourceAccept.Received` **事件零订阅**（`PipelineSourceAccept.cs:15-24`），全仓无 `+=`，实际只走 `PipelineWindow.AcceptBindings`。要么接线要么删                                                                                                                                                                                                                                                                                                                                                                                                          |
| D26-9 | P2     | 文档：`cli-getting-started.md:82-87` 未写 CLI 依赖本机 EditorPrefs 总闸，与 `pipeline-flow.md:43` 不一致；`op-recognition-and-extend.md:68` 的默认扩展名列表随 D26-3 一起改                                                                                                                                                                                                                                                                                                                                                                                            |




扫查同时确认**没有**问题的三类（避免重复排查）：`Pipeline/`+`TOol/` 内无 `TODO|FIXME|NotImplemented`；`modelUseExternalMaterials=false` 时插件 1 与插件 2 在 Incoming 区无活跃 Importer 冲突（`RetinarBatchModelBuilder.cs:599-605` 那段打架需开关为 true 才复现，已记 D24-3）。

**P2 · 删除死路径与失真文档**


| 项     | 内容                                                                                                                          |
| ----- | --------------------------------------------------------------------------------------------------------------------------- |
| D24-7 | ~~删三道校验 / 遗产导出菜单 / DirectPackage / IRetinarAcceptanceGate / 交付物写盘子树~~ **2026-09-03 一阶段 + 二阶段已删。** 管线验收 `exit=0`。详见下方 [d24-7-cut](#d24-7-cut) |
| D24-8 | 失真文档：`PACKAGING_RULES.md` 规则 33；`TOol/ARCHITECTURE.md` 168 / 335 行（把钩子当在场）；`op-recognition-and-extend.md` §1、`pipeline-phase-io.md` §3（「SO 只管后处理」，实际也闸设置自动） |




**D24-7 一阶段删除清单（2026-09-03）**

删除前核对的结论：整个遗产导出子树自成闭环，管线 ①→⑥ 一处都不经过。`BuildAssetBundles` / `ExportUnityPackages` 等看似共用的函数其实只被 `ExportArtPrefabPaths` 调用，⑥ 走的是 `RetinarAbApi` 自己那份。


| 已删                                                                                                                                                                                                                                                       | 位置                            |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------- |
| 「【遗产】从 Art 规范化导出」×2、「成品直达」×2 菜单（含 validate 共 6 个方法）                                                                                                                                                                                                      | `01_RetinarMenu.cs`           |
| `RetinarPackageScheduler.cs`、`RetinarDirectPackage.cs`（整文件 + meta）                                                                                                                                                                                       | `20_Package/`                 |
| `IRetinarAcceptanceGate.cs`（整文件 + meta）                                                                                                                                                                                                                  | `30_Business/`                |
| `ExportAllArtPrefabs` / `ExportSelectedArtPrefabs` / 两个 Validate / `ExportArtPrefabPaths`                                                                                                                                                                | `RetinarBatchModelBuilder.cs` |
| `CollectAllArtDeliveryPrefabPaths` / `CollectSelectedArtPrefabPaths` / `IsArtDeliveryPrefabPath`                                                                                                                                                         | 同上                            |
| `ValidatePrefabSpatialPlacement`                                                                                                                                                                                                                         | 同上                            |
| `PartitionAssetsThatPassValidation` / `CollectValidationFailures` / `Indent` / `WriteValidationFailureReport` / `ValidateExternalDependencies` / `HasExternalEmbeddedMediaDependency` / `BuildDependencyDiagnosticLine` / `ValidateModelFoldersAreClean` | `…AssetResolution.cs`         |


**刻意留下的**：

- `IsApprovedRuntimeDependency` + `ApprovedRuntimeDependencyPrefixes`——④ 的依赖分类与 ⑥ `RetinarAbApi` 都在读，不是校验专属。
- `materialLocation = InPrefab`（**D25-3 硬约束**）。注释已改写：约束的理由是 Unity 会自动生成 `Materials/` 与 `.fbm` 目录，跟那道已删的门禁在不在无关。
- `RetinarDeliverableIo`——⑥ 在用。

**同日追加**：`30_Business` 整层删除（`IRetinarDeliverableOutput` / `RetinarBusinessIds` / `RetinarBusinessProfile`）——四个文件只互相引用，无外部调用方，也没有已创建的 Profile 资产；`00_RetinarPaths.cs` 里一处 `<see cref="RetinarDeliverableIds"/>` 已改掉（否则 CS1574）。

**D24-7 二阶段（2026-09-03 手工删完，管线验收 `exit=0` 之后）**

交付物写盘子树与只服务它的辅助一并去掉。不再写 `00_` / `01_source` / `06_docs` / xlsx。

| 已删 | 说明 |
|---|---|
| `WriteDocsFiles` / `WriteRuntimeRequirements` / `ExportUnityPackages` / `CopyBuiltBundlesToDeliverables` / `CopyBuiltBundle` / 贴图报告一族 / `BuildAssetBundles` / `CopySourceFilesToDeliverables` / `BuildDccModelReport` | 一阶段后已无调用方 |
| `RetinarBatchModelBuilder.AssetInfoWorkbook.cs`（整文件） | 只被 `WriteDocsFiles` 调 |
| `CopyAssetFile` | `01_source` 拷贝残留 |
| `CollectAssetStats` + `AssetStats` | 只给 xlsx 填数，`.Stats` 全仓无读取 |
| `NormalizePreparedPrefabBounds` + `RebasePrefabRootToOrigin` + `GetMovablePrefabRoots` | 无调用方；FBX SafeZone 仍内联在 `CreateNormalizedPrefab` |
| `TryRestrictPackagedModelMaterialSearch` | 无调用方；`ApplyModelImportSettings` 已把 Art 模型收成 InPrefab + Local |

> **教训**：二阶段第一版用脚本按名字批量删，在去注释文本上配平却把下标用回原文，切伤 5 处。本工程没有 git，靠 `_backup_Plugin_legacy_copy` 逐段比对才复原。后续删除只用精确文本替换。

**删除带来的行为变化（不是纯死码清理，必须知道）**：出包前不再有任何兜底检查。以前 `ValidateExternalDependencies` 会在导出时对仍挂外部 `.fbm` 的资产强制跑一次 `ExtractAndBind` + `RemapAllArtMaterials`，现在 **④ 是最后一道**，平铺后仍剩的外部 `.fbm` 会原样进 AB。④ 里那条 `.fbm` 残留告警已改措辞（`已无后续兜底，将原样进 AB`）；`MoveAssetToExactPath` 失败的 `LogError` 现在是目录不干净的唯一信号，不得降级成 Warning。

**P3**


| 项     | 内容                                                 |
| ----- | -------------------------------------------------- |
| D24-9 | `importAutoOperationIds` 现网为空 → 后处理自动是死开关，确认是否保留该层 |


---



## closed-history

**历史结束事务**

> 已做 / 退化 / 明确不做。文档链接保留备查。不要从这里再拉回「待开发」，除非需求翻案。



### 已做


| ID     | 事项                                   | 收口说明                                                                                                                                                                                                                               |
| ------ | ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 接口+中间层 | 1/2 窄口 + `Plugin/Pipeline` Runner    | 编排内核                                                                                                                                                                                                                               |
| D3     | 自动化管线总面板                             | `Tools > 自动化管线总面板`                                                                                                                                                                                                                 |
| D2     | Runner：单文件导入、字符串结果；⑤后改默认开            | 曾含「写 L1 路径」（②.2），已删除；⑤ 靠 D17。StepResult 延后                                                                                                                                                                                         |
| （无 ID） | 删除②.2 导入后写 L1 批量路径                   | 编排不再改 `ResourceBatchFolderStore`；L1 Prefs 仅人手动批量；⑤ 仍用 D17 Art 单元                                                                                                                                                                   |
| D1     | ⑥ 契约核对（命名/压缩/main）                   | 契约 1/2 **退化已锁**；文档 [d1-ab-only](../04_implementation/d1-ab-only.md)                                                                                                                                                                |
| D4     | ② 路径入库进管线；收集认 `.glb`                 | 真 `-batchmode` 后由 D5 验收                                                                                                                                                                                                            |
| D5     | CLI 无头验收后固化参数/退出码表                   | 2026-09-02 本机 `-batchmode` `直18.gltf` `-materialId GLTF直18` → `exit=0`。契约冻：仅 `-source`/`-materialId`；步骤跟 SO；码 0/10/20/30/40/50/60/80；70 不赋值；⑥ `PartialOk` 仍 0。见 [cli-getting-started](../04_implementation/cli-getting-started.md) |
| D8     | 直通与 BuildAbOnly 合并 Options           | `RetinarExportSettings` + `RetinarAbApi.Build`                                                                                                                                                                                     |
| D6     | UnityGLTF 去本机 `file:`，改 git 依赖       | [d6-unitygltf-docker](../04_implementation/d6-unitygltf-docker.md)；本机拉包若未做，属环境验证不是开放功能                                                                                                                                             |
| L3     | Shared 对外 Facade                     | **已建** `Shared/Api/`                                                                                                                                                                                                               |
| D9     | materialId 选源默认名 / 清除同步              | `PipelineMaterialId` + 面板；D10 绑定列表仅预备                                                                                                                                                                                              |
| D12    | ⑤ 模型扩展名 + L3 识别只读展示                  | `ModelProcessSettings` + `ResourceRecognitionGui`                                                                                                                                                                                  |
| D13 主体 | GLB 洋红 / 交付 Shader 规范化               | [d13-glb-magenta](./d13-glb-magenta.md)；Material L1/L2/L3；ggdddd APP 验通                                                                                                                                                            |
| D16    | ⑤ `ToolPostProcessResult` + Fail(50) | 窄口返回 FailedCount（复用三层 Summary）+ Report；有一条 Execute Failed 即 50；Skip/未命中不算。⑤失败⑥仍跑。无 codec 口径不齐见 **D21**                                                                                                                             |
| D17    | ④成功后写本次 Art 单元到⑤扫描根                  | Runner 写 `PostProcessFolderPaths`；null 才回落 L1 Prefs；编排不改 Prefs                                                                                                                                                                     |
| D19    | 管线⑤⑥不以顶点刷白为门禁（降级）                    | FBX 白会被重导冲掉，主要在导出 GLB 露黄。需白：人工刷 + **不要**再触发导入导出后导 GLB。见 [L](#d19-l)                                                                                                                                                                |
| D18    | 同路径重名按单元夹清空再写                     | ②清本趟 Incoming 单元；④清本趟 Art 单元。唯一定位另议，见 [d18k](#d18k) |
| D20    | “Prefab 夹未刷顶点色”排查收口                  | 顶点色属于 Model Mesh，不属于 `.prefab` 文件；不挡 CLI，见 [d20-m](#d20-m) |
| D22    | `.gltf` 先封装 GLB 再入库                      | 明确搁置；现网整包② + 原子④，禁止拿 Unity 场景 Export 当入库，见 [d22-o](#d22-o) |
| D23    | ctx + glTF B′ 原子搬迁                        | 编辑器实跑已通；ctx 在②.5构建，B′保留相对 URI 树 |
| D24 已完成切片 | D24-1/5/6/7/10                         | Importer 入口、七步窄口、Importer Profiles、遗产删除、OBJ 轴向已落地；剩余工作另见 [当前 D24 计划](./d24-boundary-plan.md) |
| D27    | ④文件目录迁入插件 2                           | `Generated/Flatten/` + `ToolFlattenApi` 已落地；**只代表物理迁移，不代表黑盒接管完成** |
| D24-R1 | 平铺黑盒静态查封与高危保护                     | 已完成调用图、方法分类、入口/边界与风险排序，见 [flatten-core-audit](../04_implementation/flatten-core-audit.md)；并补 Begin 失败不得回退源 Prefab、外部材质复制失败不得写源材质两处护栏。真实模型回归仍按审计清单逐项补。 |
| D13-R1 | glTF透明 + 移动端 AB | 2026-09-10 OP 修复；2026-09-11 Art 玻璃确认；2026-09-14 用户确认移动端验收完成。不扩 ctx。 |
| D24-8 / D26-9 文档切片 | 当前约定同步（2026-09-14） | 修正④归属/人工完整入口、SO范围、⑤材质、导入基线与失败语义，旧规则标历史。后续代码变化须同步，不代表D24结构总收口。 |
| D26-1 | 导入根安全基线与总闸解耦                       | Incoming 基线不受总闸/排除影响；其它 Assets 与策略自动保持原闸；19 个 EditMode 测试通过。 |
| D26-2 | glTF 缺伴生失败闸                              | typed `MissingUris` → ④ `FlattenFailed(40)` 整趟停；B′ 全量核验；3 个 EditMode 测试通过。OBJ 不进此闸。 |
| D24-R2 | 平铺配置 SO 与运行快照                           | 人工/管线使用同一 `FlattenOperationSettings` 数据类、不同目录实例；分类/清夹/碰撞体在运行前冻结，内核不再读平铺 EditorPrefs。 |
| D24-R3b | 人工普通平铺 / 原子迁移                          | 平铺面板与菜单已有两个相位按钮；普通 B 遇外部 URI 拒绝，B′ 缺件拒绝；不开放七步乱序。 |
| （无 ID） | 平铺分类与碰撞体迁出 EditorPrefs                 | `AddBoxCollider` 默认 false；旧兼容属性仅代理人工 SO。 |




### 退化（现网为准，本阶段不改）


| ID        | 事项                                         | 说明                                                                                                |
| --------- | ------------------------------------------ | ------------------------------------------------------------------------------------------------- |
| D7        | 与 APP 书面确认 `main` 名、LZ4、`.assetbundle` 文件名 | **可退化**（现网取包）；LZ4 已采用                                                                             |
| D1 契约 1/2 | AB 文件名 / 包内 main                           | **仍退化。** 文件名重开须 APP；夹不变、夹内两工单名 = 评估未开发。`main` 不绑。见 [日志 §2](../05_dev-log/timeline.md#2-issue-274) |


2026-09-14勘误：全套00–06、旧规范化导出/成品直达和业务门禁已删除，不是“代码暂留、默认关”。⑥为AB/可选UP；SafeZone只留直接选FBX的人工兼容平铺。

> 注：Converter **④⑤ 默认开**（可关），已不在「默认不做」列。



### 取消 / 明确不做


| 事项                                 | 说明                                                                     |
| ---------------------------------- | ---------------------------------------------------------------------- |
| ~~整包平铺迁插件 2~~                      | **2026-09-03 曾改为「分类拷贝抽离、交付步骤留插件 1」（D24-5 窄口）。同日再翻案：插件 1 只留 ⑥，④ 实现迁出（D27）。** |
| Quiet = 退出编辑器                      | 禁止                                                                     |
| 退出默认删 `Assets/Art`                 | 禁止                                                                     |
| 为 Pack 另开一套 ②③⑥                    | 禁止（见 D14）                                                              |
| 用 UnityGLTF 场景 Export 当 `.gltf` 入库 | 禁止（DCC 重导）；入库最多做容器封装，见 [O](#d22-o) / D22                               |
