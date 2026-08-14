# Asset Plugin（资源工具集）

Unity Editor 插件集合，用于模型/贴图的**导入期处理**与**交付打包**。  
适用环境：Unity 2020.3（Built-in RP）、Windows Editor。

远程仓库：`http://swm-server.local:3000/Hanson/asset-bundle.git`  
当前版本：**v1.3.8**（`other` / `main` / 标签 `v1.3.8`）  
**迭代重点：** 自 v1.3.8 起逐步增强 **插件 1（Retinar）**——成品直通、菜单/Editor 可读分层；插件 2（TOol）维持稳定全流程。  
历史：`v1.3.7` 面板分层；`v1.3.6` 扫描/预检；`v1.3.5` 全流程基线；`v1.3.2-test` 测试版

---

## 仓库内容

| 目录 | 定位 | 菜单入口 |
|------|------|----------|
| [`TOol/`](./TOol/) | 批量 FBX 入库；导入期设置 + 源文件/模型后处理 | `Tools > 批量FBX导入`；`Tools > 资源处理总面板` |
| [`RetinarBatchBuilder_Share/`](./RetinarBatchBuilder_Share/) | **插件 1（迭代中）**：平铺 Art、规范化导出、**成品直通** | `Tools > Retinar > 批量汇总` / `成品直达` / `打开交付文件夹` |

**目录边界：** 自动处理流默认不碰 `Assets/Art/**`（交付产物区由打包工具管理）。两边不得同时改同一 Importer 属性。详见 [`RetinarBatchBuilder_Share/PACKAGING_RULES.md`](./RetinarBatchBuilder_Share/PACKAGING_RULES.md) 规则 33。  
插件 1 Editor 阅读地图：[`RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md`](./RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md)。

---

## 快速使用

### TOol（资源处理，v1.3.7）

1. 将本仓库置于 Unity 工程的 `Assets/Plugin`（或保持现有工程路径）。
2. （可选）**`Tools > 批量FBX导入`**：外部目录批量入库（同夹多 FBX 会自动消歧夹名）。
3. 打开 **`Tools > 资源处理总面板`（L1）**：共用批量路径 + 总/分项执行或仅扫描；日常批量优先在此完成。
4. 需要精准选中/单文件夹时开贴图·模型子面板（L2）；改阈值与 Op 集合进高级设置（L3）。
5. 配置资产：`TextureProcessSettings`、`ModelProcessSettings`、`BatchFbxImportSettings`。

说明文档：

- 简要：[TOol/README.md](./TOol/README.md)
- 结构与扩展：[TOol/ARCHITECTURE.md](./TOol/ARCHITECTURE.md)

### Retinar（插件 1，v1.3.8 起迭代）

1. 确认工程内存在 Retinar Editor 脚本并可编译。
2. **批量路径（规范化）**：选中 Prefab/FBX → `批量汇总 > 平铺到 Art` →（按需插件 2 处理）→ `从 Art 规范化导出`（全部/选中）。
3. **成品直通（不改 Art）**：Art 内已是成品 Prefab（尤其含 UI）→ `成品直达 > 选中预制体直通打包` → 仅 `02_unity` + `03_assetbundles`。
4. 用 **`打开交付文件夹`** 查看输出。

说明文档：

- 使用手册：[RetinarBatchBuilder_Share/RetinarBatchBuilder_分享说明.md](./RetinarBatchBuilder_Share/RetinarBatchBuilder_分享说明.md)
- Editor 阅读地图：[RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md](./RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md)
- 打包规则：[RetinarBatchBuilder_Share/PACKAGING_RULES.md](./RetinarBatchBuilder_Share/PACKAGING_RULES.md)
- 回归清单：[RetinarBatchBuilder_Share/REGRESSION_CHECKLIST.md](./RetinarBatchBuilder_Share/REGRESSION_CHECKLIST.md)

---

## 推荐工作流（两端配合）

```text
外部 FBX → Tools > 批量FBX导入（入库导入区）
    → TOol：设置自动（导入区）
    → 场景中人工调材质 / 保存 Prefab（交付名以此为准）
    → Retinar：平铺到 Art
    → TOol：总批量或分项（压 Art 贴图、刷顶点色）
    → Retinar：从 Art 导出交付物（全部或选中）
    → 空工程或真机验收
```

---

## 协作说明

- **迭代重心（v1.3.8+）：** 优先推进 **插件 1（Retinar）** 功能与可读分层；插件 2 以稳定维护与小补丁为主。
- 开发在独立分支进行，通过合并请求（PR）合入；任务用平台 **工单（Issue）** 跟踪。
- 敏感信息（账号、Token、密码）只放本地 `.env` 或环境变量，**禁止提交**。仓库已忽略 `.env`。
- 本地可同时保留 GitHub `origin` 与团队远程 `team`（指向本仓库）。

---

## 分支提示

当前常用功能分支为 `other`；`main` 为基线分支。浏览代码时请在网页左上角选择对应分支。
