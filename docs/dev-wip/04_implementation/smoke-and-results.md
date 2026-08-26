# 核对：冒烟 · 单文件输入 · 结果/错误码

返回 [总目录](../README.md) · [待办](../03_open-items/backlog.md)

> 结论已按 2026-08-26 回复更新。

---

## 1. 接线冒烟：现在有没有必要？

**结论：没有必要单独先做「代码冒烟」；跟面板一起验即可。**

| 做法 | 说明 |
|---|---|
| 现在 | Unity Refresh **能编译**即可；不必手写 `PipelineRunner.Run` 自测 |
| 面板后 | 用 D3「拖单文件 → 运行」做真正的接线+产品验证 |

窄口已在，面板会自然调用它们；提前冒烟收益小，还容易和「单文件+导入」半成品纠缠。

---

## 2. 编排输入（已确认）

| 项 | 结论 |
|---|---|
| 编排主入口 | **单模型文件**（拖入） |
| 工程外路径 | **需要**：拷入工程（真②）再进后续 |
| 批量 FBX 面板 | **保留独立**，不当编排主入口 |

### 导入之后：「资源处理总面板自动设置」

编排在完成单文件导入后，应**写好 L1 会用到的配置**（例如把导入落点夹写入批量路径、按需打开相关开关），让总面板状态与这次任务一致。

### 「自动化设置」要不要编排再调一次？

**确定：设置自动（Importer）无需编排额外调用 Op。**

```text
开关打开（Master + Texture/Model SettingsAuto）
  + Unity 对资产 Import / Reimport
  → AssetPostprocessor.OnPreprocess* 改 Importer
```

编排只要保证：**开关处于有效状态** + **触发了一次正常导入**。不要在 Pipeline 里再调一遍「设置自动」内核。

注意区分：

| 能力 | 触发方式 | 编排要不要主动调 |
|---|---|---|
| **设置自动**（改 Importer） | Unity 导入管线 | **否**（开开关 + Import 即可） |
| **后处理自动**（压图/刷白，导入后 delayCall） | 同上，开关控制 | 本阶段可不依赖；与⑤手动总批量不同 |
| **⑤ 总批量按钮**「按批量路径执行全部」 | 显式 API | **要有可调用口**；**流程暂不默认跑** |

---

## 3. 第⑤步：暂放，但接口要拆好

| 项 | 结论 |
|---|---|
| 流程默认 | **不跑⑤**（Options.RunPostProcess 默认 false） |
| 接口 | 中间层可调；对应现按钮「按批量路径执行全部（贴图→模型）」 |
| 现状 | 已有 `ToolPostProcessApi.RunMasterBatch` ← `ResourcePostProcessService`；D3/D2 时收成带 bool 的步结果即可 |

---

## 4. 要不要加 `StepResult`？（具体情况）

### 先说结论

| 阶段 | 建议 |
|---|---|
| **现在（仅编译、未做面板）** | **不必先加** |
| **做 D3 面板 / 强化 D2 错误展示时** | **再加**（或等价结构） |
| **子流程内部** | 继续用 Evaluation / RunSummary，**不要**改成 StepResult |

### 什么情况下「够用、可不加」

中间层已经能从现有返回值拼出对错时，例如：

- ③：`List<string>` 空 = 失败，非空 = 成功 + 产出  
- ⑥：`RetinarAbBuildResult.Ok / FailLines`  
- 日志字符串凑合给开发看  

此时只映射 `ExitCode` + `Messages` 也能跑通面板第一版。

### 什么情况下「应该加」

出现任一需求就加统一 `StepResult`（或增强 `PipelineResult.Steps`）：

1. **面板要按步显示**成功/失败（不只一个大红 exit）  
2. **CLI / 退出码**要稳定对应「败在哪一步」，且日志要带来源  
3. 各窄口返回形态不统一（有的 List、有的 string report、有的 AbResult），中间层 if-else 变脏  
4. ⑤ 将来接入：只有 Summary 计数，没有「整步 Ok」布尔时，编排不好决策是否继续⑥  

### 推荐形态（实现时）

```text
子流程窄口 → StepResult { Ok, StepId, Message, Outputs }
中间层     → PipelineResult { ExitCode, Steps[] }  // ExitCode 由首个 !Ok 映射
```

- **bool 是编排主信号**；ExitCode **只由中间层**写，插件 1/2 **不引用** `PipelineErrorCodes`。  
- `AssetOperationEvaluation` 仍只管「某个 Op 对某个资源要不要做」——和 StepResult **不同层**。

### 和⑤接口的关系

⑤ 接口先保证「能被中间层调用」（已有 RunMasterBatch）。  
等流程真要开⑤或面板要展示⑤成败时，再把返回值收成 `StepResult`，不必为⑤单独发明第三套。

---

## 5. 已锁定摘要

1. 不单独做代码冒烟；跟面板一起验。  
2. 单文件 + 工程外需导入；批量面板独立。  
3. 导入后可写 L1 **批量路径**；**设置自动走 Unity 管线，编排不另调**。  
4. ⑤ 流程默认关；口子已有。  
5. `StepResult` 延后；结果用字符串。  
6. **总步骤开关 → Pipeline SO；资源自动细节 → 资源总面板 Prefs**（混合问题稍后细拆）。

### 使用

`Tools > 自动化管线总面板` → 拖单文件 → 确认②区 SO → 运行。

GLB 已跑通样例（宿主）：`Assets/Art/ggdddd`。
