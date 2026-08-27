# 开发备忘目录（进行中）

> 位置：`Assets/Plugin/docs/dev-wip/`  
> 用途：CLI / 自动化一体流程开发中的**战略、结构、疑问、实现流程**分册；不替代 `PACKAGING_RULES.md` / `CHANGELOG.md`。  
> 推荐线：**v1.5.0**（`main` · 流程稳定）· 工单：[prd-docs#274](http://swm-server.local:3000/Admin/prd-docs/issues/274)

| 分册 | 路径 | 内容 |
|---|---|---|
| **1. 需求与战略** | [01_requirements/](./01_requirements/) | 已确认战略；技术选型 / 知识 / 操作要点；Converter 契约 |
| **2. 当前整体结构** | [02_structure/overview.md](./02_structure/overview.md) | 按文件夹分类的类与中文职能 |
| **3. 待处理 / 模糊项** | [03_open-items/backlog.md](./03_open-items/backlog.md) | 进行中待办；文末「历史结束事务」；D13 归档见 [d13-glb-magenta](./03_open-items/d13-glb-magenta.md) |
| **4. 流程与对外接口（A 中间层）** | [04_implementation/pipeline-flow.md](./04_implementation/pipeline-flow.md) | 两块总览；窄口表；错误码；就绪度 |
| **4b. D1 AB 核对** | [04_implementation/d1-ab-only.md](./04_implementation/d1-ab-only.md) | 仅双端 AB **已锁**（已收口） |
| **4c. 冒烟·单文件·结果** | [04_implementation/smoke-and-results.md](./04_implementation/smoke-and-results.md) | 本轮核对：输入与错误码分层 |
| **4d. D6 UnityGLTF** | [04_implementation/d6-unitygltf-docker.md](./04_implementation/d6-unitygltf-docker.md) | git 依赖替换 file:；人工步骤 |
| **4e. D5 CLI 入口（B）** | [04_implementation/cli-getting-started.md](./04_implementation/cli-getting-started.md) | `PipelineCli.Run` 第一刀已写；无头验收 |

历史长文归档入口（将逐步以本目录为准）：[`../CLI_AUTOMATION_DEV.md`](../CLI_AUTOMATION_DEV.md)

---

## 当前迭代一句话

**基线：** ② 导入 → ③ Prefab → ⑥ 仅 AB（quiet）  
**可选：** ④ 平铺、⑤ 压图/刷白、⑥ 门禁/扩展产物  
**优先已做：** 窄口 + Runner + D3 总面板 + D2 单文件 + **D1 契约收口** + **D4 GLB 入库**  
**GLB 样例：** `Assets/Art/ggdddd` 编辑器内已跑通；洋红 **D13 已归档**  
**下一步：** 回切后无头跑通 **D5**（`PipelineCli.Run`）；⑤结果码 **D16**。Prefab 夹顶点色观感 **D20 可选**  
**对外接口：** (A) 中间层已可用 · (B) CLI 第一刀已写 → [cli-getting-started](./04_implementation/cli-getting-started.md)  
**配置分层：** 总步骤 → Pipeline SO；资源自动细节 → 资源总面板 Prefs
