# D6：UnityGLTF 可进 Docker 镜像

返回 [tech-and-ops](../01_requirements/tech-and-ops.md) · [待办](../03_open-items/backlog.md)

## 做了什么

`Packages/manifest.json` 中：

```text
旧：file:C:/Users/.../UnityGLTF-release-2.9.1-rc   ← 本机绝对路径，进不了镜像
新：https://github.com/KhronosGroup/UnityGLTF.git#release/2.9.1-rc
```

Docker / 其他机器只要能访问 GitHub（或你们的 git 镜像），UPM 即可解析同一依赖。

---

## 你需要做的关键步骤（人工）

1. **用 Unity 打开工程**，等 Package Manager 解析 `org.khronos.unitygltf`（首次会拉 git，需网络）。  
2. 看 Console / Package Manager：无红色失败即可。  
3. **抽测**：再导入一个 `.glb`，确认仍能生成 Prefab。  
4. （可选）提交更新后的 `Packages/manifest.json` 与 Unity 生成的 `packages-lock.json`。  
5. Docker 构建机：镜像内需有 **git**；若公司网络封 GitHub，改为内网 git 镜像 URL 或把包 **vendoring** 进仓库 `Packages/org.khronos.unitygltf` + `file:org.khronos.unitygltf`。

---

## 注意事项 / 风险

| 风险 | 说明 |
|---|---|
| 标签不存在 | 若 `#release/2.9.1-rc` 拉失败，可改 `#release/2.9.0-rc` 或与桌面 RC 对齐的 commit |
| 与桌面 RC 行为差 | 本机曾用 Desktop 目录包；git 标签内容可能略有差异，务必 GLB 回归 |
| 离线 Docker | 纯 git URL 在无外网时失败 → 改 vendoring 或私有 registry |
| Shader Graph 警告 | 打 AB 时 UnityGLTF ShaderGraph「newer node」警告可忽略，与 D6 无关 |
| 本仓 Plugin git | `manifest.json` 在 **宿主 Plugin2022**，不在 `Assets/Plugin` 子仓；提交时注意推哪个仓 |

---

## 何谓「镜像」

**Docker 容器镜像** = 服务器跑 headless Unity 的那份只读环境。依赖必须是「环境无关」的，不能绑某台 PC 的 `C:/Users/...`。
