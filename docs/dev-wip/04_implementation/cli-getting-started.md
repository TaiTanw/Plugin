# CLI

现约：[开发者须知](../README.md)。操作者命令：[操作者须知](../../operator/README.md)。内核仍是 `PipelineRunner`。

```text
"<Unity2022.3>\Unity.exe"
  -batchmode -nographics -quit
  -projectPath "<Plugin2022 工程根>"
  -executeMethod PipelineCli.Run
  -logFile "<路径>"
  -source "<工程外模型或 Assets/…>"
  [-materialId <名>]
```

入口必须是 Editor 程序集 `public static void` 无参。先关掉占用该工程的 Editor。

| 参数 | Options | |
|---|---|---|
| `-source` | `SourcePath` | 必填；`.gltf` 整包入库 |
| `-materialId` | `MaterialId` | 可选，覆盖 ID2 |

`FromSettings` 之后 **强制** `Quiet=true`。③–⑥与清空跟步骤 SO，不加 flag。扩参另开项。`70` 本入口不赋值。

退出码与面板同一张表（见开发者须知）。看日志 `[Pipeline] exit=N`。洋红 / 顶点色 **不算** CLI 失败。产物文件名 `{stem}_android.assetbundle`，在导出 SO 的 AB 根平铺。
