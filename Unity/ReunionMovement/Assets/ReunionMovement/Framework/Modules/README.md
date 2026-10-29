# Utils —— 运行时系统与通用工具（最大目录：193 文件 / 约 3.6 万行）

**放**：

| 子目录 | 内容 |
|---|---|
| `Network/` `Http/` `Download/` | 传输、请求、断点下载 |
| `Timer/` `StateMachine/` `ObjectPool/` `GachaSystem/` | 运行时机制 |
| `Util/` | 纯工具：`StringUtil`、`FileOperationUtil`、`AlgorithmUtil`、`SaveSystem` 等 |
| `UI/` | 可复用 UI 组件：`ImageEx`、`LoopScrollRect`、`TreeView`、`Ripple` |

**不放**：依赖 `ReunionMovement.Core` 的代码。

**注意**：
- 命名空间是 `ReunionMovement.Common.Util.*`，与目录名不一致（历史原因，见 `Docs/重构方案.md` 阶段 3）。
- 这个目录是当前重构的重点。**新代码不要再往这里堆**：框架系统加到 `Core/System/`，纯工具加到 `Utils/Util/`。

**已有示例**：各系统下的 `Example/` 子目录，最终会迁到 `Samples/`。
