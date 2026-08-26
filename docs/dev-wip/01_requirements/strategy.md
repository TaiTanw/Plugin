# 已确认战略汇总

> 与产品 / 仓库形态相关的结论。变更需显式改本文并记一笔。

## 目标

用 Unity **`-batchmode` / `-executeMethod`**，从 **FBX、GLB** 到 **Android / iOS AssetBundle**，尽量无人点菜单。  
现阶段先补齐**编辑器内可静默跑通的内核**；CLI / Docker 是外壳。

## 工程与 Git

| 项 | 结论 |
|---|---|
| 主开发 Unity | **2022.3**；宿主文件夹 **Plugin2022** |
| 2020 宿主 | **ModleEvent** 对照，**不原地升 2022** |
| 插件 Git | 一套源码；分支如 `feature/cli-pipeline-2022` |
| 宿主 | **不单独建仓、不改 Gitea 名**；日常 = 开宿主 + 改 `Assets/Plugin` |
| 远程 | `team` = Gitea `asset-bundle`（协作）；`origin` = GitHub（备份） |
| 基线标签 | 从 **`v1.4.4`** 开本功能分支 |
| Plugin 落点 | `Plugin2022/Assets/Plugin` 为 **clone**（主开发）；ModleEvent 下可另有一份对照 |

## 管线产品拆分

| 项 | 结论 |
|---|---|
| 基线步骤 | **② → ③ → ⑥**（Converter / 素材库最小线） |
| 可选步骤 | **④ 平铺、⑤ 后处理**（默认关，flag/面板勾选） |
| ⑥ 基线产物 | **仅双端 AB** + quiet 日志/退出码 |
| ⑥ 可选 | 门禁/弹窗提示；UnityPackage；全套 Deliverables |
| 平铺归属 | **暂留插件 1**；③ Prefab 在插件 2；不整包迁平铺到插件 2 |
| GLB 贴图抽出 | **可延后**（④⑤ 可选后非 blocker） |
| 自动转换上云 | 产品 **V1.2**；V1.0 人工传 AB（Issue #274） |

## 插件分工（目标态）

| 侧 | 职责 |
|---|---|
| **插件 2 TOol** | 操作执行：导入/设置、③ Prefab（`Editor/Generated`）、⑤ Op；L1=资源处理子流程对外口 |
| **插件 1 Retinar** | 业务/交付：④ 平铺+remap、⑥ 出包与门禁契约；人工规范化菜单保留 |
| **Plugin/Pipeline**（已做 D3/D2） | 总步骤 SO + Runner + 总面板；调 1/2 窄口；**不写** L1 自动化 Prefs |

### 配置分层（已确认）

| 层 | 存储 | 内容 |
|---|---|---|
| 流程总步骤（含②开关） | `PipelineStepSettings` **SO** | runImport/Prefab/Flatten/Post/Ab、同步 L1 路径 |
| 资源自动细节 | 资源总面板 **EditorPrefs** + L3 SO | 设置自动/后处理自动、Op、压缩等 |

设置自动：编排**不调用**；导入触发 Unity 回调。结果汇总：暂用字符串（StepResult 延后）。D1 见 d1-ab-only，细点另核。

### ⑤ 与流程编排的数据边界（已确认）

- **流程编排**决定：要不要做资源处理（总步骤 Options；**⑤ 暂默认关**）。  
- **资源处理总面板（L1）**决定：做哪些资源类型批量 / 路径 / 开关；**单文件导入后可由管线写入批量路径**。  
- **设置自动**：只靠 Prefs 开关 + Unity `AssetPostprocessor`。  
- **⑤ 总批量口**：`ToolPostProcessApi.RunMasterBatch`。  
- **StepResult**：需要按步 UI/CLI 时再加。

## 明确不做（本阶段）

- 整包「平铺并入插件 2」
- 删规范化菜单 / 删平铺代码
- 本迭代上 Docker / 接 lean-api 队列（契约先对齐即可）
