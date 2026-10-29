# Framework/Modules —— 运行时系统（可复用模块）

**放**：有生命周期、需要被 `GameEngine` 驱动、或自身带状态的框架系统。

| 子目录 | 内容 |
|---|---|
| `Network/` `Http/` `Download/` | 传输、请求、断点下载 |
| `Timer/` `StateMachine/` `ObjectPool/` `GachaSystem/` | 运行时机制 |
| `ColliderUtil/` `CSG/` `Camera/` `Raycast/` `Deadline/` | 场景侧工具系统 |
| `SafeArea/` `ScreenResolution/` `ScreenLogger/` `Fps/` | 平台与调试 |

**不放**：纯函数式工具（去 `Utilities/`）、引擎与 UI 核心（去 `Framework/Runtime/`）。

**注意**：

- 目录名是 `Framework/Modules`，**程序集名暂时仍叫 `ReunionMovement.Utils`** —— 这是有意保留的，
  避免同时改动 `link.xml`、`Tools/HeadlessCheck` 与 `InternalsVisibleTo`。程序集改名是独立一步。
- 命名空间暂时仍是 `ReunionMovement.Common.Util.*`（阶段 3 第 3 步未做）。
- 新代码不要再往这里堆：框架系统加到 `Framework/Runtime/System/`，纯工具加到 `Utilities/`。

**已有示例**：各系统下的 `Example/` 子目录，最终会迁到 `Samples/`。
