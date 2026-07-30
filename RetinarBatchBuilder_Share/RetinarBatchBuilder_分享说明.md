# Retinar Unity 模型打包工具使用手册

版本：v1.1  
更新日期：2026-07-16  
适用环境：Unity 2020.3.49f1c1、Built-in Render Pipeline、Windows Editor

## 1. 工具用途

本工具用于把已经在 Unity 中检查和调整完成的三维模型 Prefab，整理成规范目录并输出：

- UnityPackage
- Android / PICO AssetBundle
- iOS AssetBundle
- 原始模型与贴图归档
- 贴图和模型检测报告
- `asset_info.xlsx` 信息表

工具的定位是“整理、检查和导出”。它不会替代美术人员对材质、法线、动画、模型方向和真机效果的人工验收。

## 2. 安装方法

1. 解压 `RetinarBatchBuilder_Share.zip`。
2. 关闭 Unity Play Mode。
3. 把压缩包中的 `Assets/Retinar` 整个复制到目标 Unity 工程的 `Assets` 目录。
4. 等待 Unity 自动导入和编译。
5. 确认 Console 没有红色 C# 报错。
6. Unity 菜单栏出现 `Tools > Retinar` 即安装成功。

安装后的核心结构：

```text
Assets/
└─ Retinar/
   ├─ Editor/
   │  ├─ RetinarBatchModelBuilder.cs
   │  └─ RetinarAssetInfoExporter.cs
   ├─ Templates/
   │  └─ asset_info_template.xlsx
   ├─ PACKAGING_RULES.md
   └─ CHANGELOG.md
```

## 3. 推荐工作流程

正式交付推荐选择“已经调整好的 Prefab”打包。

1. 把 FBX 导入 Unity。
2. 检查 FBX 的 Model、Rig、Animation、Materials 设置。
3. 把模型拖入场景。
4. 人工检查材质、贴图、法线、朝向、缩放、层级和动画。
5. 完成玻璃、外发光、旋转部件等实际效果调整。
6. 保存为最终 Prefab。
7. 在 Project 面板中选中这个 Prefab。
8. 执行 `Tools > Retinar > Batch Build Selected Models`。
9. 等待完成弹窗，不要在执行过程中进入 Play Mode 或关闭 Unity。
10. 点击 `Tools > Retinar > Open Deliverables Folder` 打开交付目录。
11. 在空工程导入 UnityPackage，并在目标设备加载 AB 做最终验收。

## 4. Prefab 与 FBX 应该选哪个

### 选择 Prefab（正式交付推荐）

可以保留已经调整好的：

- 材质与贴图引用
- Animator 与 Animator Controller
- 模型旋转、缩放和层级
- Collider
- 挂载的必要组件

命名以选中的 Prefab 文件名为准。工具不会修改 Prefab 中已经调好的材质参数。

### 选择 FBX（仅基础规范化）

适合快速生成基础 Prefab，但不能代表视觉效果已经人工验收。直接选择 FBX 可能无法包含场景中后续增加的材质调整、组件和复杂动画配置。

## 5. Unity 菜单说明

```text
Tools > Retinar > Batch Build Selected Models
```

完整打包：生成规范 Prefab、UnityPackage、Android/iOS AB、源文件归档、报告和表格。

```text
Tools > Retinar > Normalize Selected Models Only
```

只在 Unity 工程内建立或更新规范工作副本，不输出完整交付包。

```text
Tools > Retinar > Open Deliverables Folder
```

直接打开当前工程的交付目录。

## 6. UnityPackage 内部规范

以选中的 Prefab 名称为 `<模型名>`：

```text
Assets/
└─ Art/
   └─ <模型名>/
      ├─ Model/       必须存在，放 FBX/OBJ 等模型资源
      ├─ Texture/     必须存在，放贴图
      ├─ Material/    必须存在，放独立材质
      ├─ Prefab/      必须存在，放最终 Prefab
      ├─ Animation/   有动画时放 Clip、Controller 等
      ├─ UI/          有 UI 资源时放入
      └─ Text/        有文本依赖时放 txt、json、xml 等
```

不得出现额外的根目录 `Assets/Model`，也不得夹带 Examples、Library、Logs、Temp 或无关模型。

`Model` 中只保留 FBX、OBJ 等模型文件。原模型目录已有的材质和贴图会复制并整理到 `Material / Texture`；只有 FBX 使用内嵌材质、没有独立 `.mat` 时，工具才生成必要材质资产。

## 7. 外部交付目录

默认位于 Unity 工程根目录：

```text
Deliverables/<模型名>/
├─ 00_runtime_requirements/
│  └─ runtime_requirements.txt
├─ 01_source/
│  ├─ Model/
│  ├─ Textures/
│  ├─ texture_size_report.txt
│  └─ dcc_model_report.txt
├─ 02_unity/
│  └─ <模型名>.unitypackage
├─ 03_assetbundles/
│  ├─ Android/
│  └─ iOS/
└─ 06_docs/
   └─ asset_info.xlsx
```

预览图、演示视频、DCC 源文件、版权依据和人工审核信息需要按项目要求补齐。

带 XLua、DOTween、RichWidget 等交互组件的模型，必须先在目标工程安装 `runtime_requirements.txt` 指定的 Retinar Runtime。模型 UnityPackage 不重复携带公共 Runtime；AssetBundle 中的 C# 组件需要目标 App 已经编译相同版本的 Runtime 才能执行。

## 8. 动画模型要求

- 动画建议命名为 `Anim_<模型名>_<动画名>_<loop/once>`。
- 名称包含 `loop` 的动画应循环，包含 `once` 的动画不循环。
- Animator Controller 与对应动画名称应保持一致。
- 必须检查默认状态、触发条件、循环状态和时长。
- 机械动画应保持清晰的父子层级和正确枢轴。
- FBX 内嵌动画、Avatar、Root Motion、复杂状态机和无效关键帧仍需人工检查。

## 9. 强制注意事项

- 必须在 Edit Mode 下打包，禁止在 Play Mode 中执行。
- 原始 FBX、Prefab、材质和贴图应保留在原工程，不得手动用生成目录替换源文件。
- 打包前关闭正在打开的 `asset_info.xlsx`，避免表格被占用导致写入失败。
- FBX 依赖嵌入材质时，应确认 Materials 页签使用正确设置。
- 贴图不是 2 的幂或单张超过 2MB 时，必须查看报告并判断是否处理。
- 不要只看 Unity 编辑器效果，必须做 UnityPackage 空工程回归和 AB 目标端加载测试。

## 10. 最终验收清单

- [ ] 原工程中的 FBX、Prefab、材质和贴图仍在原位置。
- [ ] 最终名称与选中的 Prefab 文件名一致。
- [ ] UnityPackage 只包含规范的 `Assets/Art/<模型名>` 目录。
- [ ] `Model / Texture / Material / Prefab` 四个目录存在。
- [ ] Prefab 打开后模型、材质和贴图正常，不是空框。
- [ ] 法线、透明、玻璃、发光、编号和文字方向正确。
- [ ] Animator、默认动画、循环与一次性动画行为正确。
- [ ] Collider 覆盖合理，不影响交互。
- [ ] Android 与 iOS AB 文件存在且能加载。
- [ ] UnityPackage 导入空工程后无需重新修复材质。
- [ ] `asset_info.xlsx`、版权、制作人、审核人信息已人工补齐。
- [ ] 预览图和演示视频已补齐。

## 11. 常见问题

### 菜单没有出现

检查 `Assets/Retinar/Editor` 是否完整，并查看 Console 是否有红色编译错误。

### 提示 Play Mode 不能打包

等待 Unity 完全退出 Play Mode，再重新执行打包菜单。

### 找不到输出文件

执行 `Tools > Retinar > Open Deliverables Folder`。输出不在 `Assets` 内，而在 Unity 工程根目录的 `Deliverables`。

### UnityPackage 导入后只有 Collider，没有模型

不要提交。检查最终 Prefab 是否仍引用源工程散落目录，重新选择已调好的 Prefab打包，并进行空工程回归测试。

### Console 出现 AssetDatabase blacklist assertion

停止重复点击打包，等待 Unity 完成资源刷新后清空 Console 再试。若持续出现，请记录截图、Unity 版本、选中资源路径和操作步骤，反馈给工具维护人员。

## 12. 反馈问题时需要提供

- Unity 完整版本号
- 选中的是 Prefab 还是 FBX
- 选中资源的工程路径
- Console 完整错误截图
- UnityPackage 导入后的目录截图
- Prefab Inspector 和 Materials 页签截图
- 是否为重复打包同一模型
