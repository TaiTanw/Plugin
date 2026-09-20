# 输入 · 结果

现约入口：[开发者须知](../README.md)。相位 IO：[pipeline-phase-io](./pipeline-phase-io.md)。

- 面板与 CLI 共用 Runner。面板可 Bindings 多行；CLI 单 `-source`。Pack 解包填表未做。
- 面板/CLI 入库固定开。文档改链接即可；代码按风险跑测试，④不以单样例代替回归。

## 结果

| 层 | 契约 |
|---|---|
| ③ | Prefab 列表；空 → 30 |
| ④ | glTF `MissingUris` → 40 停；OBJ 不进该闸；41/42 Fail 不 return |
| ⑤ | `FailedCount>0` → 50；Report 不解析；空 Op 告警不阻断⑥ |
| ⑥ | 双端都成功才 0 |
| 整趟 | 首个非 0 退出码保留 |

统一 `StepResult` 未做。编排不窥 Op、不解析 Report。
