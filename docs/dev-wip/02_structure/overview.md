# 当前结构

现约入口：[开发者须知](../README.md)。操作者：[操作者须知](../../operator/README.md)。

```text
Assets/Plugin/                 Git 根
├─ Pipeline/                   步骤 SO、Runner、CLI、ctx、人工④调度
│  └─ Editor/ManualFlatten/
├─ TOol/                       [1][2]③⑤；④内核 Generated/Flatten
│  └─ ConfigData/Manual/       人工平铺 SO
└─ RetinarBatchBuilder_Share/  ⑥ AB
```

Incoming / IncomingPrefab / Art 是工程内工作区，不是外部源目录。管线清空勾选清的是整根内容（根夹留下），不是「只清本趟单元」。

## 自动线

```text
PipelineWindow / PipelineCli
  → FromSettings → Runner
  [1] ImportSingleModel（CloneWith 保留行配置）
  ②.5 每模型 JobContext.Build     ← 仅自动
  ③ BuildPrefabs
  ④ FromContext(ctx, request) → Run(plan)
  ⑤ RunMasterBatch（Art 单元；大类开关来自步骤 SO）
  ⑥ RetinarAbApi.Build
```

Binding：`SourcePath`、`MaterialId`、`ConvertZUpToYUp`。同一物理路径不能在一张表出现两次。

## ④

自动与人工都进 `ToolFlattenApi.Run(plan)`。公开分步转发已删。内核三份 partial 不读 ctx。

| | 自动 | 人工 |
|---|---|---|
| plan | `FromContext` | `FlattenManualPlanFactory`（按钮给 Branch） |
| 模型输入 | ③ 已写出的 Prefab | 选中模型会先③ |
| 失败 | Prefab/ctx 对不齐 → Fail | 不建 ctx |

七步：Begin → B\|B′ → E? → D → C → Finish。Finish **不写** AB 标签。能力表：[pipeline-flatten-capabilities](../04_implementation/pipeline-flatten-capabilities.md)。

目录未迁、内核未拆文件。不要顺手开：① 外置图跟拷扩范围、重写⑥、D19 当门禁。

## ⑤

Collector 定类型池 → 勾选 Op 的 `Evaluate` → 只 Execute `NeedsWork`。  
管线⑤读 Pipeline 那份 ProcessSettings + 步骤 SO 大类开关；人工总面板读 TOol SO + Prefs。  
`AllowMasterBatch=false` 从 master/import 列表剥掉。细节：[Op 扩展](../04_implementation/op-recognition-and-extend.md)。

## ⑥

`RetinarAbApi.Build`：显式 `AssetBundleBuild[]`。平台/LZ4 仍钉在代码。旧 DirectPackage / 门禁代码已删。

## 仍未收口（不是旧债清单）

- 稳定行 ID 停放；`materialId` 会重名
- 材质 SO 未拆人工/管线；L1 路径仍 Prefs
- Runner 仍有 D19 诊断/补偿，不是纯转发
- 41 / 42 闸在，缺咬住退出码的真样 → backlog 调试暂放
