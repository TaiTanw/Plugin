# 风险说明

现约入口：[开发者须知](../README.md)。硬约束仍以须知正文为准。

本页只收：**做错会静默出坏结果、通道混用、Op 之间的隐式调用**。  
操作者点哪里：[操作者须知](../../operator/README.md)。  
归档切片（D24 / D23 / D13 / D6 等）不收录。

---

## 贴图 Op 调用材质实现（亮度 → Alpha）

`bake_luminance_to_alpha` 只负责把 RGB 亮度写入贴图 Alpha，并打开 TextureImporter 的 Alpha Is Transparency。它 **不是** `ToolPostProcessApi.RunMasterBatch`，也不跑 `IMaterialAssetOperation.Execute`。

精准面板「亮度写入 Alpha」下勾选跟材质（本机 EditorPrefs，默认 **关**，与 L2 Op 勾选同一套持久化，不进贴图 SO）时，贴图 Op 会直接调用材质 Op 类上的内部方法 `NormalizeDeliverableShaderOperation.ApplyTargetSurface`（经 `ApplyFadeToMaterialsUsingMainTexture`），把 **主贴图是这张图** 的 Opaque Standard 改成 Fade。

| 风险 | 说明 |
|---|---|
| Op 交叉依赖 | 贴图 Op 绑住了材质 Op 的实现，不是编排接口。改 `ApplyTargetSurface` / 换 Shader 表面语义，亮度跟材质会一起变。以后应抽到 Shared，而不是让贴图 Op 去 `Execute` 材质 Op。 |
| 开关语义 | 默认关：烤完模型仍可能看起来没变（OBJ 导入几乎不管 MTL `map_d`，材质常是 Opaque，不读 Alpha）。这是 OBJ 与 Unity 约定差，不是漏引用。 |
| ④ 不跟此开关 | 平铺拷材质时，若主贴图 **已经** 标了 Alpha Is Transparency，仍会自动 Fade。管线再导入不依赖精准面板勾选。不要把「开关关着」理解成交付材质永远 Opaque。 |
| 误伤 | 主贴图误开 Alpha Is Transparency 的普通漫反射，④ 或勾选跟材质后会变成 Fade。不要对 ORM / 法线 / 普通 albedo 跑本 Op。 |
| 进⑤ | `AllowMasterBatch = false`。Evaluate 对适用池无条件 NeedsWork，禁止进 L1 / 管线⑤ / 导入自动。 |

---

## 三条「自动」混词

导入期跳过 Art ≠ ⑤ / L1 不碰 Art。刷白不要塞进 `OnPostprocessModel` 打 Art。全文：[tech-and-ops](../01_requirements/tech-and-ops.md)。

---

## AllowMasterBatch

贴图 Op 必须手写。Evaluate 对适用池无条件 NeedsWork 的必须 `false`。现网仅亮度 → Alpha。加 Op：[op-recognition-and-extend](../04_implementation/op-recognition-and-extend.md)。

---

## 导入栈里改资产

全局导入设置（2）高级折叠已写：OnPostprocess 之后 delayCall 改资产不保证成功（嵌套导入、冲顶点色、时序）。现网刷白 / 压图在④之后的⑤，不要把 delayCall 入队复活。Processor 空钩子勾了也不会在导入时执行。「不介入目录」现网 OnPreprocess / ⑤ 都不读。

`modelUseExternalMaterials` 默认关。打开会改变④的 **输入形态**（Incoming 出现 Materials / `.fbm`），不是导入观感。附录：[④ 能力](../04_implementation/pipeline-flatten-capabilities.md)。

---

## batchmode / CLI

禁止 `Selection`、禁止 `DisplayDialog`。CLI 强制 Quiet（≠ 退出编辑器，≠ 清空工作根）。

---

## ④ 缺件与退出码

glTF 缺必需伴生 → **40** 停。OBJ 缺 `.mtl`/贴图只 Warning，**不进 40**。不要解析 `Warnings` 文案当控制流。  
**41** leftover `.fbm` / **42** 贴图身份：⑤⑥继续。不要用「未知依赖必须停⑥」代替④缺 sidecar 闸。

④ 不以单样例代替回归（见 [smoke-and-results](../04_implementation/smoke-and-results.md)）。
