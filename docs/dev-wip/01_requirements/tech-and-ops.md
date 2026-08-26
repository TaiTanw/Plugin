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
| D6 UnityGLTF 依赖 | 包引用勿用本机 `file:C:/...` | **镜像**=Docker 转换镜像；绝对路径进不了镜像构建 |

### D6「镜像」怎么理解（短）

这里的「镜像」指 **Docker 容器镜像**（服务器上跑 headless Unity 的那份环境），不是美术贴图镜像、也不是 Git mirror。

`Packages/manifest.json` 里若 UnityGLTF 写成 `file:C:/Users/你的路径/...`，只在你本机能解析；打进 Docker 镜像或别人机器就断。D6 = 改成 registry/git/相对嵌入，让镜像构建可复现。

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

### Art 目录边界（防隐患）

`Assets/Art/**` = 插件 1 平铺写出的**交付单元**。插件 2 对它有**两条互不混淆的通道**：

| 通道 | 是否碰 Art | 机制 |
|---|---|---|
| 设置自动 / 后处理自动（导入期） | **否**（默认） | `excludedPathPrefixes` 含 `Assets/Art/` |
| ⑤ 总批量 / L1 手动（平铺后） | **是**（默认路径就是 Art） | `ResourceBatchFolderStore`；**不读** exclude 列表 |

隐患：把「自动跳过」误读成「⑤ 也碰不到 Art」→ 错误地把 Shader 烤/压图塞回插件 1，或以为开⑤会空跑。细则见 `TOol/ARCHITECTURE.md`、规则 33。

#### ④ → ⑤ 路径约定（两插件对齐注意事项）

| 约定 | 说明 |
|---|---|
| **⑤ 默认直指 Art** | L1 `ResourceBatchFolderStore` 种子 / 常用路径 = `Assets/Art`（大根）。平铺产物在此，Shader 烤/压图/模型 Op 都扫这里 |
| **④ 成功后交给⑤** | Pipeline：`runFlatten && runPostProcess` 时④后调 `ToolPostProcessApi.RunMasterBatch`；⑤**不**再走导入期 exclude |
| **两插件对齐点** | 插件 1 写 Art 单元目录结构；插件 2 ⑤ 按 L1 批量路径 `FindAssets`。当前产品约定：**路径语义就是 Art**，不要把导入夹当成⑤交付口 |
| **单任务 Art 单元（可选加固）** | ②后 `SyncFolderToL1` 可能写导入夹；开④后更稳的是把⑤路径改成**本次 Art 单元**（`PostProcessFolderPaths`）。未实现时大根 `Assets/Art` 通常够用——属注意事项，不是归属争议 |
| **不要混的词** | 插件 1 Remap = 引用收敛；插件 2 材质层 = **交付 Shader 规范化**（换 Shader + 槽映射），不是同一类 Remap |

### Remap（插件 1）

平铺时把材质/Prefab **引用改到 Art 副本路径**。  
**不是**插件 2 的职责；插件 2 的 ③ 只生成独立 Prefab。

### GLB 内嵌贴图 / 为何能拆材质球

- 磁盘上 **仍是一个 `.glb`**；UnityGLTF 把它当容器，内部 Mesh/贴图/材质/动画是 **子资源**（同一 guid，不同 fileID）。
- 平铺会把 **材质复制成独立 `.mat`**（方便 remap/改名），贴图若没有独立 png，`.mat` 仍 **引用 glb 容器里的子贴图**（ggdddd 即如此）。
- `GetAssetPath(贴图)` → 常为 `.glb` 路径，不是独立 png。
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
| ④ 平铺 | 选 Prefab → `Tools > Retinar > 平铺到 Art`；误拷 `.glb` 到 image 时应先删再平铺（Pipeline **默认开**） |
| ⑤ 总批量 | `Tools > 资源处理总面板`：路径直指 Art；顺序 **贴图 → 材质(Shader) → 模型**（Pipeline **默认开**） |
| ⑤ 第一刀验洋红 | `Tools > 资源处理 > 规范化交付 Shader（选中夹或 ggdddd）` → 再打 AB |
| ⑥ 规范化 | `从 Art 导出（规范化）`（全套+弹窗，人工线） |
| ⑥ 直通 | `成品直达`（仅 AB+UP，不改 Art） |

自动线目标：总面板一键 ②③④⑤⑥（④⑤ 默认开，可关），**无确认弹窗**。

命令行外壳（D5）概念与第一刀：[cli-getting-started](../04_implementation/cli-getting-started.md)。

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
