# 命令行自动化一体流程 — 开发说明

> 分支：`feature/cli-pipeline-2022`（自 `main` / 标签 `v1.4.4` @ `22a6a28`）  
> 状态：进行中 · 第一大块「全流程自动化」· 当前焦点「GLB 资源平铺」  
> 仓库：插件源码仓（Gitea `asset-bundle` 远程名不改）；本文只指导本分支开发，不替代 `PACKAGING_RULES.md` / `CHANGELOG.md`

---

## 0. 一句话目标

**最终目标：** 用命令行（Unity `-batchmode` / `-executeMethod`）驱动，从外部原始文件（**FBX、GLB**）到 **Android / iOS AssetBundle**，尽量无人点编辑器菜单。

当前阶段不是直接写完整 CLI，而是先把 **编辑器内可无人值守跑通的全流程** 补齐；CLI 是外壳，全流程自动化是内核。

---

## 1. 已确认的战略（不要反复争论）

| 项 | 结论 |
|---|---|
| 主开发 Unity | **2022.3**；本地宿主文件夹 **已改名为 Plugin2022** |
| 2020 宿主 | **ModleEvent** 继续对照，**不原地升 2022** |
| 插件 Git | **一套源码**；功能开发走 **插件仓分支**（如 `feature/cli-pipeline-2022`） |
| **宿主与分支** | **宿主工程不单独建 Git 仓、不改 Gitea 名**；日常开发 = 打开宿主 + 改 `Assets/Plugin` 并提交到**插件仓当前分支** |
| 双宿主 Plugin 拷贝 | **改完需手动同步**（见 §1.1 submodule）；暂不强制 submodule |
| Git 形态（A） | 主线面向 2022；2020 用拷贝/包回归 |
| Gitea | **无需改远程名** |
| 基线版本 | 从 **`v1.4.4`** 开本分支 |

### 1.1 「改完手动同步」和 submodule 是什么？

**已切换（2026-08-25）：** `Plugin2022/Assets/Plugin` 已是 **Gitea `team` 克隆**（另有 `origin`→GitHub），分支 `feature/cli-pipeline-2022`；旧无 git 拷贝在 `Assets/Plugin_legacy_copy`（确认无用后可删）。

```text
Plugin2022/Assets/Plugin/     ← Git 克隆（2022 主开发入口，推荐）
ModleEvent/Assets/Plugin/     ← 仍有一份 Git（2020 对照；两边暂时都有工作区改动时需自行对齐）
```

**Submodule** = 宿主仓只记插件某个 commit 指针（可选，尚未采用）。现在是 **宿主内直接 clone 插件仓**，比「无历史拷贝」顺。
### 1.2 本地改名 Plugin2022

- **已完成：** `...\UnityProject\JEngine1` → `...\UnityProject\Plugin2022`（2026-08-25）。  
- Hub：若仍显示旧路径，删除旧条目后 Add `Plugin2022`。  
- `productName` 仍可能显示 `JEngine1`（Player 名），与文件夹名无关；需要时再在 Project Settings 改。

---

## 1.3 架构探讨：平铺是否并入插件 2？插件 1 是否退化为业务配置？

**事实：** 当前 GLB 平铺缺陷在插件 1（`RemapMaterialTexturesToArtFolder` 等），而你更熟插件 2。  
**但：** 插件 1 不只是「导出按钮」——平铺 = **交付包结构 + 引用收敛 +（将来）门禁/输出**，与 AB/UnityPackage **强绑定**。

| 方案 | 含义 | 利 | 弊 |
|---|---|---|---|
| **A. 平铺仍留插件 1** | 修 GLB remap；插件 2 只管 ①②③⑤ | 边界清晰；PACKAGING_RULES 不搬家 | 你要多碰 Legacy |
| **B. 平铺内核迁插件 2** | 分类拷贝/remap 进 TOol（如 Shared 或新纵切） | 自动化链都在你熟的仓侧 | 大挪；与导出校验、Extract、Art 契约纠缠，易回归 |
| **C. 折中（推荐方向）** | **③ Prefab 在插件 2 Shared**（已开骨架）；④ 暂留插件 1 修 GLB；中期抽「平铺内核」接口，插件 1 渐变成 **业务 Profile / 门禁 / 输出槽配置**（已有 `30_Business` 种子） | 符合「你熟 2、交付规则仍在 1」；CLI 编排在 2 或共用入口 | 要接受一段时间双仓协作 |

**结论（写入备忘，可再拍板）：**

- **现在不要**整包「平铺并入插件 2」。  
- **现在要做：** 插件 2 `Shared/Prefab` 承担 ③；插件 1 **修 GLB 平铺 bug**；文档上把插件 1 目标态写成「业务规则 + 出包」，平铺实现可逐步接口化再迁。  
- 「插件 1 退化成业务规则配置」= **方向对**，但是 **多版本演进**，不是本迭代一次性搬空 Legacy。

---

## 2. 终局形态（命令行工具）

### 2.1 概念

Unity **没有**独立「只跑插件、不开工程」的官方 CLI SDK。自动化标准做法是：

```text
Unity.exe
  -batchmode -nographics -quit
  -projectPath <2022 工程根>
  -logFile <日志>
  -executeMethod Namespace.Class.StaticEntry
  + 自定义参数（输入目录、平台、输出目录等，自行解析）
```

| 参数 | 含义 |
|---|---|
| `-batchmode` | 无交互批处理（必加） |
| `-nographics` | 不启 GPU/窗口（CI 常用） |
| `-quit` | 入口跑完退出 |
| `-executeMethod` | 调 Editor **静态**方法 |

入口必须：**不依赖 Selection、不弹 `DisplayDialog`、路径入参、明确进程退出码**。

### 2.2 目标数据流（与 §3 六步对齐）

```text
① 路径收集 .fbx/.glb
② 导入（命名+设置自动）
③ 自动 Prefab → 专用夹          ← 新步骤
④ 平铺 Art（依赖独立）
⑤ 后处理（压图/顶点白）
⑥ 输出 Android/iOS AB（门禁等）
→ 退出码
```

FBX / GLB：**前半①②适配不同，③起汇合**；不要整条双轨重写。

### 2.3 宿主 vs 插件

| 层 | 职责 |
|---|---|
| **宿主工程**（2022） | Editor 版本、`ProjectSettings`、`Packages`（含 UnityGLTF）、batchmode 工程根 |
| **插件**（本仓 `TOol` + `RetinarBatchBuilder_Share`） | 入库 / 设置自动 / 平铺 / 出 AB；将来提供 `-executeMethod` 静态入口 |
| **外置脚本**（预留 `Tools/` 等） | 拼命令行、传文件夹、收集日志与退出码 |

「在 Plugin2022 宿主上开发」= **用 2022 打开工程改 `Assets/Plugin`、编译、平铺、出包**；不等于自动换 Gitea 远程名。

---

## 3. 全阶段流水线（第一大块 = 跑通下列六步）

终局 CLI 只是按顺序调用这六步的静态 API。菜单/面板是现有 UI 皮；自动化要剥掉 Selection 与弹窗。

```text
① 路径收集     找符合标准的 .glb / .fbx
② 导入         自动化设置 + 文件夹命名
③ 预设体       【新步骤】自动做 Prefab → 专用夹
④ 平铺         依赖拆到 Art 单元目录，便于后处理
⑤ 资源后处理   压缩贴图、顶点刷白等
⑥ 输出         Android / iOS AB（门禁、碰撞体等）
```

| 阶段 | 做什么 | 现状（对照现有 UI） | 自动化缺口 |
|---|---|---|---|
| **① 路径收集** | 递归收集符合标准的 GLB、FBX | **批量 FBX 导入面板**只扫 `.fbx` | 扩展识别 `.glb`；无拖拽、路径入参 |
| **② 导入** | 建夹命名、拷入工程、Import；导入期改 Importer | 导入面板 + **资源处理·设置自动** | GLB 走 UnityGLTF；设置自动默认/路径可注入 |
| **③ 预设体** | 自动生成 Prefab 放入专用文件夹 | **插件 2 `Shared/Prefab/` 骨架已建**（占位，未写盘） | 填充 `PrefabBuildService`；菜单/CLI 调用 |

| **④ 平铺** | 拷依赖、分类、remap、heal → `Assets/Art/<名>/` | `Tools > Retinar > 平铺到 Art` | 无 Selection；**修 GLB→image 误拷**（当前焦点） |
| **⑤ 资源后处理** | 压图、顶点全白等 Operation | **资源处理总面板**主批量 / 分项（后处理 Op） | 路径指向 Art；batchmode 读 SO Op 列表 |
| **⑥ 输出** | 门禁、碰撞体等 → Android/iOS AB | 规范化导出 / 成品直通 | 门禁 SO 未接线；弹窗旁路；平台入参 |

第一大块完成标准（草案）：

- [ ] ①–⑥ 对 FBX、GLB 各至少一条快乐路径可无菜单连跑（配置预置）  
- [ ] ③ 有明确「专用 Prefab 夹」约定与生成 API  
- [ ] ④ GLB 平铺后 `image/Texture` **无**整份 `.glb`  
- [ ] 各步有可调用静态 API（供 CLI）  
- [ ] （可后置）batchmode 一条快乐路径  

**本大块不做完也可并行的：** 外置 shell、Gitea 改名、ModleEvent 升 2022。

---

## 4. 代码根源：层级 · 类/方法（中文）· 问题成因

### 4.1 两插件分层（总览）

```text
插件 2 TOol                          插件 1 Retinar
─────────────────                    ─────────────────
① 路径收集 / ② 导入设置 / ⑤ 后处理     ③（未来可协作）④ 平铺 / ⑥ 出包
UI：面板                             UI：菜单 + 平铺分类面板
内核：Service / Processor / Op       内核：Legacy RetinarBatchModelBuilder*
                                     调度：*Scheduler / RetinarDirectPackage
                                     预留：30_Business 门禁·输出（未接线）
```

| 层级 | 含义 | 典型类型 |
|---|---|---|
| **L-UI** | 窗口、菜单，只转发 | `BatchFbxImportWindow`、`ResourceProcessWindow`、`RetinarMenu` |
| **L-调度** | 无业务细节，转调内核 | `RetinarFlattenScheduler`、`RetinarPackageScheduler`、`ImportPostProcessScheduler` |
| **L-配置** | SO / EditorPrefs | `BatchFbxImportSettings`、`*ProcessSettings`、`FlattenCategorySettings` |
| **L-内核** | 真正搬文件 / 改 Importer / 出包 | `BatchFbxImportService`、`RetinarBatchModelBuilder`、`*Operation` |
| **L-导入钩子** | Unity 导入回调 | `*ImportSettingsProcessor`、`*SourceFileProcessor` |
| **L-宿主包** | 非本仓 | UnityGLTF `GLTFImporter`（`.glb` ScriptedImporter） |

---

### 4.2 ① 路径收集

| 中文说明 | 类 / 方法 | 层级 |
|---|---|---|
| **批量 FBX 导入窗口**（拖文件夹、列表、执行） | `BatchFbxImportWindow` | L-UI |
| **导入配置**（导入根、交付区警报前缀） | `BatchFbxImportSettings` | L-配置 |
| **收集与执行服务** | `BatchFbxImportService` | L-内核 |
| 从拖入路径递归收集 FBX 项 | `CollectFromDroppedPaths` | |
| 解析入库夹名（向上 3 层 `_` 拼接等） | `ResolveFolderName` / `SanitizeFolderName` | |
| 冲突检测（已存在 / 落在交付区） | `HasBlockingAlerts` / `RebuildItems` | |
| 批量执行：建夹→拷贝→Import | `ExecuteBatch` / `ImportOne` | |

**问题成因：** 收集逻辑写死 `.fbx`，**不认 `.glb`**；依赖窗口拖拽与人工点执行，无 CLI 路径列表。

---

### 4.3 ② 导入（自动化设置 + 文件夹命名）

文件夹命名已在 ① 的 `ResolveFolderName`。导入期「设置」在钩子里：

| 中文说明 | 类 / 方法 | 层级 |
|---|---|---|
| **资源处理总面板**（开关、打开导入、主批量） | `ResourceProcessWindow` | L-UI |
| 本机自动化开关（总开关 / 设置自动 / 后处理自动） | `ResourceProcessSwitches` | L-配置 |
| **模型导入设置钩子**（External、剔灯剔相机等） | `ModelImportSettingsProcessor.OnPreprocessModel` | L-导入钩子 |
| **贴图导入设置钩子** | `TextureImportSettingsProcessor.OnPreprocessTexture` | L-导入钩子 |
| 模型/贴图 Settings SO（Op Id、排除前缀） | `ModelProcessSettings` / `TextureProcessSettings` | L-配置 |
| **GLB 导入器**（宿主包，非插件编译依赖） | UnityGLTF `GLTFImporter.OnImportAsset` | L-宿主包 |

**问题成因：**

- 「设置自动」默认关、靠 EditorPrefs，**batchmode 机与本机不一致**。  
- 默认 `excludedPathPrefixes` 含 `Assets/Art/`：交付区不被设置自动改写（正确），但 CLI 必须分清「导入区 vs Art」。  
- GLB **不是** `ModelImporter`：插件 2 的模型设置钩子**套不上** GLB；材质/贴图多在 glb 子资源里。  
- UnityGLTF **不生成**独立 `.prefab`，只生成 `.glb` 主 GameObject → 直接催生对 **③** 的需求。

---

### 4.4 ③ 预设体（插件 2 Shared/Prefab · 骨架已建）

| 中文说明 | 类 / 路径 | 层级 |
|---|---|---|
| Shared 总说明 | `TOol/Editor/Shared/README_SHARED.md` | 文档 |
| Prefab 步骤说明 | `Shared/Prefab/README.md` | 文档 |
| 配置（专用根路径、Unpack 偏好） | `PrefabBuildSettings` | L-配置 |
| 目标路径拼接 | `PrefabIncomingPaths.PrefabPathForSourceModel` | L-配置/路径 |
| 执行服务（占位，暂不 Save） | `PrefabBuildService.BuildPrefabsFromModels` | L-内核 |
| 人工另存 / Issue #2 旧边界 | 仍存在，直至 Service 写盘落地 | 人工 |

**问题成因（历史）：** 无自动 Prefab 步 → ② 与 ④ 断裂；GLB 易变成嵌套指 `.glb` 再平铺。  
**方向：** 本步留在插件 2；平铺暂留插件 1（见 §1.3）。

---

### 4.5 ④ 平铺（当前焦点 · GLB）

| 中文说明 | 类 / 方法 | 层级 |
|---|---|---|
| 菜单入口 | `RetinarMenu.MenuFlattenSelectedToArt` | L-UI |
| 平铺调度（转发） | `RetinarFlattenScheduler.FlattenSelectedToArt` | L-调度 |
| **平铺/导出巨型内核（Legacy）** | `RetinarBatchModelBuilder`（partial） | L-内核 |
| 选中路径收集（现仅 `.fbx`/`.prefab`） | `GetSelectedModelPaths` / `FlattenSelectedToArt` | |
| 按扩展分流：Prefab 或 FBX | `CreateNormalizedPrefab` | |
| **外来 Prefab 平铺主路径** | `CreatePackagedAdjustedPrefab` | |
| 是否算模型文件（`.fbx/.obj/.glb/.gltf`） | `IsModelAsset` | |
| 找预制体依赖的主模型 | `FindMainModelDependency` | |
| 按依赖拷贝并分类 | `CopyAdjustedPrefabDependencies` | |
| 后缀→相对目录（Model、image/Texture…） | `FlattenCopyRunner.ResolveRelativeFolder` | L-内核·分类 |
| 各大类处理器（模型/贴图/材质…） | `ModelFlattenProcessor` 等 | |
| 分类勾选与后缀覆盖（本机 Prefs） | `FlattenCategorySettings` | L-配置 |
| Art 路径常量 | `FlattenLayout` | |
| Extract / `.fbm` 自愈（FBX） | `ExtractAndBindPackagedModelTextures` / `TryHealExternalDependencies` | |
| **材质贴图收到本包**（GLB 误拷点） | `RemapMaterialTexturesToArtFolder` | |
| 有后缀保护的建材质拷贴图 | `CreateMaterialCopyPreserveSettings` | |
| Prefab 套空父外壳 | `WrapIncomingPrefabInEmptyShell` | |
| 可选根碰撞体 | `AddOrUpdateBoxColliderInPrefab` | |

**已做（本分支）：** `IsModelAsset` / 门禁放宽 / Model 后缀含 glb。  

**待修 · 问题成因（`image` 下出现「模型」）：**

1. GLB 贴图是 **子资源**，`AssetDatabase.GetAssetPath(texture)` → **`.glb` 文件路径**。  
2. `RemapMaterialTexturesToArtFolder` **未**调用 `IsTextureAsset`，把容器路径当贴图 `CopyAsset` 到 `image/Texture/GameObject.glb`。  
3. Project 里该夹显示模型图标 → 误以为「文件夹变成了模型」。  
4. 同文件 `CreateMaterialCopyPreserveSettings` **有**保护 → 说明是 remap 收敛漏网，不是分类器把 glb 标成图。

**观感问题：** UnityGLTF 导入态像 Prefab、实为 `.glb` 资产 — 见 §4.3 / 历史说明。

---

### 4.6 ⑤ 资源自动化后处理（压缩、顶点刷白）

| 中文说明 | 类 / 方法 | 层级 |
|---|---|---|
| 总面板主批量入口 | `ResourceProcessWindow.RunMasterBatch` | L-UI |
| 主批量 Op 列表（SO） | `*OperationRegistry.GetMasterBatchOperations` | L-配置/注册 |
| 贴图/模型执行器 | `TextureOperationRunner` / `ModelOperationRunner` | L-内核 |
| **压缩超标贴图源** | `ShrinkTextureSourceOperation` | L-内核·Op |
| **顶点色刷白** | `SetVertexColorsWhiteOperation` | L-内核·Op |
| 导入后自动后处理调度（默认关，且跳过 Art） | `ImportPostProcessScheduler` 等 | L-调度 |
| 批量路径（本机 Prefs） | `ResourceBatchFolderStore` | L-配置 |

**问题成因：**

- 后处理自动**默认关**且排除 Art：交付压缩/刷白应在 **④ 之后**对 Art 跑主批量，不是导入区自动。  
- 顺序约定：平铺后手动宜 **先贴图后模型**（先压图再写顶点色）。  
- 对 Art/Model 下 GLB：顶点色若依赖 `ModelImporter`/可读 Mesh，行为与 FBX **可能不同**，需样例验证。  
- `.fbm` 内贴图禁止压（规则 35）；GLB 内嵌贴图若未抽出，压缩 Op 可能扫不到独立 png。

---

### 4.7 ⑥ 输出（Android / iOS AB · 门禁 · 碰撞体）

| 中文说明 | 类 / 方法 | 层级 |
|---|---|---|
| 规范化导出调度 | `RetinarPackageScheduler` | L-调度 |
| 全套导出内核（校验 + AB + 报告…） | `RetinarBatchModelBuilder.ExportArtPrefabPaths` | L-内核 |
| 打 Android / iOS AB | `BuildAssetBundles(BuildTarget)` | |
| **成品直通**（仅 02+03，不改 Art） | `RetinarDirectPackage.PackageSelectedPrefabsDirect` | L-内核 |
| 碰撞体（平铺末，可关） | `AddOrUpdateBoxColliderInPrefab` + `FlattenPostProcessSettings` | L-内核/配置 |
| SafeZone 等空间校验（偏 FBX 自动预制体） | Legacy 校验路径（注释预留迁门禁） | L-内核 |
| **业务门禁接口（未接线）** | `IRetinarAcceptanceGate` / `RetinarBusinessProfile` | L-预留 |
| **交付输出槽接口（未接线）** | `IRetinarDeliverableOutput` | L-预留 |

**问题成因：**

- 门禁/输出 SO **v1.4.2 只留接口**，现行仍硬编码 SafeZone、碰撞体、全套夹名。  
- 大量 `DisplayDialog` / `Selection` → **不能直接 batchmode**。  
- 直通与规范化两套出口，CLI 第一期出「仅 AB」还是「全套 Deliverables」未决。

---

### 4.8 GLB vs FBX：要不要大量拆管线？

**不要整条双轨。** ①② 前半格式适配不同；③ 起尽量汇合到 Prefab；④⑤⑥ 共用。

| 段 | FBX | GLB |
|---|---|---|
| ①② | `BatchFbx*` + `ModelImporter` + 设置钩子 | 收集扩展 + UnityGLTF；设置钩子有限 |
| ③ | 可从 FBX 自动预制体或人工 Prefab | **强烈建议**独立 Prefab（勿长期嵌套 glb） |
| ④ | Extract/`.fbm` 有意义 | 跳过 Extract；修好 remap 后缀判断 |
| ⑤⑥ | 现有主路径 | 同源 API；样例验 Mesh/贴图 |

---

## 5. 仍较模糊 / 未拍板

开发时遇到先查本节，确认后改到「已确认」或开 Issue。

### 5.1 工程与仓库

1. 本地 `JEngine1` → `Plugin2022`；`productName` 是否同步 — 未做。  
2. 宿主是否单独建仓 / submodule — 可细化。  
3. 两宿主 `Assets/Plugin` **断开拷贝**，改完需手动同步；是否改 submodule — 未决。  
4. UnityGLTF 升级与 `file:` 绝对路径 — GLB 稳定后再评。

### 5.2 产品与流程

5. **③ 专用 Prefab 夹路径与命名**（建议写死约定）。  
6. 嵌套 GLB Prefab：禁止 vs 允许但警告。  
7. 分类面板后缀 Prefs 覆盖默认 `glb` — 是否提示重置。  
8. ⑥ 第一期：仅 AB vs 全套 `00–06`。  
9. 门禁 Profile 何时接线、CLI 是否读 SO。

### 5.3 CLI（第二大块）

10. 参数格式；11. Op/路径注入；12. 弹窗旁路；13. 退出码与许可。

---

## 6. 推荐工作切分（本分支）

```text
第一大块 · 全流程自动化（§3 六步）
  ├─ ④ GLB 平铺（当前）
  │    ├─ 修 RemapMaterialTexturesToArtFolder（IsTextureAsset）  ← 下一刀
  │    ├─ 样例：Model 一份 glb；image 无整包 glb
  │    └─ 嵌套 Prefab 约定
  ├─ ① 收集扩展 .glb
  ├─ ③ 自动 Prefab → 专用夹（新）
  ├─ ②⑤ 设置/后处理路径可无 UI 注入
  ├─ ⑥ 导出静态 API + 弹窗旁路
  └─ 串联静态入口（菜单可先做「一键」）

第二大块 · 命令行外壳
  └─ -executeMethod + 外置脚本
```

---

## 7. 速查表（按问题找类）

| 现象 / 需求 | 先看 |
|---|---|
| 只进 FBX、不进 GLB | `BatchFbxImportService.CollectFromDroppedPaths` |
| 导入夹名规则 | `ResolveFolderName` |
| 导入区 Importer 被改 | `ModelImportSettingsProcessor` / `TextureImportSettingsProcessor` |
| 没有 Prefab 步骤 | §4.4 缺口；勿指望 UnityGLTF 自动出 `.prefab` |
| 平铺报无 FBX/OBJ | `FindMainModelDependency` + `IsModelAsset`（本分支已扩 glb） |
| `image/Texture` 里出现 `.glb` | `RemapMaterialTexturesToArtFolder`（待修） |
| 压图 / 刷白 | `ShrinkTextureSourceOperation` / `SetVertexColorsWhiteOperation` |
| 出 AB | `ExportArtPrefabPaths` / `BuildAssetBundles`；直通 `RetinarDirectPackage` |
| 门禁未生效 | `30_Business/*` 未接线 |
| 阅读地图 / 规则 | `README_EDITOR.md` / `PACKAGING_RULES.md` / `TOol/ARCHITECTURE.md` |

---

## 8. 回归注意

- 修 remap 后删除错误的 `Art/**/image/Texture/*.glb` 再平铺。  
- FBX：独立 png 仍进 `image/Texture`；`.fbm` Extract 不变。  
- 以本 Git 分支为准，再同步 JEngine1/Plugin2022 拷贝。

---

## 9. 修订记录

| 日期 | 说明 |
|---|---|
| 2026-08-25 | 初稿：终局 CLI、第一大块、GLB 平铺、模糊项；分支 `feature/cli-pipeline-2022` |
| 2026-08-25 | 写入全阶段六步；代码层级与中文类/方法说明、各阶段问题成因；③ 预设体标为新步骤 |
| 2026-08-25 | 确认：本地改名 Plugin2022、宿主保持插件仓分支、submodule 释义；平铺归属探讨；Shared/Prefab 骨架 |
| 2026-08-25 | 本地宿主已重命名为 Plugin2022；澄清 Git 根仅 Plugin、2020/2022 靠拷贝 |
