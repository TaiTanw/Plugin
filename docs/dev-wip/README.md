# 开发备忘目录（进行中）

> 位置：`Assets/Plugin/docs/dev-wip/`  
> 用途：CLI / 自动化一体流程开发中的**战略、结构、疑问、实现流程**分册；当前约定看战略、当前实现看结构、待办看顶部队列；历史规则/报告不覆盖后续明确确认，CHANGELOG 保留版本追溯。\
> 已有发布标签：**v1.5.3**；本地 `main` 核对至 **e51469a**（2026-09-14）· 工单：[prd-docs#274](http://swm-server.local:3000/Admin/prd-docs/issues/274)

| 分册 | 路径 | 内容 |
|---|---|---|
| **1. 需求与战略** | [01_requirements/](./01_requirements/) | 已确认战略；技术选型 / 知识 / 操作要点；Converter 契约 |
| **2. 当前整体结构** | [02_structure/overview.md](./02_structure/overview.md) | 自动/人工入口、④临时归属、SO、⑤材质类级数据流与⑥边界 |
| **3. 当前待办 / 模糊项** | [03_open-items/backlog.md](./03_open-items/backlog.md) | 按严重程度排序的唯一当前队列；完成项在文末归档 |
| **3b. D24 边界拆分** | [03_open-items/d24-boundary-plan.md](./03_open-items/d24-boundary-plan.md) | 3,518 行平铺内核接管、ctx 临时中间层归属、平铺 SO 与插件 1 输出格式白名单 |
| **3c. ④收口步骤** | [03_open-items/d24-flatten-steps.md](./03_open-items/d24-flatten-steps.md) | 最小可验证步骤 1–14；按序开发、顺利再下一步；R1b 基本核对；11 依赖 9 |
| **4. 流程与对外接口（A 中间层）** | [04_implementation/pipeline-flow.md](./04_implementation/pipeline-flow.md) | 两块总览；窄口表；错误码；就绪度 |
| **4h. 各相位入参/返回值** | [04_implementation/pipeline-phase-io.md](./04_implementation/pipeline-phase-io.md) | 现网 + Bindings/ID2；批量输出到编排接口；1 与 2.5 |
| **4b. D1 AB 核对** | [04_implementation/d1-ab-only.md](./04_implementation/d1-ab-only.md) | 仅双端 AB **已锁**；文件名重开见开发日志 |
| **4c. 冒烟·单文件·结果** | [04_implementation/smoke-and-results.md](./04_implementation/smoke-and-results.md) | 本轮核对：输入与错误码分层 |
| **4d. D6 UnityGLTF** | [04_implementation/d6-unitygltf-docker.md](./04_implementation/d6-unitygltf-docker.md) | git 依赖替换 file:；人工步骤 |
| **4e. D5 CLI 入口（B）** | [04_implementation/cli-getting-started.md](./04_implementation/cli-getting-started.md) | **已验收**；参数/退出码已冻 |
| **4f. Op 识别与扩展** | [04_implementation/op-recognition-and-extend.md](./04_implementation/op-recognition-and-extend.md) | ⑤ 扩展名识别；加 Op / 加后缀 / 加大类 |
| **4g. 导入 ctx（D23）** | [04_implementation/pipeline-job-context.md](./04_implementation/pipeline-job-context.md) | 事实归类；探测 [§7](./04_implementation/pipeline-job-context.md#7-probe-extend) |
| **4i. ④ 能力查封** | [04_implementation/pipeline-flatten-capabilities.md](./04_implementation/pipeline-flatten-capabilities.md) | 拷贝循环 vs B′；`Art/<名>/<名>/` |
| **4k. D24-R1 黑盒审计** | [04_implementation/flatten-core-audit.md](./04_implementation/flatten-core-audit.md) | 调用图、方法归类、高风险耦合、安全拆分与人工步进决策口 |
| **4j. D23 本刀报告** | [04_implementation/d23-slice-report.md](./04_implementation/d23-slice-report.md) | D23/D18 历史切片报告；当前行为以结构总览与本报告顶部勘误为准 |
| **5. 开发日志** | [05_dev-log/timeline.md](./05_dev-log/timeline.md) | 提交次第、#274、标签、Plugin 仓 CLI 前决策 |

D18 正文（勿点表内链接）：[d18k](./03_open-items/backlog.md#d18k) · 开发日志：[timeline](./05_dev-log/timeline.md)

历史长文归档入口（将逐步以本目录为准）：[`../CLI_AUTOMATION_DEV.md`](../CLI_AUTOMATION_DEV.md)

---

## 当前迭代一句话

**基线：** ② 导入 → ③ Prefab → ⑥ 仅 AB（quiet）  
**当前默认：** SO 开启②③④⑤⑥；④⑤可关，⑥可选 UP。旧门禁/全套报告已删除\
**优先已做：** 窄口 + Runner + D3 总面板 + D2 单文件 + **D1 契约收口** + **D4 GLB 入库** + **D5 无头 CLI**  
**GLB 样例：** `Assets/Art/ggdddd` 编辑器内已跑通；原洋红 **D13 主体已归档**；R1 透明修复、Art 玻璃与移动端 AB **均已验收**\
**下一步：** ④ 第 10 步 CLI 已核（Extract 只归 E）。第 11 步暂定 Fail，因现网空槽事实会误杀 GLTF/GLB，先停。D25-2（A）① FBX 外置图已跟拷。
**对外接口：** (A) 中间层已可用 · (B) **D5 已验收** → [cli-getting-started](./04_implementation/cli-getting-started.md)  
**配置分层：** 总步骤 → Pipeline SO；平铺细节 → 同类 `FlattenOperationSettings` 的人工/管线独立资产；其它资源配置仍为 Prefs + SO 混合；⑤ Material SO 尚未分人工/管线
