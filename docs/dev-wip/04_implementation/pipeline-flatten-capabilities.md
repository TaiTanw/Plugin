# ④ 平铺能力

现约：[开发者须知](../README.md) · [结构](../02_structure/overview.md)。

`FromContext` → `Run(plan)`。调度 `Pipeline/Editor/ManualFlatten/`；内核 `TOol/Editor/Generated/Flatten/`。菜单④两条完整④，不接⑤⑥。

别处说的 **B / B′ / E / C / D / Finish** 指下表，不是管线格号 [1]–[⑥]。

---

## 步骤（查阅用）

顺序：清夹 → Begin → **B 或 B′** → 伴生整理（仅 B）→ **E** → **D** → **C** → **Finish**。互斥只有 B ↔ B′。


| 代号           | 一句话                                                                     | 现网                                   | 主函数                                                        |
| ------------ | ----------------------------------------------------------------------- | ------------------------------------ | ---------------------------------------------------------- |
| 清夹           | 只删本次 `Art/<名>/`                                                         | 管线默认开；人工默认关                          | `TryClearArtUnitFolderIfRequested`                         |
| **Begin**（A） | 在 Art 写下本趟 Prefab                                                       | 总跑                                   | `PreparePackagePrefab`                                     |
| **B**        | 按后缀拆依赖（拷贝循环）                                                            | 无外 URI                               | `CopyAdjustedPrefabDependencies`                           |
| **B′**       | 整包原子搬，交路径表                                                              | `HasExternalUris`                    | `RelocateAtomicPackage`                                    |
| 伴生整理         | 把 `Model/` 里冒出的非模型挪走                                                    | **仅 B**；B′ 跳过                        | `FlattenModelCompanionFolders`                             |
| **E**        | Art 的 ModelImporter（InPrefab+Local）；有**单元外** `.fbm` 才 `ExtractTextures` | ScriptedImporter（gltf/glb）跳过 Extract | `ApplyArtDelivery` + `ExtractAndBindPackagedModelTextures` |
| **D**        | 按拷贝表 / B′ 路径表改引用                                                        | 总跑；表空则空转                             | `RemapCopiedAssetReferences`                               |
| **C**        | Renderer 材质另存到 `Material/`，槽上贴图副本进 `image/Texture/`                     | B 与 B′ 都跑                            | `CopyPrefabRendererMaterials`                              |
| **Finish**   | 自愈（补拷+remap，**不 Extract**）、动画、空壳、碰撞体                                    | 总跑                                   | `TryHealExternalDependencies` 等                            |


质量码在 Finish **之后**由编排记：仍挂单元外 `.fbm` → **41**；贴图身份警告 → **42**。都不停⑤⑥。glTF 缺相对 URI 在 Begin 前 → **40** 整趟停。

---

## 核心

平铺把 IncomingPrefab 收成 **一个 Art 单元** `Assets/Art/<名>/`。`<名>` 跟 ID2 / ③ Prefab。

互斥只有 **B ↔ B′**：


|        | 何时                                      | 做什么                                                                 | 禁止                             |
| ------ | --------------------------------------- | ------------------------------------------------------------------- | ------------------------------ |
| **B**  | 无外 URI（FBX/OBJ/GLB 等）                   | **拷贝循环**：`GetDependencies` 按后缀拆进 Model / image / Material / Unknown | 不要在循环里特判 `.gltf`               |
| **B′** | ctx `HasExternalUris`（典型 `.gltf` 相对路径包） | **整包原子搬**到 `Art/<名>/<名>/`，交出旧→新路径表给 D                               | 不要按后缀拆；不要把 sidecar 塞进 `Model/` |


其余步两边都跑：Begin 写 Prefab、E（Art ModelImporter + 有外部 `.fbm` 才 Extract）、D 重映射、C 另存 Renderer `.mat`、Finish（自愈 / 动画 / 空壳 / 碰撞体）。管线默认清本次 `Art/<名>/`；人工 SO 默认不清。

**拷贝循环为什么不能拆 glTF：** JSON 写的是相对 URI。`.gltf` / `.bin` / 图拆到三个夹后路径全断。B′ 绕开循环，不是在循环里加 `if`。

```text
Assets/Art/<名>/
  <名>/              ← 仅 B′：相对树原样
  Prefab/  Material/  image/Texture/  Model/  Animation/
```

C 在 `image/Texture/` 的副本与 B′ 子树里那份可并存（给 `.mat` / ⑤ vs 给容器 URI）。`FlattenModelCompanionFolders` 只扫 `Model/`：B 保留（收拾 FBX 重导冒出的伴生）；B′ 必须跳过。

---

## 开发者注意

- **E ≠ 自愈。** Extract 只归 E；Finish `TryHealExternalDependencies` 只把 **本单元外** 的 `.mat`/贴图拷进本包并 remap，**不再 Extract**。自愈后 Prefab 仍挂单元外 `.fbm` → 编排 **41**，⑤⑥继续。
- **Art 材质来源写死 InPrefab + Local。** ④ `ApplyArtDelivery` 不读导入区 SO。`SaveAndReimport` 会再进 `OnPreprocessModel`，Processor **硬跳过 Art**，避免和导入钩子互相覆盖。
- **身份账本在 Begin 冻结。** Finish 发现未知贴图只警告（**42**），不按短名从兄弟单元补拷。
- **模型重绑看本单元依赖路径。** `GetDependencies` 已在本单元的贴图，在这些路径里定一张：恰好一个 `.fbm` 就用它（一张平铺加一张套层也收成套层）。这个名字不在这批依赖里，才问账本。同名两条以上都在 `.fbm`：警告（**42**），不改已有 remap，也不按文件名从账本另挑。不改材质上已经指向的平铺副本。
- **B 落点。** `.fbm` 父夹永远套一层（`image/Texture/<名>.fbm/`）；非 `.fbm` 仅当分类根文件已被占用、且来源父夹名不是分类叶名时套一层。同名已占坑不覆盖。E 外部 `.fbm` 本趟已有本单元副本则不 Extract。伴生搬移走同一 `ResolveDestAssetPath`。
- **本样。** 测试1112（Character01 + Eye_Eyeball）清空重导：`exit=0`，两包 `textureIdentityWarnings=0`、`leftoverFbm=0`。刀序见 [flatten-42-worklog](./flatten-42-worklog.md)，随 **v1.6.8** 发布。操作者看到的落点、Unknown、⑥ 后顶点色日志：[操作者须知](../../operator/README.md)。
- **glTF 缺相对 URI → 40 整趟停**（⑤⑥不跑）。OBJ 缺 `.mtl` 不进这闸。
- **不要：** 复活导入 delayCall；跳过 [1]；给管线加人工平铺入口；合并 Plan 进 Orchestration；按行数再拆 3000 行内核；插件 1 Load 管线步骤 SO；同槽别名、放宽指纹、短名兄弟搜索。
- 咬住失败码的真样（单元外 `.fbm`→41、内嵌才 Extract、跨单元短名→42）仍 **调试暂放**，见 [backlog](../03_open-items/backlog.md)。测试1112 没有顶上这三行。

窄口：`ToolFlattenApi.FromContext` → `Run(plan)`。路径表（B′）或 `copied`（B）必须交给 D，否则 Prefab 仍指 Incoming。

---

## 附录：导入区「材质关联外部」为何说明那么长、还默认关

这不是④里的勾，是 **全局导入设置（2）** / 模型 SO 的 `modelUseExternalMaterials`，默认 **false**。

勾上 = 导入时把 Incoming 的 FBX 写成 **External**（按材质名在旁边生成 `Materials/*.mat`，内嵌图常落到 `<FBX>.fbm/`）。关着 = 导入钩子**不写**材质来源。

说明长，是因为这一勾改的是 **④ 的输入形态**，不是导入观感：打开后 Incoming 多出 Materials 和 `.fbm`，平铺要拷的就不是「一个模型文件」。历史上它还和 Art 的 InPrefab 在 `SaveAndReimport` 上互相覆盖；External 若不显式 `BasedOnMaterialName` 会按贴图名出 `.mat`，没图或两材质共用一张图就紫槽。交付区 UnityPackage 第一次导入若不是 InPrefab，Unity 还会在 `Model/` 下再长出 Materials / `.fbm`。

所以默认关：日常入库不要先把导入区铺成外置材质。要看抽出 / leftover 再手动开。开了只影响 Incoming；④ 仍会把 Art 收成 InPrefab。自愈不会在导入时跑，只在平铺 Finish。