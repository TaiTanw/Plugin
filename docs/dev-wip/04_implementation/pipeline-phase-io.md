# 相位入参

现约：[开发者须知](../README.md)。流程：[pipeline-flow](./pipeline-flow.md)。ctx：[pipeline-job-context](./pipeline-job-context.md)。

| 口头 | 是 | 不是 |
|---|---|---|
| **[1] 入库** | 拷进管线 Incoming 根 + `ImportAsset` | ctx |
| **[2]** | 导入期总闸 | 2.5 |
| **2.5** | [1] 后 `JobContext.Build` | 步骤开关 |
| **ID2** | 行上的名字 | 稳定主键 |

步间靠窄口返回值接力，不改 L1 Prefs。`StepResult` 未做。批量面板筛后缀只给人看，不缩小内核表、不影响 CLI。

## 入口

| | 填表 | 入库 |
|---|---|---|
| 管线面板 | 拖入或「输出到编排」（路径+ID2+OBJ 轴向） | 跑管线时走 Runner [1]，不走 Conflict |
| 选择器「执行导入」 | — | 人工根 + Conflict |
| CLI | 一个 `-source` | 同一 Runner [1] |
| Pack | 未做 | — |

ID2 缺省：同父目录（非递归）只有一个内核文件 → 三层夹名；否则三层 + `_` + 全名。手填覆盖。`PipelineSourceBinding` 无行号字段。

内核入库白名单在 `ToolImportApi`（fbx/glb/gltf/obj）。⑤ 的模型后缀表是另一份，两边各自加行。不要把 ctx 当 [1] 入场券；`.gltf` 入库 Scan 是为了拷全包，2.5 再 Scan 是为了④。

## Runner

```text
Bindings（空则 SourcePath+MaterialId 合成一行）
  foreach： [1] 夹名=id2 → ModelPaths；CloneWith 保留轴向；Build ctx
  foreach： ③ 该行 → ④ 该行 FromContext → Run(plan)
             覆盖 prefabPaths；PostProcessFolderPaths += Art/<ID2>/
  ⑤⑥ 吃列表
```

③ 不读 ctx。④ 读 `HasExternalUris` / `ImporterKind`；轴向来自 Binding。

## 窄口

**[1]** `ImportSingleModel(source, incomingFolderName, out assetPath, out msg)`  
工程外：清 `Incoming/<id2>/` 再拷。已在 Assets：跳过、不清夹。失败 20。面板/CLI 始终入库。

**[2]** 无窄口。`ImportPipelineSettings` / `MasterEnabled`。不传给③④⑤⑥。

**2.5** `PipelineJobContext.Build(工程内路径)`。Build 不改退出码。

**③** `BuildPrefabs([该模型], 该行 ID2)`。空 → 30。

**④** `FromContext` → `Run(plan)`。`MissingUris` 或 Begin/B/B′/Finish 失败 → 40 整趟停。E/D/C 为 void。

**⑤** `RunMasterBatch`；`FailedCount>0` → 50，⑥仍跑。

**⑥** 打当前 `prefabPaths`。双端都成功才 0。

`PipelineResult.PrefabOutputs` 是③当时的列表；④覆盖不会回写这里。⑥ 用局部 `prefabPaths`。插件 1/2 不引用错误码类。
