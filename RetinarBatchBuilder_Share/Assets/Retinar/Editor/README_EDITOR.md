# Retinar Editor 阅读地图（插件 1）

面向未通读旧代码的同事：先看菜单，再看调度，最后才进 Legacy。

日常出包走管线 ①→⑥（`PipelineRunner`），不经过本目录的菜单。

## 1. 现网入口

| 入口 | 何时用 | 会不会改 Art / Prefab | Deliverables |
|------|--------|----------------------|--------------|
| 管线 ④ `RetinarFlattenApi.FlattenPaths` | 管线主路径（唯一调用方：`PipelineRunner`） | 写入 `Assets/Art/<名>/…` | 无（出包是 ⑥） |
| **批量汇总 → 平铺到 Art（选中）** | 手工把外部 FBX/Prefab 整理进规范目录 | 同上 | 无 |
| **批量汇总 → 平铺分类面板** | 勾选大类、改后缀；看只读输出路径 | 否（只改本机 EditorPrefs） | 无 |
| **打开交付文件夹** | 验收 | 否 | 打开工程根 `Deliverables/` |
| 管线 ⑥ `RetinarAbApi` | 出包 | **不改** Art | `02_unity` + `03_assetbundles` |

2026-09-03（backlog D24-7）已删：「【遗产】从 Art 规范化导出」「成品直达」、`RetinarPackageScheduler`、`RetinarDirectPackage`、出包前三道校验、`30_Business` 整层。不再写 `00_` / `01_source` / `06_docs` / xlsx。

## 2. 目录与阅读顺序

```text
Assets/Retinar/Editor/
  00_RetinarPaths.cs           路径常量
  00_RetinarEditorUtil.cs      弹窗/安全名/开交付夹
  01_RetinarMenu.cs            仅 MenuItem（平铺两项 + 打开交付夹）
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
    RetinarDeliverableIo.cs    02/03 目录写出（⑥ RetinarAbApi 在用）
  40_Api/
    RetinarFlattenApi.cs       ④ 窄口
    RetinarAbApi.cs            ⑥ 窄口
  README_EDITOR.md             本文件
  RetinarBatchModelBuilder*.cs Legacy：平铺规范化（暂不拆碎）
```

建议阅读顺序：`01_RetinarMenu` → `RetinarFlattenScheduler` → 需要改规范化时再进 `RetinarBatchModelBuilder.cs`。

## 3. 数据流

```text
管线 / 菜单平铺：外部 Prefab/FBX
  → FlattenPaths / FlattenScheduler → CreateNormalizedPrefab（Legacy）
     Prefab：拷依赖 + 套空父外壳（不缩放）+ 可选碰撞体 + 可选 OBJ 轴向修正
     FBX：空根 + 子模型 SafeZone 缩放（暂保持）
  → Assets/Art/<名>/{Model,image/Texture,Material,Prefab,…}
  → 平铺结束 TryHeal（补拷+Extract+remap）——④ 是最后一道，之后没有兜底校验

出包：管线⑥ RetinarAbApi
  → BuildPipeline(AssetBundleBuild[]) + RetinarDeliverableIo 写 02/03
  → Deliverables/<名>/02_unity + 03_assetbundles
```

## 4. 为何 Legacy 暂不拆

`CreatePackagedAdjustedPrefab` 与 AssetResolution 强耦合（Extract、顶点色、外部依赖自愈）。自愈主调用已在平铺结束。形态见 backlog D24-5：在插件 1 内部把 `FlattenPaths` 拆薄成能力方法，不搬目录。

## 5. 常量同步

新代码用 `RetinarPaths`。Legacy 内仍有同名 `private const`（如 `ArtRoot`、`AssetBundleVariant`），修改路径时必须两边一起改。
