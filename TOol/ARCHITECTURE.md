# TOol（插件 2）结构说明

版本：1.1  
最近同步：2026-07-31（总开关 MasterEnabled）  
适用：Unity 2020.3 Editor；与 `RetinarBatchBuilder_Share`（插件 1）配合使用。

本文说明目录层级、类职责、自动化两层语义，以及和打包工具的边界。便于扩展新 Operation / 新资源类型时对照。

---

## 1. 在工程中的角色


|         | 插件 1 `RetinarBatchBuilder_Share` | 插件 2 `TOol`                    |
| ------- | -------------------------------- | ------------------------------ |
| 定位      | 交付打包（Art 整理、AB、UnityPackage、报告）  | 导入期设置 + 源文件/模型后处理              |
| 主菜单     | `Tools > Retinar > …`            | `Tools > 资源处理总面板`（唯一入口）        |
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
│  └─ ModelProcessSettings.asset
└─ Editor/
   ├─ Window/
   │  └─ ResourceProcessWindow.cs       # 总面板（菜单唯一入口）
   ├─ Shared/                           # 跨贴图/模型共用
   │  ├─ ResourceProcessSwitches.cs
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

---



## 3. 自动化开关（必读）

全部存 **EditorPrefs**（本机个人设置，不进版本库）。总面板自上而下：


| 开关                      | 默认     | 作用                                    |
| ----------------------- | ------ | ------------------------------------- |
| **总开关** `MasterEnabled` | **开启** | 关掉后，任何设置自动 / 后处理自动都不跑；**手动**子面板执行不受影响 |
| 贴图 / 模型 · **设置自动**      | 关      | 导入前改 Importer（需总开关开）                  |
| 贴图 / 模型 · **后处理自动**     | 关      | 导入后跑 Operation（需总开关开）                 |


代码里用有效组合判断，勿只读分项：

- `IsTextureSettingsEffective` = Master && TextureSettingsAuto  
- `IsTexturePostProcessEffective` / `IsModelSettingsEffective` / `IsModelPostProcessEffective` 同理


| 分项含义      | 时机              | 谁执行                                   | 典型事                              |
| --------- | --------------- | ------------------------------------- | -------------------------------- |
| **设置自动**  | `OnPreprocess`* | `*ImportSettingsProcessor`            | 贴图关 Read/Write；模型 External、剔灯剔相机 |
| **后处理自动** | 导入后 `delayCall` | `ImportPostProcessScheduler` → Runner | 压缩超标；顶点色全白                       |


后处理要真正跑起来，需要同时满足：

1. **总开关**打开；
2. 该类「后处理自动」打开；
3. Settings 的 `importAutoOperationIds` 勾选了具体操作 Id；
4. 路径 **不在** `excludedPathPrefixes`（默认 `Assets/Art/`）；
5. 对贴图：还不在 `.fbm` 内（压缩会 Skip）。

**阶段顺序固定：模型 → 贴图**（为以后「材质驱动贴图派生」预留；v1 不做拖拽排序）。

```text
模型导入结束（时序）
  ├─ OnPreprocessModel          设置自动（External / 剔灯等）
  ├─ OnPostprocessModel         后处理：用 ImportRoot 层级 Mesh 写顶点色
  │                             （此时 LoadAllAssetsAtPath 常为空，不能只用库路径）
  ├─ 抽出 .fbm 贴图 → OnPreprocessTexture …
  └─ OnPostprocessAllAssets → delayCall
        ├─ RunModelPhase   LoadAllAssetsAtPath 再刷一遍（补全未挂到 Renderer 的 Mesh）
        │                  只 SaveAssets，不 Refresh
        └─ RunTexturePhase 若 Refresh 导致 FBX 重导 → 再进 OnPostprocessModel 补刷
```

---



## 4. 层级与数据流（概念图）

```text
                    ┌─────────────────────────┐
                    │  ResourceProcessWindow  │  总开关 + 打开子面板
                    └───────────┬─────────────┘
              ┌─────────────────┴─────────────────┐
              ▼                                   ▼
     TextureToolWindow                     ModelToolWindow
     （勾选操作 / 选目标 / 手动跑）         （同上）
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


| 类                            | 职能                                                                   |
| ---------------------------- | -------------------------------------------------------------------- |
| `ResourceProcessSwitches`    | **总开关** + 四路分项（EditorPrefs）。提供 `Is*Effective` 供 Import/Scheduler 门控。 |
| `ImportPostProcessScheduler` | 后处理唯一调度：入队、delayCall、防重入 `IsRunning`、模型→贴图两阶段。                       |
| `ResourceExcludeUtility`     | 根据 Settings 里的前缀列表判断路径是否排除。                                          |
| `AssetPathUtility`           | 资产路径 ↔ 磁盘路径、文件长度、是否在 `.fbm` 内等。                                      |


---



## 6. Texture 纵切



### 6.1 Config


| 类                        | 职能                                                                                                                                 |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `TextureProcessSettings` | 阈值（默认 5MB）、POT 策略、JPG 质量、TGA/亮度→Alpha 参数、`importAutoOperationIds`、Importer 开关、排除目录。资产路径：`ConfigData/TextureProcessSettings.asset`。 |




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
| `ITextureAssetOperation`                                | 扩展接口：`Id` / `DisplayName` / `CanProcess` / `Execute`  |
| `TextureOperationRegistry`                              | 反射发现全部 Operation；按 Settings 筛「导入自动」集合                 |
| `TextureOperationContext`                               | 当前资产路径、Settings、进度回调、是否导入触发                           |
| `TextureOperationResult` / `TextureOperationRunSummary` | 成功/跳过/失败 + 批量汇总                                       |
| `TextureOperationRunner`                                | 筛工作项、进度条、调用 Execute、打汇总日志                             |
| `ShrinkTextureSourceOperation`                          | **压缩超标源文件**（`shrink_source_file`）。跳过 `.fbm`。二的幂走对折阶梯。 |
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
| `TextureToolWindow`      | 贴图子面板：配置 inspector、勾选操作、范围、执行。不写业务逻辑。 |
| `TextureTargetCollector` | 按 Selection / 文件夹等收集待处理贴图路径。          |


---



## 7. Model 纵切



### 7.1 Config


| 类                      | 职能                                                                                                                            |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `ModelProcessSettings` | External 材质、剔灯剔相机、`supportedExtensions`（默认仅 `.fbx`）、`importAutoOperationIds`、排除目录。资产：`ConfigData/ModelProcessSettings.asset`。 |




### 7.2 Operations


| 类                                                                              | 职能                                                                                                                  |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| `IModelAssetOperation`                                                         | 模型扩展接口（对称于贴图）                                                                                                       |
| `ModelOperationRegistry` / `ModelOperationRunner` / Context / Result / Summary | 与贴图侧同构                                                                                                              |
| `SetVertexColorsWhiteOperation`                                                | **顶点色全白**（`set_vertex_colors_white`）。改的是导入后 Mesh 子资产，不是 FBX 二进制。Art 自动流不进；手动对 Art/Model 执行后，打包工具 v1.2.9+ 重导时会保留顶点色。 |


**新增模型操作：** 同贴图——实现接口即可被反射发现。

### 7.3 Import


| 类                              | 职能                                                                                      |
| ------------------------------ | --------------------------------------------------------------------------------------- |
| `ModelImportSettingsProcessor` | 设置自动：`materialLocation=External`、`materialName=BasedOnMaterialName`、剔灯光摄像机等；**跳过 Art**。 |
| `ModelSourceFileProcessor`     | `OnPostprocessModel` 立刻跑 importAuto；`OnPostprocessAllAssets` 入队 Scheduler。              |




### 7.4 Window


| 类                      | 职能                    |
| ---------------------- | --------------------- |
| `ModelToolWindow`      | 模型子面板                 |
| `ModelTargetCollector` | 收集待处理模型路径（选择 / 单文件夹等） |


---



## 8. 总面板


| 类                       | 职能                                              |
| ----------------------- | ----------------------------------------------- |
| `ResourceProcessWindow` | 菜单 `Tools/资源处理总面板`。总开关 + 四路分项 + 打开子面板 + 确保配置资产。 |


旧菜单（独立「贴图处理工具」「SwitchManager」等）已移除，避免双入口行为不一致。

---



## 9. 与插件 1 协作的常用流程



### 9.1 独立贴图（不在 `.fbm`）

导入区放贴图 →（可选）后处理自动压缩 → 做 Prefab → Batch Build。

### 9.2 FBX 内嵌贴图（两遍）

1. 导入区拖入 FBX（插件 2 设 External；**不要压** `.fbm`）。
2. 插件 1 第一遍打包 → 贴图落到 `Assets/Art/<名>/Texture/`。
3. 总面板打开贴图子面板，**只压 Art/Texture 超标文件**。
4. 不删 Art，再 Batch Build（插件 1 保留压缩结果）。



### 9.3 Art 模型顶点色（导 GLB 前）

1. 选中 `Assets/Art/<名>/Model/*.FBX`。
2. 模型子面板执行「顶点色设为全白」。
3. 再打 Prefab 包 / 导 GLB；插件 1 重导时保留顶点色（勿删 Art 后指望自动恢复）。

阈值对齐：贴图 Settings 的 `maxSourceMegabytes` **≤ 5**，与插件 1 告警线一致。

---



## 10. 扩展清单（抄作业用）



### 新贴图格式

1. 实现 `ITextureFileCodec`（无参构造）→ 自动进 Registry。
2. 确认 `Shrink*` 等 Operation 只依赖 Registry，无需改窗口。



### 新贴图业务操作

1. `Operations/XxxOperation.cs` 实现 `ITextureAssetOperation`。
2. 需要自动：Id 写入 `TextureProcessSettings.importAutoOperationIds`。
3. 需要排除 Art/`.fbm`：在 `CanProcess`/`Execute` 里用 Settings / `AssetPathUtility`。



### 新模型业务操作

1. 实现 `IModelAssetOperation`。
2. Id 写入 `ModelProcessSettings.importAutoOperationIds`（若要自动）。
3. 若改的是 Mesh 子资产：文档写明「FBX 重导会丢」；交付区依赖插件 1 的保留逻辑或导入后处理。



### 新资源类型（如 Animation）

建议复制 `Model/` 或 `Texture/` 整棵纵切 + Shared 增加开关与 Scheduler 阶段；总面板加一块。不要把逻辑塞进现有贴图/模型类。

---

