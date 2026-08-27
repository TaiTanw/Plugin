# Asset Plugin（资源工具集）

Unity Editor 插件集合，用于模型/贴图的**导入期处理**与**交付打包**。  
适用环境：Unity 2020.3（Built-in RP）、Windows Editor。

远程仓库：`http://swm-server.local:3000/Hanson/asset-bundle.git`  
当前版本：**v1.5.0**（`main` / 标签 `v1.5.0` · **流程稳定**）  
**本批重点：** 自动化管线 `Pipeline`（②→③→⑥，可选④⑤）+ CLI 入口；TOol Prefab/材质窄口；Retinar AB Options（D8）。  
历史：`v1.4.4` 平铺/动画循环；`v1.4.0` 平铺分类；`v1.3.8` 成品直通；`v1.3.5` 全流程基线

---

## 仓库内容

| 目录 | 定位 | 菜单入口 |
|------|------|----------|
| [`Pipeline/`](./Pipeline/) | **流程编排**：步骤 SO、Runner、自动化管线总面板 | `Tools > 自动化管线总面板` |
| [`TOol/`](./TOol/) | 批量 FBX 入库；导入期设置 + 源文件/模型后处理 | `Tools > 批量FBX导入`；`Tools > 资源处理总面板` |
| [`RetinarBatchBuilder_Share/`](./RetinarBatchBuilder_Share/) | **插件 1（v1.5.0 线）**：分类平铺、引用收敛、FBX/预设体分流、AB Options / 成品直通 | `Tools > Retinar > 批量汇总` / `成品直达` / `打开交付文件夹` |

**目录边界（防混淆，三条通道）：**  
- **导入期自动流**（设置自动 / 后处理自动，`AssetPostprocessor`）：默认**不碰** `Assets/Art/**`（`excludedPathPrefixes`），避免与插件 1 的交付 Importer 互踩（规则 33）。这是「不碰 Art」的那条，且应保持如此。  
- **L1 手动总批量**（资源处理总面板「执行全部」）：路径默认就是 `Assets/Art`，**故意**对交付区压图、刷顶点色。  
- **中间层⑤**（自动化管线勾选⑤）：**代调上一行同一内核**，不是导入期自动流。看起来像自动，但是编排在点面板按钮。  
两边不得同时改同一 Importer 属性。详见 [`PACKAGING_RULES.md`](./RetinarBatchBuilder_Share/PACKAGING_RULES.md) 规则 33、[`TOol/ARCHITECTURE.md`](./TOol/ARCHITECTURE.md)。  
插件 1 Editor 阅读地图：[`RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md`](./RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md)。  
**v1.4.0 Art 结构：** `Assets/Art/<名>/image/Texture`（默认贴图）、`image/UI`（Sprite）等单元目录；未知依赖进 `Unknown/`。

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

### Retinar（插件 1，v1.4.4）

1. 确认工程内存在 Retinar Editor 脚本并可编译。
2. （可选）打开 **`批量汇总 > 平铺分类面板`**：勾选大类、改后缀；可选根 BoxCollider。
3. **批量平铺**：选中 Prefab/FBX → `平铺到 Art`  
   - **外来 Prefab**：套空外壳、保留源 TRS/动画，不 SafeZone 缩放。  
   - **FBX 自动预制体**：仍缩进 SafeZone。  
   - 动画材质曲线 / 依赖会收敛到本包（修复引用拆解不完全）。
4. （按需）插件 2 处理 Art 贴图等 → `从 Art 规范化导出`；或 **成品直通**（仅 02+03，外依赖会报告）。
5. 用 **`打开交付文件夹`** 查看输出。已被旧逻辑改过的 Art 副本须**删后重平铺**才会自愈。

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

- **迭代重心（v1.4.x）：** 插件 1——引用收敛、FBX/预设体分流、平铺/导出模块化；插件 2 以稳定维护为主。
- 开发在独立分支进行，通过合并请求（PR）合入；任务用平台 **工单（Issue）** 跟踪。
- 敏感信息（账号、Token、密码）只放本地 `.env` 或环境变量，**禁止提交**。仓库已忽略 `.env`。
- 本地可同时保留 GitHub `origin` 与团队远程 `team`（指向本仓库）。

---

## 分支提示

当前常用功能分支为 `other`；`main` 为基线分支。浏览代码时请在网页左上角选择对应分支。  
命令行自动化一体流程（2022 / GLB）开发备忘见 [`docs/dev-wip/`](./docs/dev-wip/README.md)（入口 [`docs/CLI_AUTOMATION_DEV.md`](./docs/CLI_AUTOMATION_DEV.md)；分支 `feature/cli-pipeline-2022`）。
