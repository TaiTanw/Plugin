# D24-R1：平铺内核查封审计

返回 [D24 边界计划](../03_open-items/d24-boundary-plan.md) · [④能力查封](./pipeline-flatten-capabilities.md) · [ctx](./pipeline-job-context.md)

> 审计快照：2026-09-09。按当日工作树逐个核对 C# 定义与 `Assets/**/*.cs` 的实际引用；不是对旧文档的转述。当日三份 `RetinarBatchModelBuilder` partial 合计 **3,483 行**（2253 + 1006 + 224，含安全护栏）。2026-09-10 增补 SO 快照与人工入口后的当前合计为 **3,518 行**（2280 + 1014 + 224）。本文件不授权据此批量删除或一次性重写。

## 1. 先给结论

1. **物理归属和逻辑归属要分开说。** 约 3,500 行已经在插件 2 的 `Generated/Flatten/Service`，但仍混有菜单、管线④、Importer、Prefab 变换、材质、动画和 AB 标签。当前可把“需要 ctx 才能决定怎样跑的④相位编排”临时视为中间层能力；这只是接管期归属，不是长期硬约束。
2. **三个 partial 自身并不引用 `PipelineJobContext`。** 反向依赖集中在 `FlattenBuildService` 和 `ToolFlattenApi`：它们读 ctx、生成 `RetinarFlattenOptions`，并决定 B/B′ 与 E。也就是说，长期断 ctx 不需要重写整块内核，只需先把“ctx → 执行计划”的翻译边界上移。
3. **管线④和菜单 FBX SafeZone 确实是两条入口。** 管线只接③产出的 Prefab，按 Begin → B|B′ → E? → D → C → Finish；菜单允许选 FBX 或 Prefab，FBX 会走缩放/居中的 SafeZone 创建链，Prefab 才复用上述七步。
4. **已开放两个人工完整相位入口，不开放七步乱序。** “普通平铺（B）”与“原子迁移（B′）”只负责选择互斥分支，之后都会继续 E?/D/C/Finish。现有 `RetinarFlattenWork` 仍没有持久化阶段状态、幂等声明、回滚或完整结构化错误，因此不能把 Begin/B/E 等裸步骤做成任意点击按钮。
5. **④仍夹带明确的⑥格式副作用。** 两条 Finish 都给 Prefab Importer 写 `assetBundleName/assetBundleVariant`、清同夹重复标签；⑥实际使用显式 `AssetBundleBuild`，不依赖这些标签。这组副作用可以在行为回归建立后独立迁出。
6. **最先封的是源资产保护。** 审计发现 `CreatePackagePrefabCopy` 保存失败时曾返回原 `sourcePath`，后续 D/C/Finish 可能直接改源 Prefab；当前工作树已改为返回 null，并由 Begin 验证结果位于目标 Prefab 夹。继续反查后又发现自愈复制材质失败时会把源材质当副本修改，本轮也已加拒绝保护。两处仍需 Unity 故障注入验证。

## 2. 实际入口与调用图

### 2.1 管线④（在场、实际被调用）

```text
PipelineRunner.ImportFromBindings / AttachContextsFromModelPaths
  → AttachOneContext
    → PipelineJobContext.Build(modelPath)             // 每份源一次，②后③前

PipelineRunner.FlattenPerPrefab
  → ToolFlattenRequest.ForPipeline + binding 轴向输入
  → ToolFlattenApi / FlattenBuildService
    → TryBeginPackagedFlatten                          // 0 + A
    → ctx.HasExternalUris ? FlattenRelocateAtomic      // B′
                          : FlattenSplitDependencies   // B
    → ctx.ImporterKind == ModelImporter
        ? FlattenApplyImportAndExtract                 // E
        : skip
    → FlattenRemap                                     // D
    → FlattenCopyRendererMaterials                     // C
    → TryFinishPackagedFlatten                         // 自愈/动画/空壳/AB 标签
```

证据：`PipelineRunner.cs:364-392, 440-538`；`ToolFlattenApi.cs:17-78`；`FlattenBuildService.cs:14-107`。

重要现状：Runner 为记录、分支和错误文案多次重新调用 `ShouldRelocateAtomic(ctx)`，而 `TryBegin` 又单独把同一 ctx 翻译成 `work.Options`。分支事实没有冻结成一个 plan；ctx 和 options 都是可变对象。

### 2.2 菜单入口（在场、实际被调用）

```text
RetinarMenu.MenuFlattenSelectedToArt
  → RetinarFlattenScheduler.FlattenSelectedToArt
    → RetinarBatchModelBuilder.FlattenSelectedToArt
      → GetSelectedModelPaths                          // 只收 .fbx / .prefab
      → FlattenSourcePaths
        → CreateNormalizedPrefab
          ├─ .prefab → CreatePackagedAdjustedPrefab   // 七步，Menu Default
          └─ .fbx    → FBX SafeZone 创建链            // 缩放 + 居中 + 新建 Prefab
```

证据：`01_RetinarMenu.cs:15-26`；`RetinarFlattenScheduler.cs:12-23`；`RetinarBatchModelBuilder.cs:72-213, 304-446`。

菜单 Prefab 使用 `RetinarFlattenOptions.Default`：不清 Art、不转轴、默认走 B，且 E 仍跑。菜单 FBX 则不是管线④的 Begin/B/D/C 组合；它直接复制模型/贴图并新建 SafeZone Prefab。

### 2.3 仓内未发现调用的公开兼容口

以下结论来自全 `Assets/**/*.cs` 引用核对；它们可能被仓外程序集或反射调用，所以只能标为“疑似历史接口”，不能直接删除：

- `RetinarFlattenApi` 的七个分步包装方法：仓内没有调用方。
- `ToolFlattenApi.ArtRoot`、`ShouldApplyArtModelImporter`、`FlattenSelectedToArt`、`FlattenPaths`：仓内没有调用方；菜单直接调 Scheduler，管线只用其余窄口。
- `RetinarBatchModelBuilder.OpenDeliverablesFolder`：仓内没有调用方；菜单直接调 `RetinarEditorUtil`。
- `RetinarBatchModelBuilder.FlattenSourcePaths(IList<string>, bool)` 两参数重载：仓内没有调用方。
- `FlattenArtPaths.UnitFolder`：仓内没有调用方；`FlattenArtPaths.ArtRoot` 只被上述未调用的 API 属性转发。

## 3. 三份 partial 的方法分组

标记口径：

- **P**：管线④实际路径。
- **M**：菜单/兼容路径。
- **S**：P 与 M 的 Prefab 分支共用。
- **F**：菜单 FBX SafeZone 分支。
- **H**：仓内零引用或明显历史残留，仍需确认仓外调用。
- **⑥副作用**：平铺阶段写了 AB 输出格式元数据。

### 3.1 入口、菜单和分派

| 标记 | 方法组 | 现行职责与实际调用 |
|---|---|---|
| M | `ShowDialogDeferred`、`FlattenSelectedToArt`、`StopIfEditorIsPlaying`、`GetSelectedModelPaths`、`BuildDialogPreview` | 选中项、进度条、提示框；只由菜单总入口链调用。 |
| M | 三个 `FlattenSourcePaths` 重载 | 菜单/兼容批处理；循环调用 `CreateNormalizedPrefab`，收集 Prefab 与 Unknown。管线④没有经过这里。两参数重载仓内零调用。 |
| M | `ValidateFlattenSelectedToArt` | 菜单启用条件；当前只检查是否正在编译。 |
| H | `OpenDeliverablesFolder` | 旧兼容转发；菜单已绕过它。 |
| M/F/S | `CreateNormalizedPrefab` | 菜单分派器：Prefab 转七步包装；其他可加载 GameObject 的模型走 SafeZone。实际菜单筛选使“其他”目前只有 FBX。 |
| M/S | `CreatePackagedAdjustedPrefab`、`ToGeneratedAsset` | 菜单 Prefab 的七步同步包装及旧 `GeneratedAsset` 适配；管线直接逐步调用，不走这两个方法。 |

### 3.2 Begin（0 清夹 + A 写 Art Prefab）

| 标记 | 方法组 | 现行职责与风险 |
|---|---|---|
| S | `TryBeginPackagedFlatten` | 找主模型、定 Art 单元、可选清夹、建标准目录、扫描源 Missing、复制/原地准备 Prefab，创建 `RetinarFlattenWork`。 |
| S | `ResolvePackagedAssetIdentity` | 源在既有 `Assets/Art/<名>/` 时复用单元名，否则按 Prefab 文件名。 |
| P/S | `TryClearArtUnitFolderIfRequested` | 只有管线 request 默认开启；删除本次 Art 单元，无事务/回滚。源已在该单元时跳过。 |
| S | `PreparePackagePrefab`、`UnpackNestedPrefabInstancesInPlace`、`CreatePackagePrefabCopy`、`UnpackNestedPrefabInstances` | 原地或复制 Prefab，并彻底 Unpack 嵌套实例。当前已禁止复制失败回退源路径，并验证结果位于目标 Prefab 夹；Art/Prefab 内原地重跑仍合法。原地保存的返回值仍未检查。 |
| S | `FindMainModelDependency` | 取依赖列表中第一个 FBX/OBJ/GLB/glTF；多模型 Prefab 没有确定性主模型契约。 |
| S | `FlattenReferenceAudit.LogSourcePrefabMissingReferences`（外部服务） | 只 `LogError`，不阻断 Begin，也不进入 `PipelineResult`。 |

### 3.3 B：按后缀拆依赖

| 标记 | 方法组 | 现行职责与风险 |
|---|---|---|
| S | `FlattenSplitDependencies` | 调 B 拷贝、OBJ MTL 跟拷、Model 伴生夹整理、Unknown 日志。返回条件仅为字典非 null，**空字典也成功**。 |
| S | `CopyAdjustedPrefabDependencies` | `GetDependencies` 后按分类表把依赖以 basename 复制进各目录，并交出旧→新 map。不同源目录的同名文件会撞一个目标。 |
| S | `CopyObjMaterialLibrariesBesideCopiedModels`、`ReadObjMaterialLibraryNames`、`GetSiblingAssetPath` | 补 Unity 依赖表看不到的 OBJ `mtllib`；只读到第一条几何顶点前。缺 MTL 只 Warning。 |
| S | `CopyAssetToExactPath` | 目标已存在就直接复用，不校验内容/GUID/来源；拷贝失败返回源路径，由调用者自行猜是否成功。 |
| S | `SyncNewerSourceTextureToWorkingCopy` | 以时间、大小和字节判断是否覆盖；优先保留更小的 Art 图，未验证它是否真是同一贴图。 |
| S | `FlattenModelCompanionFolders`、`MoveAssetToExactPath`、`EnsureAssetIsInDatabase`、`DeleteEmptySubfolders` | 把 Model 子夹文件按分类移动出去。若同名目标已存在且两边都在 Art，`MoveAssetToExactPath` 会删除源文件而不比较内容。 |
| S | `FlattenCopyRunner` + Category/Registry/Settings | **2026-09-10 已改为读取本趟 `FlattenOperationPolicy` 快照。** 人工与管线使用同一 SO 数据类、不同资产；关闭大类仍会改投 Unknown，Unknown 仍只警告不阻断。 |

### 3.4 B′：外 URI 原子搬迁

| 标记 | 方法组 | 现行职责与风险 |
|---|---|---|
| P | `FlattenRelocateAtomic` | 从 `work.Options` 取主文件、sidecar、缺失列表，调用原子搬迁并更新 map。默认菜单不会选择 B′。 |
| P | `RelocateAtomicPackage` | 复制主文件与 sidecar 到 `Art/<名>/<名>/`，保留主文件目录内相对路径。当前工作树已对 typed `MissingUris`、执行时缺文件及目标存在做完整性失败。 |
| P | `CopyPackageFileToArt` | 工程内资产走 `AssetDatabase.CopyAsset`，工程外路径走 `File.Copy + ImportAsset`。已存在目标不会覆盖，正确性依赖 Begin 先清单元。 |
| P | `ResolveFullPath`、`NormalizeAssetOrFull`、`AddUniquePath` | 路径归一化与去重。 |

“原子”仍有一个边界：`GltfPackageFiles.MakeRelativeToGltfDir` 对主 glTF 目录树外的 URI 只返回 basename。若 JSON 使用 `../x.bin` 或绝对 `file:` URI，复制布局不会保留原 URI 语义，也没有改写 JSON；这种输入应先明确为拒绝、支持，或纳入回归，不能默认已覆盖。

### 3.5 E：Art ModelImporter、Extract 与自愈

| 标记 | 方法组 | 现行职责与风险 |
|---|---|---|
| P/S/F | `ApplyModelImportSettings`、`ApplyImportSettingsToPackagedModels` | 写 Art `ModelImporterProfiles.ApplyArtDelivery` 并重导；FBX SafeZone 和 Prefab E 都用。 |
| P/S | `FlattenApplyImportAndExtract`、`RemapPackagedModelImporterMaterials` | E 外壳：设置 Art ModelImporter、Extract、再把外部 Material remap 到本包。管线是否调用由 ctx importer 类型决定；菜单 Prefab 总调用。 |
| S/F | `TryHealExternalDependencies`、`CopyRemainingExternalDependencies` | Finish 的二次兜底：扫 Prefab 外部依赖、补拷、Remap，并再次调用 Extract/材质收敛。很多失败只记日志。 |
| S/F | `ExtractAndBindPackagedModelTextures`、`SnapshotTextureFolderFiles`、`RestorePreservedTexturesIfExtractGrewThem` | Extract 前保存 Art 图片字节，Extract 后若文件变大则恢复；一个模型可发生多次重导。 |
| S/F | `RemapModelImporterTexturesToArtFolder`、`CollectModelExternalFbmTextures`、`CollectExternalFbmPathsFromDependencies`、`CollectExternalFbmTextureDependencies`、`IsInsideEmbeddedMediaFolderPath` | 按依赖和文件名把外部贴图绑到 Art，并统计外部 `.fbm`。Finish 仍有 leftover 时只 Warning，不失败。 |
| S/F | `RemapAllArtMaterialsToLocalTextures`、`RemapMaterialTexturesToArtFolder` | 扫本包材质并按文件名寻找/补拷贴图。若 Importer 已跨单元借到同名图，这里会把错误来源复制进本包，而不是证明来源正确。 |
| S/F | `SaveAndReimportPreservingMeshVertexColors`、`SnapshotMeshVertexColors`、`RestoreMeshVertexColors` | 用“名字 + 顶点数”恢复重导前顶点色；重名且同顶点数 Mesh 仍可能配错。 |
| F | `CollectSourceAssets`、`AddTypedAssetPath`、`AddAssetsFromFolder`、`CopySourceTexturesToUnityArtFolder` | 只在菜单直平铺模型时主动搜源模型旁材质/贴图；递归扫描模型目录与父目录下若干命名夹，范围较宽。 |
| S/F | `IsModelAsset`、`IsMaterialAsset`、`IsTextureAsset`、`IsTextAsset`、`IsApprovedRuntimeDependency` | 类型与白名单基础谓词。其后缀表与 Category 不完全一致：Category 图片还认 EXR/HDR/PSD，核心 `IsTextureAsset` 不认；Category 文本认 Lua，核心 `IsTextAsset` 不认。运行时白名单又与插件 1 ⑥各维护一份。 |

E 与 Finish 目前职责重叠：E 显式 Extract 一次，Finish 的 `TryHealExternalDependencies` 还会再 Extract/Remap。后续拆分不能只按函数名搬走 E，否则会误以为“Extract 已只有一个入口”。

### 3.6 D：对象引用映射

| 标记 | 方法组 | 现行职责与风险 |
|---|---|---|
| S | `FlattenRemap` | 先改写复制资产，再改 Prefab 的 Mesh/Avatar/Controller/组件字段。返回 void。 |
| S | `BuildCopiedObjectMap`、`MapSubAssetsBetweenCopies`、`LoadSubAssetsForMapping` | 为源/副本子资产建映射；同类型数量不同则整类跳过并 LogError。 |
| S | `TryPairByLocalFileIdentifier`、`TryGetLocalFileIdentifier`、`PairByOrdinalIndex` | 先按 local file ID，失败时按同类型顺序和名称配对；错误不向上返回。 |
| S | `RemapCopiedMaterials`、`RemapCopiedAssetReferences`、`RemapCopiedPrefabModelReferences`、`RemapAnimationClipsInAssetFolder`、`RemapSerializedObjectReferences` | 修改材质、复制资产、Prefab 组件和动画里的对象引用。 |
| H | `RemapCopiedAssets` | 全仓仅定义，无调用；功能被 `RemapCopiedAssetReferences` 的更窄循环覆盖，属明确死码候选。 |

### 3.7 C：Renderer 材质副本

| 标记 | 方法组 | 现行职责与风险 |
|---|---|---|
| S | `FlattenCopyRendererMaterials`、`CopyPrefabRendererMaterials` | 打开 Prefab，把 Renderer 槽上的外部材质复制到本包并保存 Prefab。返回 void。 |
| S | `CreateMaterialCopyPreserveSettings` | 复制完整材质属性，并把独立贴图副本绑到本包。目标 `.mat` 已存在时覆盖其属性。 |
| F | `ApplyMaterialCopies`、`CreateOrUpdateMaterialCopy` | FBX SafeZone 建新对象时使用的相似实现；与上组重复但调用形态不同。 |

C 与 E/自愈都能复制或改绑贴图，当前没有单一“材质最终来源”验收，因此不能先把 C 做成可独立重跑按钮。

### 3.8 Finish：Prefab 结构、动画、碰撞体和格式标签

| 标记 | 方法组 | 现行职责与风险 |
|---|---|---|
| S | `TryFinishPackagedFlatten` | 自愈、检查外部 `.fbm`、动画曲线修复、材质再收敛、空壳/轴向、碰撞体、动画改名、Renderer 检查、AB 标签。只有少数条件影响 bool。 |
| S | `WrapIncomingPrefabInEmptyShell`、`IsIncomingPrefabDeliveryShell` | 为外来 Prefab 套 Identity 空壳；可给内容叠 −90°X。是否“已经是壳”仅按根组件与一个子节点判断。 |
| S | `AddOrUpdateBoxColliderInPrefab`、`AddOrUpdateBoxCollider`、`TryGetRendererBounds` | 根碰撞盒；**2026-09-10 已由 `FlattenOperationPolicy.AddBoxCollider` 显式传入**。旧 `FlattenPostProcessSettings` 仅保留为人工 SO 的兼容代理。 |
| S | `NormalizePreparedPrefabAnimations`、`CleanAnimationName` | 按首个 Clip loop 状态重命名 Clip/Controller；RenameAsset 失败不检查。 |
| S/F | `FlattenAnimationClipRemapper.CopyAndRemapPrefabClips` 及其私有方法 | 修复动画对象曲线、复制曲线引用的外部资产。Missing/复制失败主要通过 LogError/Warning 表达，不进入 Finish 结果。 |
| S/F/⑥副作用 | `ClearDuplicateBundleNames`、`ClearBundleName`，以及两处直接写 Prefab Importer AB 标签 | 修改本 Prefab乃至同目录其他 Prefab 的 `assetBundleName/variant`；属于⑥格式策略，不是平铺内容本身。 |

### 3.9 FBX SafeZone 专用创建链和共用工具

| 标记 | 方法组 | 现行职责 |
|---|---|---|
| F | `CopyModelToUnityArtFolder`、`CopyProjectAssetIfNeeded` | 原始模型菜单路径复制到 Art/Model；使用磁盘覆盖而不复制 `.meta`。 |
| F | SafeZone 常量、`SetupAnimationController`、`GetUsableAnimationClips` | 把渲染范围缩放到 0.8 立方体、中心移到 `(0, 0.15, 0)`，并为模型内动画新建 Controller。 |
| S/F | `EnsureStandardAssetFolders`、`EnsureAssetFolder`、`EnsureDiskDirectory`、`AssetPathToFullPath`、`FullPathToAssetPath`、`MakeSafeName`、`FormatBytes` | 文件夹、路径和命名工具。部分与插件 1 `RetinarEditorUtil` 重复。 |

### 3.10 明确历史残留

- 常量 `EmissionIntensity`、`MetallicValue`、`SmoothnessValue`、`EmissionColor` 只有定义，没有读取。
- `GeneratedAsset.BundleFileName` 只在构造时赋值，没有读取；`ToGeneratedAsset` 也只服务菜单旧适配。
- `RemapCopiedAssets` 全仓零调用。
- 多处注释仍叙述已删除的门禁/遗产导出链；可保留事故背景，但不能当作仍在场的验收。
- `RetinarFlattenApi` 七步包装与 `ToolFlattenApi` 有一套平行转发；仓内活跃管线只走后一套，菜单又直接走 Scheduler。

## 4. ④中夹带的⑥格式副作用

当前有两处相同写入：

1. 菜单 FBX SafeZone 的 `CreateNormalizedPrefab`（`RetinarBatchModelBuilder.cs:400-406`）。
2. Prefab 七步的 `TryFinishPackagedFlatten`（`RetinarBatchModelBuilder.cs:636-641`）。

它们会：

- 写 `assetBundleName = assetName.ToLowerInvariant()`；
- 写 `assetBundleVariant = "assetbundle"`；
- `SaveAndReimport`；
- 扫同一 Prefab 目录并清掉其他同 bundle name 的标签；
- Renderer 验证失败时反向清标签。

但插件 1 的 `RetinarAbApi.BuildAndCopyAssetBundles` 在构建时已经显式创建 `AssetBundleBuild { assetBundleName, assetBundleVariant, assetNames }`（`RetinarAbApi.cs:122-132`）。因此⑥不需要依赖④留在 Prefab Importer 上的标签。长期建议把这组写入从两条平铺路径同时移除，而不是只移 Finish 一处；先用回归确认没有第三方工具读取这些标签。

同时存在两个重复常量：插件 2 `AssetBundleVariant` 与插件 1 `RetinarPaths.AssetBundleVariant`；ArtRoot 也由 `FlattenBuildSettings` 与 `RetinarPaths` 各维护一份。这些都是边界尚未收口的证据。

## 5. 高风险耦合（按处理顺序）

### R1. 源资产误写保护（本轮已补护栏，待实跑）

审计发现两条同类风险：

- `CreatePackagePrefabCopy` 保存失败曾返回 `sourcePath`，随后 D、C、Finish 会 Load/Save 源 Prefab；当前已改为返回 null，TryBegin 也验证 `PrefabPath` 位于预期 `Art/<名>/Prefab/`。
- `TryHealExternalDependencies` 复制外部材质失败时，`CopyAssetToExactPath` 会返回源路径，旧逻辑随后对“副本”执行贴图 remap，实际可能改源材质；当前已要求副本路径位于本 Art 单元，否则拒绝修改。

Art 单元内原地重跑仍合法：`ResolvePackagedAssetIdentity` 复用单元，清夹因源在单元内而跳过，`PreparePackagePrefab` 对目标 Prefab 夹内路径原地处理。剩余缺口是 `UnpackNestedPrefabInstancesInPlace` 没检查 `SaveAsPrefabAsset` 返回值，以及路径判断是字符串前缀而非规范化目录比较；不再有已确认的 Art 外源 Prefab Save 路径。

**拆分前验证：** 用不可写/占用目标模拟复制失败，确认 Begin=false、work=null、源 Prefab/材质 hash 不变；再验证 Art/Prefab 内原地重跑仍成功。

### R2. 平铺输出受本机 EditorPrefs 影响（已收口）

2026-09-10 已落地：分类开关/后缀、清目标单元和 `AddBoxCollider` 统一进入 `FlattenOperationSettings`，执行前冻结为 `FlattenOperationPolicy` 并写入日志。深层拷贝、修复、动画和 Finish 只读该快照，不再动态读平铺 EditorPrefs。

人工与管线使用同一数据类但不同资产：`TOol/ConfigData/Manual/**` 属人工，本面板可编辑；`Pipeline/ConfigData/**` 属管线，人工面板只读；其它目录为未归类、只读但可按快照执行。管线面板只编辑固定的管线资产。旧 `FlattenPostProcessSettings` 仅作兼容适配，不再保存 EditorPrefs。

### R3. void + 日志造成“Finish 成功但内容失败”

E/D/C 都返回 void；源 Missing、子资产映射不一致、动画曲线 Missing、复制失败、外部 `.fbm` 剩余等多数只写日志。`TryFinish` 基本只以“Prefab 有 Renderer”和 AB 标签写入是否能继续作为成功标准。

**拆分前先做：** 结构化 `FlattenStepResult`，至少记录 copied/remapped/missing/leftover、失败路径和阶段；Runner 不解析日志文案。

### R4. 文件名扁平化碰撞可复用或删除错误文件

B、C、自愈大量使用 basename 作为目标；`CopyAssetToExactPath` 目标存在就直接复用；`MoveAssetToExactPath` 在 Art 内碰到同名目标会删源。来源不同但同名的贴图/材质/动画无法证明是同一文件。

**拆分前先做：** 明确冲突策略（失败、哈希去重、稳定改名及引用表），禁止“目标存在即成功”和“目标存在即删源”。

### R5. E 与 Finish 重复 Extract/Remap/重导

E 已执行 `ExtractAndBindPackagedModelTextures`；Finish 的自愈再次执行它。每次 Extract 又可能覆盖贴图并重建 Mesh，当前靠图片字节快照和顶点色快照补偿。

**拆分时：** 先画出单一所有者和调用次数，再搬文件；不要让 `FlattenImportStep` 与 `FlattenFinishStep` 各自保留一份同功能兜底。

### R6. 配置已冻结，分支事实与工作单阶段状态仍未完整冻结

分类、清夹和碰撞体已经冻结为 SO policy；但 Runner 仍读同一份 ctx 决定 B/B′，E 也读 ctx 判断 Importer 类型。`RetinarFlattenWork` 没有当前阶段、必需输入、成功计数或不可逆标志，公开能力方法仍可被代码乱序、重复调用。

**拆分时：** 在现有配置快照上继续把 ctx 事实翻译为不可变 plan，Begin 生成受控 work；每步检查合法前置并返回结果。是否把 plan 放 Pipeline 还是插件 2 可再拍，但步骤自身不应重新 Build ctx。

### R7. ctx/Prefab 仍按列表下标对齐

`FlattenPerPrefab` 在 ctx 数量不足时回退 `contexts[0]`，binding 数量不足则给 null。后续 Prefab 可能拿第一份源的 B′ sidecar 与 ImporterKind。此项是 D26-7，但会直接污染任何手动单步/续跑设计。

### R8. “原子包”与类型/白名单存在双定义

- glTF 主目录树外 URI 不保持原相对语义。
- 核心类型谓词与 Category 后缀表不一致。
- 插件 2 自愈白名单与插件 1 UP 白名单重复。
- `FindMainModelDependency` 只取第一个模型依赖。

这些都应先形成显式契约和样例，再改代码位置。

## 6. 临时归属与长期边界

### 当前接管期可采用的归属

因为④的 B/B′、E、缺伴生验收确实依赖 ②.5 ctx，当前可以把下面这一小层视为**中间层平铺相位能力**，即使文件暂时还在插件 2 目录：

- `PipelineRunner.FlattenPerPrefab`；
- `ToolFlattenApi` 中接 ctx 的窄口；
- `FlattenBuildService` 中 `Should*`、缺伴生谓词与 `CreateOptions(ctx, request)`。

而三个 `RetinarBatchModelBuilder` partial 的能力实现当前已经只收 `RetinarFlattenOptions/Work`，没有直接 ctx 依赖。它们是待接管的执行内核，不必因为上层要读 ctx 就整体搬回 Pipeline。

### 长期可拆边界（尚未拍板）

建议目标形状是：

```text
Pipeline 中间层
  ctx + binding + pipeline request
    → 构建一次 FlattenPlan（冻结 B/B′、E、sidecars、轴向、清夹、分类策略）
    → 调完整 FlattenPhase

平铺能力层（物理位置可继续讨论）
  FlattenPhase(plan)
    → Begin → B|B′ → E? → D → C → Finish
    → FlattenPhaseResult

插件 1
  RetinarAbApi(Art Prefab, AB options)
    → 平台 / 压缩 / 名称 / variant / 输出目录
```

这里的长期目标是**缩小 ctx 接触面和冻结输入**，不是先争论文件夹名字。若团队最终决定平铺整体属于中间层，也应保留“纯能力不读取 PipelineJobContext”的边界，避免未来菜单/测试只能伪造完整管线对象。

## 7. 安全拆分顺序

1. **验证已加保护，不搬代码：** 故障注入确认 Begin 失败不回退源路径、自愈失败不写源材质、Art 内原地重跑仍合法；冻结最小回归快照。
2. **补结构化结果：** 让 B/B′/E/D/C/Finish 都能上报失败与计数；保留现有顺序和行为。
3. **继续完成一次性 plan：** 平铺 SO 已冻结为 `FlattenOperationPolicy`；下一刀把 ctx、binding 也解释为 plan，Runner 后续不重复解释，步骤不自行 Build ctx。
4. **先抽两个入口壳：** `MenuFbxSafeZoneAdapter` 与 `PrefabFlattenPhase`；保持私有帮助方法原位，先切调用图而非复制实现。
5. **按数据依赖抽步骤：** Begin → B/B′ → D → C → E/自愈合并 → Finish。E 与自愈应作为同一刀审计，避免双 Extract。
6. **移出⑥副作用：** 同时删除两条平铺路径的 AB 标签写入，⑥继续使用显式 `AssetBundleBuild`；回归第三方读标签场景。
7. **最后处理死码和命名：** 先确认仓外 API，再删 `RemapCopiedAssets`、旧 wrapper、死常量，最后改 `Retinar*` 名称。

每刀只改一种风险，并跑对应样例；禁止再对约 3,500 行做文本范围式批量切割。

## 8. 人工操作入口：已定边界与后续决策口

### 2026-09-10 已定并实现

人工面板和菜单开放两个**完整④相位**入口：

| 入口 | 分支约束 | 后续动作 |
|---|---|---|
| 普通平铺（B） | 检测到外部 URI 时拒绝，提示改用原子迁移 | 继续 E?/D/C/Finish |
| 原子迁移（B′） | 必须是声明外部 URI 的 glTF；缺伴生拒绝 | 继续 E?/D/C/Finish |

直接选择 `.gltf` 时先经 `ToolPrefabApi` 建 Prefab；选择 Prefab 时要求恰好识别到一个 `.gltf` 主依赖，多包输入拒绝而不是任取第一份。直接 FBX 的普通平铺保留旧 SafeZone 语义。

这两个按钮是**安全的分支选择器**，不是只执行 B/B′ 后停下的裸文件操作，也不是可任意跳到 Begin/E/D/C/Finish 的步进器。每次点击只从原始模型或 Prefab 依赖建立一次 ctx，后续不重新探测。

### 为什么仍不开放七步乱序

原因不是“单步永远没价值”，而是当前单步缺少可恢复契约：

- Begin 可能清夹，是不可逆动作；失败没有回滚。
- B/B′ 产生的 map 只在内存 `work`，关编辑器后无法可靠续接 D。
- E/D/C/Finish 多数无结果，无法判断从哪一步安全恢复。
- E/Finish 有重复 Extract；重复执行是否幂等尚未证明。
- 菜单 FBX SafeZone 与 Prefab 七步不是同一输入模型，裸“单步 B”对 FBX 菜单仍无清晰含义。
- 每步重新 `PipelineJobContext.Build` 会观察到已经被④修改过的资产，不等于②后的事实。

### 后续若要开放裸步骤，仍需确认

1. 单步的真实用途是调试、失败续跑，还是让美术改变正常顺序？三者需要不同 API。
2. 是否允许把 plan/work/result 序列化成 checkpoint；若不允许，单步只能存在于同一次进程会话。
3. Begin 清夹后失败，是保留局部产物、回滚，还是允许从指定步骤续跑？
4. 分类后缀和碰撞体已固定读 SO policy；裸步骤是否还要把 ctx plan 一并持久化？
5. 单步成功准则是否要求“本步写完”，还是要求最终 Prefab 无外部依赖；后者不能由 B 独立判定。
6. 是否存在必须由用户单独执行的修复场景；若只有开发排错需求，优先做测试/诊断入口，不做产品菜单。

### 若最终还要开放裸步骤

建议开放的是“载入/生成一个 plan → 在同一会话按合法状态机前进”，而不是每个按钮各自扫描：

```text
New/Load Plan → Begin → (B | B′) → E? → D → C → Finish
                  ↑ 每步校验前置、记录结果，不允许任意跳转
```

可另提供只读的 `Inspect/Validate Step`，帮助人工查看下一步会做什么。真正的可写裸步骤至少要等 R1、R3-R7 的保护和结果契约完成；本次两个完整相位入口不依赖 checkpoint。

## 9. 本次查封的完成与未完成

已完成：

- 两个活跃入口的实际调用图；
- 三份 partial 全方法分组；
- Config/Service/Category/Layout/Runner 的实际引用核对；
- 仓内零引用候选；
- ④夹带⑥ AB 格式副作用的证据；
- 高风险耦合与安全拆分顺序；
- 临时中间层归属与长期边界；
- 平铺 SO 快照、按目录判定人工/管线归属与读写权限；
- “普通平铺 / 原子迁移”两个完整相位人工入口。

尚未完成、不能由静态审计替代：

- Unity 实跑基线（FBX 外图/内嵌、OBJ+MTL、GLB、完整及缺伴生 glTF、同名跨单元、轴向开关）；
- 仓外程序集/反射对公开兼容 API 的调用确认；
- AB 标签是否被第三方工具读取；
- 每个 void 步骤应升级为 Warning 还是 Fail 的产品准则。
