# Generated / Prefab — ③ 自动化预设体制作

## 在全流程中的位置

```text
基线：② 导入 → 【③ 本模块】→ ⑥ 出 AB
可选：④ 平铺(插件1，含 remap) → ⑤ 后处理(插件2 Op)
```

归属：`TOol/Editor/Generated/`（中间资产能力，非 Shared 横切）。

## Remap 是什么？（本模块不做）

**Remap** = 平铺（④）时，把材质/Prefab 上的引用改成指向 **Art 副本**里的文件，而不是导入区原路径。  
职责在 **插件 1**（`RemapMaterialTexturesToArtFolder` 等）。  

**插件 2** 在本步只负责：把已导入模型做成 **独立 `.prefab` 文件**。不压图、不平铺、不 remap。

## 子夹结构

```text
Prefab/
├─ README.md
├─ PrefabBuildMenu.cs        # Tools > 自动化预设体（选中模型）
├─ Config/PrefabBuildSettings.cs
├─ Layout/PrefabIncomingPaths.cs
└─ Service/PrefabBuildService.cs
```

## 命名

- 缺省：`BatchFbxImportService.ResolveFolderName`（源路径向上三层 `_` 拼接）→ `Assets/IncomingPrefab/{名}.prefab`
- CLI `materialId`：优先用 materialId
- 同名冲突：追加源文件 stem

## 编辑器初步验证

1. 将 `.fbx` / `.glb` 导入工程（GLB 需 UnityGLTF）
2. 在 Project 中 **选中** 模型文件（可多选）
3. 菜单 **`Tools > 自动化预设体（选中模型）`**（无确认弹窗，看 Console）
4. 在 `Assets/IncomingPrefab/` 查看生成的独立 Prefab  
5. 可选：再对该 Prefab 跑 Retinar「平铺到 Art」；基线 Converter 可跳过 ④⑤ 直接后续出 AB

## 实现状态

**已写盘**：`SaveAsPrefabAsset`；可选 Unpack 完全；无 `DisplayDialog`。
