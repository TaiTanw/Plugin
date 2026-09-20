# TOol（插件 2）结构说明

历史架构版本：1.3.9（并非当前发布号）\
最近同步：2026-09-15（步骤 12 删除旧平铺链；现用七步、SO 和⑤边界保持）\
适用：Unity 2020.3 / 2022.3 Editor；与 `RetinarBatchBuilder_Share`（插件 1）配合使用。

本文说明目录层级、类职责、自动化两层语义，以及和打包工具的边界。便于扩展新 Operation / 新资源类型时对照。  
当前④入口为 `ToolFlattenApi.Run(plan)`；人工不建立 ctx。现约：[开发者须知](../docs/dev-wip/README.md)。加 Op / `AllowMasterBatch`：[op-recognition-and-extend](../docs/dev-wip/04_implementation/op-recognition-and-extend.md)。

### 配置归属（EditorPrefs 与 SO，必读）

| 数据 | 存哪 | 改哪里 | 作用 | 不做什么 |
|------|------|--------|------|----------|
| **L1 批量扫描路径** | **EditorPrefs**（本机） | 资源处理总面板 | 总/分项批量扫哪些夹 | 不决定能否进 Art；不拦 FBX 入库 |
| **贴图 `excludedPathPrefixes`** | `TextureProcessSettings.asset` | **贴图高级设置 → 子处理配置** | 设置自动 / 后处理自动 **跳过**这些前缀（默认 `Assets/Art/`） | 不拦批量 FBX 拷贝目标 |
| **模型 `excludedPathPrefixes`** | `ModelProcessSettings.asset` | **模型高级设置 → 子处理配置** | 模型策略/后处理自动排除；Art 模型设置另有硬跳过，Incoming 基线例外 | 不控制显式⑤ |
| **`deliveryAlertPathPrefixes`** | `BatchFbxImportSettings.asset` | **批量选择器** | 导入根/目标落在前缀上 → **Conflict，禁止执行** | 不参与导入后贴图/模型自动跳过 |
| **人工④平铺细节** | `TOol/ConfigData/Manual/**` 下的 `FlattenOperationSettings` SO | 平铺操作面板，可拖入同目录 SO | 分类、清本次 Art 单元、根碰撞体 | 不控制管线④ |
| **管线④平铺细节** | `Pipeline/ConfigData/**` 下的同类 SO | 自动化管线总面板 | 同上；运行前冻结为 policy | 不被人工面板改写 |

**现存配置（不等于后续全部保留 EditorPrefs 的决定）：**

- 当前批量路径存本机 EP；后续 SO 化是已确认方向，具体迁移范围尚未全部实施；**L1 默认种子含 `Assets/Art`**，总批量本就是打交付区。\
- 「**导入期自动**不要改交付区 Importer」与「**入库不要拷进交付区**」语义不同：前者是 *skip process*（`excludedPathPrefixes`），后者是 *hard block copy*（`deliveryAlertPathPrefixes`）；故用不同字段名。  
- **切勿**把 `excludedPathPrefixes` 理解成「⑤ 也扫不到 Art」——⑤ `RunMasterBatch` **不读**该列表。  
- **切勿**把中间层⑤也叫成「导入自动流」。管线勾选⑤ = 编排代调 L1「执行全部」同一内核（手动路径），不是 `AssetPostprocessor`。  
- 默认值都是 `Assets/Art/`，**改一处不会自动同步**。若团队改交付根目录，除三份配置还须核对 FlattenBuildSettings.ArtRoot / RetinarPaths.ArtRoot 两个常量；当前不是一处改完全同步。
- 平铺人工/管线 SO 用**来源文件夹**判定归属。所在面板只能编辑自己的目录；另一作用域或未归类目录只读，但可按本趟快照执行。

导入期 Console 常见信息（可忽略与否）：

| 信息 | 来源 | 是否失败 | 建议 |
|------|------|----------|------|
| （旧）`*ImportSettingsProcessor] 已按…自动处理` | 插件 2 Info | 否 | 已改为静默；看到残留是旧脚本域 |
| `Can't calculate tangents… doesn't contain normals` | Unity 网格导入 | 否（警告） | 入库可忽略；若场景光照/法线贴图异常，回 DCC 补法线再导 |
| `.fbm` 下贴图被设置自动处理 | 内嵌材质贴图抽出 | 否 | 正常；与「只拷 FBX、旁路 Textures/ 不拷」无关 |

### 面板三层（降复杂度）

| 层 | 窗口 | 内容 | 数据 |
|----|------|------|------|
| **L1** | `ResourceProcessWindow` | 共用批量路径 + 总/分项执行·扫描；自动化开关 | 路径：EditorPrefs；Op：`masterBatchOperationIds`（SO） |
| **L2** | `TextureToolWindow` / `MaterialToolWindow` / `ModelToolWindow` | 范围（选中 / 指定文件夹 / **只读主路径**）+ 本机勾选 Op + 上次结果 | 全 EditorPrefs |
| **L3** | `*AdvancedSettingsWindow` | **子处理配置** + **操作集合配置**（主批量 Op / 导入自动 Op） | SO（与 L1 共用） |

主面板按钮一律 **L1 路径 × L3 Op**；L2 勾选只服务子面板。

---

## 1. 在工程中的角色


|         | 插件 1 `RetinarBatchBuilder_Share` | 插件 2 `TOol`                    |
| ------- | -------------------------------- | ------------------------------ |
| 定位      | 交付打包：导出 AB / 可选 UnityPackage | 导入期设置 + 源文件/模型后处理 + ③ Prefab；**④ 物理文件在此，编排暂归中间层** |
| 主菜单     | `Tools > Retinar > 批量汇总`（平铺转调插件 2）/ `打开交付文件夹` | `Tools > 资源处理总面板`（资源总入口，另有批量导入/③菜单）        |
| 介入目录    | ⑥ 读 Art Prefab 打 AB，不写平铺 | **④ 写入** `Assets/Art/`；导入期自动跳过 Art Importer；**⑤/L1 总批量故意打 Art** |
| 改贴图像素？  | 否；旧归档报告已删            | 是（压缩 / 转 PNG / 亮度→Alpha）       |
| 改 Mesh？ | 不负责写 Mesh；重导冲色见 D19                      | 是（如顶点色全白）                      |


**硬边界（现行 PACKAGING_RULES「Importer 分区」/ D24-6）：**  
两边不得同时改同一 Importer 属性。赋值集中在 `ModelImporterProfiles`：`ApplyIncoming*` 只写导入区，`ApplyArtDelivery` 只写 `Assets/Art/**`。插件 2 的 `OnPreprocessModel` **硬跳过 Art**（不单靠 SO 排除表）。交付区 `InPrefab`+`Local` 只由 ④ `FlattenBuildService` 调用 `ApplyArtDelivery`。

---



## 2. 目录树

```text
TOol/
├─ ConfigData/                          # ScriptableObject 实例（进版本库）
│  ├─ TextureProcessSettings.asset
│  ├─ MaterialProcessSettings.asset       # ⑤共用材质 SO，尚未分人工/管线
│  ├─ Manual/FlattenOperationSettings.asset # 人工④；管线实例在 Pipeline/ConfigData
│  ├─ ModelProcessSettings.asset
│  └─ BatchFbxImportSettings.asset      # 导入根 / 交付区警报路径
└─ Editor/
   ├─ Window/
   │  ├─ ResourceProcessWindow.cs       # L1 资源处理总面板（⑤ 子流程编排入口）
   │  ├─ BatchFbxImportWindow.cs        # 批量选择器（Tools / 管线 [1] 同一窗）
   │  ├─ BatchFbxImportSettings.cs
   │  └─ BatchFbxImportService.cs       # 夹名解析、冲突、单 FBX 拷贝+Import
   ├─ Shared/                           # 横切工具 / 已有对外窄口（见 Shared/README_SHARED.md）
   │  └─ （根下历史扁平：开关/批量路径/导入后调度…）
   ├─ Generated/                        # 中间资产能力（非⑤原地改）
   │  ├─ Prefab/                        # ★ ③ 自动预设体：Config / Layout / Service
   │  └─ Flatten/                       # ★ ④ 平铺 Art：Config / Layout / Service / Category
   │                                      窄口 ToolFlattenApi（接 ctx）
   ├─ Texture/
   │  ├─ Config/     TextureProcessSettings.cs
   │  ├─ Codec/      编解码 + 缩放
   │  ├─ Operations/ 接口、注册表、Runner、具体操作
   │  ├─ Import/     设置自动 + 后处理入队
   │  └─ Window/     贴图子面板 + 目标收集
   ├─ Material/                         # ⑤：Config / Operations / Window
   └─ Model/
      ├─ Config/     ModelProcessSettings.cs
      ├─ Operations/ 接口、注册表、Runner、具体操作
      ├─ Import/     设置自动 + 后处理入队 + ModelImporterProfiles（D24-6 两档口径）
      └─ Window/     模型子面板 + 目标收集
```

设计原则：**按资源类型纵向切开（Texture / Material / Model）；横切放 Shared；中间资产写盘放 Generated；配置与代码分离。**

### 2.0 面板与编排分层（勿与 Plugin 级流程编排混数据）

| 层 | 谁决定 | 数据 |
|---|---|---|
| **流程编排**（已有 `Plugin/Pipeline`） | 是否跑②③④⑤⑥、quiet、结果映射；旧门禁已删 | PipelineStepSettings SO / PipelineOptions |
| **L1 资源处理总面板** | ⑤ **子流程**：跑哪些资源类型批量、路径、导入后开关；对外暴露 PostOps 入口 | EP 路径/开关 + L3 的 master Op 列表 |
| **L2 贴图/材质/模型子面板** | 当前编辑器临时范围 + 本机勾选 Op | 当前 Prefs；单项 SO 化方向已确认，除平铺外尚未全面迁移 |
| **L3 高级设置** | 压缩程度等参数、主批量/导入自动 Op 集合 | Texture/Material/Model SO |

**共享的是 API，不是同一块 UI 状态。** 流程编排勾选⑤时调用 L1 能力；不把步骤开关写进 `ResourceProcessSwitches`。

### 2.1 批量选择器（入库边界）

菜单：`Tools > 批量选择器`（与管线 [1]「打开批量选择器」同一窗口）。

| 做 | 不做 |
|----|------|
| 拖外部文件夹递归找 `.fbx`；面板标重名/已存在/交付区冲突 | 自动建 Prefab / 改交付名 |
| 夹名 = 自身向上 3 层目录名用 `_` 拼接；不足 3 层 → 全路径消毒名（Warning，不拦执行） | 平铺 Art / 导出交付物 |
| 无冲突时统一执行；每条 = 建夹→拷 FBX→Import | 拷外置旁路贴图（v1） |
| 单条移除 / 移除全部冲突；标题标注 FBX 文件名 | 把导入夹名当成交付 `asset_id` |
| 取消：当前 FBX 整段完成后再停 | |

交付文件名仍以人工改好的 Prefab 名为准（插件 1 规则 12）。

---



## 3. 自动化开关（必读）

本节的导入自动开关仍存 **EditorPrefs**，不是所有管线/单项配置都存这里。总步骤/平铺已走 SO。总面板自上而下：


| 开关                      | 默认     | 作用                                    |
| ----------------------- | ------ | ------------------------------------- |
| **总开关** `MasterEnabled` | **开启** | 关掉后，用户设置自动 / 后处理自动都不跑；**手动**子面板执行不受影响。配置导入根内的模型安全基线例外 |
| 贴图 / 模型 · **设置自动**      | 关      | 导入前改 Importer（需总开关开）                  |
| 贴图 / 模型 · **后处理自动**     | **关**   | 导入后跑 Operation（需总开关开）。**仅导入区预览**；交付靠平铺后手动 |


代码里用有效组合判断，勿只读分项：

- `IsTextureSettingsEffective` = Master && TextureSettingsAuto  
- `IsTexturePostProcessEffective` / `IsModelSettingsEffective` / `IsModelPostProcessEffective` 同理


| 分项含义      | 时机              | 谁执行                                   | 典型事                              |
| --------- | --------------- | ------------------------------------- | -------------------------------- |
| **设置自动**  | `OnPreprocess`* | `*ImportSettingsProcessor`            | 贴图关 Read/Write；模型材质来源 External |
| **后处理自动** | 导入后 `delayCall` | `ImportPostProcessScheduler` → Runner | 压缩超标；顶点色全白（**仅导入区；exclude Art**）         |

**模型设置自动分两档（D24-1/2/6）。** 赋值都在 `ModelImporterProfiles`。`ModelImportSettingsProcessor` 只闸导入区；④ `ApplyModelImportSettings` 只调 `ApplyArtDelivery`。

| 档 | 项 | 开关来源 | 为何这样分 |
|---|---|---|---|
| **导入区基线** | 剔灯剔相机、`.obj` 法线 Calculate | `ModelProcessSettings`（SO）；在 `BatchFbxImportSettings.importRootPath` 内不受本机总闸与 `excludedPathPrefixes` | 管线产物形状不能由本机 Prefs 决定。相机灯光必须早于 ③ 剔掉；例外不扩到其它 `Assets` 路径 |
| **导入区策略** | `materialLocation = External` | SO + 本机「模型 · 设置自动」勾选 | 会让 Incoming 旁生成 `Materials/`，改变 ④ 的输入。默认关（D24-3） |
| **交付区** | InPrefab + Local + isReadable + 剔灯剔相机 + OBJ Calculate | 无开关，④ 必写 | PACKAGING_RULES 20/21/37；与导入区策略互斥 |


**设置自动建议保留**（导入区 Importer 行为需要）。**后处理自动默认关 + UI 标明「仅导入区」**：内嵌贴图压缩、Art 顶点色与贴图两遍同类，平铺前跑了易误以为交付已成功；管线代码保留，不删。

交付区（Art）的压图/刷白走下面「平铺后手动总批量」；中间层⑤只是代跑这一段，**不要**为了管线自动再把 Op 塞进 `OnPostprocessModel` 打 Art。

后处理要真正跑起来，需要同时满足：

1. **总开关**打开；
2. 该类「后处理自动」打开；
3. Settings 的 `importAutoOperationIds` 勾选了具体操作 Id；
4. 路径 **不在** `excludedPathPrefixes`（默认 `Assets/Art/`）；
5. 对贴图：还不在 `.fbm` 内（压缩会 Skip）。

**导入后处理自动阶段顺序：模型 → 贴图**（为以后「材质驱动贴图派生」预留；v1 不做拖拽排序）。  
**平铺后手动总批量顺序：贴图 → 材质 → 模型**（先压 Art 贴图，再写顶点色；避免贴图收尾冲掉 Mesh）。

```text
模型导入结束（时序，导入区自动）
  ├─ OnPreprocessModel          设置自动（基线：剔灯剔相机 / OBJ 法线；策略：External）
  ├─ OnPostprocessModel         后处理：用 ImportRoot 层级 Mesh 写顶点色
  │                             （此时 LoadAllAssetsAtPath 常为空，不能只用库路径）
  ├─ 抽出 .fbm 贴图 → OnPreprocessTexture …
  └─ OnPostprocessAllAssets → delayCall
        ├─ RunModelPhase   LoadAllAssetsAtPath 再刷一遍（补全未挂到 Renderer 的 Mesh）
        │                  只 SaveAssets，不 Refresh
        └─ RunTexturePhase 只 SaveAssets，不 Refresh（Refresh 会重导 FBX 冲顶点色）
```

平铺后手动（总面板「按批量路径执行全部」；**中间层⑤ = 代调同一入口**）：

```text
贴图批量（压 Art 下按后缀递归到的贴图，常见 image/Texture）→ 只 SaveAssets
  → 模型批量（顶点色全白等）→ 只 SaveAssets
```

---



## 4. 层级与数据流（概念图）

```text
                    ┌─────────────────────────┐
                    │  ResourceProcessWindow  │  总开关 + 批量路径执行 + 打开子面板
                    └───────────┬─────────────┘
              ┌─────────────────┴─────────────────┐
              ▼                                   ▼
     TextureToolWindow                     ModelToolWindow
     （勾选操作 / 选中·文件夹·路径批量）      （同上）
              │                                   │
              ▼                                   ▼
     TextureOperationRunner                ModelOperationRunner
              │                                   │
     ITextureAssetOperation*               IModelAssetOperation*
              │
              ▼
     TextureCodecRegistry → ITextureFileCodec
```

手动与自动 **共用同一 Runner**；区别只是 `TriggeredByImport` 与目标列表来源（窗口收集 vs Scheduler 队列）。

---



## 5. Shared 层（类与职能）


| 类                              | 职能                                                                   |
| ------------------------------ | -------------------------------------------------------------------- |
| `ResourceProcessSwitches`      | **总开关** + 四路分项（EditorPrefs）。提供 `Is*Effective` 供 Import/Scheduler 门控。 |
| `ImportPostProcessScheduler`   | 导入区后处理调度：入队、delayCall、防重入 `IsRunning`、**模型→贴图**两阶段（与平铺后总批量顺序不同）。 |
| `ResourceBatchFolderStore`     | **L1 共用**批量路径（EditorPrefs）；旧贴图/模型两套列表一次性合并。空列表默认含 `Assets/Art`。 |
| `AssetOperationEvaluation`     | Op 统一评估结果：`NotApplicable` / `Skip` / `NeedsWork` + Reason。 |
| `AssetOperationScanSummary`    | 「仅扫描」汇总（需处理行列表）。 |
| `ResourceManualOperationStore` | **仅 L2** 手动勾选 Operation（EditorPrefs）。主面板不读。 |
| `ResourceBatchFolderListGui`   | L1 可编辑列表；L2 只读主路径展示。 |
| `ResourceExcludeUtility`       | 根据 Settings 里的前缀列表判断路径是否排除。                                          |
| `AssetPathUtility`             | 资产路径 ↔ 磁盘路径、文件长度、是否在 `.fbm` 内等。                                      |


---



## 6. Texture 纵切



### 6.1 Config


| 类                        | 职能                                                                                                                                 |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `TextureProcessSettings` | 阈值、POT、JPG/TGA/亮度参数、`importAutoOperationIds`、`masterBatchOperationIds`、Importer 开关、排除目录。资产：`ConfigData/TextureProcessSettings.asset`。 |




### 6.2 Codec（只做字节 ↔ 像素，不碰业务）


| 类                                                         | 职能                |
| --------------------------------------------------------- | ----------------- |
| `ITextureFileCodec`                                       | 编解码接口             |
| `PngTextureCodec` / `JpgTextureCodec` / `TgaTextureCodec` | 各格式实现（TGA 含 RLE）  |
| `TextureCodecRegistry`                                    | 反射发现 Codec，按扩展名查找 |
| `TextureScaler`                                           | 等比缩放像素            |




### 6.3 Operations（扩展点）


| 类                                                       | 职能                                                    |
| ------------------------------------------------------- | ----------------------------------------------------- |
| `ITextureAssetOperation`                                | 扩展接口：`Id` / `DisplayName` / **`Evaluate`** / `CanProcess`(=NeedsWork) / `Execute`  |
| `TextureOperationRegistry`                              | 反射发现全部 Operation；按 Settings 筛「导入自动」集合                 |
| `TextureOperationContext`                               | 当前资产路径、Settings、进度回调、是否导入触发                           |
| `TextureOperationResult` / `TextureOperationRunSummary` | 成功/跳过/失败 + 批量汇总                                       |
| `TextureOperationRunner`                                | **Evaluate** 筛工作项、进度条、Execute；**`Scan` dry-run**；有改动只 **SaveAssets，禁止 Refresh** |
| `ShrinkTextureSourceOperation`                          | **压缩超标源文件**（`shrink_source_file`）。Evaluate 跳过 `.fbm`/已达标。二的幂走对折阶梯。 |
| `ConvertTgaToPngOperation`                              | TGA → PNG                                             |
| `BakeLuminanceToAlphaOperation`                         | 亮度写入 Alpha。`AllowMasterBatch=false`：仅 L2。Evaluate 对适用文件一律 NeedsWork |


**新增贴图操作：** 实现 `ITextureAssetOperation`。无条件全池 NeedsWork → `AllowMasterBatch=false`。要进⑤/L1/导入自动须为 true 并把 `Id` 勾进对应 SO 列表。

### 6.4 Import


| 类                                | 职能                                                                          |
| -------------------------------- | --------------------------------------------------------------------------- |
| `TextureImportSettingsProcessor` | `AssetPostprocessor`：设置自动时改 TextureImporter（如关 Read/Write）；尊重排除目录与开关。       |
| `TextureSourceFileProcessor`     | `OnPostprocessAllAssets`：收集贴图路径 → `EnqueueTexturePaths`（Scheduler 跑时忽略自触发）。 |




### 6.5 Window


| 类                        | 职能                                    |
| ------------------------ | ------------------------------------- |
| `TextureToolWindow`      | **L2** 精准：范围 + 本机 Op + 结果；底部开 L3。 |
| `TextureAdvancedSettingsWindow` | **L3**：子处理配置 / 操作集合（主批量·导入自动）。 |
| `TextureTargetCollector` | 选中 / 单文件夹 / 只读主路径；`CollectFromBatchFolders` 供 L1。 |


---



## 7. Model 纵切



### 7.1 Config


| 类                      | 职能                                                                                                                            |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `ModelProcessSettings` | External、剔灯、扩展名、`importAutoOperationIds`、`masterBatchOperationIds`、排除目录。资产：`ConfigData/ModelProcessSettings.asset`。 |




### 7.2 Operations


| 类                                                                              | 职能                                                                                                                  |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| `IModelAssetOperation`                                                         | 模型扩展接口；**`Evaluate(path, settings, importRoot)`** 与扫描/Runner 共用                                                                                                       |
| `ModelOperationRegistry` / `ModelOperationRunner` / Context / Result / Summary | 与贴图侧同构；Runner 含 **`Scan`**                                                                                                              |
| `SetVertexColorsWhiteOperation`                                                | **顶点色全白**。Evaluate 探测非全白 Mesh；Art 自动流不进；手动对 Art/Model 执行后，打包工具重导时会保留顶点色。 |


**新增模型操作：** 同贴图——实现接口即可被反射发现。

### 7.3 Import


| 类                              | 职能                                                                                      |
| ------------------------------ | --------------------------------------------------------------------------------------- |
| `ModelImporterProfiles` | D24-6 两档口径。`ApplyIncomingBaseline` / `ApplyIncomingPolicy` / `ApplyArtDelivery`；`IsArtDeliveryPath` 给 Processor 硬跳过。 |
| `ModelImportSettingsProcessor` | 设置自动闸。Art 硬跳过 → 扩展名 → 基线（配置导入根内绕过总闸/排除表）；未排除且总闸与分项都开才跑 Incoming 策略。 |
| `ModelSourceFileProcessor`     | `OnPostprocessModel` 立刻跑 importAuto；`OnPostprocessAllAssets` 入队 Scheduler。              |




### 7.4 Window


| 类                      | 职能                    |
| ---------------------- | --------------------- |
| `ModelToolWindow`      | **L2** 精准面板 |
| `ModelAdvancedSettingsWindow` | **L3** 高级设置 |
| `ModelTargetCollector` | 选中 / 单文件夹 / 只读主路径；Prefab→FBX |


---



## 8. 总面板


| 类                       | 职能                                                                 |
| ----------------------- | ------------------------------------------------------------------ |
| `ResourceProcessWindow` | **L1**：总开关 + 共用批量路径 + 总批量（贴图→材质→模型）+ 分项扫描/执行（路径×`masterBatchOperationIds`）+ 打开 L2。 |


旧菜单（独立「贴图处理工具」「SwitchManager」等）已移除，避免双入口行为不一致。

总面板批量执行：**始终**读 `ResourceBatchFolderStore`（主路径）+ `*OperationRegistry.GetMasterBatchOperations`（SO），与 L2 勾选无关。

---



## 9. 与插件 1 协作的常用流程

### 9.0 当前边界（2026-09-14）

④文件物理位于 TOol；消费 PipelineJobContext 的 API/服务与相位编排暂归中间层，不能再写“平铺仍留插件 1/不要迁到插件 2”。当前直接依赖 ctx，也不能宣称整个 TOol 已可无 Pipeline 独立复制。

```text
入库② → 每模型 ctx → Prefab③ → 完整④（B/B′）
 → 插件2⑤：贴图→材质→模型
 → 插件1⑥：RetinarAbApi.Build（AB / 可选UP）
```

人工④有普通平铺/原子迁移两个完整相位按钮；直接选模型（含 FBX）先③再 `Run(plan)`，不再 SafeZone。平铺 SO 已分人工/管线并冻结 Policy；其它资源配置尚未全部分离。

插件 1 目标只负责输出格式；旧规范化导出、成品直达、门禁、全套报告已删除。④ **不再**写 AB 标签（步骤 8）；⑥ 用 `AssetBundleBuild[]`。

⑤ Material：Collector → MaterialProcessSettings → Registry → Runner → NormalizeDeliverableShaderOperation。透明模式由 OP 在换 Shader 前捕获、换后还原，不扩模型 ctx；当前 Standard 映射不等于任意 URP Shader 支持。类级数据流与验证状态见[整体结构](../docs/dev-wip/02_structure/overview.md)。

### 9.1–9.3 贴图与顶点色

- 内嵌贴图先④落 Art，再⑤压副本，最后⑥；可一次管线连续完成，不要求两次人工跑。
- 不压 .fbm 缓存；复用 Art 时保护已压副本，清单元重建则是明确重新生成。
- 白顶点 GLB 与 AB 不是同一验收：人工刷 Art Model 后避免重导再导 GLB，D19不作 CLI 门禁。

### 9.4 批量范围

子面板范围三选一：**当前选中** / **指定文件夹** / **依据文件路径批量**（多文件夹列表，EditorPrefs）。已移除 WholeProject。  
总面板批量：只用 L1 共用路径；L2 可用「使用主面板批量路径」只读同一列表，或用选中/指定文件夹做精准根。

阈值由当前 TextureProcessSettings 决定；旧导出报告的 5MB 告警线已删除，不能据历史文档新增上限要求。

---

## 10. 扩展清单（抄作业用）

### 新贴图格式

1. 实现 `ITextureFileCodec`（无参构造）→ 自动进 Registry。
2. 确认 `Shrink*` 等 Operation 只依赖 Registry，无需改窗口。



### 新贴图业务操作

1. `Operations/XxxOperation.cs` 实现 `ITextureAssetOperation`。
2. 需要自动：Id 写入 `TextureProcessSettings.importAutoOperationIds`。
3. 需要排除 Art/`.fbm`：在 **`Evaluate`**/`Execute` 里用 Settings / `AssetPathUtility`（扫描与执行同口径）。
4. 新增判断条件时优先写进该 Op 的 `Evaluate`，不要只写在 `Execute` 里，否则「仅扫描」会漏报。



### 新模型业务操作

1. 实现 `IModelAssetOperation`。
2. Id 写入 `ModelProcessSettings.importAutoOperationIds`（若要自动）。
3. 若改的是 Mesh 子资产：写明「FBX 重导会丢」；不能依赖已删除的插件 1 导出修复兜底，也不能把⑤改成 Art 导入后自动。见 D19。



### 新资源类型（如 Animation）

建议复制 `Model/` 或 `Texture/` 整棵纵切 + Shared 增加开关与 Scheduler 阶段；总面板加一块。不要把逻辑塞进现有贴图/模型类。

---
