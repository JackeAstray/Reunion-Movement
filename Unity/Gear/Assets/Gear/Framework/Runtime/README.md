# Framework/Runtime —— 引擎与运行时系统

**放**：启动链（`Bootstrap` → `GameEntry` → `GameEngine`）、各功能系统（`System/*`）、全局设置（`GameOption`）、平台适配。

**不放**：纯工具函数（去 `Utilities/Util`）、示例与业务界面（去 `Samples~/` / 具体业务目录）。

**入口顺序**：
1. `Bootstrap.cs` —— `[RuntimeInitializeOnLoadMethod]` 自动启动，创建 `GameEngineDriver`。
2. `StartGame.cs` —— 默认 `GameEntry` 实现，注册模块列表并定义启动流程。

**换成自己的入口**（不用改框架源码）：

```csharp
// 放在你自己程序集的任意 .cs 文件里，namespace 之外
[assembly: GearGameEntry(typeof(MyGame.MyGameEntry))]
```

`GameEntryResolver` 会优先采用非框架程序集的声明；想手动掌控启动就用
`Bootstrap.Disable()` + `Bootstrap.StartAsync(new MyGameEntry())`。
测试场景判定、启动超时等配置在 `BootstrapOptions`（`Bootstrap.Options`）。

**启动顺序相关**：

- `ModuleCatalog.cs` —— 读各模块上的 `[ModuleOrder(order, DependsOn...)]`，做稳定拓扑排序 + 校验（缺依赖 / 重复注册 / 成环），失败只写日志不中断。
- `Modules.cs` —— 统一访问入口：`Modules.Get<T>()` 只返回真正注册过的模块，取不到时异常里列出已注册清单。
- `Framework/Base/IGamePauseHandler.cs` —— 引擎按接口找暂停实现者，不直接引用 `PauseSystem`。

**命名空间**：`Gear.Framework.Runtime.*`

**依赖方向**：`Runtime → Utilities → Common → Base`。`Runtime` 不引用 `Modules`；
`Modules` 通过 `Base` 的 `ModuleSourceRegistry`（Timer / Network / Http）与 `ResolutionBridge`
（ResolutionMgr）在运行时贡献能力，接入方删掉 Modules 也不会导致 Runtime 编译失败。

**新增一个系统**：参考 `Documentation/CustomSystem.cs.txt`，放到 `Framework/Runtime/System/<你的系统>/`，加上 `[ModuleOrder(排序值, 依赖...)]`，然后在 `StartGame.CreateModules()` 注册。
