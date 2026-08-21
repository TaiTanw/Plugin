# Retinar Editor 阅读地图（插件 1）

**当前正式版：v1.4.4**（动画循环沿用源 Clip；含 v1.4.1 引用拆解补全、v1.4.3 FBX/预设体分流）

面向未通读旧代码的同事：先看菜单，再看调度，最后才进 Legacy。

## 1. 两类入口（怎么用）

| 菜单 | 何时用 | 会不会改 Art / Prefab | Deliverables |
|------|--------|----------------------|--------------|
| **批量汇总 → 平铺到 Art** | 外部 FBX/Prefab 要整理进规范目录 | 写入 `Assets/Art/<名>/…` | 无 |
| **批量汇总 → 平铺分类面板** | 勾选大类、改后缀；看只读输出路径 | 否（只改本机 EditorPrefs） | 无 |
| **批量汇总 → 从 Art 导出（规范化）** | Art 内已整理，要全套交付（含报告） | Prefab 入口不再 SafeZone 缩放；FBX 产物再导出仍校验 SafeZone；可加碰撞体 / Extract | `00` `01` `02` `03` `06` 全套 |
| **成品直达 → 选中预制体直通打包** | Prefab 已是成品（尤其含 UI，怕被 SafeZone 弄歪） | **不改** Assets | **仅** `02_unity` + `03_assetbundles`；本包外依赖写入报告但不阻断 |
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
    FlattenAnimationClipRemapper.cs 原地改 m_PPtrCurves classID 23；删错误绑定重复曲线
    FlattenPostProcessSettings.cs  后处理开关（本期：是否加碰撞体）
    FlattenWindow.cs           分类面板（勾选+后缀）
    Category/                  大类处理器 + 注册表
  20_Package/
    RetinarPackageScheduler.cs 规范化导出调度 → Legacy
    RetinarDirectPackage.cs    ★ 成品直通实现
    RetinarDeliverableIo.cs    02/03 目录写出
  30_Business/                  ★ 门禁 / 输出 / 业务 Profile SO（接口预留，导出未读）
    IRetinarAcceptanceGate.cs
    IRetinarDeliverableOutput.cs
    RetinarBusinessIds.cs
    RetinarBusinessProfile.cs
  README_EDITOR.md             本文件
  RetinarBatchModelBuilder*.cs Legacy：平铺规范化 + 全套导出（暂不拆碎）
```

建议阅读顺序：`01_RetinarMenu` → `RetinarDirectPackage` → `RetinarFlattenScheduler` / `RetinarPackageScheduler` → 需要改规范化时再进 `RetinarBatchModelBuilder.cs`。

## 3. 数据流

```text
批量：外部 Prefab/FBX
  → FlattenScheduler → CreateNormalizedPrefab（Legacy）
     Prefab：拷依赖 + 套空父外壳（不缩放）+ 可选碰撞体
     FBX：空根 + 子模型 SafeZone 缩放（暂保持）
  → Assets/Art/<名>/{Model,image/Texture,Material,Prefab,…}
  → 平铺结束 TryHeal（补拷+Extract+remap）
  → PackageScheduler → ExportArtPrefabPaths（Legacy 全套校验与交付；校验不再自愈）

直通：选中 Prefab
  → DirectPackage
  → BuildPipeline(AssetBundleBuild[]) + ExportPackage（只收本 Art 夹）
  → 本包外依赖写入 _diagnostics/direct_package_dropped_deps.txt（不阻断）
  → Deliverables/<名>/02_unity + 03_assetbundles
  （不调用 EnsureStandardAssetFolders / SafeZone / 碰撞体）
```

## 4. 为何 Legacy 暂不拆

`CreatePackagedAdjustedPrefab` 与 AssetResolution 强耦合（Extract、顶点色、外部依赖自愈）。自愈主调用已在平铺结束；导出校验只处理残留 `.fbm`。与直通路径无调用关系；本阶段不继续拆碎 `CreatePackagedAdjustedPrefab`。格式脚本（01_source / xlsx）仅规范化导出使用，拆分可后置。

## 5. 常量同步

新代码用 `RetinarPaths`。Legacy 内仍有同名 `private const`，修改路径时必须两边一起改。

交付文件夹名（`02_unity` 等）可改字面量；语义 Id（`unity_package` 等）写在 `RetinarDeliverableIds`，发布后不得改含义。

## 6. 业务 SO（未接线）

对齐插件 2：门禁 / 输出是 C# 扩展类型，`RetinarBusinessProfile` 只存启用 Id。以后总面板拖入全部 Profile 并选当前业务。创建该 SO **不会**改变现行平铺或导出。SafeZone 缩放与套空父属于平铺内核，不放进门禁勾选。
