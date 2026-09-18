# 开发备忘目录（进行中）

> 位置：`Assets/Plugin/docs/dev-wip/`  
> 操作者流程在插件根 [README](../../README.md) 最上两节，不在本表里找入口。  
> 当前约定看战略，当前实现看结构，待办看顶部队列。历史规则不覆盖后续确认。  
> 已发布标签：**v1.6.0**。工单：[prd-docs#274](http://swm-server.local:3000/Admin/prd-docs/issues/274)

## 操作者（本层可见）

```text
总面板 [1]入库 → [2]导入期 → [③]Prefab → [④]平铺 → [⑤]总批量 → [⑥]导出
CLI: PipelineCli.Run -source …   （须关掉占用本工程的 Editor）
人工④: 菜单原子 / 平铺，不接⑤⑥
全局导入: Tools > 全局导入设置（2）
```

退出码：**40** 整趟停；**41** leftover `.fbm`、**42** 贴图身份、**50** ⑤失败 → 后面步仍跑；**60** ⑥全失败。④ 步骤 **1–14 已落地**；内核未迁目录。

| 分册 | 路径 | 内容 |
|---|---|---|
| **1. 需求与战略** | [01_requirements/](./01_requirements/) | 已确认战略；技术 / 操作要点 |
| **2. 当前整体结构** | [02_structure/overview.md](./02_structure/overview.md) | 入口、④归属、SO、⑤⑥边界 |
| **3. 当前待办** | [03_open-items/backlog.md](./03_open-items/backlog.md) | 唯一当前队列 |
| **3b. D24 边界** | [03_open-items/d24-boundary-plan.md](./03_open-items/d24-boundary-plan.md) | 平铺内核接管、ctx、白名单 |
| **3c. ④收口步骤** | [03_open-items/d24-flatten-steps.md](./03_open-items/d24-flatten-steps.md) | **1–14 已落地**；R1b 基本核对 |
| **4. 流程与窄口** | [04_implementation/pipeline-flow.md](./04_implementation/pipeline-flow.md) | 两块总览；错误码 |
| **4h. 相位入参** | [04_implementation/pipeline-phase-io.md](./04_implementation/pipeline-phase-io.md) | Bindings / ID2 / 1 与 2.5 |
| **4b. D1 AB** | [04_implementation/d1-ab-only.md](./04_implementation/d1-ab-only.md) | 仅双端 AB **已锁** |
| **4c. 冒烟** | [04_implementation/smoke-and-results.md](./04_implementation/smoke-and-results.md) | 输入与错误码分层 |
| **4d. D6 UnityGLTF** | [04_implementation/d6-unitygltf-docker.md](./04_implementation/d6-unitygltf-docker.md) | git 依赖替换 file: |
| **4e. D5 CLI** | [04_implementation/cli-getting-started.md](./04_implementation/cli-getting-started.md) | **已验收**；现含 41/42 |
| **4f. Op 扩展** | [04_implementation/op-recognition-and-extend.md](./04_implementation/op-recognition-and-extend.md) | ⑤ 加 Op / 后缀 / 大类 |
| **4g. 导入 ctx** | [04_implementation/pipeline-job-context.md](./04_implementation/pipeline-job-context.md) | 事实归类 |
| **4i. ④ 能力查封** | [04_implementation/pipeline-flatten-capabilities.md](./04_implementation/pipeline-flatten-capabilities.md) | 拷贝循环 vs B′（入口图已过时，先读 overview） |
| **4k. D24-R1 审计** | [04_implementation/flatten-core-audit.md](./04_implementation/flatten-core-audit.md) | **2026-09-09 快照**；现网以 overview / 步骤页为准 |
| **4j. D23 报告** | [04_implementation/d23-slice-report.md](./04_implementation/d23-slice-report.md) | 历史切片 |
| **5. 开发日志** | [05_dev-log/timeline.md](./05_dev-log/timeline.md) | 提交次第、#274 |

D18：[d18k](./03_open-items/backlog.md#d18k) · 旧入口：[CLI_AUTOMATION_DEV.md](../CLI_AUTOMATION_DEV.md)

---

## 当前迭代一句话

**本批：** 把编排路径、导入钩子、菜单和⑥产物收成可日常使用的边界。  
**默认 SO：** ③④⑤⑥全开；[2] 是全局导入设置不是步骤开关。④⑤可关。  
**④：** `FromContext` → `Run(plan)`；步骤 1–14 落地；调度在 `ManualFlatten/`，内核未迁目录。  
**⑥：** `name_android.assetbundle` / `name_ios.assetbundle`，产品夹不再分 Android/iOS。  
**对外：** (A) 总面板 · (B) [CLI](./04_implementation/cli-getting-started.md) · (C) `手动操作栏`
