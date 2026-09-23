# 开发日志

现约：[开发者须知](../README.md)。工单 [prd-docs#274](http://swm-server.local:3000/Admin/prd-docs/issues/274)。本文只记提交次第，不覆盖现约。

本文：[0](#0-how) · [1](#1-now) · [2](#2-issue-274) · [3](#3-commits) · [4](#4-tags) · [5](#5-cli) · [6](#6-pre-cli)

> **历史整理状态（2026-09-02）：** 提交次第 + #274 对齐 + **Plugin 仓智能体** CLI 前决策已补。\
> 工作区：CLI 前对话在 `ModleEvent/Assets/Plugin` clone（Cursor 工程 *ModleEvent-Assets-Plugin*）；CLI 后在 Plugin2022。同一 git 仓。  
> 条目级回退仍只写 [Retinar CHANGELOG](../../../RetinarBatchBuilder_Share/CHANGELOG.md)。

仓：`Assets/Plugin`（Gitea `Hanson/asset-bundle`）。时间以 `git log --date=short` 为准。

---

## 0-how

**0. 怎么用**

| 记什么 | 不记什么 |
|---|---|
| 提交（短哈希 + 说明）、标签、工单评论里**工具侧**结论 | 把 CHANGELOG 条目整段复制过来 |
| 智能体结论（§5/§6 **只写文字**；Cursor 对话 ID 在 GitHub 打不开） | JEngine / 热更等旁路工程 |
| 契约是否重开（D1） | 未发生的排期承诺 |

新提交：在 [§3](#3-commits) 表**顶**加一行（最新在上），并改 [§1](#1-now) 一句话。细节仍写分册（backlog / d23 报告 / d1），这里只留指针。

---

## 1-now

**1. 现在（一句话）**

2026-09-23：**v1.7.0** 为当前推荐。⑥ 在双端 AB 成功后可另写模型文件和资源信息表，默认关；取消「拷到交付夹」仍先构建。手机端加载带 unitypackage 动画的 2022 AB 会闪退，同一套代码迁到 2020 导出可加载。内核未迁目录。

操作者：[操作者须知](../../operator/README.md)。CLI：[cli-getting-started](../04_implementation/cli-getting-started.md)。队列：[backlog](../03_open-items/backlog.md)。AB：[d1-ab-only](../04_implementation/d1-ab-only.md)。工单 #274 **未回帖**。

---

## 2-issue-274

**2. 工单 #274（你回复之后）**

你的回复（[comment 14305](http://swm-server.local:3000/Admin/prd-docs/issues/274#issuecomment-14305)，2026-08-27，YuWu）：v1.5.0 已发；LZ4；现网 `AssetBundles/{Android,iOS}/{name}.assetbundle`；与方案 `{materialId}_android/ios.assetbundle` 的差 **可由上传层改名**，插件内改名另排；洋红靠材质烤 Shader；缓存策略联调前再定。

**截至2026-09-16远程核对：**

| 时 | 谁 | 要点 |
|---|---|---|
| 2026-09-06 | lean-api-bot [17984](http://swm-server.local:3000/Admin/prd-docs/issues/274#issuecomment-17984) | 评估：AB 构建器 = V1.2 核心件（Docker、双端 AB、License）；P0 与「V1.0 不实施」冲突；不知 YuWu 是否接手。**本仓未回帖。** 评估见 [backlog](../03_open-items/backlog.md) |
| 2026-08-29 | Product-bot [15049](http://swm-server.local:3000/Admin/prd-docs/issues/274#issuecomment-15049) | 移出 **milestone 49**。本单 = V1.2 自动转换，**不挡** V2 2 期核心。@Hanson |
| 2026-08-27 | YuWu [14305](http://swm-server.local:3000/Admin/prd-docs/issues/274#issuecomment-14305) | v1.5.0；LZ4；现网路径名；上传层可改名；洋红靠材质烤；lean-api / converter 边界 |

没有新的 APP 书面确认「必须改文件名」。因此 D1 契约 1 **仍退化**；插件侧只做过**评估**（未改默认命名）：

- 工单要的两个文件：`{materialId}_android.assetbundle` + `{materialId}_ios.assetbundle`（扩展名不是正文里的 `.ab`）。
- 布局意向：**夹名仍 `Android` / `iOS`**，夹内改成上述两个文件。
- 插件改 `RetinarAbApi` 即可，②③④ 不动；真正成本是 **重开 D1 契约 1（APP/COS 取包）**。未改 APP 不切默认。上传层改名仍是零插件改动的备选。
- 契约 2（包内 `main`）**不绑**进命名这一刀。

正文：[d1-ab-only](../04_implementation/d1-ab-only.md) · [d18k AB 段](../03_open-items/backlog.md#d18k) · [tech-and-ops §4](../01_requirements/tech-and-ops.md)

工单更早（产品侧，工具已吸收）：V1.0 人工传 AB、自动转换 **V1.2**；URP 平台 ID；Docker+COS 方案。不在本日志展开。

---

## 3-commits

**3. 提交次第（最新在上）**

`main` 全历史。Merge 只记合入方向。

| 日期 | 哈希 | 说明 |
|---|---|---|
| 2026-09-23 | **v1.7.0** | ⑥ 可选模型文件与资源信息表，默认关；拷 AB 到交付夹不跳过构建 |
| 2026-09-22 | **v1.6.8** | ④ 剥 Missing Script、同名图分落点、本单元路径重绑；测试1112 `exit=0`；主页写明 2022 AB 手机闪退风险 |
| 2026-09-21 | **v1.6.5** | pack 预处理进信封；三种导入写进操作者须知；⑤保留内置/Art 单元 Shader |
| 2026-09-21 | `613cb97` | 亮度转透明漏洞修复 |
| 2026-09-20 | **v1.6.2** | 逻辑优化：配置拆分、⑤批量闸、跑后清空、文档现约 |
| 2026-09-18 | **v1.6.0** | 编排边界收口：路径真源、导入钩子、菜单、AB 文件名 |
| 2026-09-16 | `23b3567` | ④ 步骤 1–14：41 leftover / 42 身份报错但不卡；操作者流程收到插件根 README；已推 GitHub/Gitea |
| 2026-09-14 | （文档，未提交） | 需求整理：D13-R1 移动端关闭；缺伴生整趟停；④目录暂留；OBJ 不进 MissingUris；当时 B 质量闸仍待拍 |
| 2026-09-11 | `e51469a` | 后处理材质透明问题修复；用户同日确认 Art 玻璃正确。移动端 AB 当时未确认，2026-09-14 已验收 |
| 2026-09-10 | （验证记录） | 7项材质专项、相关合计35/35 EditMode测试通过；本轮未重跑，不视作新增提交 |
| 2026-09-03 | `8eb6612` | 插件1拆分初步 |
| 2026-09-02 | （验证记录） | D5无头：直18.gltf → exit=0；参数/退出码记录，非新增独立提交 |
| 2026-09-02 | **v1.5.3** | **Release v1.5.3**（标注在本提交） |
| 2026-09-02 | `c9320ec` | 开发日志：提交次第与 Plugin 仓对话；D1 文件名评估 |
| 2026-09-02 | `c5881eb` | **D18** 管线重名：只清本趟 Incoming / Art 单元夹再写 |
| 2026-09-02 | `d3feecb` | **GLTF / D23**：整包②、ctx、④ B′ `Art/<名>/<名>/` |
| 2026-08-31 | `ca15617` | 流程优化（team/main 至此） |
| 2026-08-31 | `71fb839` | 部分文档 |
| 2026-08-27 | `d32cd6e` | **Release v1.5.0** |
| 2026-08-27 | `dd65655` | 流程稳定 |
| 2026-08-26 | `a0a3acc` | Merge **#9** `feature/cli-pipeline-2022` → main（Pipeline + Material + AB API） |
| 2026-08-26 | `2b13690` | GLB 流程跑通和材质处理管线（功能分支尖） |
| 2026-08-26 | `7673d12` | 总面板梳理 |
| 2026-08-25 | `65ba0d5` | Merge **#7** 同分支 → main（CLI 文档 + GLB flatten 闸 + Prefab 骨架） |
| 2026-08-25 | `8f2f3c1` | CLI 自动化文档；Shared Prefab 骨架；平铺认 GLB |
| 2026-08-21 | `22a6a28` | **Release v1.4.4**（本功能分支起点） |
| 2026-08-20 | `f0748db` | **Release v1.4.0** 平铺分类 + 自愈挪到平铺末 |
| 2026-08-14 | `86a5bfe` | **Release v1.3.8** 成品直通；插件 1 开始迭代 |
| 2026-08-11 | `9d7af54` | 面板 UX / 配置归属 / 导入日志 |
| 2026-08-10 | `2d68e0b` | **Release v1.3.7** 面板简化、批量 FBX |
| 2026-08-10 | `10a88d5` | 面板分层 |
| 2026-08-07 | `1e7b7f7` | **Release v1.3.6** 贴图预检 + 仅扫描 |
| 2026-08-07 | `71b4ca2` | 扫描逻辑拆分 |
| 2026-08-06 | `2659799` | **Release v1.3.5** 全流程（批量 FBX + 平铺/处理/导出） |
| 2026-08-06 | `f0f8594` | 修复与优化 |
| 2026-08-06 | `8f30170` | 批量 FBX 导入开发 |
| 2026-08-06 | `0f8ac29` | Release v1.3.2-test（已由 1.3.5 接替） |
| 2026-08-06 | `00b38b3` | 优化与调参 |
| 2026-08-05 | `d45d84c` | Release v1.3.1 导出菜单拆分 |
| 2026-08-05 | `faef2e5` / `bddaa48` | README / ARCHITECTURE / 忽略 `.env` |
| 2026-08-05 | `4b083eb` | 资源导入自动化修复 |
| 2026-08-04 | `c828df7` | 层级架构初步 |
| 2026-08-04 | `2ea0ed0` `ec6d973` `d642223` | 注释 |
| 2026-07-31 | `786e267` `3099459` | 亮度转透明等 |
| 2026-07-30 | `4af0323` `6bfb8f0` | 初始打包工具 / Initial commit |

---

## 4-tags

**4. 标签（推荐线）**

| 标签 | 提交 | 一句话 |
|---|---|---|
| **v1.7.0** | 本批 HEAD | 当前推荐。⑥ 可选模型文件与资源信息表，默认关 |
| **v1.6.8** | `057d646` | ④ 同名不互盖；测试1112 身份闸已关 |
| **v1.6.5** | `f6e0dbe` | pack 第一刀；操作者三种导入；⑤ Shader 按位置保留 |
| **v1.6.2** | `f33646d` | 配置拆分、批量闸、跑后清空 |
| **v1.6.0** | `5ee7af1` | 路径真源、导入钩子、菜单、AB `name_android/_ios` |
| **v1.5.3** | `8131b31` | glTF 整包 + B′；管线 D18 夹级覆盖；开发日志 |
| **v1.5.0** | `d32cd6e` | 自动化管线稳定：②③⑥ + 可选④⑤；CLI 第一刀；AB Options |
| v1.4.4 | `22a6a28` | 引用 remap；FBX/Prefab 分流；动画循环跟源 |
| v1.4.0 | `f0748db` | 平铺分类单元；自愈在平铺末 |
| v1.3.8 | `86a5bfe` | 成品直通 |
| v1.3.7 | `2d68e0b` | L1/L2/L3 面板 |
| v1.3.6 | `1e7b7f7` | 贴图预检、仅扫描 |
| v1.3.5 | `2659799` | 首个「全流程」对外标签 |
| v1.3.2-test | `0f8ac29` | 测试版，已接替 |

条目级原因/回退：只维护 [CHANGELOG.md](../../../RetinarBatchBuilder_Share/CHANGELOG.md)，此处不双写。v1.4.1–1.4.3 在 CHANGELOG 里、未单独打标签（并进 v1.4.4）。

---

## 5-cli

**5. CLI 分支以后（2026-08-25 起）**

从 `v1.4.4` 拉 `feature/cli-pipeline-2022`，PR #7 / #9 合入 main，再打 v1.5.0；其后 D23/D18 在 main。分册在 `docs/dev-wip/`（从 #7 那次文档提交起）。

Cursor 对话在 GitHub 打不开，只记结论：

- 同一管线要能吃 GLB；从 main/`v1.4.4` 开分支，不要升现有 2020 工程。
- 合 PR、打 v1.5.0、对齐远程。
- D18：路径/哈希草稿后，唯一定位暂放；落地是夹级清空再写。
- 本线：D23 B′、D18 夹级清空、AB 命名只评估未改代码、本日志；D5 无头已验收。
- 导出 GLB / 刷白与⑥ 交叉后降级为 D19，不当 CLI 门禁。

---

## 6-pre-cli

**6. CLI 前（Plugin 仓智能体）**

对话在 **2020 对照宿主** 的 Plugin clone（`ModleEvent/Assets/Plugin`）。不原地升 2022；2022 另开 Plugin2022。学习向问答不记。Cursor 对话链接省略。

按时间留下的结论：

- 07-30：插件 1 Retinar = 交付格式；插件 2 TOol = 导入设置。旧 Batch Build 后来拆成平铺 / 导出。
- 07-31 / 08-07：武直旋翼透明是样例排查（工具 vs 源文件），不挡主线。
- 08-04～06：两插件边界、顶点色语义、自动时机 → v1.3.x；贴图 Op 反射注册。
- 08-11：批量 FBX 向上三层夹名、点按钮后执行；协作上 Gitea；定型平铺→手动→导出（v1.3.1～1.3.5）。无独立 Unity CLI SDK，无头 = `-executeMethod`（后落地 `PipelineCli`）。
- 08-14：`team`=Gitea、`origin`=GitHub；v1.3.8 成品直通，开始迭代插件 1。
- 08-17：插件不转换渲染管线，只收敛依赖 + 分平台出包；视觉跟打包工程材质。眼外肌/角色样例后并入偏暗/洋红线。
- 08-18：本机 `file:` UnityGLTF 断了 → 后改 git 依赖（D6）。
- 08-20～24：平铺要带 UI/音效/Shader 并断源引用 → v1.4.0 分类单元。
- 08-21：动画 PPtr 重绑；2020 不原地升 2022；偏暗/洋红后见 D13。
- 08-25：从 v1.4.4 开 CLI 分支。之后见 §5。

不进本日志：JEngine / 热更、纯学习问答。
