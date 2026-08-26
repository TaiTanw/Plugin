# 技术选型 · 知识 · 操作要点

返回 [本夹说明](./README.md) · [总目录](../README.md)

---

## 1. 技术选型（工具侧）

| 主题 | 选型 | 说明 |
|---|---|---|
| 无头跑法 | Unity `-batchmode -nographics -quit -executeMethod` | 无官方「只跑插件不开工程」CLI SDK |
| GLB 导入 | **UnityGLTF**（宿主包） | 插件可不 `using`；无包则无法导入 `.glb` |
| 备选 | glTFast | 导入失败样本库后再评 |
| AB API | `BuildPipeline.BuildAssetBundles` | **已用** `ChunkBasedCompression`（LZ4）；文件名/main 见 d1 契约 1/2 |
| Prefab 中转 | `PrefabUtility.SaveAsPrefabAsset` | ③ 已落地写盘 |
| 服务端形态（V1.2） | Linux Docker + headless 2022.3 | 工具只保证本机静默 API；镜像/队列属基建 |
| 渲染 | 工单目标 **URP**；本机宿主 historically Built-in | 「能出 AB」≠「APP 显示正确」 |

### 与 lean-api 边界

| 角色 | 职责 |
|---|---|
| 本仓 / swm-converter 内核 | GLB→Prefab→双端 AB；日志；退出码；命名契约 |
| lean-api | 入队、版本、下载、状态表 |
| 基建 | Docker、COS、TDMQ、TCR、CLS、License 激活 |

解耦：COS 文件 + 任务状态；转换器不写业务库。

---

## 2. 相关知识（短注）

### batchmode

- 禁止依赖 `Selection`、禁止卡 `DisplayDialog`。
- 必须：路径入参、`-logFile`、进程退出码。

### Remap（插件 1）

平铺时把材质/Prefab **引用改到 Art 副本路径**。  
**不是**插件 2 的职责；插件 2 的 ③ 只生成独立 Prefab。

### GLB 内嵌贴图

- `GetAssetPath(贴图)` → 常为 `.glb` 容器路径，不是独立 png。
- 已修：remap 不再把整包 GLB 拷进 `image/Texture`。
- 压图 Op 扫的是**独立贴图文件**；内嵌未抽出则⑤可能空跑 ≠ 已合规。

### FBX 内嵌 / `.fbm`

- `.fbm` 是缓存，**禁止当压缩目标**（PACKAGING_RULES 35）。
- 要压：平铺到 Art 独立图后再压（两遍流程）。

### 命名（入库 / Prefab）

`BatchFbxImportService.ResolveFolderName`：源路径**向上三层目录名** `_` 拼接。  
CLI `materialId` 可覆盖 Prefab 名。

---

## 3. 操作要点（编辑器）

| 目的 | 操作 |
|---|---|
| ③ Prefab | Project 选中 `.fbx/.glb` → `Tools > 自动化预设体（选中模型）` → 看 `Assets/IncomingPrefab/`（代码：`TOol/Editor/Generated/Prefab/`） |
| ④ 平铺（可选） | 选 Prefab → `Tools > Retinar > 平铺到 Art`；误拷 `.glb` 到 image 时应先删再平铺 |
| ⑤ 压图（可选） | `Tools > 资源处理总面板`，路径指向 Art 独立贴图 |
| ⑥ 规范化 | `从 Art 导出（规范化）`（全套+弹窗，人工线） |
| ⑥ 直通 | `成品直达`（仅 AB+UP，不改 Art） |

自动线目标：总面板一键 ②③⑥（④⑤ 勾选），**无确认弹窗**。

---

## 4. Converter / AB 契约（尽早钉死）

| 契约 | 倾向 |
|---|---|
| 输出名 | 现状 `name.assetbundle`+平台夹；工单倾向 `{id}_android` — **待 APP**（契约1） |
| AB 内主资源名 | 建议 `main` — **待 APP**（契约2） |
| 压缩 | LZ4 ChunkBased（**已采用**） |
| 成功 | 双端文件 + 退出码 0 |
| 平台 ID | 如 `app-unity-2022.3-urp` |

UnityGLTF 勿用本机 `file:C:/Users/...` 绝对路径（进不了 Docker）。
