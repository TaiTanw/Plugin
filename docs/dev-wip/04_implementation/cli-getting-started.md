# D5：CLI 工具入口

返回 [总目录](../README.md) · [流程与中间层](./pipeline-flow.md) · [待办](../03_open-items/backlog.md)

> CLI = 无头外壳；内核仍是 `PipelineRunner.Run(options)`。面板继续直调 Runner，不改成「面板调 CLI」。  
> **第一刀已写** `PipelineCli.Run`：必填 `-source`，可选 `-materialId`；步骤跟 SO。

---

## 1. 在总架构里的位置（块 B）

```text
网页 / lean-api / Docker     ← 以后（V1.2 基建，本迭代不做）
        ↓ 调进程
Unity 命令行（D5）
        ↓
PipelineCli.Run()            ← Editor：Assets/Plugin/Pipeline/Editor/PipelineCli.cs
        ↓ 填 PipelineOptions（Quiet 强制 true）
PipelineRunner.Run(options)  ← 已有（与总面板同一内核）
        ↓
窄口 ②③④⑤⑥
        ↓
EditorApplication.Exit(code) ← = PipelineResult.ExitCode
```

| 层 | 有无 | 说明 |
|---|---|---|
| (A) 总面板 | **有** | 人填 Options → Runner |
| (A) Runner / 窄口 | **有** | 见 [pipeline-flow](./pipeline-flow.md) |
| (B) `PipelineCli` | **有（第一刀）** | 只解析 argv、组 Options、调 Runner、Exit |
| 网页/队列 | **无** | 不在本迭代 |

---

## 2. 代码结构

```text
Assets/Plugin/Pipeline/Editor/
├─ PipelineRunner.cs          # 内核（CLI 禁止绕过）
├─ PipelineOptions.cs         # CLI 填这个，不另造配置类型
├─ PipelineResult.cs          # ExitCode / Messages
├─ PipelineErrorCodes.cs      # 与进程退出码对齐
├─ PipelineWindow.cs          # (A) 人机；与 CLI 并行
└─ PipelineCli.cs             # (B) public static void Run()
```

| 类 | 职责 | 禁止 |
|---|---|---|
| `PipelineCli` | 读 argv、校验、组 Options、调 Runner、`Exit` | Selection、Dialog、业务细节 |
| `PipelineRunner` | 步骤编排 | 解析 argv |
| 窄口 | 单步能力 | 知道自己被 CLI 还是面板调用 |

---

## 3. Unity 命令行外壳

```text
Unity.exe
  -batchmode
  -nographics
  -projectPath <Plugin2022 工程根>
  -executeMethod PipelineCli.Run
  -logFile <路径>
  -quit
  -source <模型路径>
  [-materialId <name>]
```

要点：

- 入口必须是 Editor 程序集里 **`public static void` 无参**方法。
- CLI **强制** `Quiet=true`（batchmode 禁 Dialog）。**Quiet ≠ `-quit`**。
- 退出码 = `PipelineResult.ExitCode`。缺 `-source` → `10`（BadArgs）。⑤ `FailedCount>0` → `50`。未捕获异常 → `80`（Other）。
- `70` LicenseOrEnv **预留，本入口不赋值**。⑥ 部分成功仍可能 `0`。

---

## 4. 参数（第一刀）

| 参数 | 必填 | 映射 Options | 说明 |
|---|---|---|---|
| `-source <path>` 或 `-source=` | 是 | `SourcePath` | 工程外 **.glb/.fbx** 或 `Assets/…`。**.gltf 请先在 DCC/gltf-pipeline 转 GLB**（D22 编辑器封装未落地；②若直接收 gltf 只拷 JSON） |
| `-materialId <name>` | 否 | `MaterialId` | 覆盖 Prefab 三层命名 |

步骤开关全部跟 `PipelineStepSettings` SO，不做 flag 覆盖。

```text
1. 解析 -source / 可选 -materialId
2. opt = PipelineOptions.FromSettings(PipelineStepSettings.Current, source)
3. opt.Quiet = true；若有 materialId → 写入
4. r = PipelineRunner.Run(opt)
5. EditorApplication.Exit(r.ExitCode)
```

后续可加（不进第一刀）：步骤 flag、输出根、多源（D10）、清 Incoming（D11）。⑤ 扫描夹已由 **D17** 在 Runner 内写 Art 单元。

---

## 5. 验收

本机示例（路径按机器改）：

```text
"<Unity2022.3>\Unity.exe"
  -batchmode -nographics -quit
  -projectPath "D:\UnityMyCSProject\UnityProject\Plugin2022"
  -executeMethod PipelineCli.Run
  -logFile "D:\temp\pipeline-cli.log"
  -source "D:\path\to\sample.glb"
```

| 检查 | 期望 |
|---|---|
| 日志 | 有 `[PipelineCli]` 与 `[Pipeline] ⑥ Ab`（或当前 SO 开启的等价步） |
| 退出码 | `0` |
| 产物 | `AssetBundles/Android` 与 `iOS`（或 ExportSettings 根下）有包 |
| 洋红 | **不**算 CLI 失败（D13） |
| Prefab 夹顶点色 | **不**算 CLI 失败（可选 D20；色在 `Model/*.FBX`） |
| FBX 刷白 / 导出 GLB 黄 | **不**算 CLI 失败（D19 已降级；需白 GLB 见 backlog **L**） |

先回切工程等 `PipelineCli` 编译进 Editor 程序集，再跑无头命令。

---

## 6. 缺口（相对 CLI）

| 缺口 | 影响 CLI？ | 代办 |
|---|---|---|
| 参数格式未钉死全表 | 第一刀已死 `-source` | backlog **B.CLI** |
| `SourceBindings` Runner 未消费 | 多文件 CLI 不可用 | **D10** |
| `LicenseOrEnv(70)` | 表有洞 | 预留 |
| ⑥ 部分失败仍 Ok | CI 语义 | 随错误码表拍板 |

**已复用：** `PipelineOptions.FromSettings`、`PipelineRunner`、窄口、D17 Art 单元路径、D16 `ToolPostProcessResult`。
