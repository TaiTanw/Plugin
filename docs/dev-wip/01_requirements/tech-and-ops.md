# 技术要点

现约入口：[开发者须知](../README.md)。怎么点：[操作者须知](../../operator/README.md)。

## 选型

| | |
|---|---|
| 无头 | `-batchmode -nographics -quit -executeMethod PipelineCli.Run` |
| GLB | 宿主 **UnityGLTF**；`manifest` 勿写本机 `file:C:/…` |
| `.gltf` | 整包② + ④ B′；转 GLB 不开发 |
| AB | `BuildAssetBundles` + LZ4；`{stem}_android.assetbundle` / `_ios`，产品夹平铺 |
| ③ | `PrefabUtility.SaveAsPrefabAsset` |
| Docker / 队列 | V1.2 基建，本仓只保证本机静默 API |

batchmode：禁止 `Selection`、禁止 `DisplayDialog`。CLI 强制 Quiet。

## 三条「自动」（勿混词）

`Assets/Art/**` 是④写出的交付单元。

| 通道 | 谁触发 | 碰 Art？ |
|---|---|---|
| **1 导入期** | AssetPostprocessor | 模型设置 **硬跳过** Art；贴图/后处理跟 exclude（默认可跳 Art） |
| **2 L1 总批量** | 人工「执行全部」 | **是**。不读 exclude |
| **3 管线⑤** | Runner 勾选⑤ | **是**。代调通道 2 同一口，`triggeredByImport: false` |

不要把通道 1 的跳过读成「⑤/L1 也不碰 Art」。不要把刷白塞进 `OnPostprocessModel` 打 Art。

Incoming 受支持 ModelImporter 的安全基线已与总闸解耦。Art 模型硬跳过 ≠ 贴图 exclude。

## ④ vs ⑤

**④ = 复制并改「指到谁」。⑤ = 改「被指着的那份」内容。**  
Remap 不是换 Shader。交付 Shader 规范化是⑤ Material Op。

顶点刷白走通道 2/3。GLB（ScriptedImporter）刷白 Op 会 Skip。D19 不是 CLI 门禁。

## 命名

入库夹名：源路径向上三层 `_` 拼接。管线 ID2 覆盖③④⑥那根名字。人工③无 ID2 时用 Incoming 三层名。
