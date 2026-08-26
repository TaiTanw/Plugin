# TOol / Editor / Generated

**语义（已确认）：** 插件 2 **写入工程内、供后续步骤消费的中间资产**能力。  
不是交付区 `Assets/Art/`，也不是⑤对已有资源的原地改写（压图 / 顶点白等）。

当前子模块：

| 路径 | 步骤 | 工程落盘示例 |
|---|---|---|
| [`Prefab/`](./Prefab/) | ③ 自动化预设体 | `Assets/IncomingPrefab/*.prefab` |

对外调用入口（编排层将来调这里，而非 Shared）：`PrefabBuildService`。
