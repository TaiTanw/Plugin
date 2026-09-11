# Retinar Editor 阅读地图（插件 1）

面向未通读旧代码的同事：先看菜单，再看调度，最后才进 Legacy。

日常出包走管线 ①→⑥（`PipelineRunner`），不经过本目录的菜单。

## 1. 现网入口

| 入口 | 何时用 | 会不会改 Art / Prefab | Deliverables |
|------|--------|----------------------|--------------|
| 管线 ④ `ToolFlattenApi` | 管线主路径（`PipelineRunner` → 插件 2 `Generated/Flatten`） | 写入 `Assets/Art/<名>/…` | 无（出包是 ⑥） |
| **批量汇总 → 平铺到 Art（选中）** | 人工普通分支 B；随后执行完整④ | 同上 | 无 |
| **批量汇总 → 原子迁移到 Art** | 相对 URI glTF 走 B′；随后执行完整④ | 同上；缺伴生拒绝 | 无 |
| **批量汇总 → 平铺操作与配置面板** | 拖入平铺 SO、编辑人工目录资产、执行上述两个完整相位入口 | 配置本身不改 Art；按钮会改 | 无 |
| **打开交付文件夹** | 验收 | 否 | 打开工程根 `Deliverables/` |
| 管线 ⑥ `RetinarAbApi` | 出包 | **不改** Art | `02_unity` + `03_assetbundles` |

2026-09-03（backlog D24-7）已删：「【遗产】从 Art 规范化导出」「成品直达」、`RetinarPackageScheduler`、`RetinarDirectPackage`、出包前三道校验、`30_Business` 整层。不再写 `00_` / `01_source` / `06_docs` / xlsx。

## 2. 目录与阅读顺序

```text
Assets/Retinar/Editor/
  00_RetinarPaths.cs           路径常量（ArtRoot 须与 FlattenBuildSettings 同字面量）
  00_RetinarEditorUtil.cs      弹窗/安全名/开交付夹
  01_RetinarMenu.cs            仅 MenuItem（平铺两项转调插件 2 + 打开交付夹）
  20_Package/
    RetinarDeliverableIo.cs    02/03 目录写出（⑥ RetinarAbApi 在用）
  40_Api/
    RetinarAbApi.cs            ⑥ 窄口
  README_EDITOR.md             本文件
```

④ 平铺实现：`Assets/Plugin/TOol/Editor/Generated/Flatten/`（`ToolFlattenApi`）。

建议阅读顺序：`01_RetinarMenu` → 插件 2 `Generated/Flatten/README.md`。

## 3. 数据流

```text
管线 / 菜单平铺：外部 Prefab/FBX/glTF
  → PipelineRunner 或 ManualFlattenService
  → Begin → (B 分类拆分 | B′ 原子迁移) → E? → D → C → Finish
     Prefab：拷依赖 + 套空父外壳（不缩放）+ 可选碰撞体 + 可选 OBJ 轴向修正
     FBX：空根 + 子模型 SafeZone 缩放（暂保持）
  → Assets/Art/<名>/{Model,image/Texture,Material,Prefab,…}
  → 平铺结束 TryHeal（补拷+Extract+remap）——④ 是最后一道，之后没有兜底校验

出包：管线⑥ RetinarAbApi
  → BuildPipeline(AssetBundleBuild[]) + RetinarDeliverableIo 写 02/03
  → Deliverables/<名>/02_unity + 03_assetbundles
```

## 4. 为何 Legacy 暂不拆

`RetinarBatchModelBuilder` 三份 partial 已物理迁到插件 2，但仍把菜单、管线④、Extract、外部依赖自愈等混在约 3,500 行内。当前 ctx 驱动的相位编排暂归中间层；后续按能力文件逐刀拆，不再一次性切大文件。详见 D24 边界计划。

## 5. 常量同步

新代码用 `RetinarPaths`。Legacy 内仍有同名 `private const`（如 `ArtRoot`、`AssetBundleVariant`），修改路径时必须两边一起改。
