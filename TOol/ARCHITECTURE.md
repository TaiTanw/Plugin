# TOol（插件 2）结构说明

版本：1.3.7  
最近同步：2026-08-11（补充「配置归属」；导入设置自动成功日志改为静默）  
适用：Unity 2020.3 Editor；与 `RetinarBatchBuilder_Share`（插件 1）配合使用。

本文说明目录层级、类职责、自动化两层语义，以及和打包工具的边界。便于扩展新 Operation / 新资源类型时对照。

### 配置归属（EditorPrefs vs 三份 SO，必读）

| 数据 | 存哪 | 改哪里 | 作用 | 不做什么 |
|------|------|--------|------|----------|
| **L1 批量扫描路径** | **EditorPrefs**（本机） | 资源处理总面板 | 总/分项批量扫哪些夹 | 不决定能否进 Art；不拦 FBX 入库 |
| **贴图 `excludedPathPrefixes`** | `TextureProcessSettings.asset` | **贴图高级设置 → 子处理配置** | 设置自动 / 后处理自动 **跳过**这些前缀（默认 `Assets/Art/`） | 不拦批量 FBX 拷贝目标 |
| **模型 `excludedPathPrefixes`** | `ModelProcessSettings.asset` | **模型高级设置 → 子处理配置** | 同上（模型侧） | 同上 |
| **`deliveryAlertPathPrefixes`** | `BatchFbxImportSettings.asset` | **批量 FBX 导入**面板 | 导入根/目标落在前缀上 → **Conflict，禁止执行** | 不参与导入后贴图/模型自动跳过 |

**为何分两块（三份列表）：**  
- 批量路径要本机可改、不进版本库 → EP。  
- 「自动流不要碰交付区」与「入库不要写进交付区」语义不同：前者是 *skip process*，后者是 *hard block copy*；故用不同字段名，各挂在自己的 SO。  
- 默认值都是 `Assets/Art/`，**改一处不会自动同步**。若团队改交付根目录，请在 L3 贴图/模型高级设置与批量 FBX 配置里对照改三处。

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
| **L2** | `TextureToolWindow` / `ModelToolWindow` | 范围（选中 / 指定文件夹 / **只读主路径**）+ 本机勾选 Op + 上次结果 | 全 EditorPrefs |
| **L3** | `*AdvancedSettingsWindow` | **子处理配置** + **操作集合配置**（主批量 Op / 导入自动 Op） | SO（与 L1 共用） |

主面板按钮一律 **L1 路径 × L3 Op**；L2 勾选只服务子面板。

---

## 1. 在工程中的角色


|         | 插件 1 `RetinarBatchBuilder_Share` | 插件 2 `TOol`                    |
| ------- | -------------------------------- | ------------------------------ |
| 定位      | 交付打包：平铺 Art、导出 AB/UnityPackage/报告 | 导入期设置 + 源文件/模型后处理              |
| 主菜单     | `Tools > Retinar > 平铺到 Art` / `从 Art 导出交付物`（全部·选中）/ `打开交付文件夹` | `Tools > 资源处理总面板`（唯一入口）        |
| 介入目录    | **写入** `Assets/Art/`**           | **自动流跳过** `Assets/Art/`**（可手动） |
| 改贴图像素？  | 否（只归档告警）；重导时保留已压缩 Art            | 是（压缩 / 转 PNG / 亮度→Alpha）       |
| 改 Mesh？ | 重导时保留已写入顶点色                      | 是（如顶点色全白）                      |


**硬边界（PACKAGING_RULES 规则 33）：**  
两边不得同时改同一 Importer 属性。`Assets/Art/**` 是打包产物区；导入区 FBX 的 `materialLocation=External` 只由插件 2 管；交付区 Model 的 `InPrefab`+`Local` 只由插件 1 管。

---



## 2. 目录树

```text
TOol/
├─ ConfigData/                          # ScriptableObject 实例（进版本库）
│  ├─ TextureProcessSettings.asset
│  ├─ ModelProcessSettings.asset
│  └─ BatchFbxImportSettings.asset      # 导入根 / 交付区警报路径
└─ Editor/
   ├─ Window/
   │  ├─ ResourceProcessWindow.cs       # L1 总面板（路径 + 批量 + 开关）
   │  ├─ BatchFbxImportWindow.cs        # 批量 FBX 入库（独立菜单）
   │  ├─ BatchFbxImportSettings.cs
   │  └─ BatchFbxImportService.cs       # 夹名解析、冲突、单 FBX 拷贝+Import
   ├─ Shared/                           # 跨贴图/模型共用
   │  ├─ ResourceProcessSwitches.cs
   │  ├─ ResourceBatchFolderStore.cs      # L1 共用批量路径（已合并贴图/模型）
   │  ├─ ResourceManualOperationStore.cs  # L2 精准 Op 勾选（Prefs）
   │  ├─ ResourceBatchFolderListGui.cs
   │  ├─ ImportPostProcessScheduler.cs
   │  ├─ ResourceExcludeUtility.cs
   │  └─ AssetPathUtility.cs
   ├─ Texture/
   │  ├─ Config/     TextureProcessSettings.cs
   │  ├─ Codec/      编解码 + 缩放
   │  ├─ Operations/ 接口、注册表、Runner、具体操作
   │  ├─ Import/     设置自动 + 后处理入队
   │  └─ Window/     贴图子面板 + 目标收集
   └─ Model/
      ├─ Config/     ModelProcessSettings.cs
      ├─ Operations/ 接口、注册表、Runner、具体操作
      ├─ Import/     设置自动 + 后处理入队
      └─ Window/     模型子面板 + 目标收集
```

设计原则：**按资源类型纵向切开（Texture / Model），横切能力放 Shared；配置与代码分离（ConfigData 资产 vs Config 类）。**

### 2.1 批量 FBX 导入（入库边界）

菜单：`Tools > 批量FBX导入`（总面板也可打开）。

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

全部存 **EditorPrefs**（本机个人设置，不进版本库）。总面板自上而下：


| 开关                      | 默认     | 作用                                    |
| ----------------------- | ------ | ------------------------------------- |
| **总开关** `MasterEnabled` | **开启** | 关掉后，任何设置自动 / 后处理自动都不跑；**手动**子面板执行不受影响 |
| 贴图 / 模型 · **设置自动**      | 关      | 导入前改 Importer（需总开关开）                  |
| 贴图 / 模型 · **后处理自动**     | **关**   | 导入后跑 Operation（需总开关开）。**仅导入区预览**；交付靠平铺后手动 |


代码里用有效组合判断，勿只读分项：

- `IsTextureSettingsEffective` = Master && TextureSettingsAuto  
- `IsTexturePostProcessEffective` / `IsModelSettingsEffective` / `IsModelPostProcessEffective` 同理


| 分项含义      | 时机              | 谁执行                                   | 典型事                              |
| --------- | --------------- | ------------------------------------- | -------------------------------- |
| **设置自动**  | `OnPreprocess`* | `*ImportSettingsProcessor`            | 贴图关 Read/Write；模型 External、剔灯剔相机 |
| **后处理自动** | 导入后 `delayCall` | `ImportPostProcessScheduler` → Runner | 压缩超标；顶点色全白（**不覆盖 Art**）         |


**设置自动建议保留**（导入区 Importer 行为需要）。**后处理自动默认关 + UI 标明「仅导入区」**：内嵌贴图压缩、Art 顶点色与贴图两遍同类，平铺前跑了易误以为交付已成功；管线代码保留，不删。

后处理要真正跑起来，需要同时满足：

1. **总开关**打开；
2. 该类「后处理自动」打开；
3. Settings 的 `importAutoOperationIds` 勾选了具体操作 Id；
4. 路径 **不在** `excludedPathPrefixes`（默认 `Assets/Art/`）；
5. 对贴图：还不在 `.fbm` 内（压缩会 Skip）。

**导入后处理自动阶段顺序：模型 → 贴图**（为以后「材质驱动贴图派生」预留；v1 不做拖拽排序）。  
**平铺后手动总批量顺序：贴图 → 模型**（先压 Art 贴图，再写顶点色；避免贴图收尾冲掉 Mesh）。

```text
模型导入结束（时序，导入区自动）
  ├─ OnPreprocessModel          设置自动（External / 剔灯等）
  ├─ OnPostprocessModel         后处理：用 ImportRoot 层级 Mesh 写顶点色
  │                             （此时 LoadAllAssetsAtPath 常为空，不能只用库路径）
  ├─ 抽出 .fbm 贴图 → OnPreprocessTexture …
  └─ OnPostprocessAllAssets → delayCall
        ├─ RunModelPhase   LoadAllAssetsAtPath 再刷一遍（补全未挂到 Renderer 的 Mesh）
        │                  只 SaveAssets，不 Refresh
        └─ RunTexturePhase 只 SaveAssets，不 Refresh（Refresh 会重导 FBX 冲顶点色）
```

平铺后手动（总面板「按批量路径执行全部」）：

```text
贴图批量（压 Art/Texture 等）→ 只 SaveAssets
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
| `BakeLuminanceToAlphaOperation`                         | 亮度写入 Alpha（玻璃/裁切类需求）                                  |


**新增贴图操作：** 在 `Operations/` 实现 `ITextureAssetOperation` + 无参构造 → 自动出现在子面板；若要导入自动跑，把 `Id` 填进 Settings 的 `importAutoOperationIds`。

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
| `ModelImportSettingsProcessor` | 设置自动：`materialLocation=External`、`materialName=BasedOnMaterialName`、剔灯光摄像机等；**跳过 Art**。 |
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
| `ResourceProcessWindow` | **L1**：总开关 + 共用批量路径 + 总批量（贴图→模型）+ 分项扫描/执行（路径×`masterBatchOperationIds`）+ 打开 L2。 |


旧菜单（独立「贴图处理工具」「SwitchManager」等）已移除，避免双入口行为不一致。

总面板批量执行：**始终**读 `ResourceBatchFolderStore`（主路径）+ `*OperationRegistry.GetMasterBatchOperations`（SO），与 L2 勾选无关。

---



## 9. 与插件 1 协作的常用流程

### 9.0 边界（已确认）

| | 插件 2 `TOol` | 插件 1A 平铺 Art | 插件 1B 交付输出 |
|--|--|--|--|
| 独立到其它工程 | **必须可以** | Retinar 约定 | Retinar 约定 |
| 生成 `Assets/Art` | **否** | **是** | 否（只读 Art） |
| 改像素/顶点色 | **是** | 否 | 否 |
| 导入自动含平铺 | **否** | — | — |

**不要**把平铺并进插件 2。**不要**指望导入区自动顶点色随 `CopyAsset(FBX)` 进入 Art。

```text
（可选）批量 FBX 导入 → 导入区（插件 2 设置/后处理自动）
  → 人工调材质并保存 Prefab（改名为交付名）
  → 插件 1「平铺到 Art」
  → 插件 2 手动/总面板（平铺后：先压 Art 贴图 → 再刷顶点色）
  → 插件 1「从 Art 导出交付物」（全部 / 选中 Prefab）
```

已移除一键 Batch Build。总面板批量 = **L1 共用路径** + **L3 masterBatchOperationIds**；**平铺后顺序为贴图→模型**。

### 9.1–9.3 摘要

- 外置贴图：导入区可压 → 1A → 1B。
- 内嵌贴图：1A 落到 Art/Texture 再压；勿压 `.fbm`。
- 顶点色：1A 后刷 **Art/Model**（或 Prefab，会解析到 FBX）；再导 GLB。

### 9.4 批量范围

子面板范围三选一：**当前选中** / **指定文件夹** / **依据文件路径批量**（多文件夹列表，EditorPrefs）。已移除 WholeProject。  
总面板批量：只用 L1 共用路径；L2 可用「使用主面板批量路径」只读同一列表，或用选中/指定文件夹做精准根。

阈值：`maxSourceMegabytes` ≤ 5。

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
3. 若改的是 Mesh 子资产：文档写明「FBX 重导会丢」；交付区依赖插件 1 的保留逻辑或导入后处理。



### 新资源类型（如 Animation）

建议复制 `Model/` 或 `Texture/` 整棵纵切 + Shared 增加开关与 Scheduler 阶段；总面板加一块。不要把逻辑塞进现有贴图/模型类。

---

