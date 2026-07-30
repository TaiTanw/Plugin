# Retinar Unity 打包工具硬性规范

版本：1.0  
生效日期：2026-07-16

本文是后续修改打包工具时必须遵守的基线。发生冲突时，以本文和最新经确认的变更记录为准。

## 一、不可违反的规则

1. 打包是“生成与导出”，不是“清理工程”。不得删除、移动或改名用户原始 FBX、Prefab、材质、贴图、动画及其 `.meta`。
2. 不得直接修改用户选中的原始 FBX Importer。需要统一法线、切线、动画或材质设置时，只处理 `Assets/Art/<模型名>/Model` 中的工作副本。
3. 重复打包必须保留工程中的原始资源和可继续调整的 Prefab。刷新工作副本时不得删除其 `.meta`，避免 GUID 和引用变化。
4. FBX 材质默认采用已验证的 `Import via MaterialDescription` 与“使用嵌入材质”方向；不得再次默认强制提取外部材质。
5. UnityPackage 只从最终 Prefab 导出并包含实际依赖，不得递归打包整个工程目录或无关示例资源。
6. Play Mode 下禁止开始打包。工具应先停止并要求用户回到 Edit Mode 后重新执行，不能产生半成品。
7. 完成提示必须显示 `Deliverables` 的绝对路径，并提供 `Tools > Retinar > Open Deliverables Folder`。
8. 动画 FBX 可生成基础 AnimatorController，但不得宣称复杂状态机、Root Motion、Avatar、循环和触发逻辑已经自动验收。
9. 自动检查不能替代人工验收。材质、法线、编号方向、SafeZone、Collider、动画、真机效果、版权与考据必须保留人工检查项。
10. 每次代码或流程修改必须先更新 `CHANGELOG.md`，写明原因、改动、影响、验证、回退方式和关联问题；没有记录的修改不得作为正式分享版本发布。
11. 正式交付优先选择已经在 Unity 中调整并验收过的 Prefab；工具不得修改其材质参数。FBX 入口仅用于基础规范化，不代表最终视觉效果已验收。
12. 选中 Prefab 时，交付目录、Prefab、UnityPackage、AB 和 `asset_id` 必须以该 Prefab 文件名为命名基准。
13. `Assets/Art/<选中Prefab名>` 必须至少存在 `Model / Texture / Material / Prefab`；有相关内容时必须分别归档到 `Animation / UI`，不得把源工程散落目录夹带进 UnityPackage。
14. 原工程模型目录中已有的外部材质和贴图必须复制到交付副本的 `Material / Texture` 并复用，不得无条件再次生成重复材质；只有 FBX 内嵌材质没有独立 `.mat` 时才允许生成必要材质资产。
15. 交付副本的 `Model` 只放 FBX、OBJ 等模型文件；`.txt / .bytes / .json / .xml / .csv` 文本依赖统一放入 `Text`，不得残留在 `Model`。
16. 带动画或交互的模型 UnityPackage 只导出 `Assets/Art/<模型名>` 私有资源；XLua、DOTween、RichWidget、原生插件等公共代码必须由版本匹配的 Retinar Runtime 提前安装，不得在每个模型包中重复携带。
17. AssetBundle 可以包含 Prefab、Animator、动画、材质、贴图和 Lua/TextAsset，但公共 C# Runtime 必须预先编译进验收 App；不得把 AB 能显示模型误判为交互功能已经验收。
18. Prefab 引用未知的外部 `Assets` 资源时必须停止正式打包并报告路径；不得生成缺依赖的 UnityPackage 或 AB。
19. 正式导出前必须验证 `Model` 纯净性：不得包含任何子文件夹、材质、贴图或文本，只允许 FBX/OBJ 等模型文件及其 `.meta`。验证失败必须停止打包。
20. `Assets/Art/<模型>/Model` 内的 FBX/OBJ 工作副本必须使用 `ModelImporterMaterialLocation.InPrefab`，禁止使用会在目标工程首次导入时自动生成 `Materials` 和 `<FBX名>.fbm` 目录的 External 模式。该设置只能修改交付工作副本，不得修改原始 FBX Importer。
21. 上述 FBX 伴生目录问题已于 2026-07-20 由用户完成导入回归验证。后续发布前必须继续检查：UnityPackage 导入完成后，`Model` 仍只有 FBX/OBJ，材质和贴图只存在于顶层 `Material / Texture`，且 Prefab 外观与引用不丢失。
22. 复制到交付目录的 FBX 若存在 `ModelImporter.externalObjectMap`，其材质映射必须自动改指当前模型顶层 `Material`副本。任何仍指向原始 `fbx.fbm` 或 `Materials` 目录的映射都必须阻止打包，不得依赖人工逐项重选。
23. AR 交付 Prefab 的根节点必须归一：Position 为零、Rotation 为 Identity、Scale 为一。源 Prefab 若使用根偏移与子节点反向补偿，必须在交付副本中无损重建；Renderer Bounds 必须位于 SafeZone 中心，BoxCollider 必须与可见模型中心一致。任何根节点大幅偏移、模型远离线框或尺寸近似为零的 Prefab 必须阻止 AB 输出。
24. 贴图文件已出现在顶层 `Texture` 不代表整理完成；所有交付材质的 Texture Property 必须实际引用当前 `Assets/Art/<模型>/Texture` 内资源。工具必须先执行材质贴图专用重映射，再执行外部依赖验证；仍引用源 `fbx.fbm` 的材质不得输出。
25. 材质贴图重映射必须每次遍历当前模型顶层 `Material` 内的全部材质，不得只遍历本次新复制的资源。首次打包和重复打包必须得到相同的依赖收敛结果。
26. 材质依赖收敛必须是机型无关的通用功能，禁止使用 L15、直20、米15 或任何具体模型名称作为分支条件。即使本次没有新复制依赖，也必须执行当前模型全材质贴图路径验证与收敛。
27. 真机中模型远离线框、尺寸过小或看似空包属于阻断级回归问题。所有机型在输出前必须通过通用空间门禁：根 Transform 为 `(Position 0 / Rotation Identity / Scale 1)`，Renderer Bounds 中心与 SafeZone 中心距离不超过容差，尺寸不得近似为零或超出 SafeZone，BoxCollider 中心必须与 Renderer Bounds 对齐。不得以 Unity 场景中“看起来正常”取代该验证。
28. 贴图验收必须区分 Unity 导入尺寸与原始文件体积。TextureImporter `Max Size/Compression` 不得被宣称为已修改原始 PNG/JPG；源文件归档默认必须保真。原始贴图文件体积的通用告警阈值为 5MB，报告必须同时显示 Unity Imported Size 与 Source File Size。
29. 用户通过 Photoshop 等外部工具直接修改并保存 Unity 定位到的原 PNG/JPG 时，该文件视为新的贴图实体内容。打包工作副本必须在源文件更新时同步图像内容，但必须保留目标 `.meta`/GUID；不得反向覆盖更新的工作副本。`01_source/Textures` 必须以最终 Prefab 实际引用的贴图为唯一归档来源。
30. 每次代码修改、流程变更、Unity 版本变更或正式分享前，必须执行 `REGRESSION_CHECKLIST.md`。已自动化项必须保留阻断，无法自动化项必须保留人工验收记录。发现新问题时必须同步更新 `CHANGELOG.md`、`PACKAGING_RULES.md` 和 `REGRESSION_CHECKLIST.md`，不得只修代码不留回溯基线。
31. 校验失败的阻断粒度是**单个资产**，不是整批。Model 纯净性、SafeZone 空间和外部依赖三道校验必须逐个资产判定：未通过的资产必须清掉 `assetBundleName`、不得产出它的 AB 与 UnityPackage、并写入 `Deliverables/_diagnostics/validation_failures.txt`；同一批中通过校验的资产必须正常出包。禁止因为一个资产不合规就让整批终止——那会迫使用户去生成目录里重新选中预制体补救，而补救本身又会引入新的重复资产。完成弹窗必须同时显示实际出包数量与被排除清单。
32. 命名基准（规则 12）只适用于 `Assets/Art` 之外的预制体。如果被选中的预制体已经位于 `Assets/Art/<名字>/` 下（即本工具上一轮的产物），必须复用该 `<名字>` 与该资产目录；若它就在目标 `Prefab/` 目录内，必须原地处理而不是再复制一份副本。禁止出现 `Assets/Art/<名字>_prefab/`、`<名字>_prefab_prefab/` 这类逐次叠加的目录。
33. 本工具与资源导入插件（`TOol`）必须按目录划清职责，禁止两边同时设置同一个 Importer 属性。`Assets/Art/**` 是本工具的产物区，导入插件必须在其 `excludedPathPrefixes` 中排除它；其它目录是艺术家导入区，`materialLocation = External` 由导入插件负责。若本工具的产物根目录改名，必须同步修改导入插件的排除配置，否则会复现“打包中途终止”。
34. 贴图源文件压缩由导入插件在艺术家导入区完成，且必须发生在打包之前；本工具只做“归档 + 报告”，不压缩、不改写任何贴图像素。由此产生三条必须同时成立的跨插件约束：
    - 压缩后的尺寸必须仍是二的幂（导入插件的 `preservePowerOfTwo`，对二的幂源图走对折阶梯）。规则 28 的贴图报告会把非二的幂记为问题项，“压缩成功”不得换来“交付告警”。
    - 导入插件的 `maxSourceMegabytes` 不得大于本工具的 5MB 告警线，否则超标贴图会一路走到交付报告才被发现。
    - 先压缩、再打包。若在打包之后才压缩源图，`Assets/Art` 工作副本与 `01_source/Textures` 仍是旧的大图，必须按规则 29 重跑打包让工作副本重新同步。
35. 任何工具都不得改写 `<FBX名>.fbm` 目录里的文件。那是 Unity 从 FBX 二进制抽取内嵌媒体生成的缓存，模型重新导入时会被原始数据覆盖，改它既留不住、又会和 Unity 正在进行的模型导入抢同一批文件，导致材质在导入中途解析失败。需要压缩内嵌贴图时，只能压打包工具平铺到 `Assets/Art/<模型>/Texture/` 之后的那一份。
36. FBX **内嵌贴图**（导入时被 Unity 抽取到 `<FBX名>.fbm/` 的贴图）是上一条的例外，必须按两遍流程处理，不得指望一次打包就达标：Unity 每次导入模型工作副本都会从 FBX 二进制里重新抽取一份原始大图，艺术家在导入区压过的 `.fbm` 副本对交付副本无效。正确做法是第一遍打包让贴图落到 `Assets/Art/<模型>/Texture/`，用导入插件的窗口手动压缩它，再打包第二遍——`MoveAssetToExactPath` 在目标已存在时保留目标、删除新抽取的源文件，所以压缩结果在后续重跑中稳定保留。反过来，FBX 里的内嵌贴图内容真的更新过时，必须先手动删除 `Texture/` 里的旧副本才能让新内容进来。

## 二、发布前强制验收

- 原始 FBX、Prefab 的路径和 GUID 在打包前后保持不变。
- 原始 FBX Importer 在打包前后保持不变。
- `Assets/Art/<模型名>` 中保留 FBX 工作副本和最终 Prefab，可在工程中继续调整。
- 重复打包后 Prefab 引用有效，材质与贴图未丢失。
- 空工程导入 UnityPackage 后无需再次手动修复材质即可显示。
- Android 与 iOS AB 都能加载最终 Prefab，且不是空包。
- 输出目录可通过菜单直接打开，完成弹窗显示绝对路径。
- 分享包不包含 `Examples`、`Library`、`Logs`、`Temp` 或具体模型资产。
- 已调好的 Prefab 材质参数在打包前后保持一致。
- UnityPackage 内只有 `Assets/Art/<选中Prefab名>/...` 的规范依赖，不出现额外的根目录 `Assets/Model`。
- `Model` 中不存在重复的材质、贴图伴生子目录；已有外部材质不会被重复生成为另一套 `Mat_*`。
- 带交互模型生成 `00_runtime_requirements/runtime_requirements.txt`，并在标准 Runtime 验收工程中通过动画、Lua、DOTween和触发事件测试。

## 三、历史问题禁止回归

- 禁止再次强制提取外部材质，导致嵌入材质模型失效。
- 禁止为了“整理目录”移动或删除原工程 FBX、Prefab。
- 禁止清空生成目录后让人工调整结果消失。
- 禁止删除目标副本再复制，导致 `.meta`/GUID 改变。
- 禁止输出包夹带整个 Art 目录、示例资源或无关文件。
- 禁止只给相对路径，导致接收者找不到交付文件。
- 禁止在 Play Mode 中继续执行打包。
- 禁止两个菜单维护两套不同的整理逻辑；辅助菜单必须转交给 `Tools > Retinar` 主流程。
- 禁止恢复“任一资产校验失败即整批终止”的阻断粒度。
- 禁止让导入插件介入 `Assets/Art` 产物区的 FBX/贴图导入设置。
- 禁止让贴图压缩输出非二的幂尺寸，导致每张被压缩的贴图都在交付报告里告警。
