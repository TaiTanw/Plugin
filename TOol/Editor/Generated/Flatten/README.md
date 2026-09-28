# Generated / Flatten — ④ 平铺到 Art

现约：[开发者须知](../../../../docs/dev-wip/README.md) · [④ 能力](../../../../docs/dev-wip/04_implementation/pipeline-flatten-capabilities.md)。  
窄口：[`../../Shared/Api/ToolFlattenApi.cs`](../../Shared/Api/ToolFlattenApi.cs)。对齐 ③ [`../Prefab/`](../Prefab/)。

文件在插件 2，编排在 Pipeline。自动入口 `FromContext` → `Run(plan)`；手动入口从选中资源建 plan → `Run(plan)`。公开七步转发已撤。⑥ 不随④重写。

手动 / 管线各一份 `FlattenOperationSettings`；开跑冻 `FlattenOperationPolicy`。手动菜单只有一个完整④入口，不接⑤⑥。

```text
Run(plan)
  Begin → 按模型复制依赖 → E? → D → C → Finish
```

| 决策 | 读什么 | 不读什么 |
|---|---|---|
| 模型复制策略 | `ModelRelativeFileProbe` 的格式扫描结果 → `ModelUnits` | 单看 `.bin` 是否存在 |
| E | `ImporterKind == ModelImporter` | 步骤 SO |
| 清夹 / 轴向 | `ToolFlattenRequest` | ctx |

模型级 `MissingReferences` → 编排 40。Finish 后 leftover `.fbm` → 41、身份 → 42，不停⑤⑥。OBJ 缺件不进 40。Finish 不写 AB 标签。模型重绑按本单元依赖路径定绑，规则在④能力。
