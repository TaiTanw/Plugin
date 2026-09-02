# ⑤ Op：扩展名识别与扩展方式（归档）

返回 [总目录](../README.md) · [待办](../03_open-items/backlog.md) · [TOol 结构](../../../TOol/ARCHITECTURE.md)

> 原散落在 backlog **G**、[d13-glb-magenta](../03_open-items/d13-glb-magenta.md)「总面板如何认识操作」、Codec 源码注释。本文收口成一份。  
> **D12 已做：** 模型默认 `.fbx/.glb/.gltf`；L3 只读展示。无 codec 口径不齐见 backlog **D21**。

---

## 1. 识别：谁决定「这个文件进不进⑤」

总批量先按 **L1/D17 扫描根递归找文件**，再用扩展名（或 Unity 类型）过滤。未知文件夹名一般无妨；**未注册后缀会静默跳过**。④ 平铺的 `Unknown/` 分类表与⑤ **不是同一套**。

| 大类 | 进总列表靠什么 | 改哪里 | 单 Op 再筛（见 §2） |
|---|---|---|---|
| **贴图** | `t:Texture2D` ∩ `TextureCodecRegistry.IsSupported` | **加 Codec 类**即加后缀；L3「资源识别」只读 | Evaluate：体积 / `.tga` / `.fbm` 等 |
| **模型** | `t:Model` + Prefab 依赖 ∩ `supportedExtensions` | **改该 SO 列表**；L3 只读 | 刷白：非 `ModelImporter`（如 glb）Evaluate **Skip** |
| **材质** | `t:Material` 且路径 `.mat` | **不用后缀表** | Op 按 Shader 名 / 白名单 Skip |

② 导入另有一套：`ToolImportApi` 写死 `.fbx/.glb/.gltf/.obj`，与模型 Op 的 SO 列表独立。  
**`.gltf`：** 扩展名认；② 整包拷（JSON + 相对 URI 伴生）；④ 有外 URI 时原子搬迁。转 GLB **可选**（D22 不开发）。见 backlog **O**。

L3 窗口：`ResourceRecognitionGui`（贴图 Codec 探测、模型 SO 列表、材质说明）。**不做专用后缀编辑器。**

---

## 2. 文件怎么流：总可处理集合 → Op 再筛 → 执行

**不是**「每个 Op 自己扫一遍磁盘」。**也不是**「Collector 列出什么就无条件 Execute」。

三层共用同一模式：**双筛选 + 筛选与执行分离**。

```text
扫描根（⑤=D17 Art 单元；L1 手动=Prefs 批量路径）
    │
    ▼  第一筛 · Collector（大类总可处理文件，与勾了哪些 Op 无关）
贴图 / 材质 / 模型 各一份路径列表
    │
    ▼  本轮 Op 子集 · Registry.GetMasterBatchOperations（L3 勾选，按 Order）
    │
    ▼  第二筛 · Runner：勾选 Op × 总列表，逐对 Evaluate
仅 NeedsWork → PendingWork(Op, path)
NotApplicable / Skip → 不进执行、不算 Failed
    │
    ▼  执行 · 只跑 PendingWork
Execute 仍可 Changed / Skip / Failed（D16 只吃 Failed）
```

| 说法 | 实际 |
|---|---|
| **双筛选** | Collector 定「这类文件」；Evaluate 定「这个 Op 要不要动这份」 |
| **筛选 / 执行分离** | `Evaluate` 与「仅扫描」共用；`Execute` 不再当第一道筛。材质 Op 进 Execute 后还会再 Evaluate 一次防竞态 |
| **直接全部处理？** | **默认否。** 只有某 Op 的 Evaluate 对池内几乎全是 NeedsWork 时，看起来像全跑（见贴图「亮度→Alpha」） |
| **笛卡尔积** | 同一文件可被多个 Op 依次处理（按 `Order`）。例：超标 `.tga` 可先压图再转 PNG |

导入期自动流（通道 1）另有 **exclude 前缀**（默认跳过 `Assets/Art/`），**不经过** 上面的 Collector；⑤ / L1 总批量 **不读** exclude。

### 2.1 第一筛：各大类「总可处理文件」

都在扫描根下递归。未知文件夹名一般无妨；未注册后缀 / 非 `.mat` **进不了列表**（静默）。

| 大类 | Collector | Unity 粗查 | 再收成「总列表」的条件 | ⑤ 列表为空时 |
|---|---|---|---|---|
| **贴图** | `TextureTargetCollector` | `FindAssets("t:Texture2D")` | `TextureCodecRegistry.IsSupported`（现有 Codec：`.png` `.jpg/.jpeg` `.tga`） | 「没有命中贴图」 |
| **材质** | `MaterialTargetCollector` | `FindAssets("t:Material")` | 路径以 `.mat` 结尾（丢掉内嵌材质等非文件） | 「没有命中 .mat」 |
| **模型** | `ModelTargetCollector` | `t:Model`，以及 `t:Prefab` 的 **GetDependencies** | `ModelProcessSettings.IsSupportedModelExtension`（默认 `.fbx/.glb/.gltf`） | 「没有命中模型」 |

模型多一步：**Prefab 夹里往往只有 `.prefab`**，Mesh 在依赖的 FBX 上。Collector 会把 Prefab 依赖里命中后缀的模型 **展开进总列表**（去重）。Op 仍写在 FBX/GLB 上，不写 `.prefab` 文件。

L2 子面板同一套 Collector，只是 Scope 换成「当前选中 / 指定夹」；进 Runner 之后与⑤相同。

### 2.2 第二筛：子 Op 怎么用这份总列表

Runner **不会**按 Op 再收集一遍。每个勾选 Op 都看完整份总列表，自己 `Evaluate`：

| Eligibility | 含义 | 进 Execute？ | D16 50？ |
|---|---|---|---|
| `NotApplicable` | 类型/扩展名不对 | 否 | 否 |
| `Skip` | 适用但已达标 / 策略不碰 | 否 | 否 |
| `NeedsWork` | 要改 | 是 | 仅当随后 Execute=`Failed` |

**贴图（共用 Codec 总列表，各 Op 再切子集）**

| Op | 相对总列表怎么筛 | 接近「全跑」？ |
|---|---|---|
| 压缩超标源文件 `shrink_source_file` | 无 Codec / 找不到文件 → NA；`.fbm` 内嵌缓存 → Skip；体积已 ≤ 阈值 → Skip；超标 → NeedsWork | 否，按体积 |
| TGA→PNG `convert_tga_to_png` | 非 `.tga` → NA；同目录已有同名 png → Skip | 否，只动 TGA |
| 亮度→Alpha `bake_luminance_to_alpha` | 无 Codec / 空文件 → NA；`.fbm` → Skip；**其余一律 NeedsWork**（不做像素探测，防误伤靠「别乱勾进主批量」） | **是**（勾了就会处理池内几乎全部贴图） |

**材质（总列表=根下全部 `.mat`）**

| Op | 相对总列表怎么筛 |
|---|---|
| 规范化交付 Shader | 非 `.mat` / 加载不到 → NA；已是目标 Shader 或白名单 → Skip；源子串命中或不在白名单 → NeedsWork |

**模型（总列表=后缀命中的 FBX/GLB/gltf，含 Prefab 依赖展开）**

| Op | 相对总列表怎么筛 |
|---|---|
| 顶点色全白 | 后缀不支持 → NA；非 `ModelImporter`（如 glb ScriptedImporter）→ **Skip**（文件已在总列表里，本 Op 仍不执行）；已全白 / 暂无 Mesh → Skip；有非白 Mesh → NeedsWork |

因此：**GLB 会出现在模型总可处理列表里**（D12），但刷白 Op 会 Skip——这是第二筛，不是 Collector 漏了。

### 2.3 加新 Op 时怎么接这条流

不要自己再写一套 `FindAssets`。接到现有 Collector 总列表，在 `Evaluate` 里切子集：

- 只要池内某类文件 → Evaluate 对它们返回 NeedsWork，其余 NA/Skip。
- 几乎要处理池内全部（像亮度→Alpha）→ 写清楚，并默认 **不要** 勾进 `masterBatchOperationIds` / 导入自动。
- 需要一种 Collector 还不认的后缀 → 先加 Codec（贴图）或改 SO 列表（模型），否则文件进不了总列表，Evaluate 永远看不到。

---

## 3. 扩展：加 Op vs 加格式 vs 加大类

**两层不要混。**

| 层 | 反射？ | 现状 |
|---|---|---|
| 资源大类（贴图 / 材质 / 模型） | **否** | 总面板写死三块 + Prefs `MasterBatchInclude*`。顺序：贴图 → 材质 → 模型 |
| 大类内的 Op | **是** | `*OperationRegistry` 扫 `I*AssetOperation` 无参构造；L3 勾进 `masterBatchOperationIds` / `importAutoOperationIds` |

### 已有大类里加一个 Op（例：再压一种图）

```text
新建 class XxxOperation : ITextureAssetOperation（无参 ctor）
  → 反射进 Registry.All
  → L3「操作集合」勾进 masterBatchOperationIds
  → 总批量 / 管线⑤下次会跑（不必改总面板按钮）
```

模型 / 材质同理，分别实现 `IModelAssetOperation` / `IMaterialAssetOperation`。

### 贴图加一种扩展名

```text
新建 class XxxTextureCodec : ITextureFileCodec
  → TextureCodecRegistry 反射发现
  → IsSupported(".xxx") 为 true，收集器能扫到
  → 压图/亮度等 Op 才能 Evaluate 到该文件
```

只加 Op、不加 Codec：新后缀仍然进不了贴图批量。

### 模型加一种扩展名

改 `ModelProcessSettings.supportedExtensions`（SO）。不要只改某个 Op 里的硬编码。

### 加一个全新资源大类（已做过：材质）

```text
1. IXxxAssetOperation + Registry（反射）
2. Collector + Runner
3. ConfigData/*ProcessSettings.asset（含 masterBatchOperationIds）
4. 总面板新一块 + MasterBatchIncludeXxx Prefs
5. ResourcePostProcessService.RunMasterBatch 接入
6. 管线⑤仍只调同一 RunMasterBatch，不必新窄口
```

**反射只扩展「某大类里有哪些 Op」。总面板有几块按钮必须手接线。**

---

## 4. 相关代码

| 点 | 路径 |
|---|---|
| ⑤ 编排（三层顺序） | `TOol/Editor/Shared/ResourcePostProcessService.cs` |
| 贴图收集 / 模型收集 / 材质收集 | `*TargetCollector.cs` |
| Runner（Evaluate→Pending→Execute） | `*OperationRunner.cs` |
| Evaluate 三态 | `TOol/Editor/Shared/AssetOperationEvaluation.cs` |
| 贴图 Codec | `TOol/Editor/Texture/Codec/TextureCodecRegistry.cs`、`ITextureFileCodec.cs` |
| 模型后缀 SO | `TOol/Editor/Model/Config/ModelProcessSettings.cs` |
| L3 只读识别 | `TOol/Editor/Shared/ResourceRecognitionGui.cs` |
| Op 注册 | `TOol/Editor/*/Operations/*OperationRegistry.cs` |
| 结构总览 | `TOol/ARCHITECTURE.md` |
