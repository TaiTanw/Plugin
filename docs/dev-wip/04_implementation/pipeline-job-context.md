# 导入信息 ctx：同一管线、按能力选操作

返回 [总目录](../README.md) · [流程](./pipeline-flow.md) · [相位入参/返回值](./pipeline-phase-io.md) · [待办](../03_open-items/backlog.md)

> **状态：D23a/b + B′ 已落；D26-2 已把 glTF 缺伴生收成 typed `MissingUris` 与④失败闸。OBJ 缺件不进该闸。** 当前入口与谁读 ctx → [结构总览](../02_structure/overview.md)；[D23报告](./d23-slice-report.md)为历史切片。探测扩展见 [§7](#7-probe-extend)。\
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
| 2–3 之间 | **不加**用户可见相位。`Build` 不是步骤；**仅编排**读 ctx 并译成 FlattenPlan。① 不承担 `Build`，不为无头排错扩字段（见 [D24 R3](../03_open-items/d24-boundary-plan.md#r3-flatten-split)） |

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
| JSON 写了相对 URI，但伴生缺失 | 仍 `true` | **不要**当成单文件包。写入 typed `MissingUris`，同时保留 Warning 给人看；④在 Begin 前失败 |

所以第 3 点 = **闸跟物理布局走，不跟产品格式名走**。全内嵌 gltf 与 GLB 共用「可拆后缀」这条能力，不是给 gltf 开第三套管线。

缺文件 ≠ 无外 URI。`HasExternalUris` 仍为 true；`MissingUris` 是独立损坏事实，并由④映射为 `FlattenFailed(40)`。

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

当前组合（2026-09-14）：

```text
要平铺？     = options.RunFlatten          ← 目的
禁止拆文件？ = ctx.HasExternalUris         ← 事实
另存 .mat？ = 开了④，两分支都跑 C；MaterialForm 仅观测，不开闸
FBX Extract = 目的（开了④）× 事实（ImporterKind == ModelImporter）
```

**不要**在 ctx 里放 `FlattenFileMode`（那是目的/策略名，应由 Runner 从上面两行推出来）。

现网处理区地址仍是编排接力路径（`ModelPaths` / `prefabPaths` / Art 单元夹），不是 ctx。ctx 第一刀只**描述**归类，不改这些入参。

---

## 4. 平铺薄分支：按能力组合，不按格式分叉

管线④现由 `PipelineRunner.FlattenPerPrefab` 通过 `ToolFlattenApi` 组合七步；`CreatePackagedAdjustedPrefab` 是遗产整相位路径，不再是管线入口。底层仍叠有：

| 能力（薄） | 当前实现 | 当前谁闸 |
|---|---|---|
| **A. 写 Art Prefab** | 拷/另存 Prefab、Unpack 嵌套 | `RunFlatten` |
| **B. 按后缀拆依赖** | `GetDependencies` → `ResolveRelativeFolder` → `Model/` `Texture/` … | **仅** `!HasExternalUris` |
| **B′. 原子搬迁** | 已实现：主文件 + `SidecarPaths` 保持相对布局 | **仅** `HasExternalUris` |
| **C. 另存 Renderer `.mat`** | `CopyPrefabRendererMaterials` | 开④即可；与拆不拆文件正交 |
| **D. 重映射引用** | 拷完改 Prefab/材质指向 | 有拷就做 |
| **E. ModelImporter Extract/Bind** | 对 ModelImporter（含 FBX/OBJ）执行；ScriptedImporter 跳过 | `ImporterKind == ModelImporter` |

**薄分支可行**：不要 `if gltf / if fbx / if glb` 三套平铺。  
B 与 B′ **互斥**，唯一开关是 `HasExternalUris`。A/C/D 两条路都跑。E 只认 Importer 事实。

实际顺序是 **Begin → B/B′ → E? → D → C → Finish**。外 URI 包不跑 B，ScriptedImporter 跳过 E；全内嵌包走 B。A/C/D 是能力分类，不代表执行顺序。

**历史切片：D23a 当时只记 ctx；后续 D23b/B′ 已落地。** 不能再把“不做 B′”当当前约定。

「拷贝循环」= `CopyAdjustedPrefabDependencies` 里按后缀把每个依赖拷到不同 Art 子夹的 `for`。D23a 不碰它；D23b 只在循环外用 `HasExternalUris` 决定跑 B 还是 B′，**不重写循环内部**。查封表 → [pipeline-flatten-capabilities](./pipeline-flatten-capabilities.md)。

---

## 5. ctx 会不会让 ③④⑤ 立刻改成「精确文件地址」？

**当前不会。** ctx 只为④提供事实，不替换③模型路径或⑤ Art 单元 Collector。

| 步 | 现网地址从哪来 | ctx 以后能提供什么 | 本刀 |
|---|---|---|---|
| ③ | `ModelPaths`（已是文件列表，不扫夹） | `MainAssetOk` 断言 | 最多日志 |
| ④ | `prefabPaths`；B 用 GetDependencies、B′用主文件+sidecar | SidecarPaths/HasExternalUris 已使用 | B/B′已落地 |
| ⑤ | `PostProcessFolderPaths` **按夹 Collector 扫**（贴图/模型/材质各一套） | 曾提议只动 ctx 地址，但未确认；本次透明修复不走此方案 | **仍扫 Art 单元** |

⑤ 现在「按配置扫区域所有文件」是 L1 总批量语义，中间层只是代调。把⑤收成「只处理 ctx 地址」只是未采纳的旧提议，不是必做切片；入库 ctx 地址也不等于重映射后的 Art 地址。透明判断由 Material OP 读取每个材质自身状态，不扩 PipelineJobContext。

---

## 6. 字段（D23a 收口）

### 6.1 第一刀就有（事实 + 健康）

| 字段 | 类型（示意） | 含义（事实） |
|---|---|---|
| `PrimaryAssetPath` | `string` | ② 成功后的主文件 |
| `SourceExtension` | `string` | 供探测分派/日志；人工入口还会验证 .gltf。B/B′的选择本身仍只认 HasExternalUris |
| `ImporterKind` | `ModelImporter` / `ScriptedImporter` / `Unknown` | `AssetImporter.GetAtPath` |
| `HasExternalUris` | `bool` | ④ 拆文件**唯一闸**。见 §2 |
| `SidecarPaths` | `List<string>` | 相对主文件解析到的 `.bin` / 外图等（可空） |
| `MissingUris` | `List<string>` | **当前仅 glTF 探针写入**：JSON 已声明但磁盘不存在的 URI；④直接据此失败。OBJ/FBX/GLB 的 `Build` 不填此项 |
| `MainAssetOk` | `bool` | 主资产能加载为 GameObject |
| `MaterialForm` | `SubAssetOnly` / `HasStandaloneMat` / `Unknown` | 依赖里有没有独立 `.mat` 文件 |
| `Warnings` | `List<string>` | 展示诊断，不参与控制流；缺伴生虽也留 Warning，但实际闸只读 `MissingUris` |

启发式（可被磁盘/JSON 推翻）：

```text
.fbx / .obj  → HasExternalUris=false, ImporterKind=ModelImporter
.glb         → HasExternalUris=false, ImporterKind=ScriptedImporter
.gltf        → 先看 JSON URI + 伴生；全 data: 且无伴生 → false；有相对 URI → true
```

### 6.2 结构化失败与 Warnings

Build 时：**仅** `.gltf` 把缺伴生同时写入 `MissingUris` 与 Warning。Runner 不因普通 Warning 自动 Fail，但会在④ Begin 前读取 `MissingUris` 并返回 40、停止整趟④；不得解析 Warning 文案。

`.obj` / `.fbx` 在 `Build` 里把 `HasExternalUris=false` 后返回，**不调用** `ObjPackageFiles.Scan`。OBJ 缺 `.mtl`/贴图只在① `CopyObjSidecarsBeside` 打 Warning，ctx 的 `MissingUris` 仍为空，④ 40 闸不会咬。详见 [backlog · obj-vs-gltf](../03_open-items/backlog.md#obj-vs-gltf)。

| 建议码/文案方向 | 何时 |
|---|---|
| 缺伴生（已接闸） | glTF JSON 相对 URI 指向的 `.bin`/图磁盘上没有；进入 `MissingUris`，④ 失败 40 |
| OBJ 缺 `.mtl`/贴图 | ① Warning；**当前不进** `MissingUris`，可能白膜且 `exit=0` |
| JSON 不可用 | `.gltf` 读失败 / 非对象 |
| 主资产空 | Import 声称成功但 `LoadMainAssetAtPath` 不是 GO（应与 `MainAssetOk=false` 同时出现） |
| 启发式被推翻 | 后缀像单文件，但扫到外 URI 或伴生 |
| 零字节 / 未注册 Importer | 文件在但无法作为模型导入 |
| 声明了外 URI 但 Sidecar 列表空 | 需区分确实没有 sidecar 与缺失 URI；当前缺件检查和 B′完整性检查已实现 |

损坏是事实，失败映射归中间层。glTF MissingUris 已接④40，不是将来才开发；OBJ 缺件与其它 Warnings 不能一概视为已接失败闸。

### 6.3 不要放进 ctx

| 不要 | 原因 |
|---|---|
| `RunFlatten` / `RunPostProcess` / `RunPrefab` | 目的，在 Options |
| `FlattenFileMode` | 策略名；由 Runner 用「目的 × HasExternalUris」推导 |
| `triggeredByImport` | 导入钩子执行态 |
| `ShouldBakeShader` | ⑤ 看 Shader 名 |
| 可配置「识别分支 ID」 | 硬编码填标志即可 |

`EmbeddedTexturesRemainInContainer` 是历史备选字段，未实现也未确认为本轮需求；不能据此扩 ctx。

---

## 7. Probe extend

**7. 切片状态** · 探测如何扩展

| 刀 | 状态 |
|---|---|
| **D23a** | **已做** `PipelineJobContext.Build` + 日志 |
| **D23b** | **已做** 跳过拷贝循环 |
| **B′** | **已做** `RelocateAtomicPackage` → `Art/<名>/<名>/`；② 入库顺带拷伴生 |
| **④ 退出码** | glTF MissingUris 在 Begin 前触发 40 并停止整趟；主资产不可用通常在③产物为空时先 30，Build 本身不设置退出码 |

### glTF 探测如何扩展（② 后 / ③ 前）

```text
PipelineJobContext.Build
    → 扩展名 .gltf
    → PipelineGltfUriProbe.Apply(ctx)
         → GltfPackageFiles.Scan(磁盘路径)     ★ 只改这里
         → 填 HasExternalUris / SidecarPaths / MissingUris / Warnings
```

| 改法 | 文件 |
|---|---|
| 换解析器（buffers[]/images[]） | 改 `GltfPackageFiles.Scan` 转调新方法，或在 `Scan` 里先新后旧 |
| 保留正则、再补一层 | `PipelineGltfUriProbe.Apply` 在 Scan 之后合并结果 |
| ② 伴生拷 | 已用同一 `Scan`（`ToolImportApi.CopyGltfSidecarsBeside`） |

不要在 Flatten 里扫 JSON。④ 只消费 FlattenOptions 里已经填好的路径。

OBJ 若将来要进同一闸：在 `Build` 的 `.obj` 分支调用 `ObjPackageFiles.Scan` 并写入 `MissingUris`，不要让④自己扫 `.mtl`。是否升闸与 B 质量闸一并在 R3 后评估。

人工操作不经过②.5：ManualFlattenService 点击时对 Assets 模型或 Prefab 模型依赖建立自身 ctx，再完整复用到 Finish；这不是每分步重新解析。自动与人工当前有两处相位组合，详见[整体结构](../02_structure/overview.md)。
