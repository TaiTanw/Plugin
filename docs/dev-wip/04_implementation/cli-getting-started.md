# D5 入门：CLI 是什么、怎么接（先概念后动手）

返回 [总目录](../README.md) · [待办](../03_open-items/backlog.md) · [技术要点](../01_requirements/tech-and-ops.md)

> 2026-08-26：编辑器内 ②③⑥（可选④）已用 `Assets/Art/ggdddd` 跑通。  
> **下一步才是命令行外壳**；本文只讲概念与第一刀，不替代参数表拍板。

---

## 1. 先分清三层（不要混）

```text
网页 / lean-api / Docker     ← 以后（V1.2 基建）
        ↓ 调进程
Unity 命令行（本阶段 D5）    ← 无窗口开工程、跑同一套 Runner、退出码
        ↓ 调
PipelineRunner.Run(options)  ← 已经有了（总面板也走这里）
```

| 层 | 现在有没有 | 说明 |
|---|---|---|
| 总面板 | **有** | 人拖文件 → 填 Options → `PipelineRunner` |
| Runner / 窄口 | **有** | ② `ToolImportApi` → ③ Prefab → ④ Flatten → ⑥ `RetinarAbApi` |
| Unity CLI | **还没有** | 缺一个 `static void Xxx()` 给 `-executeMethod` 调 |
| 网页/队列 | **没有** | 不在本迭代 |

CLI **不是**另一套管线，只是「用命令行代替点按钮」。

---

## 2. Unity 命令行在干什么

Unity **没有**「只跑插件、不开工程」的官方 SDK。做法是：

```text
Unity.exe
  -batchmode          不要编辑器窗口
  -nographics         不要 GPU 窗口（服务器常用）
  -projectPath <工程> 打开 Plugin2022
  -executeMethod 类.方法   进编辑器后立刻跑这个静态方法
  -logFile <路径>     日志写文件
  -quit               方法结束后退出
```

要点：

- 方法必须是 **Editor 程序集里的 `public static void`**，且 **无参数**（参数从命令行自己解析）。
- **禁止** `Selection`、`DisplayDialog`（batchmode 会卡住或失败）。路径必须当参数传入。
- 进程退出码应对齐 `PipelineErrorCodes`（0/10/20/…）。
- Quiet 面板勾选 **不等于** `-quit`；`-quit` 才是关 Unity 进程。

---

## 3. 建议的第一刀（最小可测，不一次做完 D5）

**先做一个入口，只认 1～2 个参数，跑通已验证的 GLB 样例。**  
参数格式（flag / 环境变量 / 临时 SO）仍模糊（待办 B.9），第一刀用最笨的即可：

```text
-source <磁盘上的 .glb 或工程内 Assets/…>
（可选）-materialId <名>
```

伪流程（实现时写进例如 `Pipeline/Editor/PipelineCli.cs`）：

```text
1. 解析命令行得到 source
2. PipelineOptions.FromSettings(SO, source)   // 步骤开关仍用现有 SO
3. PipelineResult r = PipelineRunner.Run(opt)
4. EditorApplication.Exit(r.ExitCode)
```

本机验收命令（路径按你机器改）：

```text
"<Unity2022.3>/Unity.exe"
  -batchmode -nographics -quit
  -projectPath "D:\UnityMyCSProject\UnityProject\Plugin2022"
  -executeMethod PipelineCli.Run
  -logFile "D:\temp\pipeline-cli.log"
  -source "D:\path\to\ggdddd.glb"
```

成功标志：日志里 `[Pipeline] ⑥ Ab` 成功、进程退出码 **0**、`AssetBundles/Android` 与 `iOS` 有包。  
洋红材质是 **APP 加载问题**（见待办 D13），不要当成 CLI 失败。

---

## 4. 故意先不做的

| 不做 | 原因 |
|---|---|
| 一次钉死全部 flag | B.9 未拍板；先 1 个 `-source` 验证通道 |
| Docker / 队列 | 战略：本迭代只保证本机静默 API |
| 面板改走 CLI | 面板继续直调 Runner；CLI 与面板并行入口 |
| 用 CLI 修洋红 | 着色器/管线问题，与「能不能无头跑」无关 |

---

## 5. 开工顺序（你点头后再改代码）

1. 加 `PipelineCli.Run` + 解析 `-source`  
2. 用已跑通的 GLB 无头打一遍 AB  
3. 把错误码与日志约定写进 D5 表  
4. 再加 `materialId`、输出根等（与 D8 Options 对齐）
