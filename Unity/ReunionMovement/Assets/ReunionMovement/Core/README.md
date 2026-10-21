# Core —— 引擎与运行时系统

**放**：启动链（`Bootstrap` → `GameEntry` → `GameEngine`）、各功能系统（`System/*`）、全局设置（`GameOption`）、平台适配。

**不放**：纯工具函数（去 `Utils/Util`）、示例与业务界面（去 `Samples` / 具体业务目录）。

**入口顺序**：
1. `Bootstrap.cs` —— `[RuntimeInitializeOnLoadMethod]` 自动启动，创建 `GameEngineDriver`。
2. `StartGame.cs` —— `GameEntry` 实现，注册模块列表并定义启动流程。

**命名空间**：`ReunionMovement.Core.*`

**依赖方向**：`Core → Utils → Common`。**不要**让 `Utils` 反向引用 `Core`（会形成程序集环；`Core/Base/ModuleRuntime.cs` 就是为此而生的补丁）。

**新增一个系统**：参考 `Documentation/CustomSystem.cs.txt`，放到 `Core/System/<你的系统>/`，然后在 `StartGame.CreateModules()` 注册。
