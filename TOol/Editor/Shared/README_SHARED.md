# TOol / Editor / Shared — 目录说明

> 横切能力：不绑 Texture / Model 单纵切，也不承载「中间资产写盘」能力。  
> ③ 预设体已迁至 [`../Generated/Prefab/`](../Generated/Prefab/)。

## 当前文件（尚未全部迁入子夹）

根下仍有历史扁平脚本（功能未改）：

| 文件 | 中文职责 |
|---|---|
| `ResourceProcessSwitches.cs` | 本机自动化开关（总开关 / 设置自动 / 后处理自动） |
| `ResourceBatchFolderStore.cs` | L1 共用批量扫描路径（EditorPrefs） |
| `ResourceBatchFolderListGui.cs` | 批量路径列表 UI |
| `ResourceManualOperationStore.cs` | L2 精准 Op 勾选（Prefs） |
| `ResourceExcludeUtility.cs` | 排除前缀判断 |
| `ImportPostProcessScheduler.cs` | 导入后 delayCall 跑后处理 |
| `AssetPathUtility.cs` | 路径工具 |
| `ScriptableObjectSettingsGui.cs` | SO 设置 GUI 片段 |
| `AssetOperationEvaluation.cs` / `AssetOperationScanSummary.cs` | Evaluate / 仅扫描摘要 |

**目标分类（逐步搬迁，勿一次大挪，保留 .meta）：**

```text
Shared/
├─ README_SHARED.md          # 本说明
├─ Switches/                 # ← 将来：ResourceProcessSwitches
├─ BatchPath/                # ← 将来：ResourceBatchFolder*
├─ ImportSchedule/           # ← 将来：ImportPostProcessScheduler
├─ OpsMeta/                  # ← 将来：Evaluate / ScanSummary
└─ Util/                     # ← 将来：AssetPathUtility / Exclude / SO GUI
```

**不放 Shared：** `Generated/`（中间资产）、`Texture/` / `Model/`（单资源处理）、`Window/`（人机总入口）。

将来对外 Facade（Import / Prefab / PostOps 窄口）→ **已建** [`Api/`](./Api/)。
