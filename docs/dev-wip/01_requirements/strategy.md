# 已确认战略

现约入口：[开发者须知](../README.md)。差距与类名：[结构](../02_structure/overview.md)。队列：[backlog](../03_open-items/backlog.md)。

| 项 | 口径 |
|---|---|
| 宿主 | Unity 2022.3 `Plugin2022`；插件仓 `Assets/Plugin`；`team` Gitea、`origin` GitHub；推荐 **v1.6.8** |
| 线 | 面板/CLI 入库固定开；SO 管③④⑤⑥（默认全开，④⑤可关）。②→③→⑥ 是可裁剪最小线，不是默认 |
| ④ | 物理在 TOol；自动 `FromContext`→`Run(plan)`；人工选 Branch 组 plan，**禁止** `PipelineJobContext.Build`。两按钮都是完整④，不接⑤⑥。内核不读 ctx、不 Load 管线 SO。旧 3518 行对照删除，禁止先清空 |
| ⑤ | 代调 L1 `RunMasterBatch`。材质透明在 Material Op，不扩模型 ctx。贴图无条件 NeedsWork 的 Op 见 `AllowMasterBatch` |
| ⑥ | `RetinarAbApi.Build`；`{stem}_android.assetbundle` / `_ios`；产品夹不分平台。旧门禁/全套导出已删，不是「默认关仍可开」 |
| glTF | ②整包入库，④ B′ 相对 URI 树。转 GLB（D22）不开发 |
| 配置 | 管线/人工同数据类可两份实例。L2 勾选、L1 路径仍 Prefs。材质 SO 尚未拆份 |
| 停放 | Docker/队列/License、七步乱序产品按钮、坏行跳过、稳定行 ID、⑥随④重写 |

Docker / lean-api / COS 属 V1.2 基建，不是本仓内核。错误码 **70 从未赋值**。
