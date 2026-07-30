# Retinar Unity 打包工具变更记录

记录规则：最新版本写在最上方；每次修改必须填写“原因、改动、影响、验证、回退”。

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
