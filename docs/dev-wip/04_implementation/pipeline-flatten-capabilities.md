# ④ 平铺能力查封（D23来源，按当前七步校正）

返回 [总目录](../README.md) · [ctx](./pipeline-job-context.md) · [相位入参](./pipeline-phase-io.md) · [本刀报告](./d23-slice-report.md)

> **状态：查封有效；B′ 已接入。** 现网入口：`FlattenPerPrefab` → `FromContext` → `Run(plan)`。菜单 FBX **不再**走 `CreateNormalizedPrefab`（步骤 7/12 已删）。旧段若仍写 SafeZone / FlattenPaths，以 [overview](../02_structure/overview.md) 为准。

---

## 1. 「拷贝循环」是什么（人话）

不是 Unity 的 `File.Copy` 本身，也不是「导入区拷进 Incoming」。

指的是平铺里这一段：**把 Prefab 依赖到的每个文件，按后缀丢进 Art 不同子夹**。

代码：`CopyAdjustedPrefabDependencies`（位置以方法名定位，旧行号不再适用）

```text
Prefab 的 GetDependencies（每个被引用的文件）
    for 每一个 path:                          ← 这就是「拷贝循环」
        看后缀属于哪一类（ResolveRelativeFolder）
            .fbx/.glb/.gltf/.obj → Model/
            .png/.jpg/…         → Texture/（或 image/Texture）
            .mat                → Material/
            对不上（.bin、奇怪后缀）→ Unknown/
        CopyAsset 到那个夹（文件名不变，目录变了）
```

**为什么 gltf 多文件包会被它弄坏：** JSON 里写的是相对路径（`foo.bin`、`./tex.png`）。循环把 `.gltf`、`.bin`、`.png` 拆到三个夹后，相对路径全部失效，再 Import 这份 `.gltf` 就残了。

**「本刀不改拷贝循环」是什么意思：**

| 刀 | 对这段循环做什么 |
|---|---|
| **D23a** | **碰都不碰。** 只建 ctx。FBX/GLB/gltf 平铺结果与现在完全一样 |
| **D23b** | **不重写循环内部**（分类规则、CopyAssetToExactPath、SyncNewer 贴图等保持原样）。只在循环**外面**加闸：`HasExternalUris==true` 时 **整段不跑**，改走「原子搬迁」 |

所以「不改拷贝循环」≠「D23b 永远还按后缀拆」。而是：拆文件这条老路的实现冻结；外 URI 包绕开它，不在循环里加 `if (.gltf)`。

另存 `.mat` 里还有一个**小循环**（材质槽上的贴图再 Copy 到 Texture/）。那是能力 C，不是这段依赖拆分循环。

---

## 2. 管线④现网顺序（查封）

当前：`PipelineRunner.FlattenPerPrefab` → `ToolFlattenApi` → `FlattenBuildService` → 各步内核。下表A对应Begin，尾部对应Finish：

| 序 | 能力名（薄） | 现网函数（主） | 现网是否总跑 | D23b 闸 |
|---|---|---|---|---|
| 0 | 清本次 Art 单元夹 | `TryClearArtUnitFolderIfRequested` → `AssetUnitFolder` | 管线④是；菜单否 | `ClearDestinationArtFolder`；只删 `Art/<名>/` |
| A | 写 Art Prefab | `PreparePackagePrefab`（拷或原地）+ Unpack 嵌套 | 是 | 仅 `RunFlatten` |
| B | 按后缀拆依赖 | **`CopyAdjustedPrefabDependencies`**（拷贝循环） | 是 | **仅 `!HasExternalUris`** |
| B′ | 原子搬迁 | `RelocateAtomicPackage` → `Art/<名>/<名>/` | 选择外URI包时跑（不是B失败后的回退） | **仅 `HasExternalUris`**；须交路径表 |
| （夹带） | 伴生夹整理 | `FlattenModelCompanionFolders` | 是 | B 必跑；**B′ 跳过** |
| E1 | ModelImporter 导入设置 | `ApplyImportSettingsToPackagedModels` → `ApplyModelImportSettings` | 是；无 ModelImporter 则空转 | `ImporterKind==ModelImporter` 才有意义 |
| E2 | Extract 内嵌贴图并绑定 | `ExtractAndBindPackagedModelTextures` + `RemapPackagedModelImporterMaterials` | 同上，ScriptedImporter在API层直接跳过E；ctx=null兼容路径沿用旧处理 | 同上 |
| D | 重映射引用 | `RemapCopiedAssetReferences` + `RemapCopiedPrefabModelReferences` | 是（objectMap 空则几乎空转） | 有拷就做；B′ 也要有对应 map |
| C | 另存 Renderer `.mat` | `CopyPrefabRendererMaterials` | 是 | **B 与 B′ 两条都跑**（可贴图到 Texture，接受双份） |
| （收尾） | 自愈 / 动画 clip / 空壳 / 碰撞盒 / AB 名 | `TryHealExternalDependencies`、`CopyAndRemapPrefabClips`、`WrapIncomingPrefabInEmptyShell`… | 是 | 本刀不改；不列入拆/不拆互斥 |

**互斥只有 B ↔ B′。** A、C、D、收尾两边都在。E 对 ScriptedImporter 当前直接跳过；人工ctx=null另走旧兼容路径。

先前说的六条能力 = 上表 A / B / B′ / C / D / (E1+E2)。伴生夹、自愈、动画、空壳是厚流程尾巴。

---

## 3. 已拍 / 仍须知道

### 3.1 原子搬迁落到哪（已拍）

按**导入单元名**进 Art（Incoming 三层夹名 / `materialId`，与 ③ Prefab、现网 Art 单元同一套），**父子夹同名**，源包相对路径放进子夹。Prefab、独立 `.mat`、C 的贴图副本与子夹**同级**：

```text
Assets/Art/<名>/
  <名>/                 ← B′ 原子树（.gltf + .bin + 相对图…，URI 不拆）
  Prefab/               ← A
  Material/             ← C 另存球
  image/Texture/        ← C 给 .mat 用的 Unity 贴图副本
  Model/                ← 标准夹仍会建；B′ 不往这里塞 sidecar
  Animation/ …
```

禁止把外 URI 包装进 `Model/`（⑥：Model 只能放模型文件、不能有子夹）。

### 3.2 拷到 Texture（已接受）

C 在 `image/Texture/` 的副本与 B′ 子树里那份**并存**：前者给 Unity `.mat`/⑤，后者给容器 URI。D23b 不去重。

### 3.3 `FlattenModelCompanionFolders`

只扫 **`Art/<名>/Model/`**：Refresh → 按后缀 **Move** 出 Model → 删空子夹。给FBX重导冒出的伴生做整理。旧⑥ValidateModelFoldersAreClean已删除，不能把它当后续兜底。

- **B保留整理**：否则可能残留伴生和错误引用；当前不会由已删的⑥业务门禁兜底。\
- **B′ 应跳过**：包在 `Art/<名>/<名>/`，本函数本来扫不到；若 sidecar 被误放进 Model，再跑等于第二道拆文件。旧⑥校验已删，不再调用；不得把同名原子子夹当Model去拆。

### 3.4 B′ 必须交出路径表（确认）

`Dictionary<旧 Assets 路径, 新 Assets 路径>`（主文件 + 每个 sidecar）。D 只认这张表。没有表 = Prefab 仍指向 Incoming。

### 3.5 菜单 FBX 直平铺

`CreateNormalizedPrefab` 不是这段拷贝循环。管线④进不去。D23b 不管。

---

## 4. 从插件 1 抽离？能力分离之后

**2026-09-03 D27物理迁移已做；2026-09-14口径：** ④文件在插件2，消费ctx的API/服务/编排暂归中间层，黑盒拆分未完成。管线只调 `ToolFlattenApi`（接 ctx）。D24-5 的七步仍在，Bridge 已删。

| 抽离形态 | 是否具备 | 说明 |
|---|---|---|
| **窄口变薄** | **已落地（D24-5）** | 七步仍在；管线入口换成 `ToolFlattenApi` |
| **编排认能力** | **已落地** | `PipelineRunner.FlattenPerPrefab` 组合；B ↔ B′ 互斥 |
| **整段④迁出插件 1** | **2026-09-03 已落地（D27）** | 文件在 `TOol/Editor/Generated/Flatten/`；管线只调 `ToolFlattenApi`。内核类名未改。人工 FBX 已改走③+`Run(plan)`（步骤 7）。 |
| **只把 B′ 先搬走** | **不建议作第一刀** | 路径表、布局、随后 D/C 仍在大文件里；横切不如按「菜单 FBX 直平铺 vs 管线 Prefab 平铺」切开 |

D26 是编排语义，不属于目录迁移。2026-09-10：D26-1 已让配置导入根的安全基线脱离总闸；D26-2 已用 typed `MissingUris` 在④ Begin 前 Fail(40) **并停止整趟**（仅 glTF 探针路径）。B′ 也改为全部必需输入生成精确目标才成功。OBJ 缺件不进该闸。

---

## 5. 本刀（含 B′）

- ctx.Build；`SkipDependencySplit` 不跑拷贝循环。  
- B′：`RelocateAtomicPackage` → `Art/<名>/<名>/`，交出路径表。  
- ② 工程外 `.gltf` 入库时按同一 Scan 拷伴生。  
- 管线④ `ClearDestinationArtFolder`：只清本次 `Art/<名>/`（人工SO默认不清，可在自身来源配置；管线SO默认清）。\
- 探测扩展见 [pipeline-job-context §7](./pipeline-job-context.md#7-probe-extend)。
