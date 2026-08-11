# Retinar Unity 打包工具变更记录

记录规则：最新版本写在最上方；每次修改必须填写“原因、改动、影响、验证、回退”。

## 2026-08-11 — v1.3.7 补丁：配置归属说明 + 入库/批量路径易用性

- 原因：批量入库时导入成功 Info 刷屏易被当成警告；「添加文件夹」无系统选框体验差；成功项仍留在列表需手清；三份 Art 前缀配置易混淆。
- 改动：
  1. 贴图/模型 `ImportSettingsProcessor` 成功路径改为静默（不打 Info）。
  2. 总面板「添加文件夹」弹出 Assets 内选夹；保留空行可拖入；Save 后补回空占位。
  3. 批量 FBX 执行后移出 Success 项。
  4. 文档/Tooltip/HelpBox 明确：EP 批量路径 vs `excludedPathPrefixes` vs `deliveryAlertPathPrefixes` 三份独立配置。
- 影响：仍属 **v1.3.7** 线，不新打标签。
- 验证：批量导入后 Console 无「已按…自动处理」刷屏；添加文件夹可选到 Assets/Art 或 Incoming；成功行从列表消失。
- 回退：恢复 Log、旧添加空行逻辑、不清 Success 即可。
- 关联问题：配置归属；入库体验。

## 2026-08-10 — v1.3.7 优化面板分层 + 降低使用难度

- 原因：总面板/子面板配置与勾选混在一起，上手成本高；同文件夹多 FBX 批量入库时夹名冲突直接 Conflict，迫使美术先改目录才能导入。
- 改动：
  1. **插件 2 面板三层**：L1 总面板（共用批量路径 + 总/分项执行·扫描）；L2 子面板（精准范围 + 本机勾选）；L3 高级窗口（SO：子处理配置 + 主批量/导入自动 Op 集合）。主面板一律 **L1 路径 × L3 Op**。
  2. 主批量 Op 写入 Settings SO（`masterBatchOperationIds`），与导入自动 Op 分区；降低「勾选在哪、生效在哪」的歧义。
  3. **批量 FBX 导入**：同基名多 FBX 时夹名自动追加无扩展文件名消歧（Warning，允许导入）；仅目标已存在 / 交付区 / 消歧后仍重名才 Conflict。
- 影响：当前**推荐使用**标签为 `v1.3.7`。日常路径更短：总面板跑批量，进阶再开 L2/L3。
- 验证：总面板执行应对 L3 勾选的主批量 Op；同夹两 FBX 列表应 Warning 消歧而非整批 Conflict；L2 勾选不影响 L1。
- 回退：恢复单层面板与「同基名即 Conflict」逻辑即可。
- 关联问题：降低使用难度；批量入库同夹多 FBX。

## 2026-08-07 — v1.3.6 导出贴图预检提前 + 校验类别码；插件 2 Evaluate/仅扫描

- 原因：打 AB 后才看到贴图 WARN 偏晚；失败报告缺少稳定类别标签；插件 2 执行前判断与扫描需统一口径，并避免全库盲跑。
- 改动：
  1. **插件 1**：`ExportArtPrefabPaths` 在 `BuildAssetBundles` 前对已过门禁资产做贴图 5MB/POT 预检（Console `[Retinar] 贴图预检`）；校验失败与贴图报告行增加类别码（`MODEL_FOLDER_DIRTY` / `SAFEZONE` / `EXTERNAL_DEP` / `TEXTURE_POT` / `TEXTURE_SIZE` / `TEXTURE_LOAD`）。
  2. **插件 2**：`Evaluate` + `AssetOperationEvaluation`；Runner `Scan` dry-run；子面板/总面板「仅扫描」；批量路径空列表默认含 `Assets/Art`（一次性补种）。
- 影响：曾为推荐标签；**现由 v1.3.7 接替**。导出仍先自愈再门禁；贴图预检不阻断出包。插件 2 不调用插件 1。
- 验证：导出超标 Art 贴图应在打 AB 前打出预检 Warning；子面板勾选压图后「仅扫描」应只列超标项；顶点色仅扫描应只列非全白模型。
- 回退：去掉预检调用与类别码前缀；插件 2 回退 Evaluate/Scan 相关改动。
- 关联问题：检测/扫描分层；交付预检与后处理候选分离。

## 2026-08-06 — v1.3.5 完全版：全流程支持（批量 FBX 入库 + 平铺/处理/导出）

- 原因：需对外提供可交付的完整工具链版本：外部 FBX 批量入库 → 人工 Prefab → 平铺 Art → 贴图/顶点色处理 → 导出；此前 `v1.3.2` 仅作过渡，现降为测试版标签。
- 改动：
  1. **插件 2**：新增 `Tools > 批量FBX导入`（拖入外部目录、重名检测、统一入库导入区）；配置 `BatchFbxImportSettings`；**不**自动建 Prefab / 不平铺 / 不导出。
  2. 总面板与 Runner 修复优化（含总批量顺序、导入告警策略、设置 GUI 抽取等，见近期提交「批量FBX导入开发」「修复与优化」）。
  3. 文档：README / TOol README / ARCHITECTURE 同步全流程入口；本 CHANGELOG 标注版本角色。
- 影响：当时标注为完全版基线；**现由 v1.3.6 接替为当前推荐标签**。旧标签 `v1.3.2` 已改为 `v1.3.2-test`。
- 验证：批量导入若干外部 FBX 无冲突入库；再走人工 Prefab → 平铺 → 总面板处理 → 导出 Art 全部/选中，Deliverables 正常。
- 回退：回退至 `v1.3.2-test` 指向提交，并去掉批量 FBX 相关菜单与资产即可。
- 关联问题：全流程支持；批量入库与 Issue #2（Prefab 是否自动化）边界——本版仍要求人工 Prefab。

## 2026-08-06 — v1.3.2（测试版）总面板批量优化与导入告警降噪

- 原因：交付流程里常要一次跑模型+贴图批量，又希望能关掉某一侧；导入自动命中大量已达标资源时，`CanProcess` 全否的 Warning 刷屏干扰排查。
- 改动：
  1. 总面板「执行全部」：固定模型→贴图顺序；新增「纳入贴图 / 纳入模型」开关（EditorPrefs，默认开；不影响分项按钮）。
  2. 贴图/模型 Runner：仅手动执行时对「命中但操作均不适用」打 Warning；导入自动触发不再刷屏。
  3. 同步回归检查表与结构说明中的总面板描述。
- 影响：当时曾标为稳定版；**现已降为测试版**（远程标签改为 `v1.3.2-test`），由 **v1.3.5 完全版** 接替。Retinar 平铺/导出菜单行为相对 v1.3.1 不变。
- 验证：总面板关掉「纳入贴图」再执行全部，应只跑模型；重开工程导入大量已压缩贴图，Console 不应再被 CanProcess 全否 Warning 刷满。
- 回退：去掉 MasterBatch 开关相关 UI/Prefs，并恢复 Runner 对导入路径的 Warning 即可。
- 关联问题：总批量可控；导入告警降噪。

## 2026-08-05 — v1.3.1 导出拆成「Art 全部 / 选中 Art Prefab」子菜单

- 原因：平铺后 Prefab 分散在 `Art/<名>/Prefab/`，多选手找麻烦；需要固定 Art 根上一键全量导出，同时保留选中导出并对非 Art 选中警告。
- 改动：
  1. `从 Art 导出交付物` 改为子菜单：`导出 Art 全部`、`导出选中的 Art 预制体`。
  2. 全部：扫描 `Assets/Art/*/Prefab/*.prefab`，确认数量后走原导出管线。
  3. 选中：仅 Art 下 Prefab；非 Art / 非 Prefab / 文件夹写入跳过警告（Console + 弹窗）；合格项为 0 则中止。
  4. 抽出共用 `ExportArtPrefabPaths`。
- 影响：无需再逐个点进 Prefab 目录即可全量导出；选中导出对导入区误选有明确警告。
- 验证：无选中点「全部」应列出 Art 内交付 Prefab；选导入区 Prefab 点「选中」应警告跳过。
- 回退：恢复单一导出 MenuItem 即可。
- 关联问题：导出批量、非 Art 警告。

## 2026-08-05 — v1.3.0 菜单拆成平铺 / 导出，移除一键 Batch Build

- 原因：交付需在 Art 平铺与出包之间插入插件 2 手动（压贴图、刷顶点色）。一键 `Batch Build` 把平铺与导出绑死，易跳过中间步骤；另需与插件 2「后处理自动不保证交付」的流程对齐。
- 改动：
  1. **移除** `Tools > Retinar > Batch Build Selected Models` 与 `Normalize Selected Models Only`。
  2. 新增 `平铺到 Art（选中）`：Project 多选 Prefab/FBX → 只写 `Assets/Art/<名>/`，不出 AB/Deliverables。
  3. 新增 `从 Art 导出交付物（选中 Prefab）`：只接受 `Assets/Art/` 下 Prefab（可多选）→ 校验 + AB + UnityPackage + 交付归档。
  4. `Open Deliverables Folder` 改名为 `打开交付文件夹`。
  5. **文档**：本 CHANGELOG、`PACKAGING_RULES`（两遍流程菜单名）、分享说明 §3/§5、回归检查表菜单名。
- 影响：操作变为两步；导入区资源必须先平铺再导出。可多选；不支持选中文件夹递归（后续可加，非框架改动）。
- 验证：选导入区 Prefab 平铺 → Art 出现目录；选 Art Prefab 导出 → Deliverables 有产物；选导入区 Prefab 点导出应提示先平铺。
- 回退：恢复旧 MenuItem 与 `BatchBuildSelectedModels` / `NormalizeSelectedModelsOnly` 即可。
- 关联问题：平铺与导出分离；插件 2 交付须手动。

## 2026-07-31 — v1.2.9 打包重导 FBX 时保留已写入的 Mesh 顶点色

- 原因：用户在 `Assets/Art/<模型>/Model` 上手动「顶点色设为全白」后，再选 Prefab Batch Build，Model 里顶点色又变回源数据。根因与贴图覆盖同类：打包链路 `ApplyModelImportSettings` / `ExtractAndBind` / 材质 remap / Local 校正会对交付区 FBX 多次 `SaveAndReimport`，Unity 从 FBX 二进制重建 Mesh 子资产，导入后改过的 `mesh.colors` 全部丢失。日志上贴图保护已生效（`保留已压缩 Art 贴图`），但顶点色此前无快照。
- 改动：
  1. 新增 `SaveAndReimportPreservingMeshVertexColors`：重导前按（Mesh 名 + 顶点数）快照 `colors`，重导后写回。
  2. 交付区 Model 相关 `SaveAndReimport` 全部改走该封装。
  3. 无外部 `.fbm` 时跳过 `ExtractTextures`（避免无意义重导与贴图盖写）。
  4. TOol 顶点色操作说明改为与打包保留行为一致。
  5. **文档**：`PACKAGING_RULES` 规则 40；回归检查与分享说明 FAQ；新增插件 2 结构说明 `TOol/ARCHITECTURE.md`。
- 影响：手动改过的 Art 模型顶点色，在不删 Art 的重复打包后应保留。删掉 Art 重打仍会从 FBX 源色开始，需再手动跑一次顶点色。
- 验证：对 `Plane_Jian31/Model/fbx.FBX` 设全白 → 选 Prefab 再打包 → Console 应有「SaveAndReimport 后已恢复 Mesh 顶点色」或因跳过 Extract 而无冲掉；模型子资产顶点色仍为白。标记为待在编辑器中验证。
- 回退：恢复直接 `importer.SaveAndReimport()` 即可；回退会再次出现“处理顶点色后再打包又变回去”。
- 关联问题：模型文件夹处理后，点预制体导出包，模型文件夹顶点色问题又来了。

## 2026-07-31 — v1.2.8 重新打包不再用 FBX 内嵌大图盖掉已压缩的 Art 贴图

- 原因：两遍流程里先压好 `Assets/Art/<模型>/Texture/` 后再次 Batch Build，贴图又变回超标。根因是为切断外部 `.fbm` 依赖而调用的 `ExtractTextures` 会把 FBX 内嵌原始大图写回同名 Art 文件；另外 `SyncNewerSourceTextureToWorkingCopy` 仅看时间戳，导入区 `.fbm` 再导入变“更新”时也会用更大源文件覆盖已压缩副本。用户侧表现：压缩工具显示「1 文件修改」，立刻再打包 `texture_size_report` 又 WARN。关联误判：Art「看起来都达标」但报告仍 issue——实际是磁盘字节略超 5MB（如 `J310003.png` 5.64MB），或压缩结果已被盖回大图。
- 改动：
  1. `ExtractTextures` 前快照 Art/Texture，抽取后若同名文件变大（或被删）则写回快照。
  2. `SyncNewer`：源文件更大时跳过覆盖，并打 `[Retinar]` 日志。
  3. 超标告警文案改为与上述保护一致。
  4. **文档同步**：`PACKAGING_RULES.md` 修订规则 34–36，新增 37–39 与「已知问题对照表」；`REGRESSION_CHECKLIST.md` 增补两遍流程/Console 期望与第八节速查表；`RetinarBatchBuilder_分享说明.md` 新增 §6.1。
- 影响：首次打包仍会抽出大图（需手动压 Art）；压完再打应保留压缩结果。外部 `.fbm` 的 Extract+remap 自愈仍保留。
- 验证：压小 `J310003.png` 后不删 Art 再打包，Console 应出现“恢复 N 张更小的 Art 贴图”或 SyncNewer 跳过；`texture_size_report.txt` 该行保持 OK。标记为待在编辑器中验证。
- 回退：去掉快照恢复与 SyncNewer 体积判断即可；回退会再次出现“压完再打包又超标”。文档可随代码一并回退对应章节。
- 关联问题：压缩显示 1 文件修改，再次打包体积回去；Art 未超标观感与报告 WARN 并存。

## 2026-07-31 — v1.2.7 补齐 ExtractTextures / 外部 .fbm 自愈链路日志

- 原因：`Plane_Jian31` 仍报外部 `AAA/.../fbx.fbm` 依赖时，Console 看不清打包流程、自愈、校验各阶段是否跑过 Extract/AddRemap，以及前后还剩哪些 .fbm 路径，排查困难。此前为切断依赖已加入交付区 `materialSearch=Local`、`ExtractTextures`+`AddRemap`、材质已在 Art 时仍 remap 等逻辑；缺日志时无法确认是否执行及是否被后续步骤抵消。
- 改动：在打包流程两轮 Extract、自愈前后、校验强制 Extract 前后，以及 `ExtractAndBind` / `AddRemap` / `RemapAllArtMaterials` 内补齐 `[Retinar]` 日志（含外部 .fbm 清单与 remap 计数）。
- 影响：仅增加 Console 输出，不改变修复逻辑本身。**注意**：本版引入的 Extract 写回同名文件，会与两遍压缩冲突，由 **v1.2.8** 补保护。
- 验证：重新打包 `Plane_Jian31`，Console 应能按阶段看到 Extract/AddRemap 与 .fbm 清零或残留清单；标记为待在编辑器中验证。
- 回退：去掉新增 `Debug.Log`/`LogWarning`/`LogError` 即可。
- 关联问题：外部 `.fbm` 依赖自愈排查。

## 2026-07-30 — v1.2.6 子资产映射不再按名字匹配，修复重名节点导致的错位、碎片与破面

- 原因：`Plane_Zhi18` 导入源模型时正常，打包出来的预制体却位置错乱、主体外出现碎片状物体并伴随破面。取证：
  1. 扫 `Model/fbx-all.FBX` 二进制，里面有 **117** 个 Model 节点但只有 **88** 个不同名字——美术按材质给物体命名，`yy3d-zhi18-0012` 重复 6 次、`yy3d-zhi18-0003` 重复 5 次，`0010/0005/0009/0007` 各 4 次，`0011/0008/0006` 各 3 次。
  2. 扫打包产物 `Prefab/Plane_Zhi18.prefab`，**112** 个 MeshFilter 只指向 **83** 个不同的 Mesh，且被重复引用最多的几个 Mesh 恰好是 6/5/4/4/4/4/3/3/3 个节点——与 FBX 的重名次数逐项吻合，塌缩数量同为 29。
  3. 该预制体里有 40 个负缩放（镜像）节点和 45 个非等比缩放节点，最大到 `37.3, 37.3, 39.6`。
  根因是 `BuildCopiedObjectMap` 用 `(类型, 名字) + FirstOrDefault` 把源资产子对象映射到交付副本子对象。Unity 导入 FBX 时不会给重名 Mesh 改名，于是 6 个不同的 Mesh 全部映射到副本里的第一个同名 Mesh，5 个节点拿到了别人的几何体。这些节点又带着镜像和几十倍的非等比缩放，错配的表现就是碎片飞到主体外、镜像节点绕序翻转成破面。`Plane_WuZhi10w` 的节点名（`Z10_M001` 一类）恰好互不重复，所以一直没暴露。
- 改动：重写 `BuildCopiedObjectMap`，拆成 `MapSubAssetsBetweenCopies` / `LoadSubAssetsForMapping` / `TryPairByLocalFileIdentifier` / `PairByOrdinalIndex` 四个函数：
  1. 先按 `localFileIdentifier` 精确配对，与 `LoadAllAssetsAtPath` 的返回顺序无关。
  2. ID 集合两边对不上时（例如副本的 ID 表没保留）退化成"同类型内按序号配对"，并逐项核对名字，名字错位就报错放弃这一类的改写。
  3. 两条路径都保证一对一，不可能再出现多个源对象塌缩到同一个副本上。
  4. 子对象数量两边不一致时报错跳过而不是猜——引用没改写会被外部依赖校验拦下来，改错了却是静默产出坏几何体。
  5. `LoadSubAssetsForMapping` 排除模型资产里的 GameObject 节点：预制体引用的是 Mesh 而不是这些节点，纳入映射没有意义还会干扰按序号配对。
- 影响：
  - 只要 FBX 里存在重名节点，之前打出的所有包都可能有错配的几何体。`Plane_Zhi18` 已确认受影响，需要重新打包；其余模型按"FBX 节点名是否有重复"逐个判断。
  - `Assets/Art/Plane_Zhi18/Prefab/Plane_Zhi18.prefab` 里的错误 Mesh 引用已经落盘，直接对它重跑不会自愈，必须删掉 `Assets/Art/Plane_Zhi18/` 后从源预制体重新走一遍。
  - 正常（无重名）的模型行为不变，映射结果与从前一致。
- 验证：Roslyn 全量编译零错误零警告。重新打包 `Plane_Zhi18` 并确认预制体的 MeshFilter 数与不同 Mesh 数关系恢复正常、无碎片无破面，标记为待在编辑器中验证。
- 回退：恢复原来的按名字匹配即可；回退会重新引入"FBX 重名节点导致交付预制体几何体错配"。
- 关联问题：`Plane_Zhi18` 导入正常但导出预制体位置错乱、主体外有碎片状物体、破面。
- 已知遗留：`RebasePrefabRootToOrigin` 仍用 `Transform.lossyScale` 回写子节点缩放。`lossyScale` 表达不了镜像（负缩放）和斜切，对这个有 40 个负缩放、45 个非等比缩放节点的模型是同一类风险。当前源预制体根节点是单位变换、该函数会提前返回，所以没有触发；一旦有人给根节点加了缩放或旋转就会重现同样的错位与破面。尚未修复。

## 2026-07-30 — v1.2.5 外部材质改按材质名生成，修复材质槽显示紫色

- 原因：拖入 `fbx.FBX` 后模型显示紫色（材质槽解析不到材质）。取证：扫描 FBX 二进制得到里面有 **4** 个材质节点（`Material #25` 到 `#28`），但 `Assets/New Folder/Materials/` 只生成了 **3** 个 `.mat`。根因是导入插件设了 `materialLocation = External` 却没有指定 `materialName`，于是沿用了 External 模式的默认值 `BasedOnTextureName`——按材质用到的贴图名来生成和查找外部 `.mat`。这个策略隐含要求"每个材质都有贴图且互不相同"，一旦某个材质没有贴图、或两个材质共用同一张贴图，就会少生成 `.mat`，对应的材质槽解析不到外部材质，编辑器里就是紫色。
- 改动：`ModelImportSettingsProcessor` 在设置 External 的同时显式指定 `materialName = BasedOnMaterialName`。该模式直接用 FBX 里的材质名，与材质严格一对一，不依赖贴图，几个材质就是几个 `.mat`。打包工具的 `ApplyModelImportSettings` 用的也是这一项，两边就此一致。
- 影响：
  - 生成的 `.mat` 文件名从贴图名改成 FBX 材质名（本例是 `Material #25` 这类），观感不如从前，但数量与材质严格对应，不会再有解析不到的槽位。
  - 已经按旧命名导入过的模型会重新生成一套按材质名命名的 `.mat`，旧的那套变成孤儿资产，需要人工确认后删除；若已有 Prefab 手动引用过旧 `.mat`，重新指认后再删。
  - 规则 14（复用原工程已有外部材质）不受影响，复用的仍是模型目录顶层 `Material` 里的资产。
- 验证：Roslyn 全量编译零错误零警告。删除 `fbx.fbm` 与 `Materials` 后重新导入、确认生成 4 个 `.mat` 且模型不再显示紫色，标记为待在编辑器中验证。
- 回退：删掉 `materialName` 那一行即可退回 Unity 默认；回退会重新引入"材质数量多于贴图数量时少生成 .mat、材质槽变紫"。
- 关联问题：拖入 FBX 后模型显示紫色；4 个材质只生成 3 个 `.mat`。

## 2026-07-30 — v1.2.4 导入插件不再改写 .fbm 内嵌媒体缓存

- 原因：把 `fbx.FBX` 拖进 `Assets/New Folder/` 后出现材质丢失。日志显示导入插件的自动压缩改写了 `Assets/New Folder/fbx.fbm/` 下三张贴图（12/16/16 MB 压到 2.40/1.78/2.26 MB，2048 降到 1024），而 `.fbm` 是 Unity 从 FBX 二进制抽取内嵌贴图生成的缓存目录，不是艺术家维护的资产目录。两个后果：
  1. 白做——模型下次重新导入，Unity 会照 FBX 原始数据重新抽取覆盖回去（`Assets/Art` 交付副本拿到 12 MB 原图正是这么来的）。
  2. 有害——`ShrinkAndWriteBack` 里的 `AssetDatabase.ImportAsset` 会连带触发依赖它的 FBX 重新导入，于是"我们正在逐张改写 .fbm 文件"和"Unity 正在重新抽取 .fbm 并解析材质"两件事交叠，材质解析落在不一致的中间状态上。
- 改动：
  1. 新增 `TextureAssetPathUtility.IsInsideEmbeddedMediaFolder`，按路径段识别 `<FBX名>.fbm`。
  2. `TextureSourceFileProcessor.QueueCandidates` 不再把 `.fbm` 里的贴图排进自动队列。
  3. `ShrinkTextureSourceOperation.Execute` 对 `.fbm` 路径返回带原因的 Skipped，而不是静默跳过——手动在窗口里选中它时会明确告诉用户"该去压 Assets/Art/<模型>/Texture/ 里的那一份"。
- 影响：
  - 内嵌在 FBX 里的贴图，导入期不再有任何自动压缩，也压不了——唯一能压的位置是打包工具平铺到 `Assets/Art/<模型>/Texture/` 之后，即规则 35 的两遍流程。
  - 导入插件不再在模型导入过程中改写模型的依赖文件，消除了上述重入风险。
  - 独立存在的贴图（艺术家自己的贴图目录）不受影响，自动压缩照常。
- 验证：Roslyn 全量编译零错误零警告。删除 `fbx.fbm` 与 `Materials` 后重新导入 FBX、确认材质恢复且 `.fbm` 贴图保持原始尺寸，标记为待在编辑器中验证。
- 回退：撤销上述三处改动即可；回退会重新引入"压缩白做 + 材质在导入中途丢失"。
- 关联问题：拖入 FBX 后材质丢失；`.fbm` 贴图被压到 1024 但交付副本仍是 2048 原图。

## 2026-07-30 — v1.2.2 修复 .fbm 抽取贴图搬移失败 + 内嵌贴图超标的处理指引

- 原因：打包 `Plane_WuZhi10w` 时 Console 出现 `Assertion failed on expression: 'm_hasValue'` 加 `Asset to move is not in asset database`，源路径是 `Assets/Art/Plane_WuZhi10w/Model/fbx.fbm/ch_ahe_z-10w_rgb_test.tga`。根因：`ApplyModelImportSettings` 的 `SaveAndReimport()` 让 Unity 把 FBX 内嵌贴图抽取到 `Model/<FBX名>.fbm/`，文件立刻落盘但要等一次 Refresh 才进 AssetDatabase；而 `FlattenModelCompanionFolders` 是用 `Directory.GetFiles` 按磁盘枚举后直接调 `AssetDatabase.MoveAsset`，于是对一个“存在但未导入”的路径发起搬移，触发 Unity 内部断言并让文件留在 `Model/` 里，可能连带把 Model 纯净性校验一起弄失败。
- 改动：
  1. `FlattenModelCompanionFolders` 在按磁盘枚举之前先 `AssetDatabase.Refresh()`。
  2. `MoveAssetToExactPath` 新增 `EnsureAssetIsInDatabase` 前置检查：路径不在 AssetDatabase 但磁盘上有文件时，补一次 `ForceSynchronousImport`；仍注册不上才报错跳过。这样搬移逻辑不再依赖调用方记不记得刷新，也不会再触发 Unity 内部断言。
  3. 源文件超标的告警补上可执行的处理指引（去 `Tools > 贴图处理工具` 压缩后重新打包），并说明重新打包会保留已压缩的那一份。
- 影响：
  - 只是把“搬移前确保资产已注册”这一步补齐，搬移结果与原设计一致，没有放宽任何校验。
  - 新增一次 `AssetDatabase.Refresh()`，单个模型增加的耗时可忽略。
  - 已知边界（本次未改，属既有设计）：`MoveAssetToExactPath` 在目标已存在时保留目标、删除新抽取的源文件。所以 FBX 里的内嵌贴图内容真的更新过时，必须先删掉 `Assets/Art/<模型>/Texture/` 里的旧副本，才能让新内容进来。
- 验证：Unity 2020.3.49f1c1 自带 Roslyn 全量编译零错误零警告。实际重跑打包时 Console 不再出现 `m_hasValue` 断言与 `Asset to move is not in asset database`，标记为待在编辑器中验证。
- 回退：恢复 `RetinarBatchModelBuilder.cs` 与 `.AssetResolution.cs` 本次改动即可；回退会重新引入上述断言与文件滞留在 `Model/` 的风险。
- 关联问题：`Assertion failed on expression: 'm_hasValue'`；`Asset to move is not in asset database`；交付贴图报告中 `ch_ahe_z-10w_rgb_test.tga` 12 MB 超标。

## 2026-07-30 — v1.2.3 修复 asset_info 模板因插件改路径而永远找不到

- 原因：`AssetInfoTemplatePath` 常量写死为 `Assets/Retinar/Templates/asset_info_template.xlsx`，前提是插件整体放在 `Assets/Retinar` 下。本工程把插件收纳到 `Assets/Plugin/RetinarBatchBuilder_Share/Assets/Retinar/`，模板文件确实存在，但常量路径永远命中不了，于是每次打包都打一条 `Asset info template not found` 警告并静默回退成自己生成的版式。交付的 `06_docs/asset_info.xlsx` 因此不是交付方模板的版式——属于“弹窗显示完成、实际交错东西”的隐患，正是规则 9 要防的情况。
- 改动：新增 `ResolveAssetInfoTemplatePath`：先按常量路径找，命中不了就用 `AssetDatabase.FindAssets` 按文件名在全工程搜一遍 `.xlsx`。常量退化为“默认位置”而不是唯一位置。回退时的警告文案也补上“回退版式不是交付方模板版式，交付前请确认”。
- 影响：
  - 之前所有 `06_docs/asset_info.xlsx` 都是回退版式，需要重新打包一次才会换成模板版式；已经交付出去的包若对版式有要求，需要重新出包。
  - 插件以后被挪到任何目录都能找到模板，不用改代码。
- 验证：静态确认模板实际位于 `Assets/Plugin/RetinarBatchBuilder_Share/Assets/Retinar/Templates/asset_info_template.xlsx`，与常量路径不一致；Roslyn 全量编译零错误零警告。重新打包后不再出现该警告、且 `asset_info.xlsx` 为模板版式，标记为待在编辑器中验证。
- 回退：恢复 `RetinarBatchModelBuilder.AssetInfoWorkbook.cs` 本次改动即可；回退会重新导致模板永远找不到。
- 关联问题：`Asset info template not found. Falling back to generated workbook`。

## 2026-07-30 — v1.2.1 贴图压缩必须保持二的幂

- 原因：v1.2 给导入插件加的压缩操作用连续二分找“刚好达标的最大尺寸”，算出来的尺寸几乎一定不是二的幂（4096 可能停在 2913）。而本工具的 `GetTextureIssueCount` / `BuildTextureReportLine` 会把每张非二的幂贴图记为一条问题项并在 Console 打警告。两个插件的目标直接对撞：压缩越成功，交付报告里的告警越多，且用户无法判断这些告警该不该管。
- 改动：
  1. 导入插件新增 `preservePowerOfTwo` 配置（默认开启）。源图长宽都是二的幂时，改在对折阶梯（2048→1024→512…）上从大到小取第一个达标档位——对折同时作用于长宽，长宽比精确不变，结果仍是二的幂。源图本来就不是二的幂时没有可保的东西，仍走连续二分。
  2. `FindLargestSizeUnderLimit` 重写为“总体流程”形态：先试原尺寸（无损）→ 按源图是否为二的幂选搜索策略 → 都没找到解才走最小边长兜底。两种搜索各自独立成函数。
  3. 顺带修掉一个会吞错误的分支：编解码器明确报错时不再落进“最小边长兜底”，而是原样上报（新增 `EncodeAttempt.Failed` 与 `Succeeded` 区分“报错”和“没找到达标尺寸”）。
  4. `maxSourceMegabytes` 的 Tooltip 明确写出“不得大于 5”，并说明超出后会在交付报告才暴露。
- 影响：
  - 被压缩的贴图最多比“理论最优尺寸”少一档分辨率（例如 2913 → 2048），换来交付端零告警。这个取舍写进 `PACKAGING_RULES.md` 规则 34。
  - 编码次数从固定 12 次降到阶梯档位数（8K 到 64 只有 8 档），批量处理反而更快。
  - 关闭 `preservePowerOfTwo` 会恢复旧行为，交付报告会重新出现非二的幂告警，属于明确的取舍而不是缺陷。
- 验证：Unity 2020.3.49f1c1 自带 Roslyn 全量编译 `Assets/Plugin`，零错误零警告。实际压缩结果的二的幂性、以及压缩后重跑打包时 `texture_size_report.txt` 不新增问题项，标记为待在编辑器中验证（见 `REGRESSION_CHECKLIST.md` 第三节新增三项）。
- 回退：把 `preservePowerOfTwo` 设为 false 即可恢复连续二分，无需改代码。
- 关联问题：压缩后的贴图在交付贴图报告中被记为非二的幂问题项。

## 2026-07-30 — v1.2 阻断粒度改为单资产 + 与导入插件划清目录职责

- 原因：三个独立问题。
  1. 打包会“出现终止”，用户只能去 `Assets/Art` 里重新选中已生成的预制体再打一次。根因是三道校验（Model 纯净性、SafeZone 空间、外部依赖）对整批资产一起判定，任意一个资产不通过就 `return`，已经生成好且完全合规的其它资产一并不出包。
  2. 上述手动补救每执行一次就多一份重复资产。`CreatePackagedAdjustedPrefab` 无条件用被选中文件的文件名当资产名，于是选中 `Assets/Art/Chair/Prefab/Chair_prefab.prefab` 会新开一个 `Assets/Art/Chair_prefab/` 目录，AssetBundle 名和交付目录也跟着变，交付时容易交错版本。
  3. 与导入插件（`TOol`）在 FBX `materialLocation` 上互相覆盖。本工具设 `InPrefab` 后调 `SaveAndReimport()`，这次 reimport 会触发导入插件的 `OnPreprocessModel` 把它改回 `External`，于是 Unity 在 `Assets/Art/<模型>/Model/` 下生成 `Materials/` 与 `<FBX名>.fbm`，正好撞上 Model 纯净性校验，导致终止。最终生效哪一边取决于时序，无法稳定复现。
- 改动：
  1. 新增 `PartitionAssetsThatPassValidation`，三道校验改为逐个资产判定。未通过的资产被清掉 `assetBundleName`（保证它不会被打进 AB）并单独记入报告，通过的资产照常出包。完成弹窗同时显示 `已出包 N / 共 M` 与被排除清单预览。
  2. 三份分散的失败报告（`model_folder_not_clean.txt` / `prefab_spatial_placement_failed.txt` / `unsupported_external_dependencies.txt`）合并为一份按资产分段的 `Deliverables/_diagnostics/validation_failures.txt`。
  3. 新增 `ResolvePackagedAssetIdentity` 与 `PreparePackagePrefab`：被选中的预制体如果已经位于 `Assets/Art/<名字>/` 下（即本工具上一轮的产物），复用该 `<名字>` 与该目录；如果它就在目标 `Prefab/` 目录里，则原地处理不再复制副本。重跑任意多次结果一致。
  4. `materialLocation = InPrefab` 保持不变（防回归基线）。冲突改由导入插件侧解决：`TOol` 新增 `excludedPathPrefixes` 配置，默认排除 `Assets/Art/`，其三个导入回调都不再介入本工具的产物区。
  5. 删除死代码 `TrySetModelImporterMaterialLocation`（无任何调用方）和 `RetinarAssetInfoExporter.cs`（一次性调试脚本，内含他人机器的绝对路径 `C:/Users/小陶子/...`）。
  6. `asset_info.xlsx` 的约 500 行手写 OOXML/zip 代码拆到 partial 分文件 `RetinarBatchModelBuilder.AssetInfoWorkbook.cs`，主文件从 2801 行降到约 2300 行。纯文件位置调整，逻辑逐行未改。
- 影响：
  - 阻断粒度从“整批”变为“单资产”，这是对规则 18/19/23/27 的粒度细化，不是放松——不合规的资产依然不会产出 AB 或 UnityPackage。相应的规则表述已在 `PACKAGING_RULES.md` 更新为规则 31。
  - 规则 12（以选中 Prefab 文件名为命名基准）现在明确只适用于 `Assets/Art` 之外的预制体；对本工具产物重跑时以既有资产目录名为准，见新增规则 32。
  - 一次打包只产出一份诊断报告，旧的三个文件名不再生成，已有的排查文档若引用旧文件名需同步。
  - 导入插件不再对 `Assets/Art` 下的 FBX/贴图做任何导入期改动；如果以后产物目录改名，必须同步改 `TextureProcessSettings.excludedPathPrefixes`，否则会复现终止。
- 验证：静态检查——`materialLocation` 在主文件中仅出现在 `ApplyModelImportSettings` 一处且值为 `InPrefab`；主文件已无 `ZipArchive`/`XElement` 引用；三个旧报告写入函数已无定义与调用。Unity 编译、单资产失败隔离的实际打包、重跑不再产生 `Assets/Art/<名字>_prefab`、以及 UnityPackage 导入后 `Model` 仍只有 FBX —— 均标记为待在编辑器中验证，需按 `REGRESSION_CHECKLIST.md` 执行。
- 回退：恢复本次提交前的三个脚本（`RetinarBatchModelBuilder.cs`、`.AssetResolution.cs`，并删除 `.AssetInfoWorkbook.cs`）即可。回退会重新引入“一个资产失败导致整批终止”“重跑产生重复目录”，且必须同时回退 `TOol` 的 `excludedPathPrefixes`，否则两个插件会再次在 `materialLocation` 上互相覆盖。
- 关联问题：打包中途终止、需要手动重新选中生成预制体补救、补救后出现重复的 `Assets/Art/<名字>_prefab` 目录。

## 2026-07-16 — v1.1 非破坏式打包基线

- 原因：历史版本曾清空生成目录、移动资源、直接修改原始 FBX Importer，与“原工程可继续调整并重复打包”的要求冲突。
- 改动：入口不再修改用户选中的原始 FBX Importer；取消打包前清空 `Model/Texture/Material/Prefab/Animation`；刷新模型工作副本改为覆盖文件内容并保留目标 `.meta`；完成提示显示绝对路径；补充“打开交付目录”菜单。
- 影响：用户原始资产与已有生成 Prefab 更安全；旧的无用生成文件不会被自动清理，需要人工确认后处理。
- 验证：2026-07-16 静态检查通过——入口不存在对 `sourcePath` 的 `ApplyModelImportSettings` 调用，`CleanGeneratedAssetFolder` 已移除，脚本花括号数量一致。Unity 2020.3.49f1c1 批处理未生成日志，未能证明已进入工程，因此 Unity 编译、重复打包、空工程导入和 AB 加载仍标记为待验证。
- 回退：恢复本次提交前脚本，并明确告知会重新引入清空目录、修改原始 Importer 和 GUID 风险；正式版本禁止无说明回退。
- 关联问题：原 FBX/Prefab 被移动或消失；重复打包后无法继续调整；接收者找不到输出目录。
- 会话整合：已合并任务 `019f3b5e-a573-7601-9638-d5085a8d5780` 与 `019f49c8-f25d-7e21-9489-cfe13406e067` 的有效要求。补充“Prefab 命名为准、不得修改材质参数、四个必需分类目录、禁止夹带 `Assets/Model`、AssetDatabase 断言防回归”。
- 入口统一：旧 `Tools > Model Package > Organize Selected Prefab` 不再执行独立整理代码，统一转交 `RetinarBatchModelBuilder.NormalizeSelectedModelsOnly()`。
- 对外文档：将分享说明扩充为完整使用手册，加入安装、Prefab/FBX 选择、菜单、输出目录、动画规范、强制注意事项、验收清单、常见问题和反馈材料要求；重新生成正式分享包。
- 资源归类：交付副本的 `Model` 只保留模型文件；外部材质、贴图复用整理后的 `Material / Texture` 副本，不再无条件重复生成 `Mat_*`；FBX 内嵌材质仍保留必要的外部化兜底；新增 `Text` 分类，归档 `.txt / .bytes / .json / .xml / .csv`。
- 动画/交互 Runtime 隔离：模型 UnityPackage 改为只导出 `Assets/Art/<模型名>` 依赖，不再通过 `IncludeDependencies` 重复夹带 XLua、DOTween、RichWidget 和插件；自动输出 `00_runtime_requirements/runtime_requirements.txt`；发现未知外部 Assets 依赖时停止打包并列出路径。AB 仍保留 Prefab、动画和 TextAsset 数据，公共 C# Runtime 由验收 App 预编译提供。
- 验证状态：2026-07-17 静态检查通过，花括号数量一致且主脚本中 `ExportPackageOptions.IncludeDependencies` 为 0；Unity batchmode 未生成日志，尚未证明完成 Unity 编译。需要在编辑器 Console、模型 UnityPackage 回归和 Runtime AB 触发场景中继续验证。
- 依赖拦截提示修复：外部依赖过多时 Unity 2020 原生弹窗可能显示空白。现改为弹窗预览前 8 条，并把完整列表写入 `Deliverables/_diagnostics/unsupported_external_dependencies.txt`。Play Mode 下菜单不再直接禁用，点击后由入口保护主动退出播放并显示说明。
- 动画/交互引用修复：复制资源后除了 Mesh、Controller、Avatar，现进一步重定向 Prefab 全部组件的序列化引用，并重定向复制后的 Controller/材质等资产内部引用。用于修复 Lua `TextAsset`、Animator Controller 内嵌 Clip 仍指向原 `Assets/Model`，进而递归带出原 FBX、Materials、Textures 的问题。
- 重复副本抑制：只在 `Assets/Art` 生成区内部整理 FBX 伴生资源时，如果目标分类文件已存在，则复用目标并删除本次新产生的生成区重复项，不再继续生成 `文件名 1/2/3`；原工程资源目录不受此清理影响。
- 编译回归修复：移除新增的重复 `RemapSerializedObjectReferences` 定义，统一复用脚本原有的异常保护版本，修复 `CS0111`。
- Model 纯净性门禁：正式构建前检查每个 `Assets/Art/<模型>/Model`，发现子文件夹或非模型文件立即停止，并写入 `Deliverables/_diagnostics/model_folder_not_clean.txt`。用于硬性保证 UnityPackage 的 Model 只包含 FBX/OBJ 等模型文件。
- FBX 导入后伴生目录修复：Prefab 打包流程现在会对复制到 `Assets/Art/<模型>/Model` 的 FBX/OBJ 工作副本强制应用 `ModelImporterMaterialLocation.InPrefab`。修复 UnityPackage 导入界面只显示 FBX，但导入完成后 Unity 又在 `Model` 下自动生成 `Materials` 和 `<FBX名>.fbm` 贴图目录的问题。原始 FBX 及其 Importer 不会被修改。
- 2026-07-20 回归验收通过：用户已在验收工程重新导入并确认，`Model` 内只保留 FBX，不再自动生成 `Materials` 或 `<FBX名>.fbm` 目录，且顶层 `Material / Texture` 结构正确。此项标记为硬性防回归基线：后续任何 FBX Importer、Prefab 依赖整理或 UnityPackage 导出修改，均不得恢复 External 提取行为。
- 2026-07-22 FBX `externalObjects` 自动重映射：修复直20 Prefab 复制后，交付 FBX Importer 的 Remapped Materials 仍引用原目录 `fbx.fbm/Materials` 的问题。工具现在使用已复制依赖表自动重建 `ModelImporter.externalObjectMap`，并只允许映射到当前 `Assets/Art/<模型>/Material`。避免因同名材质手动选错或重新打包后映射被覆盖。
- 2026-07-22 L15 空间归一与真机防偏移：源 Prefab 使用“根节点大幅偏移 + 子模型反向补偿”结构，Unity 中外观正常，但 AR 端重设根节点位置后模型会远离线框并看似空包。交付 Prefab 现在会在保持子节点世界外观的前提下将根节点 Position/Rotation/Scale 归一，再执行 SafeZone 缩放、居中和 Collider 重算。新增构建前空间门禁，检查根节点、Renderer Bounds 中心/尺寸和 BoxCollider 中心，失败时写入 `Deliverables/_diagnostics/prefab_spatial_placement_failed.txt` 并禁止输出 AB。
- 2026-07-22 L15 贴图“已复制但仍报外部依赖”修复：确认 15 张 JPG/PNG 已存在于顶层 `Texture`，但交付材质仍保留源 `fbx.fbm/1` 贴图 GUID。根因是已有的 `RemapCopiedMaterials` 专用流程未被主流程调用。现已接回主流程，并在 GUID 对象映射失败时，按当前交付目录 `Texture/<原文件名>` 进行受限兜底匹配。依赖检查仍保留，只有材质实际引用交付副本后才允许输出。
- 2026-07-22 L15 重复打包补充修复：首次接回材质重映射后仍有源贴图依赖。进一步确认，重复打包时既有顶层材质不一定进入“本次新复制依赖列表”，导致只遍历本轮副本时漏处理。现改为每次强制扫描当前 `Assets/Art/<模型>/Material` 的全部材质并重映射贴图，确保首次与重复打包结果一致。
- 2026-07-22 通用材质依赖收敛修正：取消“本次复制依赖为空就提前返回”对材质扫描的影响。现在无论机型、目录名、首次或重复打包，都会无条件遍历当前交付模型的全部 Material Texture Property。若当前顶层 `Texture` 已有同名资源则复用；若没有则从真实源贴图路径复制后重映射。该逻辑不包含 L15 或任何具体机型名判断。
- 2026-07-22 AR 空间错位防回归加固：空间门禁补充检查根节点 Scale 必须为 `(1,1,1)`。正式输出前现必须同时满足根 Position 归零、Rotation Identity、Scale One、Renderer Bounds 居中且尺寸有效、根 BoxCollider 与可见模型中心一致。任一项失败均在 AB/UnityPackage 输出前停止。本项为通用 Prefab 发布基线，不允许按机型跳过。
- 2026-07-24 贴图报告口径与 5MB 阈值：源贴图文件体积告警阈值由 2MB 调整为 5MB。明确 TextureImporter `Max Size/Compression` 只影响 Unity 导入后 Texture2D 与 AB 内数据，不会重写原始 PNG/JPG；`01_source/Textures` 继续保真复制原文件。`texture_size_report.txt` 新增口径说明与列标题，分开表达 Unity Imported Size 和 Source File Size。
- 2026-07-24 Photoshop 实体改图后的贴图归档：区分 TextureImporter 虚拟缩放与 Photoshop 重写原 PNG/JPG。重复打包时，若原贴图文件修改时间更新且内容不同，则刷新 `Assets/Art/<模型>/Texture` 工作副本的图像内容，保留目标 `.meta`/GUID 与材质引用；若工作副本更新则不反向覆盖。`01_source/Textures` 改为只从最终规范 Prefab 的真实贴图依赖归档，避免源 Prefab 与交付 Prefab 的同名贴图无序覆盖。
- 2026-07-24 新增 `REGRESSION_CHECKLIST.md`：将原资源保护、Model 纯净性、FBX 伴生目录、材质/贴图依赖收敛、Photoshop 改图同步、5MB 阈值、空间居中、动画/交互、Runtime、输出时间、UnityPackage/AB/真机验收和 GLB 边界统一为发布前必查基线。后续新问题必须同时更新变更记录、硬性规范和回归检查表。

## 历史决策摘要

- 2026-07-11：打包改为非破坏式思路；分享包精简为工具脚本、模板与说明，不携带 Examples。
- 2026-07-10：加入 Play Mode 保护；加入基础动画 FBX 与 AnimatorController 支持。
- 2026-07-09：确认部分 FBX 必须使用嵌入材质；撤销“默认强制提取外部材质”；UnityPackage 收敛为 Prefab + 依赖。
- 2026-07-08：建立批量入口、SafeZone 归一、Prefab/AB/UnityPackage、源文件归档、贴图检查和表格输出。
