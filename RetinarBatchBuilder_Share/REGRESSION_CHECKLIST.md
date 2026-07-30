# Retinar Batch Builder 回归检查表

版本：1.0  
生效日期：2026-07-24

本文是每次修改工具、更换 Unity 版本、发布分享包或正式批量打包前必须执行的回归基线。不得因为某个模型打包成功就跳过其他类型。

## 一、发布前必测样本

- [ ] 纯静态模型：验证 Mesh、材质、贴图、Collider、SafeZone。
- [ ] 带动画模型：验证 Animator、Controller、Clip、循环/一次播放。
- [ ] 带交互模型：验证 Runtime、XLua、DOTween、RichWidget、触发事件。
- [ ] 存在 `fbx.fbm/Materials` 的模型：验证外部材质和贴图收敛。
- [ ] 根节点存在偏移补偿的模型：验证 AR 端居中和尺寸。
- [ ] Photoshop 修改过实体贴图的模型：验证工作副本、AB 和 `01_source` 版本一致。
- [ ] 同一模型连续打包两次：验证重复打包与首次结果一致。

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

## 三、贴图回归

- [ ] Unity TextureImporter `Max Size/Compression` 与原 PNG/JPG 文件体积分开报告。
- [ ] 原始贴图文件体积超过 5MB 才触发当前体积告警。
- [ ] Photoshop 修改原贴图后，源文件更新时必须刷新交付工作副本的图像内容。
- [ ] 刷新工作副本时必须保留目标 `.meta` 和 GUID。
- [ ] 交付工作副本更新时，不得被较旧源图反向覆盖。
- [ ] `01_source/Textures` 只归档最终 Prefab 实际引用的贴图版本。
- [ ] `texture_size_report.txt` 必须显示 Unity Imported Size 和 Source File Size。

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
