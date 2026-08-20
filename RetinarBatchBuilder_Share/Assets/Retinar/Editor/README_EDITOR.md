# Retinar Editor 阅读地图（插件 1）

**当前正式版：v1.4.0**（平铺分类单元 + 自愈改到平铺结束；对照「平铺开工评估」落地）

面向未通读旧代码的同事：先看菜单，再看调度，最后才进 Legacy。

## 1. 两类入口（怎么用）

| 菜单 | 何时用 | 会不会改 Art / Prefab | Deliverables |
|------|--------|----------------------|--------------|
| **批量汇总 → 平铺到 Art** | 外部 FBX/Prefab 要整理进规范目录 | 写入 `Assets/Art/<名>/…` | 无 |
| **批量汇总 → 平铺分类面板** | 勾选大类、改后缀；看只读输出路径 | 否（只改本机 EditorPrefs） | 无 |
| **批量汇总 → 从 Art 导出（规范化）** | Art 内已整理，要全套交付（含报告） | **会再跑规范化**（SafeZone/碰撞体/Extract 等） | `00` `01` `02` `03` `06` 全套 |
| **成品直达 → 选中预制体直通打包** | Prefab 已是成品（尤其含 UI，怕被 SafeZone 弄歪） | **不改** Assets | **仅** `02_unity` + `03_assetbundles` |
| **打开交付文件夹** | 验收 | 否 | 打开工程根 `Deliverables/` |

外部未整理资源：**必须先走批量平铺**，不要指望直通去建 Art 结构。

## 2. 目录与阅读顺序

```text
Assets/Retinar/Editor/
  00_RetinarPaths.cs           路径常量
  00_RetinarEditorUtil.cs      弹窗/安全名/开交付夹
  01_RetinarMenu.cs            仅 MenuItem
  10_Flatten/
    RetinarFlattenScheduler.cs 平铺调度 → Legacy
    FlattenLayout.cs           Art/<名>/ 单元路径（夹名来自 Processor const）
    FlattenCopyRunner.cs       依赖分类：无人认领 → Unknown/
    FlattenReferenceAudit.cs   源预制体 Missing 提醒（只打 Error，不修复）
    FlattenWindow.cs           分类面板（勾选+后缀）
    Category/                  大类处理器 + 注册表
  20_Package/
    RetinarPackageScheduler.cs 规范化导出调度 → Legacy
    RetinarDirectPackage.cs    ★ 成品直通实现
    RetinarDeliverableIo.cs    02/03 目录写出
  README_EDITOR.md             本文件
  RetinarBatchModelBuilder*.cs Legacy：平铺规范化 + 全套导出（暂不拆碎）
```

建议阅读顺序：`01_RetinarMenu` → `RetinarDirectPackage` → `RetinarFlattenScheduler` / `RetinarPackageScheduler` → 需要改规范化时再进 `RetinarBatchModelBuilder.cs`。

## 3. 数据流

```text
批量：外部 Prefab/FBX
  → FlattenScheduler → CreateNormalizedPrefab（Legacy）
  → Assets/Art/<名>/{Model,image/Texture,Material,Prefab,…}
  → 平铺结束 TryHeal（补拷+Extract+remap）
  → PackageScheduler → ExportArtPrefabPaths（Legacy 全套校验与交付；校验不再自愈）

直通：选中 Prefab
  → DirectPackage
  → BuildPipeline(AssetBundleBuild[]) + ExportPackage
  → Deliverables/<名>/02_unity + 03_assetbundles
  （不调用 EnsureStandardAssetFolders / SafeZone / 碰撞体）
```

## 4. 为何 Legacy 暂不拆

`CreatePackagedAdjustedPrefab` 与 AssetResolution 强耦合（Extract、顶点色、外部依赖自愈）。自愈主调用已在平铺结束；导出校验只处理残留 `.fbm`。与直通路径无调用关系；本阶段不继续拆碎 `CreatePackagedAdjustedPrefab`。格式脚本（01_source / xlsx）仅规范化导出使用，拆分可后置。

## 5. 常量同步

新代码用 `RetinarPaths`。Legacy 内仍有同名 `private const`，修改路径时必须两边一起改。
