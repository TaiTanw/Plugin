# 已确认战略汇总

> 2026-09-16 按当前代码与用户确认更新。操作者入口：[插件根 README](../../../README.md)。实现差距看 [结构现状](../02_structure/overview.md)，待确认项看 [backlog](../03_open-items/backlog.md)。历史方案不自动成为新要求。

## 目标

用 Unity `-batchmode / -executeMethod`，从 FBX、OBJ、GLB、glTF 到 Android / iOS AssetBundle。编辑器与 CLI 共用自动化 Runner；Docker / 队列仍是 V1.2 外壳，不属于当前拆分。

## 工程与 Git

| 项 | 结论 |
|---|---|
| 主开发宿主 | Unity 2022.3，`Plugin2022`；2020 的 `ModleEvent` 只作对照，不原地升级 |
| 插件仓 | `Assets/Plugin`；宿主不另建整仓、不改 Gitea 仓名 |
| 远程 | `team` 为 Gitea 协作源，`origin` 为 GitHub 备份 |
| 版本 | 当前推荐 **v1.6.0**；v1.5.3 为上一发布标签 |

## 管线产品拆分

| 项 | 已确认要求与当前口径 |
|---|---|
| 最小线 | ②→③→⑥是可裁剪最小线，不是当前默认配置 |
| 默认线 | 操作者入口的②入库固定开启；`PipelineOptions.FromSettings` 读取仓内 SO 控制③④⑤⑥，默认均开启，④⑤可关。底层直接 new Options 可显式改写 |
| ④归属 | 实现已物理迁入 TOol；消费 ctx 的编排暂归中间层。**内核目标改为只收 FlattenPlan，不再读 ctx。** 旧 3518 行对照删除，禁止先清空。⑥ 已有独立 Build 口，不随④重写 |
| ④人工操作 | “普通平铺（B）”“原子迁移（B′）”均执行完整④；内部七步拆分服务代码结构，不开放任意单步产品按钮。用户已确认，这不是待决风险 |
| ctx | 自动管线每模型在②后构建一次；人工**禁止** `PipelineJobContext.Build`，Scan 组 plan。事实与 SO/人工决定分开 |
| ⑤材质 | 材质自身的透明/裁切状态由插件 2 Material OP 读取，不为此扩展模型级 ctx |
| ⑥ | 默认双端 AB，可选 UnityPackage；旧业务门禁、runtime/xlsx/报告等全套导出已删除，不是“默认关但仍可启用” |
| glTF | ②整包入库，④ B′保持相对 URI 树；先封装 GLB 的 D22 不开发，不用 Unity 场景 Export 替代入库 |

## 插件分工（目标与差距）

| 侧 | 负责 | 当前尚未收口 |
|---|---|---|
| 中间层 Pipeline（含暂归的④编排内核） | 总步骤、按模型 ctx、分支、请求组合、结果/退出码 | 自动与人工④仍分别编排；首错保存已修，稳定行 ID 仍停放 |
| 插件 2 TOol | 导入与设置、③ Prefab、资源执行能力、⑤ Op | 物理承载④的约 3,518 行遗产，尚未按七步拆文件；不能因在 TOol 就要求它自行重建 ctx |
| 插件 1 Retinar | ⑥输出格式与 AB；现存平铺菜单只是转发适配 | ④已停写 AB 标签（步骤 8）；格式白名单见 D24-R4 |

插件 1 不应重新承担 Art Importer、Extract、Prefab Transform、材质和引用变换。这里的目标不等于当前已完全消除所有副作用。

## 配置分层

**已确认方向：管线和单项操作都走 SO；同数据类可以有人工与管线不同实例。面板按 SO 来源判断可编辑/只读。**

| 配置 | 当前落地 | 边界 |
|---|---|---|
| 总步骤 | `PipelineStepSettings` SO | 仅控制③④⑤⑥与 quiet；操作者面板和 CLI 的②入库固定开启。底层 API 仍可直接设置 `PipelineOptions.RunImport` |
| 平铺单项 | `FlattenOperationSettings` 同类两实例：TOol/ConfigData/Manual 与 Pipeline/ConfigData | 人工可拖 SO；面板只编辑本来源目录，跨来源/未知来源只读；运行时冻结 `FlattenOperationPolicy` |
| 导出 | `RetinarExportSettings` SO → 构建 Options | 不含旧门禁/全套报告能力 |
| ⑤资源类型纳入、人工批量路径、部分 UI 状态 | 仍有 EditorPrefs | 尚未完成 SO 化，不得宣称 CLI 已脱离机器状态 |
| ⑤材质等细节 | 各资源 Settings SO；Material 目前共用一份 | 尚未拆人工/管线实例，也没有平铺式完整快照 |

SO 的只读是当前面板编辑规则，不是全局 Inspector 权限锁；后续其它单项配置迁移按相同方向推进，具体范围与风险另核对。

## “自动”与 Art 的边界

- ②导入会触发 AssetPostprocessor；不等于 Runner 主动调用全部“设置自动”。
- 配置导入根内、受支持 ModelImporter 的安全基线已与总闸/排除解耦；其它 Assets 基线和策略自动仍服从原闸。Art 模型由 ModelImportSettingsProcessor 硬跳过，④负责交付副本设置。
- 贴图/后处理自动的排除规则与模型硬跳过不是同一种保护，不能统称“所有自动永不触碰 Art”。
- ⑤是显式后处理，会处理 Art。④成功且未覆盖路径时，Runner 传本次 Art 单元；接口传 null 仍会回落人工批量路径。编排不写这些 Prefs，但部分类型纳入开关仍会读取它们。

## 已确认方向与开工前核对

1. D26-6（2026-09-14）：用户倾向合法跳过并明确提示，前提是架构边界足够。窄口有FailedCount/Canceled/Report，未统一typed跳过原因；先核对汇总，不让Runner读Op内部/解析报告。不足时评估中间层排错层，具体方案另核对，不直接加硬失败。
2. D24-R4：**已落地**（步骤 8）。④ 停写 Prefab Importer AB 标签；⑥ 用 `AssetBundleBuild[]`。
3. D13-R1（2026-09-14）：Art 玻璃与目标移动端 AB 均已验收，退出顶部队列。透明语义仍在插件 2 Material OP，不扩模型级 ctx。
4. 缺伴生（2026-09-14）：glTF typed `MissingUris` 非空 → ④ `FlattenFailed(40)` **整趟停止**（不承诺其余行继续，也不回滚已写出的 Art）。OBJ 缺 `.mtl`/贴图不进该闸。是否改「坏行跳过」属 Runner，须另拍。
5. B 质量（残留外部 `.fbm`）：步骤 14 **Fail(41)** 不停⑤⑥。贴图身份 **Fail(42)** 不停⑤⑥、槽位不清。空槽只观察。
6. ④拆分：plan 口与步骤 1–14 已落地；内核三份 partial **未拆文件、未迁目录**。① 不 `Build` ctx。⑥ 不重写。见 [D24 R3](../03_open-items/d24-boundary-plan.md#r3-flatten-split)。

当前不另开 Docker/队列、不开放七步乱序、**不先清空旧④**、不顺手改命名/APP 取包契约。⑥ 不随④重写。
