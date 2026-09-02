# D23 本刀报告（现状）

返回 [总目录](../README.md) · [ctx](./pipeline-job-context.md) · [④ 查封](./pipeline-flatten-capabilities.md) · [相位入参](./pipeline-phase-io.md) · [待办](../03_open-items/backlog.md)

本文：[0](#0-doc-map) · [1](#1-status) · [2](#2-code-structure) · [3](#3-test-run) · [4](#4-notes) · [4-1](#4-1) · [5](#5-remaining)

> **状态（2026-09-02）：本刀已落，编辑器实跑已通。**  
> `直18.gltf` 全流程 `exit=0`；B′ 10 条 → `Assets/Art/Desktop_D001_ZHI18/Desktop_D001_ZHI18`。  
> **D22 封装成 GLB 仍不开发。** `-source` 可直接给 `.gltf`；转 GLB 可选。  
> **下一步不在本刀：** D5 无头验收。D18 管线夹级清空已落（见 [4-1](#4-1)）。

---

## 0. Doc map

**0. 相关文档（总目录定位）**

文档根：[README.md](../README.md)。编号即总目录行。建议顺序：本文 → [4g](./pipeline-job-context.md) → [4i](./pipeline-flatten-capabilities.md) → [4h](./pipeline-phase-io.md) → [D18 d18k](../03_open-items/backlog.md#d18k)。

链接请点下面列表（不要点表格里的链接：编辑器对表内 href 经常不跳）。

- **4j** 本文
- **4g** [pipeline-job-context.md](./pipeline-job-context.md) · 探测扩展 [§7](./pipeline-job-context.md#7-probe-extend)
- **4i** [pipeline-flatten-capabilities.md](./pipeline-flatten-capabilities.md)
- **4h** [pipeline-phase-io.md](./pipeline-phase-io.md)
- **4** [pipeline-flow.md](./pipeline-flow.md)
- **4e** [cli-getting-started.md](./cli-getting-started.md)
- **4f** [op-recognition-and-extend.md](./op-recognition-and-extend.md)
- **2** [overview.md](../02_structure/overview.md) · D23 类见本文 [§2](#2-code-structure)
- **3 A** [backlog 表 A](../03_open-items/backlog.md#a-open-items) · D18 状态一行
- **3 K** [D18 d18k](../03_open-items/backlog.md#d18k)
- **3 O** [backlog `#d22-o`](../03_open-items/backlog.md#d22-o) · D22 搁置
- **3 L** [backlog `#d19-l`](../03_open-items/backlog.md#d19-l) · D19 顶点色
- **3 M** [backlog `#d20-m`](../03_open-items/backlog.md#d20-m) · D20 Prefab 夹观感
- **3 F** [backlog `#d11-f`](../03_open-items/backlog.md#d11-f) · D11 清 Incoming
- **1** [strategy.md](../01_requirements/strategy.md)

④⑥ 架构未变：编排新壳 + 插件 1 旧核。⑥ **不跑**规范化门禁。插件 1 整包迁出仍暂放。

---

## 1. Status

**1. 当前情况（一句话）**

| 项 | 现状 |
|---|---|
| D23a ctx | **已做。** ② 后 `Build`，挂 `PipelineOptions.JobContext`，打日志 |
| D23b 跳过拷贝循环 | **已做。** 不重写循环；`HasExternalUris` 时整段不跑 |
| B′ 原子搬迁 | **已做。** `Art/<名>/<名>/`；remap（D）+ 另存材质（C）仍跑 |
| ② 整包入库 | **已做。** `.gltf` 跟拷相对 URI 伴生（与 Scan 同一套） |
| 编辑器实跑 | **已通**（`直18.gltf` → `exit=0`） |
| D22 | **不开发** |
| D5 无头 | **未验收**（CLI 入口已有；Fix All 不会卡住 CLI） |
| D18 | **管线已落**（夹级清空再写；唯一定位暂放） |

D18 交叉见 [§4](#4-notes)。

---

## 2. Code structure

**2. 代码结构（对应已提问：2.5 / 谁读 ctx / ③ 为何不分支）**

### 2.1 没有用户可见的「2.5 步」

所谓 2.5 **不是步骤开关、不调用③**。② 成功之后 Runner 调 `AttachJobContext` → `PipelineJobContext.Build`，把结果挂在 `PipelineOptions.JobContext` 上。后面的步**自己决定要不要读**。

```text
② ImportSingleModel
    → AttachJobContext（Build + 日志）     ← 观测 / 事实层，不是步骤
    → ③ BuildPrefabs(ModelPaths)          ← 不读 ctx
    → ④ FlattenPaths(FlattenOptions)      ← ★ 当前唯一已接入 ctx 的步
    → ⑤ RunMasterBatch(Art 单元夹)        ← 不读 ctx
    → ⑥ Build(Art Prefab)                 ← 不读 ctx
```

### 2.2 当前只有④从 ctx 拿到参数

Runner 用 `PipelineFlattenBridge.ToFlattenOptions` 把事实映射成平铺开关。**ctx 与 Flatten 类型不互相引用。**

| 步 | 读 ctx？ | 实际吃什么 |
|---|---|---|
| ③ | **否** | `options.ModelPaths` |
| ④ | **是（经 Bridge）** | `HasExternalUris` → `SkipDependencySplit` + 主文件/伴生路径 |
| ⑤ | **否** | Art 单元夹 Collector（文件夹，不是 ctx 文件列表） |
| ⑥ | **否** | Art Prefab 路径 |

### 2.3 ③ 不按 ctx 分支（问题 4，已拍默认）

`MainAssetOk=false` **只打日志**，不在②后 `Fail(20)`。③ 仍直接 `BuildPrefabs`。主资产不是 GameObject → 空列表 → **PrefabFailed(30)**。

与「ctx 在②后主动 `Fail(ImportFailed=20)`」都是整趟停，**结果等价，退出码不同**（30 vs 20）。默认维持 30，③ 不必读 ctx。

多文件：ctx 始终一份（主文件 `ModelPaths[0]`）；以后多源也是串行单文件，只影响导入区展示。

### 2.4 文件地图（相对 `Assets/Plugin/`）

| 层 | 路径 | 干什么 |
|---|---|---|
| 编排 | `Pipeline/Editor/PipelineRunner.cs` | `AttachJobContext`（② 后、③ 前）；④ 调 Bridge |
| 事实 | `Pipeline/Editor/PipelineJobContext.cs` | `Build(primary)`：外 URI、伴生、`MainAssetOk`… |
| 挂载 | `Pipeline/Editor/PipelineOptions.cs` | 字段 `JobContext` |
| 探测 | `Pipeline/Editor/PipelineGltfUriProbe.cs` | 填 ctx；转调 Scan |
| 探测核 | `TOol/Editor/Shared/GltfPackageFiles.cs` | `Scan()`：当前正则 `"uri":"..."`，跳过 `data:`。**换解析器只改这里** |
| 映射 | `Pipeline/Editor/PipelineFlattenBridge.cs` | **仅④用。** 事实 → `RetinarFlattenOptions` |
| ④ 闸 | `RetinarBatchBuilder_Share/.../40_Api/RetinarFlattenOptions.cs` | `SkipDependencySplit` + `ClearDestinationArtFolder` + 主文件/伴生 |
| ④ 门面 | `RetinarBatchBuilder_Share/.../40_Api/RetinarFlattenApi.cs` | 编排只调这个窄口 |
| ④ B | `RetinarBatchModelBuilder.CopyAdjustedPrefabDependencies` | 拷贝循环，**内部未改** |
| ④ B′ | `RetinarBatchBuilder_Share/.../RetinarBatchModelBuilder.AtomicRelocate.cs` | `RelocateAtomicPackage` → `Art/<名>/<名>/` |
| ② 落盘 | `TOol/Editor/Shared/Api/ToolImportApi.cs` | 管线：先清本趟 `Incoming/<三层>/` 再拷（D18）；`CopyGltfSidecarsBeside` |
| 删单元夹 | `TOol/Editor/Shared/AssetUnitFolder.cs` | 只删 `parent/单段`；② Incoming、④ Art 共用 |

**菜单平铺**仍走 `RetinarFlattenOptions.Default`（按后缀拆、**不清 Art**），**不走 B′**。管线④才接 ctx 与 `ClearDestinationArtFolder`。

---

## 3. Test run

**3. 实跑（Desktop_D001_ZHI18 / 直18.gltf）**

| 项 | 结果 |
|---|---|
| 退出码 | **0** |
| ctx | `hasExternalUris=True`，sidecars=9，`mainOk=True`，warnings=0 |
| ③ | IncomingPrefab 1 个 |
| ④ | SkipDependencySplit + B′ **10 条** → `Art/Desktop_D001_ZHI18/Desktop_D001_ZHI18` |
| ⑤ | 贴图 16 无需处理；材质改动 18；**模型命中 0**（gltf 不在 `Model/`，见 [§4](#4-notes)） |
| ⑥ | AB 成功 1 |
| 观感 | `Assets/Art/Desktop_D001_ZHI18` 按包显示正确 |

导入区黄字已改成 Info（可整包入库，转 GLB 可选）。UnityGLTF `linear/sRGB` + Inspector「Fix All」：**只是 Console 警告 + 检视器按钮**，不是 `DisplayDialog`。CLI **不会卡住**。未点 Fix All 仍可出包；偏色另说。

⑥ 里 PBRGraph Shader Graph 警告来自 UnityGLTF 包，不是本刀失败。

---

## 4. Notes

**4. 潜在注意 × 其它工单**

## 4-1

**4.1 D18 · 同路径重名（管线夹级清空）**

管线已落：② 只删 `Incoming/<三层>/`，④ 只删本次 `Art/<名>/`，再按现网拷。不扫整棵 Incoming/Art。菜单 Default 仍 Skip。唯一定位暂放。正文 [D18 d18k](../03_open-items/backlog.md#d18k)。

| 场景 | 后果 |
|---|---|
| 同一任务反复跑同一源 | 槽被清空再写（内容更新） |
| 同名新版本 / 换了 `.bin` 图 | 管线会更新该槽；菜单平铺仍 Skip |
| D22 若改落盘 `.glb` | 落盘扩展名变；仍见 [O](../03_open-items/backlog.md#d22-o) |

### 4.2 ⑤ 扫不到 gltf 模型（和 D19 / D20 / D15 同源）

B′ 把容器放在 `Art/<名>/<名>/`，**不进 `Model/`**（为⑥干净：不要 URI 包进 Model）。ExtractAndBind / 顶点色诊断找的是 `Model/` 下 `t:Model` → **模型数=0**。刷白对 ScriptedImporter 本就会 Skip（[L `#d19-l`](../03_open-items/backlog.md#d19-l)）。不是这次失败。

⑤ 仍按 Art **单元夹** Collector 扫，不消费 ctx 文件列表。以后若要按 ctx 地址处理，另开切片（与 D15 收窄扫描相关，[J `#d15-j`](../03_open-items/backlog.md#d15-j)）。观感见 [M `#d20-m`](../03_open-items/backlog.md#d20-m)。

### 4.3 其它

探测漏检：只改 `GltfPackageFiles.Scan` → [ctx §7](./pipeline-job-context.md#7-probe-extend)。URI 含 `..` / 菜单平铺口径 → [④ 查封](./pipeline-flatten-capabilities.md)。

| 点 | 说明 |
|---|---|
| 探测漏检 | 正则扫 `"uri"`；漏检相对 URI 会误走拷贝循环（拆夹、URI 断） |
| URI 含 `..` | B′ 拒绝跳出原子夹 |
| C / Texture 双份 | 另存 `.mat` 仍可拷一份到 `image/Texture/`，与原子树并存，已接受 |
| 菜单平铺 | 仍按后缀拆，不走 B′ |
| 不要整包把 flatten 迁出插件 1 | 本迭代不做 |

---

## 5. Remaining

**5. 余量（不挡本刀收口）**

- **D18** 管线夹级清空已落；唯一定位仍暂放。  
- ⑤ 仍按文件夹 Collector，不读 ctx。  
- **D5** 无头验收未做。  
- 不要开工：D22、delayCall-before-③、插件 1 flatten 整包迁出、唯一定位。
