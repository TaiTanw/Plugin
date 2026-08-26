# 功能实现：流程与代码结构

返回 [总目录](../README.md)

---

## 1. 目标流程（产品）

```text
基线（默认）：
  ② 导入 → ③ Prefab → ⑥ 仅 Android/iOS AB（quiet）

可选（面板/flag）：
  ④ 平铺 Art（含 remap）
  ⑤ 压图 / 顶点白
  ⑥+ 门禁提示、UnityPackage、全套 Deliverables
```

终局 CLI：同一套静态编排 + `-executeMethod`。

---

## 2. 步骤 → 现有代码 → 计划代码

| 步 | 现状入口 | 计划 |
|---|---|---|
| ② | `BatchFbxImportService` / 导入面板；GLB 靠 UnityGLTF | 管线可跳过（已在工程）或调静默导入 |
| ③ | **`PrefabBuildService`**（`TOol/Editor/Generated/Prefab/`）+ 菜单 | 面板/编排直接调用（已可用） |
| ④ | `RetinarFlattenScheduler` → `RetinarBatchModelBuilder` | `enableFlatten` 时调用 |
| ⑤ | `ResourceProcessWindow.RunMasterBatch` / Op Runner | `enablePostProcess` 时调用 |
| ⑥ | 规范化 `ExportArtPrefabPaths` / 直通 `RetinarDirectPackage` | **新** `BuildAbOnly(paths, options)` |

### ③ 已实现要点

```text
选中/路径列表
  → ResolveFolderName 或 materialId
  → Instantiate → 可选 Unpack
  → SaveAsPrefabAsset → Assets/IncomingPrefab/{名}.prefab
```

不做 remap（remap 在④）。

### ⑥ 现状拆分（待抽 API）

| 类别 | 规范化里有的 | 自动线默认 |
|---|---|---|
| 打双端 AB | ✓ | **ON** |
| 校验/SafeZone/依赖阻断 | ✓ | OFF（可选门禁包） |
| 贴图预检 | 警告 | OFF 或仅日志 |
| UnityPackage / docs / 全套夹 | ✓ | OFF |
| DisplayDialog | ✓ | **禁止**（quiet） |

直通：已接近「仅 AB+UP」；自动线可再收成「仅 AB」。

---

## 3. 编排与窄口（已建）

```text
Assets/Plugin/Pipeline/Editor/
├─ PipelineOptions / Result / ErrorCodes
└─ PipelineRunner.Run(options)     # ③→⑥；④⑤ 可选

插件 2：TOol/Editor/Shared/Api/
├─ ToolImportApi / ToolPrefabApi / ToolPostProcessApi

插件 1：Retinar/.../40_Api/
├─ RetinarFlattenApi / RetinarAbApi.BuildAbOnly
```

D1 最小口径见 [d1-ab-only.md](./d1-ab-only.md)。

---

## 4. 自动化管线总面板（D3，待做）

```text
Tools > 自动化管线总面板（计划）
  输入：选中 / 文件夹列表
  ☑ ③ Prefab（默认开）
  ☐ ② 导入（可选）
  ☐ ④ 平铺
  ☐ ⑤ 后处理
  ☑ ⑥ 基本 AB（默认开）
  [运行] → PipelineRunner，无确认框
```

与 **资源处理总面板**（只做⑤ Op）分工不同。

---

## 5. 错误码草案

| 码 | 含义 |
|---|---|
| 0 | 成功 |
| 10 | 参数/路径 |
| 20 | 导入 |
| 30 | Prefab |
| 40 | 平铺 |
| 50 | 后处理 |
| 60 | AB |
| 70 | License/环境 |
| 80 | 其它 |

---

## 6. 建议实现顺序（已调整）

1. ~~1/2 窄口 + PipelineRunner 骨架~~ **已做**  
2. ~~**D3** 总面板 + **D2** 单文件导入~~ **已做**（`Tools > 自动化管线总面板`）  
3. ~~实机验证~~ **已做**（GLB 样例 `Assets/Art/ggdddd`；安卓洋红见待办 D13）  
4. ~~**D1** 核对收口~~ **已做**（命名/压缩见 d1-ab-only；契约 1/2 退化）  
5. **D5 CLI 第一刀**（见 [cli-getting-started.md](./cli-getting-started.md)）  

结果：当前用 `PipelineResult` 字符串 Messages，不加 StepResult。
