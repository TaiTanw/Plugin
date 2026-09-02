# 导入信息 ctx：同一管线、按能力选操作

返回 [总目录](../README.md) · [流程](./pipeline-flow.md) · [相位入参/返回值](./pipeline-phase-io.md) · [待办](../03_open-items/backlog.md)

> **状态：D23a/b + B′ 已落；gltf 编辑器实跑已通。** 现状与谁读 ctx → [d23-slice-report](./d23-slice-report.md)。探测扩展见 [§7](#7-probe-extend)。  
> ④ 查封 → [pipeline-flatten-capabilities](./pipeline-flatten-capabilities.md)。

---

## 1. 已拍板

| 项 | 结论 |
|---|---|
| 管线 | 仍是 ②③④⑤⑥；**不为 gltf 另开一条** |
| ctx 是什么 | **中间层事实：文件归类**。描述这次导入包「有什么、能不能拆、Importer 是谁」 |
| Options / SO 是什么 | **目的**：本趟要不要跑 ③④⑤⑥。不写进 ctx |
| ④ 拆文件唯一闸 | **`HasExternalUris`**。执行层不 `if (.gltf)` |
| D22 | **不开发**。`.gltf` 可直接入库；转 GLB 可选 |
| 2–3 之间 | **不加**用户可见相位。`Build` 不是步骤；**仅④**经 Bridge 读 ctx |

**刻意不等 delayCall 再③：** 后处理自动可关、开着会再导入抢资产、CLI 常在 `delayCall` 前 `Exit`。交付处理走⑤。

---

## 2. 核对第 3 点怎么理解

原问：`.gltf` 无伴生、全 `data:` URI 时，`HasExternalUris=false`，按 GLB 整文件拷——可否？

**闸问的不是「后缀是不是 gltf」，而是「按后缀把文件拆到不同夹会不会拆坏相对 URI」。**

| 包长什么样 | `HasExternalUris` | 含义 |
|---|---|---|
| `.glb`（二进制自包含） | `false` | 现网：整文件进 `Model/`，内嵌图本来就不是独立贴图后缀 |
| `.gltf` 且 JSON 里 buffer/image 全是 `data:`、磁盘无 `.bin`/外图 | `false` | **物理上已是单文件**，和 GLB 同类。走现网「按后缀分类拷」时，可拆的只有这一份 `.gltf` → `Model/`，没有旁路文件可被拆走 |
| `.gltf` + 相对 URI（有 `.bin` / 外 png 等） | `true` | **禁止**把 json / bin / png 按后缀拆到 `Model/` `Texture/` `Unknown/` |
| JSON 写了相对 URI，但伴生缺失 | 仍 `true` | **不要**当成单文件包。补 `Warnings`（缺文件 / 可能损坏），让人评估；不要为了「能拆」把闸打成 false |

所以第 3 点 = **闸跟物理布局走，不跟产品格式名走**。全内嵌 gltf 与 GLB 共用「可拆后缀」这条能力，不是给 gltf 开第三套管线。

缺文件 ≠ 无外 URI。缺的是损坏事实（Warnings），闸仍是 true。

---

## 3. 两份数据：事实 vs 目的（必须分开）

处理区（③④⑤）现网已经有两条输入，职责不能混：

```text
PipelineJobContext     ← 事实（文件归类、健康警告）
PipelineOptions / SO   ← 目的（本趟开哪些步）
        ↓
   Runner 组合：目的决定调不调窄口；事实收窄「怎么调」
```

| | ctx（事实） | Options / 步骤 SO（目的） |
|---|---|---|
| 问的是 | 这次包**是什么样** | 这次任务**想做什么** |
| 例子 | 有没有外 URI、伴生路径、Importer 种类、主资产能否加载、材质是否已是独立 `.mat` | `RunPrefab` / `RunFlatten` / `RunPostProcess` |
| 禁止 | 存 `RunFlatten`、存「走哪条产品线」、存 `FlattenFileMode` 当命令 | 靠后缀或「像 gltf」去改开关语义 |
| ④ 拆文件 | 只提供 `HasExternalUris` | 只提供「要不要平铺」 |

组合（以后④真正改行为时）：

```text
要平铺？     = options.RunFlatten          ← 目的
禁止拆文件？ = ctx.HasExternalUris         ← 事实
另存 .mat？ = 目的（开了④）× 事实（MaterialForm 仍是内嵌）
FBX Extract = 目的（开了④）× 事实（ImporterKind == ModelImporter）
```

**不要**在 ctx 里放 `FlattenFileMode`（那是目的/策略名，应由 Runner 从上面两行推出来）。

现网处理区地址仍是编排接力路径（`ModelPaths` / `prefabPaths` / Art 单元夹），不是 ctx。ctx 第一刀只**描述**归类，不改这些入参。

---

## 4. 平铺薄分支：按能力组合，不按格式分叉

现网④是一条厚流程（`CreatePackagedAdjustedPrefab`），内部已经叠了几件独立的事：

| 能力（薄） | 现网在干什么 | 以后谁闸 |
|---|---|---|
| **A. 写 Art Prefab** | 拷/另存 Prefab、Unpack 嵌套 | `RunFlatten` |
| **B. 按后缀拆依赖** | `GetDependencies` → `ResolveRelativeFolder` → `Model/` `Texture/` … | **仅** `!HasExternalUris` |
| **B′. 原子搬迁** | （未做）主文件 + `SidecarPaths` 保持相对布局 | **仅** `HasExternalUris` |
| **C. 另存 Renderer `.mat`** | `CopyPrefabRendererMaterials` | 开④即可；与拆不拆文件正交 |
| **D. 重映射引用** | 拷完改 Prefab/材质指向 | 有拷就做 |
| **E. ModelImporter Extract/Bind** | FBX 才有效；GLB `as ModelImporter` 空转 | `ImporterKind == ModelImporter` |

**薄分支可行**：不要 `if gltf / if fbx / if glb` 三套平铺。  
B 与 B′ **互斥**，唯一开关是 `HasExternalUris`。A/C/D 两条路都跑。E 只认 Importer 事实。

gltf 外 URI 包 = A + B′ + C + D（不跑 B，避免拆坏 URI）。  
全内嵌 gltf / GLB = A + B + C + D（B 实际只搬走容器文件，效果等于整文件进 `Model/`）。  
FBX = A + B + C + D + E。

**D23a 不做 B′，也不改 B。** 只把事实记进 ctx。

「拷贝循环」= `CopyAdjustedPrefabDependencies` 里按后缀把每个依赖拷到不同 Art 子夹的 `for`。D23a 不碰它；D23b 只在循环外用 `HasExternalUris` 决定跑 B 还是 B′，**不重写循环内部**。查封表 → [pipeline-flatten-capabilities](./pipeline-flatten-capabilities.md)。

---

## 5. ctx 会不会让 ③④⑤ 立刻改成「精确文件地址」？

**不会。第一刀不做。** 归类有了，处理区改解析要一步步来。

| 步 | 现网地址从哪来 | ctx 以后能提供什么 | 本刀 |
|---|---|---|---|
| ③ | `ModelPaths`（已是文件列表，不扫夹） | `MainAssetOk` 断言 | 最多日志 |
| ④ | `prefabPaths`；内部 `GetDependencies` 再按后缀分夹 | `SidecarPaths` 作原子白名单；拆文件认 `HasExternalUris` | 不改拷贝循环 |
| ⑤ | `PostProcessFolderPaths` **按夹 Collector 扫**（贴图/模型/材质各一套） | 将来可改为「只动 ctx 归类出的 `.mat` / 模型 / 伴生图」 | **仍扫 Art 单元** |

⑤ 现在「按配置扫区域所有文件」是 L1 总批量语义，中间层只是代调。把⑤收成「只处理本趟 ctx 列出的地址」是后续切片（可与 D15 单单元范围一起想），**不绑在 D23a**。

---

## 6. 字段（D23a 收口）

### 6.1 第一刀就有（事实 + 健康）

| 字段 | 类型（示意） | 含义（事实） |
|---|---|---|
| `PrimaryAssetPath` | `string` | ② 成功后的主文件 |
| `SourceExtension` | `string` | 只供日志 / 填启发式；**执行不 if 后缀** |
| `ImporterKind` | `ModelImporter` / `ScriptedImporter` / `Unknown` | `AssetImporter.GetAtPath` |
| `HasExternalUris` | `bool` | ④ 拆文件**唯一闸**。见 §2 |
| `SidecarPaths` | `List<string>` | 相对主文件解析到的 `.bin` / 外图等（可空） |
| `MainAssetOk` | `bool` | 主资产能加载为 GameObject |
| `MaterialForm` | `SubAssetOnly` / `HasStandaloneMat` / `Unknown` | 依赖里有没有独立 `.mat` 文件 |
| `Warnings` | `List<string>` | **非失败**。供人评估包是否残缺/损坏；默认不改 ExitCode |

启发式（可被磁盘/JSON 推翻）：

```text
.fbx / .obj  → HasExternalUris=false, ImporterKind=ModelImporter
.glb         → HasExternalUris=false, ImporterKind=ScriptedImporter
.gltf        → 先看 JSON URI + 伴生；全 data: 且无伴生 → false；有相对 URI → true
```

### 6.2 Warnings 建议（评估损坏，不是步骤开关）

Build 时写入、Runner 打日志即可。第一刀 **不** 因 Warning 自动 Fail（残缺是否挡③由你以后另拍）。

| 建议码/文案方向 | 何时 |
|---|---|
| 缺伴生 | JSON 相对 URI 指向的 `.bin`/图磁盘上没有 |
| JSON 不可用 | `.gltf` 读失败 / 非对象 |
| 主资产空 | Import 声称成功但 `LoadMainAssetAtPath` 不是 GO（应与 `MainAssetOk=false` 同时出现） |
| 启发式被推翻 | 后缀像单文件，但扫到外 URI 或伴生 |
| 零字节 / 未注册 Importer | 文件在但无法作为模型导入 |
| 声明了外 URI 但 Sidecar 列表空 | 闸为 true 却搬不了白名单，下一刀 B′ 会缺输入 |

损坏是**事实**；要不要停管线是**目的**（以后才接到 Options / ExitCode）。第一刀只让 Console 看得见。

### 6.3 不要放进 ctx

| 不要 | 原因 |
|---|---|
| `RunFlatten` / `RunPostProcess` / `RunPrefab` | 目的，在 Options |
| `FlattenFileMode` | 策略名；由 Runner 用「目的 × HasExternalUris」推导 |
| `triggeredByImport` | 导入钩子执行态 |
| `ShouldBakeShader` | ⑤ 看 Shader 名 |
| 可配置「识别分支 ID」 | 硬编码填标志即可 |

`EmbeddedTexturesRemainInContainer` 可第二刀再加（提醒⑤压图 0 命中）；不是拆文件闸。

---

## 7. Probe extend

**7. 切片状态** · 探测如何扩展

| 刀 | 状态 |
|---|---|
| **D23a** | **已做** `PipelineJobContext.Build` + 日志 |
| **D23b** | **已做** 跳过拷贝循环 |
| **B′** | **已做** `RelocateAtomicPackage` → `Art/<名>/<名>/`；② 入库顺带拷伴生 |
| **④ 退出码** | `MainAssetOk=false` 维持 ③→30 |

### glTF 探测如何扩展（② 后 / ③ 前）

```text
PipelineJobContext.Build
    → 扩展名 .gltf
    → PipelineGltfUriProbe.Apply(ctx)
         → GltfPackageFiles.Scan(磁盘路径)     ★ 只改这里
         → 填 HasExternalUris / SidecarPaths / Warnings
```

| 改法 | 文件 |
|---|---|
| 换解析器（buffers[]/images[]） | 改 `GltfPackageFiles.Scan` 转调新方法，或在 `Scan` 里先新后旧 |
| 保留正则、再补一层 | `PipelineGltfUriProbe.Apply` 在 Scan 之后合并结果 |
| ② 伴生拷 | 已用同一 `Scan`（`ToolImportApi.CopyGltfSidecarsBeside`） |

不要在 Flatten 里扫 JSON。④ 只消费 FlattenOptions 里已经填好的路径。
