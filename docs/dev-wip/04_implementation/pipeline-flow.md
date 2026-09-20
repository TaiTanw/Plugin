# 流程与窄口

现约入口：[开发者须知](../README.md)。CLI：[cli-getting-started](./cli-getting-started.md)。相位 IO：[pipeline-phase-io](./pipeline-phase-io.md)。

面板与 CLI **共用** `PipelineRunner`。CLI 不是第二套管线。

```text
(A) PipelineWindow / 测试
(B) PipelineCli.Run（argv → Options → Runner → Exit）
        └→ PipelineOptions → PipelineRunner.Run
              [1] ToolImportApi.ImportSingleModel
              ③ ToolPrefabApi.BuildPrefabs
              ④ ToolFlattenApi.FromContext → Run(plan)
              ⑤ ToolPostProcessApi.RunMasterBatch
              ⑥ RetinarAbApi.Build
```

管线 [1] 拷进 **步骤 SO 的 Incoming 根**，不是人工选择器那份。人工「执行导入」才读 `BatchFbxImportSettings`。

处理区连锁：关③则④⑤关；关④则⑤关。`ApplyTo` / Runner 同样约束。⑥ 不绑③。

```text
Bindings（或 SourcePath+MaterialId 合成一行）
  → 逐行入库 + ②.5 ctx
  → 逐行③ → ④ 该行 ctx 译 plan → Run
  → 开④时 PostProcessFolderPaths = 本次 Art 单元
  → ⑤（大类开关来自步骤 SO；Op 列表来自管线 ProcessSettings）
  → ⑥ 打当前 prefabPaths（④改写后的 Art Prefab；⑤不另交给⑥一份路径）
```

⑤ `FailedCount>0`（Execute Failed）→ 50；Skip / NA / 未勾选 / 仅取消进度 **不算**。不解析 Report。Runner 不探 Op 内部。

| 字段 | 作用 |
|---|---|
| `SourceBindings` | 主输入；空则合成一行 |
| `RunPrefab/Flatten/PostProcess/Ab` | 来自步骤 SO |
| `PostProcessInclude*` | 步骤 SO 大类；不是人工 Prefs |
| `FlattenPolicy` | 管线平铺 SO 冻结快照 |
| `Quiet` | 禁 Dialog；CLI 写入后强制 true |
| `CleanupImportRootsAfterRun` | 整趟结束后清 Incoming+IncomingPrefab |
| `CleanupArtAfterRun` | 整趟结束后清 Art；先于导入区，一次 Refresh |

硬失败中途不清工作根。

## 退出码

| 码 | | 后面 |
|---:|---|---|
| 0 | Ok | — |
| 10 | BadArgs | 停 |
| 20 | ImportFailed | 停 |
| 30 | PrefabFailed | 停 |
| 40 | FlattenFailed（含 glTF MissingUris） | 停 |
| 41 | leftover `.fbm` | ⑤⑥继续 |
| 42 | 贴图身份 | ⑤⑥继续；与 41 同趟时 41 优先 |
| 50 | ⑤ FailedCount>0 | ⑥继续 |
| 60 | ⑥ 双端未都成功 | 停 |
| 70 | License | **从未赋值** |
| 80 | 未捕获 | — |

glTF 缺伴生 40 整趟停、不回滚已写出的 Art。OBJ 缺件不进该闸。

## 窄口

| | Runner | 缺口 |
|---|---|---|
| `ToolImportApi` | 单文件 | CLI 无文件夹批量 |
| `ToolPrefabApi` | ✓ | — |
| `ToolFlattenApi` | plan | 内核未拆文件 |
| `ToolPostProcessApi` | Result | 材质 SO 未拆份 |
| `RetinarAbApi` | ✓ | 平台/LZ4 钉死 |
| `PipelineCli` | `-source` / `-materialId` | 扩 flag 另开项 |
