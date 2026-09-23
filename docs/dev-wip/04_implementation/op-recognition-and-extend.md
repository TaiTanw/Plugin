# ⑤ Op：识别与扩展

现约入口：[开发者须知](../README.md)（含 `AllowMasterBatch`）。结构：[TOol ARCHITECTURE](../../../TOol/ARCHITECTURE.md)。

## 约束

贴图 Op 必须手写 `AllowMasterBatch`：

- `true`：可进 L1 / 管线⑤ / 导入自动（仍要过 Collector + Evaluate）。
- `false`：只出现在 L2 精准面板。Registry 会从 master / import 列表剥掉。

**Evaluate 对适用池无条件 `NeedsWork` 的 Op 必须为 false。** 不是自动探测「没有 if」。现网仅 `bake_luminance_to_alpha`（像素判断只在 Execute）。材质 / 模型接口无此字段。  
该 Op 在精准面板下勾选跟材质（本机 Prefs）时会调用材质类内部方法，不是⑤对外口。见 [风险说明](../02_structure/risks.md)。

命中列表 = 当前勾选且允许本通道的 Op 的 `NeedsWork`，不是 Collector 全池。

## 流

```text
扫描根（⑤ = 本次 Art 单元；L1 = Prefs 路径）
  → Collector（类型池：Codec / 模型后缀 / .mat）
  → 本轮 Op 子集
       L2 = 本机 Prefs
       L1 = TOol/ConfigData SO
       管线⑤ = Pipeline/ConfigData SO（管线无扫描入口）
  → Evaluate（与执行同一判断）
       NeedsWork → 执行
       Skip / NA → 不动
```

未注册后缀静默跳过。④ 的 `Unknown/` 分类表与⑤不是一套。导入自动另有 exclude，不经 Collector；⑤/L1 不读 exclude。

| 大类 | 进总列表 | 加格式 |
|---|---|---|
| 贴图 | `t:Texture2D` ∩ Codec | 加 `ITextureFileCodec` |
| 模型 | `t:Model` + Prefab 依赖 ∩ SO 后缀（默认 fbx/glb/gltf） | 改 `ModelProcessSettings.supportedExtensions` |
| 材质 | `t:Material` 且路径 `.mat` | 不用后缀表 |

模型 Collector 会把 Prefab 依赖里的 FBX/GLB 展开进列表。刷白 Op 对非 `ModelImporter`（如 glb）**Skip**——文件在列表里，本 Op 仍不跑。

② 导入写死 `.fbx/.glb/.gltf/.obj`，与模型 Op 后缀表独立。四张表互不带动：[模型后缀](../02_structure/model-suffixes.md)。

## 加东西

大类（贴图/材质/模型）**不**反射，总面板手接线。大类内 Op **反射**无参 `I*AssetOperation`。

```text
新贴图 Op     → 实现接口 → L3 勾进 masterBatchOperationIds（AllowMasterBatch=true 才进⑤/L1）
新贴图后缀     → 加 Codec，否则 Collector 看不到
新模型后缀     → 改 SO 列表，不要只改某个 Op 硬编码
全新大类       → 接口+Registry+Collector+Runner+SO+总面板一块+RunMasterBatch 接线
```

## 代码

| | |
|---|---|
| ⑤ 编排 | `TOol/Editor/Shared/ResourcePostProcessService.cs` |
| Evaluate 三态 | `AssetOperationEvaluation.cs` |
| 贴图 Registry / 剥 master | `TextureOperationRegistry.cs` |
| Codec | `Texture/Codec/TextureCodecRegistry.cs` |
