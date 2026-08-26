# 待处理事项 · 模糊项 · 风险

返回 [总目录](../README.md)

确认后请改到 [01_requirements/strategy.md](../01_requirements/strategy.md)，并从此表删除或标「已关闭」。

---

## A. 待开发（已拍板方向）

| ID | 事项 | 优先级 | 状态 |
|---|---|---|---|
| **接口+中间层** | 1/2 窄口 + `Plugin/Pipeline` Runner | P0 | 已做 |
| D3 | 自动化管线总面板（单文件；② 独立 SO 区） | P0 | **已做** `Tools > 自动化管线总面板` |
| D2 | Runner：单文件导入、写 L1 路径、字符串结果；⑤默认关 | P0 | **已做**（StepResult 延后） |
| D1 | ⑥ 契约核对（命名/压缩/main）；最小 BuildAbOnly 已有 | P0 | 按 [d1-ab-only](../04_implementation/d1-ab-only.md)；细点另核 |
| D4 | ② 无头入库进管线；收集扩展认 `.glb` | P1 | |
| D5 | `-executeMethod` 参数与错误码表固化 | P1 | |
| D6 | UnityGLTF 改为可进镜像依赖（去 `file:` 绝对路径） | P1 | |
| D7 | 与 APP 书面确认：`main` 名、LZ4、`.assetbundle` 文件名 | P1 | 并入 D1 核对 |

---

## B. 仍模糊 / 未拍板

### 工程

1. `productName` 是否改为 Plugin2022  
2. 宿主是否将来 submodule  
3. ModleEvent 与 Plugin2022 两份 Plugin 长期如何对齐  

### 产品 / 流程

4. 素材库 V1.2 是否要跑全套 Art ④⑤门禁，还是永远只要双端 AB  
5. 嵌套 GLB Prefab：禁止 vs 警告  
6. Prefab 落盘用「纯三层名」还是「三层名/stem」子夹（实现已用扁平行 `{名}.prefab`）  
7. 门禁 Profile / SO 何时接线  
8. Converter 工程用 **专用 URP** 还是双管线  

### CLI / 运维

9. 参数格式（自定义 flag / 环境变量 / 临时 SO）  
10. License：Personal vs Pro；多机互踢策略  
11. Unity 精确版本号（如 2022.3.48f1）  
12. iOS on Linux 失败时的拆分构建方案（基建）  

---

## C. 已知风险（实现时注意）

| 风险 | 说明 |
|---|---|
| ⑥ 输入源 | 未平铺时应用**任意 Prefab**（近直通），勿写死必须 Art |
| ④ 后打 AB | Runner 暂用③路径；若需 Art Prefab 需刷新列表 |
| Legacy 大函数拆 options | 易影响现有「导出全部/选中」菜单回归 |
| ⑤ + GLB 内嵌 | 压图空跑 ≠ 合规；面板需提示 |
| 总面板命名 | 勿与「资源处理总面板」混淆 |
| URP 落差 | 本机 BiRP 出的 AB 可能在 APP URP 粉红 |
| 贴图抽出 | 已延后；勿当基线 blocker |

---

## D. 可退化（自动线默认不做，代码暂保留）

- 导出确认/完成弹窗  
- Converter 默认④⑤  
- 全套 00–06 Deliverables  
- SafeZone 硬阻断（自动线可关）  
- 整包平铺迁插件 2（明确不做）

---

## E. 低优先级 / 与当前总目标弱相关

| ID | 事项 | 说明 |
|---|---|---|
| L1 | L2 子面板数据：继续 Prefs，或抽「数据资源来源」SO | 操作参数仍来自 L3；L2 只是临时范围+勾选。**非本迭代** |
| L2 | Shared 根下扁平脚本迁入 Switches/BatchPath/… 子夹 | 纯目录卫生，保留 .meta |
| L3 | ~~Shared 对外 Facade~~ | **已建** `Shared/Api/` |
