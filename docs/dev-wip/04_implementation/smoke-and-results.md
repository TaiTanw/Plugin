# 核对：输入 · 结果 · 验证边界

返回 [总目录](../README.md) · [待办](../03_open-items/backlog.md) · [各相位入参/返回值](./pipeline-phase-io.md)

> 2026-09-16：操作者入口见[插件根 README](../../../README.md)。2026-08“面板未建、暂不做冒烟”的建议只适用于当时切片。

## 1. 当前输入与验证

- 自动总面板与CLI共用PipelineRunner；面板可用bindings多行，CLI仍单-source。
- ②可导入工程外模型；Pack解包填表未实现，不另开一套管线。
- 当前SO默认②③④⑤⑥开启；总面板强制RunImport=true、CLI跟SO，差异见D26-5。
- 文档改动检查链接/一致性即可；代码改动按风险运行针对测试，④大范围拆分需真实模型回归，不以“能编译”或单个样例成功代替。
- 2026-09-10相关EditMode测试35/35通过、2026-09-11Art玻璃验收是历史证据，本轮未重跑；2026-09-14用户确认移动端AB已验收。

## 2. 配置与导入自动

编排不写L1批量路径；④成功且未指定覆盖范围时传本次Art单元到⑤。接口范围为null仍可回落L1路径，类型纳入默认还读EditorPrefs。

设置自动靠Unity导入回调，不由Runner再次手动调用。Incoming受支持ModelImporter安全基线已与总闸解耦；策略自动保留原闸。Art模型硬跳过、贴图/后处理自动排除是不同机制，不能写成全部“总闸+Import”。

总步骤与平铺已走SO，平铺同类不同来源实例、运行Policy快照；⑤材质等细节仍共用SO，尚未完成所有单项配置来源隔离。

## 3. 已有结果契约

| 层 | 当前结果 | 编排用途 |
|---|---|---|
| ③ | Prefab路径列表 | 空则30 |
| ④ | Begin/B/B′/Finish的bool及work；E/D/C为void | glTF MissingUris预检 → 40 并停止整趟；OBJ缺件不进该闸；尚无统一FlattenPhaseResult |
| ⑤ | ToolPostProcessResult：FailedCount、Canceled、Report | Execute硬失败累计>0映射50；Report给人读，不解析作控制流 |
| ⑥ | RetinarAbBuildResult | 全失败60，PartialOk仍0（既有契约） |
| 整趟 | PipelineResult：ExitCode、Messages、PrefabOutputs、AbOutputs | CLI退出/面板显示；后错覆盖首错仍是D26-4 |

⑤50后⑥仍跑；⑥全失败会把50覆盖成60。统一StepResult/首错保留尚未实现，不能把下文建议当当前保证。

## 4. D26-6：空操作与排错边界

2026-09-14用户意见：倾向合法跳过并明确提示，前提是架构已安排好；编排不宜直接获取执行层内部。若边界不足，再考虑归中间层的专门排错层，具体方案需核对。

当前ResourcePostProcessService已有“未纳入”“空批量路径”“未选Op”“无目标”文字报告；窄口没有统一typed跳过原因。MaterialProcessSettings.EnsureMasterBatchDefaults还会补空列表，所以不可笼统说“所有空列表都Skip”。

下一步先核对窄口信息是否足够，分清配置未选、目标为空、Evaluate不适用、实际失败。若要增强，只通过明确的返回契约向中间层提供诊断，不让Runner窥探Registry/Op、不解析Report、不为零改动直接新增失败码。

## 5. StepResult：候选而非已定实现

结构化步骤结果有助按步展示与保留首错；具体字段/是否另建排错层待上述核对。操作层继续保留Evaluation/RunSummary，不强行替换为整趟StepResult。退出码映射仍归中间层，⑤透明材质事实仍归Material OP，不扩模型ctx。
