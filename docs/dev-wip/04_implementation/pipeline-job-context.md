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

④ 拆文件只看 **`HasExternalUris`**，执行层不 `if (.gltf)`。B 与 B′ 互斥。

| 包 | `HasExternalUris` |
|---|---|
| `.glb` / 全 `data:` 的 `.gltf` | false → B |
| `.gltf` + 相对 URI（即使文件缺失） | true → B′；缺件另见 `MissingUris` |
| `.fbx` / `.obj` | false（Build **不扫** OBJ 包） |

缺文件 ≠ 无外 URI。`MissingUris` 当前 **仅 glTF 探针**写入 → ④ Begin 前 **40**。OBJ 缺件只在入库 Warning，不进 40。不要解析 `Warnings` 文案当控制流。

## 字段

`PrimaryAssetPath` · `SourceExtension`（日志/分派）· `ImporterKind` · `HasExternalUris` · `SidecarPaths` · `MissingUris` · `MainAssetOk` · `MaterialForm`（观测，不开闸）· `Warnings`

不要放：`Run*`、`triggeredByImport`、`ShouldBakeShader`。

③⑤⑥ 不读 ctx。⑤ 仍按 Art 单元 Collector 扫，不改成「只处理 ctx 地址」。

## 探测

`.gltf`：`PipelineGltfUriProbe` → `GltfPackageFiles.Scan`（与②跟拷共用 Scan，职责仍两截：拷全包 vs 告诉④能不能拆）。换解析器只改 Scan。不要在 Flatten 里扫 JSON。

OBJ 若将来进同一闸：在 `Build` 的 `.obj` 分支写 `MissingUris`，不要让④自己扫 `.mtl`。
