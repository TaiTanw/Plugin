# 开发日志（提交次第 + 工单对齐）

返回 [总目录](../README.md) · 插件根 [README](../../../README.md) · 工单 [prd-docs#274](http://swm-server.local:3000/Admin/prd-docs/issues/274)

本文：[0](#0-how) · [1](#1-now) · [2](#2-issue-274) · [3](#3-commits) · [4](#4-tags) · [5](#5-cli) · [6](#6-pre-cli)

> **状态（2026-09-02）：** 提交次第 + #274 对齐 + **Plugin 仓智能体** CLI 前决策已补。  
> 工作区：CLI 前对话在 `ModleEvent/Assets/Plugin` clone（Cursor 工程 *ModleEvent-Assets-Plugin*）；CLI 后在 Plugin2022。同一 git 仓。  
> 条目级回退仍只写 [Retinar CHANGELOG](../../../RetinarBatchBuilder_Share/CHANGELOG.md)。

仓：`Assets/Plugin`（Gitea `Hanson/asset-bundle`）。时间以 `git log --date=short` 为准。

---

## 0-how

**0. 怎么用**

| 记什么 | 不记什么 |
|---|---|
| 提交（短哈希 + 说明）、标签、工单评论里**工具侧**结论 | 把 CHANGELOG 条目整段复制过来 |
| 本仓相关对话（标题可点） | JEngine / 热更等旁路工程 |
| 契约是否重开（D1） | 未发生的排期承诺 |

新提交：在 [§3](#3-commits) 表**顶**加一行（最新在上），并改 [§1](#1-now) 一句话。细节仍写分册（backlog / d23 报告 / d1），这里只留指针。

---

## 1-now

**1. 现在（一句话）**

本地 `main`：推荐线 **v1.5.3**（本发布）。相对 v1.5.0：流程文档、**D23 glTF B′**、**D18 夹级清空**、开发日志。

功能分支 `feature/cli-pipeline-2022` 停在 `2b13690`（已合入 main 的 #9）。之后工作直接在 **main**。

下一步产品向：无头 **D5**；AB 文件名见 [§2](#2-issue-274)（未改代码）。

---

## 2-issue-274

**2. 工单 #274（你回复之后）**

你的回复（[comment 14305](http://swm-server.local:3000/Admin/prd-docs/issues/274#issuecomment-14305)，2026-08-27，YuWu）：v1.5.0 已发；LZ4；现网 `AssetBundles/{Android,iOS}/{name}.assetbundle`；与方案 `{materialId}_android/ios.assetbundle` 的差 **可由上传层改名**，插件内改名另排；洋红靠材质烤 Shader；缓存策略联调前再定。

**之后工单上只有一条：**

| 时 | 谁 | 要点 |
|---|---|---|
| 2026-08-29 | Product-bot [15049](http://swm-server.local:3000/Admin/prd-docs/issues/274#issuecomment-15049) | 移出 **milestone 49**。本单 = V1.2 自动转换，**不挡** V2 2 期核心。后续进转换流水线专用里程碑。@Hanson |

没有新的 APP 书面确认「必须改文件名」。因此 D1 契约 1 **仍退化**；插件侧 2026-09-02 只做了**评估**（未开发）：

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
| **v1.5.3** | 本发布 | glTF 整包 + B′；管线 D18 夹级覆盖；开发日志 |
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

从 `v1.4.4` 拉 `feature/cli-pipeline-2022`，PR #7 / #9 合入 main，再打 v1.5.0；其后 D23/D18 在 main。

| 对话 | 内容 |
|---|---|
| [Automation process evaluation](48670d6a-14b2-4d24-b6a2-f766a2b7ccde) | 确认同流程含 GLB；从 main/v1.4.4 建分支 |
| [对话、项目与分支](aba73be4-8e94-4808-99d9-a415aa7c1b94) | PR 合入、v1.5.0、远程 |
| [Analysis of D18 and hashing](b9c5ae93-d3b7-4927-b0bf-2c76d70845ef) | D18 路径/哈希草稿（唯一定位后暂放） |
| [CLI usage and plugin setup](ab54b5e0-3dc5-4e3c-9655-e8aa37c14b39) | 本线：D23 B′、D18 夹级清空、AB 命名评估、本日志 |
| [GLB export and testing issues](d9bca2b5-99f1-49da-9b16-d9d895390c41) | 导出 GLB / 刷白与⑥ 交叉（后降级 D19） |

分册：`docs/dev-wip/`（本目录从 #7 那次文档提交起）。

---

## 6-pre-cli

**6. CLI 前（Plugin 仓智能体）**

对话在 **2020 对照宿主** 上的 Plugin clone 里进行（路径 `ModleEvent/Assets/Plugin`）。不原地升 2022；2022 另开宿主 Plugin2022。学习向（编辑器问答、Shader 课）不进本表。

按时间的**决策**（不是每条排错）：

| 约 | 对话 | 留下的结论 |
|---|---|---|
| 07-30 | [分析此项目的代码，当前有两个插件](c6e6fe44-e30c-4766-ac7c-650dd1b68176) | 插件 1 Retinar = 交付格式；插件 2 TOol = 导入设置。一起点 Batch Build 的旧菜单后来拆成平铺 / 导出 |
| 07-31 / 08-07 | [武直旋翼透明](3c24a425-5dec-4137-8cbf-00ceb08949b1) · [续](e6b76f44-a7b8-47e7-8597-6866af17bc02) | 样例排查（工具 vs 源文件）；不挡主线 |
| 08-04～06 | [Unity plugin code analysis](dfa41b14-ac4f-4dab-bd0c-83b0ef560551) | 两插件边界、顶点色语义、自动时机；导向 v1.3.x |
| 08-06 | [Texture operation registry](d27c70fd-efb1-4bc4-81dd-bd1f63cd3ee9) | 贴图 Op 反射注册（插件 2 扩展点） |
| 08-11 | [检测与后处理路径分析](6643ab36-b639-41b8-80a1-dbf0f76d1344) | **批量 FBX**：向上三层夹名；点按钮后执行 |
| 08-11 | [Evaluate与学习清单](a311a046-30d5-4e99-b14e-caa5e90fcfe6) | 日复盘：协作上 Gitea + 管线定型平铺→手动→导出（v1.3.1～1.3.5） |
| 08-11 | [U3dCLI tool discussion](a648901a-a506-42a8-9b16-970cd30a2fc9) | 无独立 Unity CLI SDK；无头 = `-executeMethod`。后来落地为 `PipelineCli` |
| 08-14 | [Git integration concepts](5fe25ca2-1743-491a-affd-2b92b3e9bf93) | `team`=Gitea、`origin`=GitHub；**v1.3.8 成品直通**，开始迭代插件 1 |
| 08-17 | [URP rendering and packaging](b4bbcc76-c769-48e0-8600-b31da8724116) | 插件**不转换管线**，只收敛依赖 + 分平台出包；视觉跟打包工程材质 |
| 08-17 | [Android AB packaging](48fb7b1d-59ee-4dea-af73-7a6bfd6d15c4) · [Character 无声](500a4556-ecc1-42fd-b478-a18de80099a3) | 眼外肌/角色样例（明暗、动画音频）；后并入偏暗/洋红线 |
| 08-18 | [Unity package resolution error](a82b0766-544d-4e6e-8924-ad5b3717c9d0) | 本机 `file:` UnityGLTF 路径断了 → 后改 git 依赖（D6） |
| 08-20～24 | [Plugin 1 resource evaluation](81c55697-c00a-44a3-8d71-a4ec10eedea5) | 平铺要带 UI/音效/Shader 并断源引用 → **v1.4.0 分类单元** |
| 08-21 | [平铺是否修好材质](1f772875-9693-4e61-84b3-9e676c57b8c4) · [眼外肌AB偏暗](5db1de01-c2eb-4152-8703-8b75ecf6357c) | 动画 PPtr 重绑；2020 **不原地升** 2022；引擎交互与业务拆分。偏暗/洋红后见 D13 |
| 08-25 | [Automation process evaluation](48670d6a-14b2-4d24-b6a2-f766a2b7ccde) | 从 **v1.4.4** 开 CLI 分支（不要升现有 2020 工程）。之后见 [§5](#5-cli) |

不进本日志：JEngine / 热更、纯学习问答（如 [Editor development knowledge](5b2b4ce2-7e1f-4a58-964e-cacb756b619a)）。
