# TOol / Editor / Shared — 目录说明

> 横切能力放 Shared（不绑 Texture 或 Model 单纵切）。  
> 自动化一体流程的 **③ 预设体制作** 落在本目录 `Prefab/`。

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
├─ Util/                     # ← 将来：AssetPathUtility / Exclude / SO GUI
└─ Prefab/                   # ★ ③ 预设体制作（本分支新建）
   ├─ README.md
   ├─ Config/                # 路径与开关 SO/常量
   ├─ Layout/                # 专用 Prefab 夹路径约定
   └─ Service/               # 收集模型 → 生成/保存 Prefab
```

## Prefab/（③ 新步骤）

流水线位置：② 导入之后、④ 平铺之前。  
产出：独立 `.prefab` 落入专用夹，供 Retinar 平铺（避免长期「嵌套 PrefabInstance → .glb」）。

详见 `Prefab/README.md`。
