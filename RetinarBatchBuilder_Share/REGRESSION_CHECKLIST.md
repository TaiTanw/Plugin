# Retinar Batch Builder 回归检查表

版本：1.1  
生效日期：2026-07-24  
最近同步：2026-08-05（v1.3.1 导出 Art 全部 / 选中；v1.3.0 平铺/导出拆分）

本文是每次修改工具、更换 Unity 版本、发布分享包或正式批量打包前必须执行的回归基线。不得因为某个模型打包成功就跳过其他类型。

## 一、发布前必测样本

- [ ] 纯静态模型：验证 Mesh、材质、贴图、Collider、SafeZone。
- [ ] 带动画模型：验证 Animator、Controller、Clip、循环/一次播放。
- [ ] 带交互模型：验证 Runtime、XLua、DOTween、RichWidget、触发事件。
- [ ] 存在 `fbx.fbm/Materials` 的模型：验证外部材质和贴图收敛。
- [ ] 根节点存在偏移补偿的模型：验证 AR 端居中和尺寸。
- [ ] Photoshop 修改过实体贴图的模型：验证工作副本、AB 和 `01_source` 版本一致。
- [ ] 同一模型连续打包两次：验证重复打包与首次结果一致。
- [ ] 含内嵌大贴图的模型（如曾超标的 `Plane_Jian31` / `Plane_WuZhi10w`）：验证「平铺 → 压 Art 贴图 → 导出」两遍流程。
- [ ] 导入区仍残留同名 `.fbm` 的模型：验证第二遍打包后 Prefab 依赖不再指向导入区 `.fbm`。

## 二、自动阻断项

- [ ] Unity 正在编译或存在红色编译错误时不得打包。
- [ ] Play Mode 中不得生成正式交付物。
- [ ] `Model` 只允许 FBX/OBJ 及 `.meta`，禁止子文件夹、材质、贴图和文本。
- [ ] Prefab 必须有 Renderer，禁止只有 Collider 的空包。
- [ ] Prefab 的所有私有资源依赖必须收敛到 `Assets/Art/<模型>`。
- [ ] FBX `externalObjects` 不得继续引用原 `fbx.fbm` 或 `Materials`。
- [ ] 顶层 `Material` 的全部 Texture Property 必须引用当前顶层 `Texture`。
- [ ] 首次和重复打包都必须扫描全部既有材质，不得依赖“本轮新复制列表”。
- [ ] 根 Transform 必须为 Position Zero / Rotation Identity / Scale One。
- [ ] Renderer Bounds 必须居中 SafeZone，尺寸不得过小或越界。
- [ ] 根 BoxCollider 必须存在且中心与 Renderer Bounds 对齐。
- [ ] 未知外部依赖必须停止，不得强行生成缺依赖包。
- [ ] 阻断必须是单资产粒度：一批中混入一个不合规资产时，其余资产仍要正常出包，不合规的那个不得产出 AB/UnityPackage。
- [ ] 被排除的资产必须出现在 `Deliverables/_diagnostics/validation_failures.txt`，且完成弹窗显示实际出包数量。
- [ ] 连续对同一个生成预制体重跑两次，不得出现 `Assets/Art/<名字>_prefab/` 这类多余目录，AssetBundle 名与交付目录名必须保持一致。
- [ ] 导入插件（`TOol`）的总开关处于开启状态时**平铺/导出**，`Assets/Art/<模型>/Model` 下仍不得出现 `Materials/` 或 `<FBX名>.fbm`。
- [ ] 交付区 Model 的 `materialSearch` 为 Local；`GetDependencies(Prefab)` 不得再出现 `Assets/` 下 Art 以外的 `*.fbm/*` 贴图路径。

## 三、贴图回归

- [ ] Unity TextureImporter `Max Size/Compression` 与原 PNG/JPG 文件体积分开报告。
- [ ] 原始贴图文件体积超过 5MB 才触发当前体积告警（严格按磁盘字节，`>` 5×1024×1024）。
- [ ] 报告 WARN 时先打开 `01_source/texture_size_report.txt` 核对具体路径；不得仅凭“Art 文件夹里看起来都不大”判定误报（常见漏网：单张 5.x MB）。
- [ ] Photoshop 修改原贴图后，源文件更新时必须刷新交付工作副本的图像内容。
- [ ] 刷新工作副本时必须保留目标 `.meta` 和 GUID。
- [ ] 交付工作副本更新时，不得被较旧源图反向覆盖。
- [ ] **已压缩的更小 Art 贴图，不得被更新的更大导入区源图通过 SyncNewer 覆盖。**
- [ ] `01_source/Textures` 只归档最终 Prefab 实际引用的贴图版本。
- [ ] `texture_size_report.txt` 必须显示 Unity Imported Size 和 Source File Size；问题行路径应落在 `Assets/Art/<模型>/Texture/`。
- [ ] 被导入插件压缩过的二的幂贴图，压缩后仍必须是二的幂；`texture_size_report.txt` 中不得因压缩而新增非二的幂问题项。
- [ ] 导入插件的 `maxSourceMegabytes` 必须 ≤ 5，与本工具的告警线一致。
- [ ] **两遍流程（内嵌贴图）**：
  1. 第一遍打包后 Art/Texture 可暂时超标；
  2. 只压 `Assets/Art/<模型>/Texture/` 下超标文件（确认结果区路径，且不得选 `.fbm`）；
  3. 不删 Art，第二遍打包后磁盘体积与报告均保持 &lt; 5MB OK。
- [ ] 第二遍打包 Console 允许出现「恢复 N 张更小的 Art 贴图」或「SyncNewer 跳过（保留更小的 Art 贴图）」；**不得**在无恢复日志的情况下体积又回到超标。
- [ ] 开着导入插件拖入含内嵌贴图的 FBX：`<FBX名>.fbm` 里的贴图必须保持原始尺寸不被改写，且模型材质不得丢失；手动压 `.fbm` 应被 Skip 并提示改压 Art。
- [ ] 导入插件生成的外部 `.mat` 数量必须等于 FBX 里的材质数量；模型在 Scene/Inspector 中不得出现紫色材质槽。
- [ ] 搬移 `.fbm` 抽取出来的贴图时，Console 不得出现 `Assertion failed on expression: 'm_hasValue'` 或 `Asset to move is not in asset database`；`Model/` 里不得残留 `.fbm` 目录。
- [ ] Extract/remap 自愈开启时，压缩后再打包仍不得把 Art 贴图盖回大图（与外部 `.fbm` 切断可同时成立）。
- [ ] **顶点色**：对 `Art/Model` FBX 手动「顶点色设为全白」后，不删 Art、选 Prefab 再**导出**；Model 子 Mesh 顶点色须仍为白。Console 可出现「SaveAndReimport 后已恢复 Mesh 顶点色」，或因无外部 `.fbm` 而跳过 Extract。
- [ ] **菜单拆分**：`Tools/Retinar` 见「平铺到 Art」「从 Art 导出交付物」子菜单（全部 / 选中）、「打开交付文件夹」；无 Batch Build。选导入区 Prefab 点「导出选中」应警告跳过；「导出 Art 全部」不依赖选中。

## 四、动画与交互回归

- [ ] 打包后 Controller 的 Motion 不得无故改指其他 Clip。
- [ ] 默认状态、循环、一次性动画和切换条件与原 Prefab 一致。
- [ ] Lua/TextAsset 必须收敛到 `Text`，序列化引用不得断开。
- [ ] Lua `OnClick` 等函数必须有平台事件或 C# 显式调用，不得只因函数存在就宣称可触发。
- [ ] 带 XLua/DOTween/RichWidget 的包必须生成 `runtime_requirements.txt`。
- [ ] AB 显示模型不代表交互已验收，必须在匹配 Runtime 中实际触发。

## 五、输出与回归验收

- [ ] 完成弹窗不得作为成功依据；必须检查实际文件存在、大小非零、修改时间已更新。
- [ ] UnityPackage 必须在干净工程删除旧同名 `Assets/Art/<模型>` 后重新导入验证。
- [ ] UnityPackage 导入后 `Model` 仍只有 FBX/OBJ，不得自动生成 `Materials` 或 `<FBX>.fbm`。
- [ ] Android 和 iOS AB 都必须更新并能加载最终 Prefab。
- [ ] 手机/AR 端必须确认模型位于线框中心，尺寸可见，不得只看 Unity Scene 窗口。
- [ ] 材质、透明、法线、双面、动画、Collider 和交互必须人工验收。
- [ ] 发布分享包前必须更新 `CHANGELOG.md`、`PACKAGING_RULES.md` 和本检查表。
- [ ] `RetinarBatchBuilder_Share.zip` 必须在最后一次代码/文档修改后重新生成。

## 六、GLB 边界

- [ ] GLB 不得直接并入当前核心打包工具。
- [ ] GLB 必须在独立转换工程中，从已验收 UnityPackage 的最终 Prefab 导出。
- [ ] GLB 只是派生交换文件，不得替代原 FBX、UnityPackage 或 AB。

## 七、问题回溯要求

每次发现新问题必须记录：

- 发现日期与 Unity 版本。
- 模型类型，不得将通用问题记为某个机型特例。
- 用户操作步骤、实际结果和期望结果。
- 根因、修复位置、影响范围和回退方式。
- 自动阻断条件与人工回归步骤。
- 首次打包、重复打包、UnityPackage 导入、AB 加载和手机端结果。

没有回归记录和检查结果的修改，不得发布为正式分享版。

## 八、近期贴图相关问题速查（操作 ↔ 期望）

| 步骤 | 错误做法 | 正确做法 / 期望 |
|------|----------|-----------------|
| 压缩对象 | 选中 `Assets/**/xxx.fbm/*.tga` | 选中 `Assets/Art/<模型>/Texture/` 下同名文件 |
| 看是否超标 | 只看分辨率或 Inspector | 看磁盘 MB 与 `texture_size_report.txt` 的 Source File Size |
| 第二遍打包前 | 删除整个 `Assets/Art/<模型>/` | 保留 Art，直接再打（否则等于从头抽大图） |
| 第二遍后体积又大 | 以为工具没压上 | v1.2.8 前是 Extract/SyncNewer 覆盖；现应有恢复/跳过日志且体积保持小 |
| 弹窗 Texture issue | 以为扫错路径 | 打开报告看具体文件名；常见是单张略超 5MB |
