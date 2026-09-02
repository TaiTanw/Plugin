# ④ 平铺能力查封（D23b 前置）

返回 [总目录](../README.md) · [ctx](./pipeline-job-context.md) · [相位入参](./pipeline-phase-io.md) · [本刀报告](./d23-slice-report.md)

> **状态：查封有效；B′ 已接入。** 现状与谁读 ctx → [d23 报告](./d23-slice-report.md)（总目录 **4j**）。  
> 管线④入口永远是 **Prefab**（③ 的产物）→ `CreatePackagedAdjustedPrefab`。菜单「对 FBX 直接平铺」走另一条 `CreateNormalizedPrefab`，**不在本查封范围**（D23b 先不管）。

---

## 1. 「拷贝循环」是什么（人话）

不是 Unity 的 `File.Copy` 本身，也不是「导入区拷进 Incoming」。

指的是平铺里这一段：**把 Prefab 依赖到的每个文件，按后缀丢进 Art 不同子夹**。

代码：`CopyAdjustedPrefabDependencies`（约 951 行）

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

`FlattenSourcePaths` → 每个 Prefab → `CreatePackagedAdjustedPrefab`：

| 序 | 能力名（薄） | 现网函数（主） | 现网是否总跑 | D23b 闸 |
|---|---|---|---|---|
| 0 | 清本次 Art 单元夹 | `TryClearArtUnitFolderIfRequested` → `AssetUnitFolder` | 管线④是；菜单否 | `ClearDestinationArtFolder`；只删 `Art/<名>/` |
| A | 写 Art Prefab | `PreparePackagePrefab`（拷或原地）+ Unpack 嵌套 | 是 | 仅 `RunFlatten` |
| B | 按后缀拆依赖 | **`CopyAdjustedPrefabDependencies`**（拷贝循环） | 是 | **仅 `!HasExternalUris`** |
| B′ | 原子搬迁 | `RelocateAtomicPackage` → `Art/<名>/<名>/` | Skip 时跑 | **仅 `HasExternalUris`**；须交路径表 |
| （夹带） | 伴生夹整理 | `FlattenModelCompanionFolders` | 是 | B 必跑；**B′ 跳过** |
| E1 | ModelImporter 导入设置 | `ApplyImportSettingsToPackagedModels` → `ApplyModelImportSettings` | 是；无 ModelImporter 则空转 | `ImporterKind==ModelImporter` 才有意义 |
| E2 | Extract 内嵌贴图并绑定 | `ExtractAndBindPackagedModelTextures` + `RemapPackagedModelImporterMaterials` | 同上，GLB/gltf 打 Warning 后 continue | 同上 |
| D | 重映射引用 | `RemapCopiedAssetReferences` + `RemapCopiedPrefabModelReferences` | 是（objectMap 空则几乎空转） | 有拷就做；B′ 也要有对应 map |
| C | 另存 Renderer `.mat` | `CopyPrefabRendererMaterials` | 是 | **B 与 B′ 两条都跑**（可贴图到 Texture，接受双份） |
| （收尾） | 自愈 / 动画 clip / 空壳 / 碰撞盒 / AB 名 | `TryHealExternalDependencies`、`CopyAndRemapPrefabClips`、`WrapIncomingPrefabInEmptyShell`… | 是 | 本刀不改；不列入拆/不拆互斥 |

**互斥只有 B ↔ B′。** A、C、D、收尾两边都在。E 对 ScriptedImporter 现网已空转，不必为 gltf 再写一套。

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

只扫 **`Art/<名>/Model/`**：Refresh → 按后缀 **Move** 出 Model → 删空子夹。给 FBX 重导冒出的 `.fbm` 做保洁，好过 ⑥ `ValidateModelFoldersAreClean`。

- **B 不能跳**：跳过则 `.fbm` 留在 Model，⑥ 失败。  
- **B′ 应跳过**：包在 `Art/<名>/<名>/`，本函数本来扫不到；若 sidecar 被误放进 Model，再跑等于第二道拆文件。⑥ 校验里还会再调一次；空 `Model/` 上是空转。不要让校验把同名子夹当成 Model 去拆。

### 3.4 B′ 必须交出路径表（确认）

`Dictionary<旧 Assets 路径, 新 Assets 路径>`（主文件 + 每个 sidecar）。D 只认这张表。没有表 = Prefab 仍指向 Incoming。

### 3.5 菜单 FBX 直平铺

`CreateNormalizedPrefab` 不是这段拷贝循环。管线④进不去。D23b 不管。

---

## 4. 从插件 1 抽离？能力分离之后

战略仍是 **平铺暂留插件 1、不整包迁插件 2**。拆开改变的是「以后能不能抽」，不是本迭代搬家。

| 抽离形态 | 是否具备 | 说明 |
|---|---|---|
| **窄口变薄** | **具备，这是分离的收益** | 现网一个 `FlattenPaths` 吞 A–E。之后 `40_Api` 可拆成写 Prefab / 拆依赖 / 原子搬迁 / 另存球 / remap；Pipeline 只组合。实现仍住 Retinar |
| **编排认能力** | **D23b 后局部具备** | Runner 用 ctx 选 B 或 B′；不必搬走 `CreatePackagedAdjustedPrefab` |
| **整段④迁插件 2** | **本阶段不具备、也不该做** | ⑥ 门禁、菜单平铺、FBX Extract/InPrefab、动画 remap、自愈仍绑 PACKAGING_RULES。迁走会复制 Art 契约 |
| **只把 B′ 放到插件 2** | **不建议** | 路径表、布局、随后 D/C/⑥ 仍在插件 1；跨插件传 map 更脏 |

结论：分离是为了 **在插件 1 内可组合、可闸**，并为更薄的 Flatten API 打底。抽离若做，也只加 `RetinarFlattenApi` 能力方法，不搬目录。

---

## 5. 本刀（含 B′）

- ctx.Build；`SkipDependencySplit` 不跑拷贝循环。  
- B′：`RelocateAtomicPackage` → `Art/<名>/<名>/`，交出路径表。  
- ② 工程外 `.gltf` 入库时按同一 Scan 拷伴生。  
- 管线④ `ClearDestinationArtFolder`：只清本次 `Art/<名>/`（菜单 Default 不清）。  
- 探测扩展见 [pipeline-job-context §7](./pipeline-job-context.md#7-probe-extend)。
