# ctx：事实，不是目的

现约：[开发者须知](../README.md)。④ 能力：[pipeline-flatten-capabilities](./pipeline-flatten-capabilities.md)。相位 IO：[pipeline-phase-io](./pipeline-phase-io.md)。

不为 glTF 另开一条管线。`Build` 不是用户可见步骤；仅自动线在 [1] 后调用。人工禁止 `PipelineJobContext.Build`。① 不承担 Build。

| | ctx（事实） | Options / SO（目的） |
|---|---|---|
| 问 | 这次包是什么样 | 这次要跑哪些步 |
| 例 | 外 URI、伴生、Importer、`MissingUris` | `RunFlatten` 等 |
| 禁止 | 存步骤开关、存 `FlattenFileMode`、存轴向 | 靠后缀改开关语义 |

`ConvertZUpToYUp` 在 Binding / `ToolFlattenRequest`，不进 ctx。

## 闸

④ 的执行计划由 Prefab 的模型依赖逐个生成。`ctx.HasExternalUris` 仍是自动线的入库观测事实，不能决定整份 Prefab 的复制方式。`ModelRelativeFileProbe` 按格式选择扫描器，当前注册 `.gltf`；扫描器读取实际文件引用，决定该模型采用分类复制还是保留相对目录。`.bin` 后缀本身不是判定条件。

| 模型 | ④模型策略 |
|---|---|
| `.glb` / 全 `data:` 的 `.gltf` | 分类复制 |
| `.gltf` + 相对 URI（即使文件缺失） | 保留相对目录；缺件由模型计划阻断 |
| `.fbx` / `.obj` | 分类复制；OBJ `.mtl` 由既有跟拷逻辑处理 |

缺文件 ≠ 无外 URI。`FlattenModelUnit.MissingReferences` 在④ Begin 前阻断当前行。`ctx.MissingUris` 保留自动线早期探测和兼容用途。不要解析 `Warnings` 文案当控制流。

## 字段

`PrimaryAssetPath` · `SourceExtension`（日志/分派）· `ImporterKind` · `HasExternalUris` · `SidecarPaths` · `MissingUris` · `MainAssetOk` · `MaterialForm`（观测，不开闸）· `Warnings`

不要放：`Run*`、`triggeredByImport`、`ShouldBakeShader`。

③⑤⑥ 不读 ctx。⑤ 仍按 Art 单元 Collector 扫，不改成「只处理 ctx 地址」。

## 探测

`.gltf`：入库侧 `PipelineGltfUriProbe` → `GltfPackageFiles.Scan`；④侧 `ModelRelativeFileProbe` 调用同一 glTF 扫描器并生成每个模型的策略。④复制器只消费扫描结果，不自行解析 JSON。

新增有相对文件的模型格式时，在模型类型识别与分类规则中登记后，为 `ModelRelativeFileProbe` 添加格式扫描器；扫描器必须给出伴生文件与缺失引用。OBJ 现由 `.mtl` 专用逻辑处理，迁入通用扫描器时须先覆盖材质贴图路径与导入行为。
