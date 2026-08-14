# Retinar Unity 模型打包工具使用手册

版本：v1.3.8  
更新日期：2026-08-13  
适用环境：Unity 2020.3 / 2022.3、Built-in Render Pipeline、Windows Editor

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
   │  ├─ 00_RetinarPaths.cs / 00_RetinarEditorUtil.cs
   │  ├─ 01_RetinarMenu.cs          # 菜单唯一挂载点
   │  ├─ 10_Flatten/ …              # 平铺调度
   │  ├─ 20_Package/ …              # 规范化导出调度 + 成品直通
   │  ├─ README_EDITOR.md           # 阅读地图
   │  └─ RetinarBatchModelBuilder*.cs  # Legacy 规范化实现
   ├─ Templates/
   │  └─ asset_info_template.xlsx
   ├─ PACKAGING_RULES.md
   └─ CHANGELOG.md
```

编辑器代码怎么读：见 `Assets/Retinar/Editor/README_EDITOR.md`。

同仓库另有导入/后处理插件 **`Assets/Plugin/TOol`**（菜单 `Tools > 资源处理总面板`）。目录层级、类职责与扩展方式见：

`Assets/Plugin/TOol/ARCHITECTURE.md`

两插件目录边界见 `PACKAGING_RULES.md` 规则 33；贴图两遍与顶点色保留见规则 34–40。

## 3. 推荐工作流程

正式交付推荐选择“已经调整好的 Prefab”。**平铺与导出已拆成两步**；另有「成品直达」最净打包。

### 3.A 外部资源 → 批量汇总（全套 Deliverables）

1. 把 FBX 导入 Unity（`Assets/Art` 之外）。
2. 检查 FBX 的 Model、Rig、Animation、Materials 设置。
3. 把模型拖入场景，人工检查材质、贴图、法线、朝向、缩放、层级和动画。
4. 完成玻璃、外发光、旋转部件等实际效果调整。
5. 保存为最终 Prefab（建议同一导入区文件夹内，便于多选）。
6. 选中 Prefab，执行 `Tools > Retinar > 批量汇总 > 平铺到 Art（选中）`。
7. （按需）打开 `Tools > 资源处理总面板`：压 Art 贴图、刷 Art 模型顶点色。
8. 执行 `Tools > Retinar > 批量汇总 > 从 Art 导出（规范化） > 导出选中`（或「导出全部」）。
9. 用 `打开交付文件夹` 验收全套 Deliverables（含 00/01/02/03/06）。
10. 空工程导入 UnityPackage，目标设备加载 AB。

### 3.B 成品直达（仅 AB + UnityPackage）

Prefab **已经是成品**（尤其含 World-Space UI，不想再跑 SafeZone/碰撞体/建夹）时：

1. 在 Project 选中一个或多个 `.prefab`（可在 `Art` 内，也可在其它 Assets 路径）。
2. 执行 `Tools > Retinar > 成品直达 > 选中预制体直通打包`。
3. 产物仅 `Deliverables/<名>/02_unity` 与 `03_assetbundles/{Android,iOS}`；**不改** Art、**不改** Prefab 内容。
4. 未整理的外部 FBX/散落资源请仍走 §3.A 平铺，不要指望直通建规范目录。

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

### 批量汇总

```text
Tools > Retinar > 批量汇总 > 平铺到 Art（选中）
```

只把选中的 Prefab/FBX（可多选）整理进 `Assets/Art/<名>/`，**不**输出 AB / UnityPackage / Deliverables。  
不支持选中文件夹递归；请多选文件。

```text
Tools > Retinar > 批量汇总 > 从 Art 导出（规范化） > 导出全部
```

扫描 `Assets/Art/*/Prefab/*.prefab`，确认后全部走规范化导出（全套 Deliverables）。不依赖 Project 选中。

```text
Tools > Retinar > 批量汇总 > 从 Art 导出（规范化） > 导出选中
```

只接受选中的 Art 下 Prefab（可多选）。非 Art / 非 Prefab 会警告并跳过。导入区请先平铺。

### 成品直达

```text
Tools > Retinar > 成品直达 > 选中预制体直通打包
```

选中任意 Prefab（可多选）→ 仅打 AB（Android/iOS）与 UnityPackage。  
**不**跑 SafeZone / 碰撞体 / Extract / 动画改名 / EnsureStandardAssetFolders；**不写** `00_` / `01_source` / `06_docs`。  
AB 用显式 `AssetBundleBuild[]`，避免工程内同名 bundle 把其它 Art 目录打进同一包。

### 共用

```text
Tools > Retinar > 打开交付文件夹
```

打开本机 `Deliverables` 绝对路径。

**已迁移旧路径（勿再找）：**  
`平铺到 Art（选中）`、`从 Art 导出交付物/…` → 现位于「批量汇总」下。  
**已移除更早入口：** `Batch Build Selected Models`、`Normalize Selected Models Only`。

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

## 6.1 贴图超标与两遍压缩（必读）

完成弹窗若出现 `Texture check: N texture issue(s)`，**先打开**：

`Deliverables/<模型名>/01_source/texture_size_report.txt`

告警看的是 **磁盘源文件体积**（阈值 5MB），不是 Inspector 里的导入分辨率。Art 目录“看起来都不大”时，仍可能有单张略超 5MB（例如 5.64 MB）。

### FBX 内嵌贴图（最常见）

贴图最初在 FBX 里，Unity 抽到 `<FBX名>.fbm/`。这种图：

1. **不要压 `.fbm`**——重新导入会被原始数据盖掉；导入插件也会跳过并提示。
2. **第一遍打包** → 贴图落到 `Assets/Art/<模型名>/Texture/`。
3. 用 `Tools > 资源处理总面板`（贴图子面板）只压 **Art/Texture** 里超标的文件。
4. **不要删 Art**，选同一预制体 **再打第二遍**。

第二遍必须保留已压缩结果。若出现“压缩显示改了 1 个文件，再打包又变大”，属于历史缺陷（`ExtractTextures` / `SyncNewer` 用内嵌大图覆盖 Art）；自 **v1.2.8** 起已用快照恢复 + 体积保护修复。细节见 `PACKAGING_RULES.md` 规则 34–39 与 `CHANGELOG.md` v1.2.8。

### 外部 `.fbm` 依赖校验失败

若校验报 Prefab 仍依赖导入区 `Assets/**/xxx.fbm`，而 Art/Texture 已有同名副本：多为交付区模型材质搜索/同名贴图复用问题。工具会对交付区做 Extract + remap，并打 `[Retinar]` 日志；与两遍压缩保护同时生效（不得为了切依赖而盖掉已压小的 Art 贴图）。

## 7. 外部交付目录

默认位于 Unity 工程根目录。

### 7.A 批量汇总（规范化导出）— 全套

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

### 7.B 成品直达 — 最净

```text
Deliverables/<模型名>/
├─ 02_unity/
│  └─ <模型名>.unitypackage
└─ 03_assetbundles/
   ├─ Android/
   └─ iOS/
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

检查 `Assets/Retinar/Editor` 是否完整，并查看 Console 是否有红色编译错误。阅读入口见 `Editor/README_EDITOR.md`。

### 成品直达和规范化导出怎么选

- Prefab 已验收、含 UI、不想改内容 → **成品直达**（仅 02+03）。
- 外部未整理 / 需要 01_source、xlsx、规范化碰撞体与 SafeZone → **批量汇总**。

### 提示 Play Mode 不能打包

等待 Unity 完全退出 Play Mode，再重新执行打包菜单。

### 找不到输出文件

执行 `Tools > Retinar > 打开交付文件夹`。输出不在 `Assets` 内，而在 Unity 工程根目录的 `Deliverables`。

### UnityPackage 导入后只有 Collider，没有模型

不要提交。检查最终 Prefab 是否仍引用源工程散落目录，重新选择已调好的 Prefab打包，并进行空工程回归测试。

### Console 出现 AssetDatabase blacklist assertion

停止重复点击打包，等待 Unity 完成资源刷新后清空 Console 再试。若持续出现，请记录截图、Unity 版本、选中资源路径和操作步骤，反馈给工具维护人员。

### 弹窗提示 Texture check / 贴图问题，但 Art 里感觉都不大

打开 `Deliverables/<模型名>/01_source/texture_size_report.txt`，看 **Source File Size** 与 Status。告警按磁盘字节 &gt; 5MB，与分辨率不是一回事；常见是单张 5.x MB。详见 §6.1。

### 压缩显示改了 1 个文件，再打包又超标

确认压的是 `Assets/Art/<模型>/Texture/`，不是 `.fbm`；第二遍导出前不要删 Art。v1.2.8 起应保留压缩结果；若仍变大，把带 `[Retinar]` 的 Console 日志一并反馈。

### 校验失败提到导入区 `xxx.fbm` 路径

Prefab/FBX 仍依赖工程里另一份同名 `.fbm`。看 Console `[Retinar]` Extract/remap 日志；规范见 `PACKAGING_RULES.md` 规则 37–38。

### 模型顶点色处理好了，再打 Prefab 包又变回去

与贴图同类：打包会重导 `Art/Model` 下 FBX，Mesh 被重建。v1.2.9 起会快照/恢复顶点色；请确认脚本已编译，流程仍是「先处理 Art/Model，再打 Prefab，不要删 Art」。Console 可搜「恢复 Mesh 顶点色」。

## 12. 反馈问题时需要提供

- Unity 完整版本号
- 选中的是 Prefab 还是 FBX
- 选中资源的工程路径
- Console 完整错误截图（含 `[Retinar]` 贴图/Extract 相关日志）
- `01_source/texture_size_report.txt`（若涉及贴图告警）
- UnityPackage 导入后的目录截图
- Prefab Inspector 和 Materials 页签截图
- 是否为重复打包同一模型；若做过压缩，压的是 Art 还是 `.fbm`