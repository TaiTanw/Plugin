# 模型后缀（四张表）

现约入口：[开发者须知](../README.md)。④ 怎么搬：[④ 能力](../04_implementation/pipeline-flatten-capabilities.md)。⑤ 加 Op：[Op 扩展](../04_implementation/op-recognition-and-extend.md)。

三层各认各的。中间层没有模型后缀表，入盘时问插件 2。下面四张表现在都是 `.fbx` / `.glb` / `.gltf` / `.obj`。改一处不带动另外三处。

## 中间层：管线收什么进盘

预览和入库调用插件 2 的 `ToolImportApi`。模型只认那四个后缀。`.unitypackage` 另算，走信封，不进模型表。文件框上的类型列表只是对话框筛选项。

中间层没有加后缀的口。要多一种可入盘模型，改 `ToolImportApi` 里的数组。人工④选源时，`FlattenSidecarFacts` 另有一份同样的四后缀判断，和入库表不是同一处。

## 插件 2：⑤处理认什么

`ModelProcessSettings.supportedExtensions`。人工、管线各一份 SO，可在 SO 里增删。⑤、后处理、导入回调用它：是模型，并且后缀在这份表上，才进处理列表。总面板只显示，不在那里改。

只改这份表，①不会因此接收新后缀。会出现「能入库、⑤不认」，或反过来⑤表上有、①拒收。

⑤里加一个处理动作是另一件事：实现 Op 接口，反射注册。贴图多认一种文件，加 Codec。见 [Op 扩展](../04_implementation/op-recognition-and-extend.md)。

## ④平铺：文件进哪个夹

也在插件 2，和上面那张「可处理后缀」不是一张表。

普通平铺（B）按后缀把依赖拆进大类夹。模型大类默认 `fbx`、`obj`、`glb`、`gltf`，文件直接放在 `Model/`。贴图、材质、动画等各大类各自有默认后缀。平铺 SO 可以改某一大类的后缀，或关掉该大类。人工、管线各一份平铺 SO。没人认领的进 `Unknown/`。

外置 glTF 不走这张后缀表。有相对 URI 时整包搬到 `Art/<名>/<名>/`，相对路径保持原样，不把 `.gltf`、`.bin`、图拆进 `Model/` 和贴图夹。没有外置 URI 的 `.glb`、`.fbx`、`.obj` 仍走普通平铺。

## 插件 1：交付时带哪些文件

⑥拷模型时又写死了同样四个后缀，再决定旁边带什么。

| 后缀 | 交付 |
|---|---|
| `.fbx` / `.glb` | 只拷该文件 |
| `.gltf` | 再按相对 URI 跟拷伴生。扫描在插件 2 的 `GltfPackageFiles`，①和④也用 |
| `.obj` | 只拷容器，不跟 `.mtl` |

没有扩展口。新类型要改⑥里的判断和跟着的拷贝规则。④在 Art 里怎么摆夹，⑥不沿用那套夹名。
