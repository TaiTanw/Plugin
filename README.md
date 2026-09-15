# Asset Plugin（资源工具集）

Unity Editor 插件集合，用于模型/贴图的**导入期处理**与**交付打包**。  
主开发环境：Unity 2022.3（Plugin2022，Built-in RP）、Windows Editor；Unity 2020.3 宿主保留作对照。

远程仓库：`http://swm-server.local:3000/Hanson/asset-bundle.git`  
已发布标签：**v1.5.3**；当前本地 `main` 已包含后续拆分与透明修复（2026-09-14 核对至 `e51469a`），不要把标签等同于当前代码\
**本批重点：** `.gltf` B′ 原子平铺；人工普通/原子两个完整④入口；平铺配置 SO 快照；管线只清本趟 Incoming/Art 单元夹。**D5 无头已验收**（契约见 [cli-getting-started](./docs/dev-wip/04_implementation/cli-getting-started.md)）。  
历史：`v1.5.0` 流程稳定；`v1.4.4` 平铺/动画循环；`v1.4.0` 平铺分类；`v1.3.8` 成品直通；`v1.3.5` 全流程基线

---

## 仓库内容

| 目录 | 定位 | 菜单入口 |
|------|------|----------|
| [`Pipeline/`](./Pipeline/) | **流程编排**：步骤 SO、Runner、自动化管线总面板 | `Tools > 自动化管线总面板` |
| [`TOol/`](./TOol/) | 批量入库选择器；导入期设置 + 源文件/模型后处理；物理承载③ Prefab 与④平铺内核 | `Tools > 批量选择器`；`Tools > 资源处理总面板` |
| [`RetinarBatchBuilder_Share/`](./RetinarBatchBuilder_Share/) | **插件 1（v1.5.3 线）**：⑥ AB/输出格式；保留人工平铺菜单薄转调 | `Tools > Retinar > 批量汇总` / `打开交付文件夹` |

**目录边界（2026-09-14）：**

- **导入期自动流**：Art 模型设置硬跳过，④负责交付副本 ModelImporter；贴图/后处理自动另走排除表。Incoming 模型安全基线已与总闸解耦，不能概括成所有自动都受同一个开关控制。\
- **L1 手动总批量**（资源处理总面板「执行全部」）：路径默认就是 `Assets/Art`，**故意**对交付区压图、刷顶点色。  
- **中间层⑤**（自动化管线勾选⑤）：**代调上一行同一内核**，不是导入期自动流。看起来像自动，但是编排在点面板按钮。  
当前职责、SO 和入口差距以[整体结构](./docs/dev-wip/02_structure/overview.md)与[已确认要求](./docs/dev-wip/01_requirements/strategy.md)为准；旧规则保留作历史追溯。\
插件 1 Editor 阅读地图：[`RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md`](./RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md)。  
**v1.4.0 Art 结构：** `Assets/Art/<名>/image/Texture`（默认贴图）、`image/UI`（Sprite）等单元目录；未知依赖进 `Unknown/`。

---

## 快速使用

### TOol（资源处理，v1.3.7）

1. 将本仓库置于 Unity 工程的 `Assets/Plugin`（或保持现有工程路径）。
2. （可选）**`Tools > 批量选择器`**：外部目录收集后入库，或「输出到编排」给管线（同夹多模型会消歧夹名）。
3. 打开 **`Tools > 资源处理总面板`（L1）**：共用批量路径 + 总/分项执行或仅扫描；日常批量优先在此完成。
4. 需要精准选中/单文件夹时开贴图·模型子面板（L2）；改阈值与 Op 集合进高级设置（L3）。
5. 配置资产包括 `TextureProcessSettings`、`MaterialProcessSettings`、`ModelProcessSettings`、`BatchFbxImportSettings`；④另有人工/管线两份 `FlattenOperationSettings`。

说明文档：

- 简要：[TOol/README.md](./TOol/README.md)
- 结构与扩展：[TOol/ARCHITECTURE.md](./TOol/ARCHITECTURE.md)

### 人工平铺与 Retinar 输出（v1.5.3）

1. 确认工程内存在 Retinar Editor 脚本并可编译。
2. （可选）打开 **`批量汇总 > 平铺操作与配置面板`**：可拖入 `FlattenOperationSettings` SO；人工目录资产可编辑，管线/未归类目录资产只读。
3. **人工执行完整④**：选中 Prefab/模型后，按数据形态选择“普通平铺（B）”或“原子迁移（B′）”。两个入口都会继续完成 E?/D/C/Finish。  
   - **Prefab / 直接选模型（含 FBX）**：先③（若选的是模型）再套空外壳、保留源 TRS/动画；**不再** SafeZone 缩 0.8。  
   - **相对 URI glTF**：选择普通平铺时弹出“仍要平铺 / 取消”确认；只有明确继续才执行，取消或关闭对话框不执行。缺伴生的原子迁移直接失败。
   - 动画材质曲线 / 依赖会收敛到本包（修复引用拆解不完全）。
4. （按需）插件 2 对 Art 执行⑤贴图/材质/模型处理，再由管线⑥输出 AB/可选 UnityPackage；旧“规范化导出/成品直达”菜单已删除。
5. 用 **`打开交付文件夹`** 查看输出。旧透明模式已经丢失的 Art 材质需从原始输入重建④⑤；不要泛化成所有故障都靠删 Art 修复，清夹前确认单元与人工修改。

说明文档：

- 历史分享手册（菜单/打包能力已过时）：[RetinarBatchBuilder_Share/RetinarBatchBuilder_分享说明.md](./RetinarBatchBuilder_Share/RetinarBatchBuilder_分享说明.md)
- Editor 阅读地图：[RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md](./RetinarBatchBuilder_Share/Assets/Retinar/Editor/README_EDITOR.md)
- 历史规则及现行适用范围：[RetinarBatchBuilder_Share/PACKAGING_RULES.md](./RetinarBatchBuilder_Share/PACKAGING_RULES.md)
- 回归清单：[RetinarBatchBuilder_Share/REGRESSION_CHECKLIST.md](./RetinarBatchBuilder_Share/REGRESSION_CHECKLIST.md)

---

## 推荐工作流（两端配合）

```text
外部模型 → Tools > 批量选择器（入库导入区，或输出到编排）
    → TOol：设置自动（导入区）
    → 场景中人工调材质 / 保存 Prefab（交付名以此为准）
    → 人工④：普通平铺或 glTF 原子迁移到 Art
    → TOol：总批量或分项（压 Art 贴图、刷顶点色）
    → 管线⑥ / RetinarAbApi.Build：按导出 SO 输出 AB / 可选 UP
    → 空工程或真机验收
```

---

## 协作说明

- **迭代重心（v1.5.x）：** 自动化管线（②③⑥，可选④⑤）+ glTF 整包；④实现物理位于 TOol、职责暂归中间层，插件 1 目标只保留⑥输出格式/AB 和菜单薄适配。
- 开发在独立分支进行，通过合并请求（PR）合入；任务用平台 **工单（Issue）** 跟踪。
- 敏感信息（账号、Token、密码）只放本地 `.env` 或环境变量，**禁止提交**。仓库已忽略 `.env`。
- 本地可同时保留 GitHub `origin` 与团队远程 `team`（指向本仓库）。

---

## 分支提示

已发布快照看 **标签 `v1.5.3`**，后续开发看 **`main`**，二者并不相同。Gitea 仓库**默认分支若仍是 `other`**，首页 README 会停在 **v1.4.4**；左上角改选 `main` 即是现网文档。\
命令行自动化一体流程（2022 / GLB）开发备忘见 [`docs/dev-wip/`](./docs/dev-wip/README.md)（入口 [`docs/CLI_AUTOMATION_DEV.md`](./docs/CLI_AUTOMATION_DEV.md)）。  
**开发日志**（提交次第 / 工单对齐）：[`docs/dev-wip/05_dev-log/timeline.md`](./docs/dev-wip/05_dev-log/timeline.md)。
