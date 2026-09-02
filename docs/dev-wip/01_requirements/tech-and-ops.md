# 技术选型 · 知识 · 操作要点

返回 [本夹说明](./README.md) · [总目录](../README.md)

---

## 1. 技术选型（工具侧）

| 主题 | 选型 | 说明 |
|---|---|---|
| 无头跑法 | Unity `-batchmode -nographics -quit -executeMethod` | 无官方「只跑插件不开工程」CLI SDK |
| GLB 导入 | **UnityGLTF**（宿主包） | 插件可不 `using`；无包则无法导入 `.glb` |
| `.gltf` 源（D22） | **直接整包② + ④ B′**；转 GLB 可选 | 编辑器不做 DCC 重导。见 backlog **O** |
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

### Art 目录边界（三条通道，勿混「自动」一词）

`Assets/Art/**` = 插件 1 平铺写出的**交付单元**。插件 2 / 中间层对它有三条通道；都叫「自动」时最容易混：

| 通道 | 谁触发 | 是否碰 Art | 机制 |
|---|---|---|---|
| **1. 导入期自动流** | Unity `AssetPostprocessor`（设置自动 / 后处理自动） | **否**（正确） | `excludedPathPrefixes` 含 `Assets/Art/`。不改交付区 Importer（规则 33），钩子里也不跑 Art 的 Op |
| **2. L1 手动总批量** | 资源处理总面板「执行全部」 | **是** | `RunMasterBatch`；**不读** exclude；`triggeredByImport: false` |
| **3. 中间层⑤** | `PipelineRunner` 勾选⑤ | **是** | **代调通道 2 同一口**（`ToolPostProcessApi.RunMasterBatch`）。看起来像自动，但是编排在调面板手动内核，不是通道 1 |

隐患：把通道 1 的「自动跳过」误读成「管线⑤ / 总批量也碰不到 Art」→ 错误地把 Shader 烤/压图塞回插件 1，或以为开⑤会空跑。  
另一隐患：为了让管线「自动」生效，把刷白塞进 `OnPostprocessModel` 打 Art → **把通道 3 的事做成了通道 1**，违反规则 33。交付区刷白只走 2/3。

细则见 `TOol/ARCHITECTURE.md`、规则 33。

**顶点刷白（对照）：** Art 上要白，靠通道 2/3，不是靠导入钩子。  
- FBX（`ModelImporter`）：⑤/手动可开 Read/Write 再写全白。  
- GLB（UnityGLTF `ScriptedImporter`）：⑤会命中文件，但 Op **跳过**（非失败）。要白顶点需源文件已白或另做 GLB 方案。  
曾误报 `不是 ModelImporter 资产` 为失败 → 已改为 Skip。  
**D19（已降级，非管线门禁）：** 色写在导入结果上；⑥ `BuildAssetBundles` 或贴图批标脏会重导 FBX、冲掉白顶点。这主要在 **UnityGLTF 导出 GLB** 时露出来。交付 AB **不以**工程 Mesh 全白为必要。若要白 GLB：人工对 `Model/*.FBX` 刷白，且**不要**再打 AB / 无保护重导后再导出。禁止把刷白塞进导入钩子打 Art。见 backlog **L**。

#### ④ → ⑤ 路径约定（两插件对齐注意事项）

| 约定 | 说明 |
|---|---|
| **⑤ 默认直指 Art** | L1 `ResourceBatchFolderStore` 种子 / 常用路径 = `Assets/Art`（大根）。平铺产物在此，Shader 烤/压图/模型 Op 都扫这里 |
| **④ 成功后交给⑤** | Pipeline：`runFlatten && runPostProcess` 时④后调 `ToolPostProcessApi.RunMasterBatch`；⑤**不**再走导入期 exclude |
| **两插件对齐点** | 插件 1 写 Art 单元目录结构；插件 2 ⑤ 按 L1 批量路径 `FindAssets`。当前产品约定：**路径语义就是 Art**，不要把导入夹当成⑤交付口 |
| **单任务 Art 单元** | 编排不改 L1 Prefs。开④后 Runner 把本次 Art 单元写入 `PostProcessFolderPaths`（D17） |
| **不要混的词** | 插件 1 Remap = 引用收敛；插件 2 材质层 = **交付 Shader 规范化**（换 Shader + 槽映射），不是同一类 Remap |

### Remap（插件 1）

平铺时把材质/Prefab **引用改到 Art 副本路径**。  
**不是**插件 2 的职责；插件 2 的 ③ 只生成独立 Prefab。

### ⑤ 材质处理 / 交付 Shader（为何算资源处理、不归④）

> 核对口径（2026-08-27）：手感像 Inspector 换 Shader；工程上仍是⑤资产 Op。细节归档见 [d13-glb-magenta](../03_open-items/d13-glb-magenta.md)。

#### 关键：源资产 vs 编辑器资产

| | 源侧（入库前 / 容器内） | 编辑器交付资产（Art 等） |
|---|---|---|
| 形态 | 磁盘 FBX/GLB、或 glb **子资源**（同 guid 不同 fileID） | 工程内独立 `.mat` / `.png` / `.prefab` / `.controller`… |
| 谁维护 | DCC / 导入器 | Converter 规则改写后的**可出包文件** |
| ⑤ 扫什么 | 一般不直接改「源容器」当交付契约 | **已落盘的 Unity 资产路径**（`FindAssets` → 改内容 → `SaveAssets`） |

GLB 线常见：平铺后有独立 `.mat`，贴图仍可挂在 `.glb` 子资源上——材质球已是编辑器资产，贴图源仍可能嵌在容器里。

#### 为何不归④平铺

| | ④ 平铺 | ⑤ 材质（及压图等） |
|---|---|---|
| 主动作 | **复制**依赖进 Art + **改关联引用**（GUID/路径指到本包副本） | **修改**已被引用的那份资产**内容**（换 Shader、压图字节、以后改 Controller…） |
| 引用关系 | Prefab → 新路径上的 mat/贴图 | Prefab **仍指向同一 GUID**；改的是该 GUID 指向的文件内部 |
| 解决的问题 | 外引切断、目录规范 | APP 契约（认不认 Shader、图是否够小…） |

一句话：**④ = 复制并改「指到谁」；⑤ = 改「被指着的那份东西长什么样」。**  
不要把 Shader 烤塞进平铺拷贝循环；也不要把 Remap（引用收敛）和「交付 Shader 规范化」混称。

#### 入口与做法（技术要点）

- **入口不是扩展名 `.glb`**，而是 Art 里 `.mat` 的 **`material.shader.name`**：白名单精确匹配则跳过；源名子串（如 `UnityGLTF`/`PBRGraph`）或不在白名单 → 烤到 `targetShaderName`（默认 `Standard`）。
- **做法**：读旧槽 → 换目标 Shader → 写回 `_MainTex`/`_Color`/metallic/gloss 等；有资产 IO（脏标记 + 保存），与「设置自动」改 Importer 元数据不同，与压源图同属**改资产内容**。
- **为何治洋红**：APP 无 Packages 里的 ShaderGraph 时整片 Error Shader；换成现网能亮的 Standard（或可配 URP Lit）即可。内嵌贴图是正交问题。

#### 和「设置自动」的像与不像

手感都像改 Inspector；设置自动改 **Importer**；材质烤 / 压图改 **资产正文**。二者都可走资源面板，但导入期自动默认 **exclude Art**；交付靠④后⑤总批量。

#### 推及：动画状态机等

若改的是 Art 里已有 `.controller` / `.anim` **文件内容**，同属⑤类资产后处理（新 Op/大类即可）。  
若要从 glb **抽出新 Clip 并改 Prefab 绑定**，更偏④增强；不必为「改状态机」单开一条平铺管线。

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
| ④ 平铺 | `Tools > Retinar > 批量汇总 > 平铺到交付中间区 Art`（Pipeline **默认开**；根写死 `Assets/Art`） |
| ⑤ 总批量 | `Tools > 资源处理总面板`：路径直指 Art；顺序 **贴图 → 材质(Shader) → 模型**（Pipeline **默认开**） |
| ⑥ / 日常出包 | 管线⑥，或 `成品直达 > 选中预制体直通打包（推荐）`（读 `RetinarExportSettings`） |
| 【遗产】规范化全套导出 | `批量汇总 > 【遗产】从 Art 规范化导出`（路径仍写死 `Deliverables`；后续以直达/⑥为准） |

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
