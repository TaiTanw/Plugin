# 当前回归检查表

核对日期：2026-09-14。现约：[开发者须知](../docs/dev-wip/README.md) · [待办](../docs/dev-wip/03_open-items/backlog.md)。

这是人工/测试回归清单，不表示每项已有自动阻断。按本次修改范围选相应检查，发布或大范围拆分时覆盖完整样例；未经实测不得勾选。旧版门禁、全套交付报告要求已不适用。

## 1. 输入与源保护

- [ ] FBX 外置贴图、FBX 内嵌贴图分别验收；不能以一类成功代替另一类。
- [ ] OBJ + MTL、GLB、完整外 URI glTF、缺件 glTF、自包含 glTF 分别覆盖。
- [ ] 原始外部模型与源 Prefab/材质不被改写；故障注入时核对源 hash、路径、GUID。
- [ ] Begin 失败不得回退源 Prefab；外部材质复制失败不得继续写源材质。
- [ ] ②重入库只清本次 Incoming 单元；源已在 Assets 时不走工程外清夹逻辑。
- [ ] ④清夹只清选定 Art 单元；人工默认不清、管线默认清。受控重建可改变副本 GUID，不承诺保留人工调整。
- [ ] 同名跨单元贴图不串图；D25-4 尚未全收口，记录实际失败，不把现状当合格。

## 2. ④完整相位与两个人工按钮

- [ ] 管线与人工④都走 `ToolFlattenApi.Run(plan)`；调度层不出现 `FlattenPaths` / SafeZone 创建链。
- [ ] 普通平铺选择 B，遇外 URI glTF **确认后才平铺**（取消/叉号中止）；原子迁移选择 B′，缺件/无外 URI/多个 glTF 主包拒绝。
- [ ] 人工按钮输入已导入的 Assets 模型/Prefab；原模型（含 FBX）先经③生成 Prefab。
- [ ] 两个人工按钮均完成 Begin→B/B′→E?→D→C→Finish，而不是只搬文件；不会自动追加⑤⑥。
- [ ] 自动每模型②.5一次 ctx；人工**不** `PipelineJobContext.Build`。
- [ ] 缺 sidecar（glTF）：typed MissingUris 在 Begin 前失败，管线 exit=40，整趟④停；执行时文件消失也使 B′失败。OBJ 缺 `.mtl`/贴图当前不要求 40，记录白膜/`exit=0` 事实。
- [ ] leftover 外部 `.fbm`：内核仍成功；管线 exit=41，⑤⑥仍跑。贴图身份警告 exit=42，槽位引用不清。二者同时有则 41。空槽不算 41/42。
- [ ] B′在 Art/名称/名称/ 保持相对树；核对主文件及所有 sidecar，Prefab/材质引用到本单元。失败不保证自动回滚。
- [ ] B 的复制映射、OBJ MTL、E Extract、D 引用、C 材质独立化均检查；B 返回 true 不代替资源完整验收。
- [ ] C 两分支都跑；glTF 包原图与 Unity 材质用贴图副本并存是已接受布局。
- [ ] 直接选 FBX 与管线/外来 Prefab 一样验证空父、源内容 TRS 与动画；**不**再要求缩入 SafeZone。
- [ ] 轴向开/关分别测试，binding.CloneWith 不丢配置；重跑不叠加多余外壳和名称。
- [ ] 碰撞体按 SO 开关验收，默认关闭；关闭不因缺 BoxCollider 判失败。
- [ ] 动画材质曲线、Controller Motion、LoopTime 与源一致；实际播放，不只查文件存在。

## 3. 配置与导入自动

- [ ] 人工/管线平铺 SO 同类不同实例；面板只编辑自身来源目录，跨来源/未知目录只读；能按只读 SO 快照执行。
- [ ] Policy 在开跑前冻结，内核不再读平铺 EditorPrefs；只读规则不误称为全局 Inspector 权限。
- [ ] Incoming 的受支持 ModelImporter 安全基线在总闸关/排除命中时仍生效；策略自动仍按原闸。
- [ ] Art 模型设置 Processor 硬跳过；贴图/后处理自动按各自排除表验证。不能泛称“所有自动不碰 Art”。
- [ ] Art ModelImporter 使用 InPrefab + Local，避免全工程自动搜图；④自愈不改用户原始 Importer。
- [ ] 显式⑤仍可处理 Art；未提供范围/类型覆盖时，记录实际 SO 与 EditorPrefs 来源。
- [ ] 总面板强制入库而 CLI 跟 SO 的差异按 D26-5 记录，尚不能勾为“两入口完全等价”。

## 4. ⑤贴图、材质、模型

- [ ] 总批量顺序为贴图→材质→模型；范围为指定 Art 单元，未传范围才回落人工路径。
- [ ] 贴图体积检查按磁盘源文件字节与当前 SO 阈值；Importer 尺寸/压缩不是原文件体积。
- [ ] 不压 .fbm 缓存；压平铺副本。复用既有 Art 时，Extract/SyncNewer 不把已压副本盖回大图。
- [ ] 区分“只重跑⑤⑥”和“清单元后重建④⑤⑥”；后者会有意再生成资产，不应套用无条件保留旧图/GUID 的约定。
- [ ] NormalizeDeliverableShaderOperation 覆盖 Opaque/Cutout/Blend、cutoff、模式往返、低 alpha Opaque 不误判。
- [ ] 换 Shader 前捕获源表面状态；glTF BLEND→Standard Fade，检查混合/ZWrite/queue/keywords，不只看 _Mode。
- [ ] 歼15 glTF 的 41 个材质为 37 Opaque、4 Fade（ID03/05/20/25），ID20 玻璃透明。Art 已于 2026-09-11 用户验收；本项用于后续回归。
- [ ] 材质目标已是 Standard 时会 Skip；旧坏副本需从原始输入重建④⑤，不能靠再跑⑤猜回源透明性。
- [ ] 目标 Shader 改名不等于任意 URP 兼容，换目标需独立属性映射与端上测试。
- [ ] ⑤无操作/无目标/不适用须分别记录；当前汇总边界是否足以明确提示仍待 D26-6 核对，不新增硬失败约定。
- [ ] FBX 顶点色被重导冲掉的 D19 仍记录，但不作为 CLI 必须全白门禁；需要白顶点 GLB 时按专门人工流程验证。

## 5. ⑥输出与端上验收

- [ ] 使用管线⑥ / RetinarAbApi.Build，核对代码中的双端/LZ4及导出SO的路径、可选UP、拷贝开关。模型文件与资源信息表默认关；打开后失败仍不得把退出码改成 60。
- [ ] 检查实际 AB 存在、非零、更新时间与可加载性；不能只靠弹窗或 exit=0。
- [ ] 输出 AB/UP 只含指定 Prefab 及依赖，不打整棵 Art。UP 开启时在干净工程验证。
- [ ] 插件 1 不主动做④材质/Importer/Prefab变换；④ **不再**写 AB 标签。核对⑥仍出双端 AB。
- [ ] 歼15 R1 在目标移动端验证透明、颜色、深度/排序与最终 AB；目前未获此项确认。
- [ ] 其它材质、法线、双面、动画、Collider 按目标应用实际需求验收，模型能显示不代表交互已验收。
- [ ] 记录⑤50后⑥仍跑、⑥失败覆盖60、部分出包仍0等当前行为；对应 D26-4/既有契约，不自行改码。

## 6. 旧清单不再适用的要求

- 旧规范化导出/成品直达菜单、30_Business、门禁诊断文件、runtime_requirements、texture_size_report 和全套 00–06 已删除。导出 SO 默认关的 `01_source/Model` 与 `asset_info.xlsx` 是窄口附加，不是把旧全套导出做回来。
- “未知依赖必须在⑥停包”“⑥校验时强制 Extract 自愈”不是当前能力；④缺必需 sidecar 的失败闸仍有效，两者不能互相替代。
- GLB/glTF 现可直接入库；“GLB 必须先由 UnityPackage 派生”已过时。
- Model 仅 FBX/OBJ、全资产都要求 SafeZone/Collider、所有生成副本 GUID 永不改变等旧一刀切要求不适用。
- 发布分享包或更换宿主才做相应分发兼容测试；日常文档同步不要求重打 zip，也不假称已完成 Unity/端上回归。
