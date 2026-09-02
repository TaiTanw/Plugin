# 当前整体结构（文件夹 · 类 · 中文职能）

返回 [总目录](../README.md)

根仓：`Assets/Plugin/`（插件 Git 根）

```text
Assets/Plugin/
├─ docs/                         文档
│  ├─ CLI_AUTOMATION_DEV.md      历史长文（入口改指向 dev-wip）
│  └─ dev-wip/                   ★ 本开发备忘目录
├─ Pipeline/                     ★ 流程编排（SO / Runner / 总面板）
├─ TOol/                         插件 2
└─ RetinarBatchBuilder_Share/    插件 1
```

### Pipeline（编排 · 对外两块）

| 相对路径 | 职能 |
|---|---|
| `Pipeline/ConfigData/` | `PipelineStepSettings` 总步骤 SO |
| `Pipeline/Editor/PipelineWindow.cs` | **(A)** 人机：`Tools > 自动化管线总面板` |
| `Pipeline/Editor/PipelineRunner.cs` | **(A/B 共用)** ②→③→④→⑤→⑥；② 后 `AttachJobContext`（不调③） |
| `Pipeline/Editor/PipelineJobContext.cs` | D23 事实：外 URI / 伴生 / `MainAssetOk` |
| `Pipeline/Editor/PipelineOptions.cs` | 步骤开关；字段 `JobContext`（③⑤⑥ 不读） |
| `Pipeline/Editor/PipelineFlattenBridge.cs` | **仅④**读 ctx → FlattenOptions（类型不互相引用） |
| `Pipeline/Editor/PipelineGltfUriProbe.cs` | ② 后探测；转调 `GltfPackageFiles.Scan` |
| `Pipeline/Editor/PipelineCli.cs` | **(B)** CLI `-executeMethod PipelineCli.Run` · 第一刀 `-source` |
| 文档 | [d23 报告](../04_implementation/d23-slice-report.md) · [pipeline-flow](../04_implementation/pipeline-flow.md) · [cli-getting-started](../04_implementation/cli-getting-started.md) |

---

## 插件 2 · `TOol/`

路径：`Assets/Plugin/TOol/`

| 相对路径 | 职能简述 |
|---|---|
| `Editor/Window/` | L1 资源处理总面板、批量导入（⑤/② 人机入口，≠ Plugin 流程编排） |
| `Editor/Window/BatchFbxImportService.cs` | ①②：收集、**向上三层命名**、拷贝 Import |
| `Editor/Window/BatchFbxImportWindow.cs` | 批量 FBX 导入窗口 |
| `Editor/Window/ResourceProcessWindow.cs` | L1 资源处理总面板（路径×Op；⑤ 子流程对外口） |
| `Editor/Shared/` | 横切：开关、批量路径、排除、导入后调度 |
| `Editor/Shared/GltfPackageFiles.cs` | gltf URI `Scan`（② 伴生拷 + ctx 探测共用；换解析器只改这里） |
| `Editor/Shared/Api/` | **对外窄口**：Import / Prefab / PostProcess；管线②夹级清空 = **D18** |
| `Editor/Shared/AssetUnitFolder.cs` | 只删 `parent/单段`（② Incoming、④ Art） |
| `Editor/Generated/` | 中间资产能力（非 Art、非⑤原地改） |
| `Editor/Generated/Prefab/` | **③ 自动 Prefab**（Config / Layout / Service / 菜单） |
| `Editor/Generated/Prefab/.../PrefabBuildService.cs` | 写盘 `SaveAsPrefabAsset` |
| `Editor/Generated/Prefab/.../PrefabIncomingPaths.cs` | Prefab 目标路径 / 三层命名 |
| `Editor/Generated/Prefab/PrefabBuildMenu.cs` | `Tools > 自动化预设体（选中模型）` |
| `Editor/Texture/` | 贴图设置钩子 + Op（压图等）⑤ |
| `Editor/Model/` | 模型设置钩子 + Op（顶点白等）⑤ |
| `ConfigData/` | 进库的 Settings SO 实例 |
| `ARCHITECTURE.md` / `README.md` | 插件 2 结构说明 |

**层级习惯：** L-UI → L-配置(SO/Prefs) → L-内核(Service/Op) → L-导入钩子。

---

## 插件 1 · `RetinarBatchBuilder_Share/`

路径：`Assets/Plugin/RetinarBatchBuilder_Share/`

| 相对路径 | 职能简述 |
|---|---|
| `Assets/Retinar/Editor/40_Api/` | **对外窄口**：Flatten / BuildAbOnly |
| `Assets/Retinar/Editor/01_RetinarMenu.cs` | 菜单唯一挂载：平铺 / 导出 / 直通 |
| `Assets/Retinar/Editor/10_Flatten/` | ④ 平铺调度、分类、布局 |
| `.../10_Flatten/RetinarFlattenScheduler.cs` | 平铺调度（转发内核） |
| `.../10_Flatten/Category/*` | 后缀→Model/image/… 分类器 |
| `Assets/Retinar/Editor/20_Package/` | 导出调度、成品直通 |
| `.../20_Package/RetinarPackageScheduler.cs` | 规范化导出调度 |
| `.../20_Package/RetinarDirectPackage.cs` | ⑥ 直通：仅 AB+UP，不改 Art |
| `Assets/Retinar/Editor/30_Business/` | 门禁/输出**接口种子**（未接线） |
| `Assets/Retinar/Editor/RetinarBatchModelBuilder*.cs` | Legacy 巨型内核：④ 平铺 + ⑥ 规范化导出 |
| `.../RetinarBatchModelBuilder.AtomicRelocate.cs` | ④ B′：`RelocateAtomicPackage` → `Art/<名>/<名>/` |
| `PACKAGING_RULES.md` | 交付硬规则 |
| `Assets/Retinar/Editor/README_EDITOR.md` | Editor 阅读地图 |

### ④ / Remap 关键类（中文）

| 类/方法 | 职能 |
|---|---|
| `CreatePackagedAdjustedPrefab` | 外来 Prefab 平铺主路径 |
| `CopyAdjustedPrefabDependencies` | 依赖拷贝分类 |
| `RemapMaterialTexturesToArtFolder` | **贴图引用收到 Art**（已加 IsTextureAsset） |
| `IsModelAsset` | 认 fbx/obj/glb/gltf |
| `AddOrUpdateBoxColliderInPrefab` | 平铺末可选碰撞体 |

### ⑥ 相关类（中文）

| 类/方法 | 职能 |
|---|---|
| `ExportArtPrefabPaths` | 规范化：校验→预检→双端 AB→Deliverables→UP→docs→弹窗 |
| `BuildAssetBundles` | 调 BuildPipeline |
| `RetinarDirectPackage` | 最净打包 |
| `IRetinarAcceptanceGate` | 业务门禁接口（未用） |

---

## 宿主工程（非本仓，但相关）

| 位置 | 说明 |
|---|---|
| `Plugin2022/` | Unity 2022 宿主根（无整仓 Git） |
| `Plugin2022/Packages/` | 含 UnityGLTF 等 |
| `Plugin2022/Assets/Art/` | ④ 交付产物区。插件 2：**导入期自动流跳过**；**L1 手动总批量 / 中间层⑤**默认打这里（⑤=代跑 L1，不是导入钩子） |
| `Plugin2022/Assets/IncomingPrefab/` | ③ 默认输出根 |
| `Plugin2022/Deliverables/` | ⑥ 规范化/直通输出 |

---

## 两插件对照（一览）

```text
插件 2 TOol                         插件 1 Retinar
① 收集 / ② 导入设置 / ③ Prefab      ④ 平铺+remap / ⑥ 出包
⑤ 后处理 Op                         30_Business 门禁预留
管线总面板（已有）+ CLI 壳（D5 待建）   菜单人工交付线保留
```
