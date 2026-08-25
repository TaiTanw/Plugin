# Shared / Prefab — ③ 自动化预设体制作

## 在全流程中的位置

```text
① 路径收集 → ② 导入 → 【③ 本模块】→ ④ 平铺(插件1) → ⑤ 后处理 → ⑥ 出 AB
```

## 子夹结构

```text
Prefab/
├─ README.md                 # 本说明
├─ Config/                   # 配置：专用夹根路径、是否 Unpack、命名规则开关
│  └─ PrefabBuildSettings.cs
├─ Layout/                   # 只回答「Prefab 落到哪个 Assets 路径」
│  └─ PrefabIncomingPaths.cs
└─ Service/                  # 执行：从已导入模型生成独立 Prefab
   └─ PrefabBuildService.cs
```

| 子夹 | 中文职责 | 不做什么 |
|---|---|---|
| **Config** | 默认路径、开关、以后挂 SO | 不 `SaveAsPrefabAsset` |
| **Layout** | 拼 `Assets/.../xxx.prefab` 路径字符串 | 不扫磁盘业务 |
| **Service** | 收集 `.fbx`/`.glb` 主对象 →（可选 Unpack）→ 保存独立 Prefab | 不平铺、不打 AB、不压图 |

## 与插件 1 / 平铺的边界

- 本模块产出 **独立 Prefab 文件**（非 PrefabInstance 指着 `.glb`）。  
- **平铺 / Extract / 出包** 仍在插件 1（或未来迁出的平铺内核）；此处不调用 `RetinarBatchModelBuilder`。  
- GLB：依赖宿主 UnityGLTF 已导入；本 Service 只基于 `AssetDatabase` 已有主对象建 Prefab。

## 实现状态

当前为 **骨架 + 空实现注释**，供分类与评审；逻辑在后续提交填充。
