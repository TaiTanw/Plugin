# Asset Plugin（资源工具集）

Unity Editor 插件：工程外模型 → Incoming → Prefab → Art → 资源处理 → 双端 AssetBundle。  
宿主：Unity 2022.3（Plugin2022，Built-in RP）。当前推荐标签 **v1.6.0**。  
远程：Gitea `team`（协作）· GitHub `origin`（备份）

**本批目的：** 把编排路径、导入钩子、菜单和⑥产物收成可日常使用的边界——管线自管三根路径，导入期只看全局总开关，手工菜单按步骤排，AB 用文件名区分平台。

④ 收口步骤 **1–14 已落地**。内核仍在 `TOol/Generated/Flatten`（未迁目录）。

---

## 操作者入口

日常只有下面几条路。细则：[Pipeline](./Pipeline/README.md) · [TOol](./TOol/README.md) · [开发备忘](./docs/dev-wip/README.md)

### 1. 自动化管线（主路径）

菜单：**`Tools > 自动化管线总面板`**

| 格 | 做什么 | 写出 |
|---|---|---|
| **[1] 入库** | 工程外 `.fbx/.obj/.glb/.gltf` 拷进导入根（工作区在步骤 SO） | `Assets/Incoming/<ID2>/`（默） |
| **[2] 导入期** | 全局导入设置：OnPreprocess 总开关与分项。**不是⑤** | — |
| **[③] Prefab** | 按 ID2 写 Prefab 根 | `Assets/IncomingPrefab/`（默） |
| **[④] 平铺** | 写入交付根；分类/清夹在平铺 SO | `Assets/Art/<名>/`（默） |
| **[⑤] 总批量** | 代跑资源处理「执行全部」（压图、刷顶点色、换 Shader） | 改 Art |
| **[⑥] 导出** | 双端 AB / 可选 UnityPackage | `AssetBundles/` · `Deliverables/` |

连锁：关③则④⑤关；关④则⑤关。总面板跑管线时**强制入库**；CLI 跟步骤 SO。同一物理文件不能在一张绑定表里出现两次。

### 2. 无头 CLI（同一套 Runner）

先关掉占用本工程的 Editor。

```text
"<Unity2022.3>\Unity.exe"
  -batchmode -nographics -quit
  -projectPath "D:\UnityMyCSProject\UnityProject\Plugin2022"
  -executeMethod PipelineCli.Run
  -logFile "<路径>"
  -source "<工程外模型或 Assets/…>"
  [-materialId <名>]
```

| 退出码 | 含义 | 本趟后面 |
|---:|---|---|
| 0 | 成功 | — |
| 10 / 20 / 30 / **40** | 参数 / 导入 / Prefab / ④硬失败（含 glTF 缺伴生） | **停** |
| **41** / **42** | leftover 外部 `.fbm` / 贴图身份（槽位不清） | ⑤⑥继续 |
| 50 | ⑤硬失败 | ⑥继续 |
| 60 | ⑥全失败 | 停 |
| 80 | 未捕获异常 | — |
| 70 | 预留，本入口不赋值 | — |

看日志 `[Pipeline] exit=N`。进程码设计上相同。契约：[cli-getting-started](./docs/dev-wip/04_implementation/cli-getting-started.md)

### 3. 人工步骤（不自动接整条管线）

`Tools > 手动操作栏`：按 [1][③][④][⑤][⑥] 排。④ 只有原子迁移 / 平铺两条，无专属平铺面板。⑥ 可导出选中 Prefab，或选文件夹批量打 AB。

### 4. 只做入库或只做资源处理

| 菜单 | 等于 | 用途 |
|---|---|---|
| `Tools > 手动操作栏 > 步骤 > [1]` | 总面板 **[1]** 同一窗 | 收集入库，或「输出到编排」（带本表 ID2） |
| `Tools > 手动操作栏 > 步骤 > [⑤]` | ⑤ 同一内核 | 对 Art「执行全部」 |
| `Tools > 全局导入设置（2）` | 总面板 **[2]** 同一窗 | Unity OnPreprocess 总开关与分项 |

---

## 仓库内容

| 目录 | 定位 | 菜单 |
|------|------|------|
| [`Pipeline/`](./Pipeline/) | 步骤 SO、Runner、总面板、CLI、人工④调度 | `Tools > 自动化管线总面板` |
| [`TOol/`](./TOol/) | [1] 入库、③ Prefab、⑤ Op；物理承载④内核；[2] 导入设置窗 | `手动操作栏` / `全局导入设置（2）` |
| [`RetinarBatchBuilder_Share/`](./RetinarBatchBuilder_Share/) | 插件 1：⑥ AB（`name_android` / `name_ios`） | `手动操作栏` [⑥] / `打开交付文件夹` |

**三条「自动」不要混：** 导入期 OnPreprocess（总开关 + Art 硬跳过）≠ L1 总批量（故意打 Art）≠ 管线⑤（④之后代调 L1）。结构：[overview](./docs/dev-wip/02_structure/overview.md)

**Art 单元：** `Assets/Art/<名>/`（Model / Texture / Material / Prefab…）；未知依赖进 `Unknown/`。

---

## 协作与分支

- Gitea 默认分支若仍是 `other`，首页会停在 **v1.4.4**；左上角改选 `main`。
- 开发走独立分支 + PR；工单跟踪。禁止提交 `.env` / Token。
- 开发备忘入口：[docs/dev-wip](./docs/dev-wip/README.md)（旧链 [CLI_AUTOMATION_DEV.md](./docs/CLI_AUTOMATION_DEV.md)）。
- 日志：[timeline](./docs/dev-wip/05_dev-log/timeline.md) · 回归：[REGRESSION_CHECKLIST](./RetinarBatchBuilder_Share/REGRESSION_CHECKLIST.md)
