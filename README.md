# Asset Plugin（资源工具集）

Unity Editor 插件：工程外模型 → Incoming → Prefab → Art → 资源处理 → 双端 AssetBundle。  
宿主：本分支 `other` 对应 Unity **2020.3**（ModleEvent，Built-in RP）。当前推荐标签 **v1.7.0**。  
同一套代码在 Unity 2022.3 的宿主是 Plugin2022，发布看 `main`。  
远程：Gitea `team`（协作）· GitHub `origin`（备份）

日常：[操作者须知](./docs/operator/README.md)  
约束与实现：[开发者须知](./docs/dev-wip/README.md)  
分层总目：[docs/](./docs/README.md)

| 你要做什么 | 去哪 |
|---|---|
| 选区、导入、人工/管线、⑤批量 vs 精准、⑥名字 | [操作者须知](./docs/operator/README.md) |
| 硬约束、结构、待办、CLI | [开发者须知](./docs/dev-wip/README.md) |
| 中间层地图 | [Pipeline/](./Pipeline/README.md) |
| 插件 2 | [TOol/](./TOol/README.md) |
| 插件 1 AB | [RetinarBatchBuilder_Share/](./RetinarBatchBuilder_Share/) |

菜单：`Tools > 自动化管线总面板` · `Tools > 手动操作栏` · `Tools > 全局导入设置（2）`

## 仓库

| 目录 | 定位 |
|------|------|
| [`Pipeline/`](./Pipeline/) | 步骤 SO、Runner、总面板、CLI、人工④调度 |
| [`TOol/`](./TOol/) | [1] 入库、③ Prefab、⑤ Op；④ 内核；[2] 导入设置 |
| [`RetinarBatchBuilder_Share/`](./RetinarBatchBuilder_Share/) | ⑥ `name_android` / `name_ios` |

## 协作

开发走独立分支 + PR。禁止提交 `.env` / Token。`other` 对应该 2020.3 工程；发布线仍看 `main`。  
旧链：[CLI_AUTOMATION_DEV.md](./docs/CLI_AUTOMATION_DEV.md) · 日志：[timeline](./docs/dev-wip/05_dev-log/timeline.md)

## 须知风险

当前若只导出纯模型，Unity 2022 与 2020 都可以。一旦涉及 unitypackage 里的动画等资源，手机端目前不能加载 **2022 导出的 AB**（会闪退）。同一套 2022 工程代码迁到 2020 再导出，已经确认手机端可以加载。
