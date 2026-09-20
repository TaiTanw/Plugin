# Generated / Flatten — ④ 平铺到 Art

现约：[开发者须知](../../../../docs/dev-wip/README.md) · [④ 能力](../../../../docs/dev-wip/04_implementation/pipeline-flatten-capabilities.md)。  
窄口：[`../../Shared/Api/ToolFlattenApi.cs`](../../Shared/Api/ToolFlattenApi.cs)。对齐 ③ [`../Prefab/`](../Prefab/)。

文件在插件 2，编排在 Pipeline。入口 `FromContext` → `Run(plan)`。公开七步转发已撤。⑥ 不随④重写。内核仍三份 partial，未迁目录。

人工 / 管线各一份 `FlattenOperationSettings`；开跑冻 `FlattenOperationPolicy`。菜单两条完整④，不接⑤⑥。

```text
Run(plan)
  Begin → B|B′ → E? → D → C → Finish
```

| 分支 | 读什么 | 不读什么 |
|---|---|---|
| B vs B′ | `HasExternalUris` → plan.Branch | 后缀是不是 `.gltf` |
| E | `ImporterKind == ModelImporter` | 步骤 SO |
| 清夹 / 轴向 | `ToolFlattenRequest` | ctx |

`MissingUris` → 编排 40。Finish 后 leftover `.fbm` → 41、身份 → 42，不停⑤⑥。OBJ 缺件不进 40。Finish 不写 AB 标签。
