# 开发备忘目录（进行中）

> 位置：`Assets/Plugin/docs/dev-wip/`  
> 用途：CLI / 自动化一体流程开发中的**战略、结构、疑问、实现流程**分册；不替代 `PACKAGING_RULES.md` / `CHANGELOG.md`。  
> 分支：`feature/cli-pipeline-2022` · 工单：[prd-docs#274](http://swm-server.local:3000/Admin/prd-docs/issues/274)

| 分册 | 路径 | 内容 |
|---|---|---|
| **1. 需求与战略** | [01_requirements/](./01_requirements/) | 已确认战略；技术选型 / 知识 / 操作要点；Converter 契约 |
| **2. 当前整体结构** | [02_structure/overview.md](./02_structure/overview.md) | 按文件夹分类的类与中文职能 |
| **3. 待处理 / 模糊项** | [03_open-items/backlog.md](./03_open-items/backlog.md) | 进行中待办；文末「历史结束事务」 |
| **4. 实现流程与代码结构** | [04_implementation/pipeline-flow.md](./04_implementation/pipeline-flow.md) | ②③⑥ 基线、窄口、Runner |
| **4b. D1 AB 核对** | [04_implementation/d1-ab-only.md](./04_implementation/d1-ab-only.md) | 仅双端 AB **已锁**（已收口） |
| **4c. 冒烟·单文件·结果** | [04_implementation/smoke-and-results.md](./04_implementation/smoke-and-results.md) | 本轮核对：输入与错误码分层 |
| **4d. D6 UnityGLTF** | [04_implementation/d6-unitygltf-docker.md](./04_implementation/d6-unitygltf-docker.md) | git 依赖替换 file:；人工步骤 |
| **4e. D5 CLI 入门** | [04_implementation/cli-getting-started.md](./04_implementation/cli-getting-started.md) | 概念 + 第一刀；尚未写 executeMethod |

历史长文归档入口（将逐步以本目录为准）：[`../CLI_AUTOMATION_DEV.md`](../CLI_AUTOMATION_DEV.md)

---

## 当前迭代一句话

**基线：** ② 导入 → ③ Prefab → ⑥ 仅 AB（quiet）  
**可选：** ④ 平铺、⑤ 压图/刷白、⑥ 门禁/扩展产物  
**优先已做：** 窄口 + Runner + D3 总面板 + D2 单文件 + **D1 契约收口** + **D4 GLB 入库**  
**GLB 样例：** `Assets/Art/ggdddd` 编辑器内已跑通；安卓 AB 洋红 → 待办 D13  
**下一步：** **D5 CLI 第一刀**（见 [cli-getting-started](./04_implementation/cli-getting-started.md)）；洋红/⑤ 按需  
**配置分层：** 总步骤 → Pipeline SO；资源自动细节 → 资源总面板 Prefs
