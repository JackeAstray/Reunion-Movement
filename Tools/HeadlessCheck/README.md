# HeadlessCheck · 无 Unity 的核心逻辑自检

一个独立的 .NET 控制台程序，用**反射**加载 Unity 侧编译出来的程序集，在没有 Unity 编辑器（也没有 Unity 授权）的环境里验证框架核心逻辑。

## 为什么需要它

`dotnet build` 只能证明"能编译"，证明不了"逻辑对"。而 Unity Test Runner 需要打开编辑器、需要授权。
这个工具填补中间那一段：**纯托管代码的行为**，用一条命令就能验。

## 前置

工程里的 `.csproj` 是 Unity 生成的（不入库），所以先用 dotnet 编一遍：

```bash
cd Unity/ReunionMovement
dotnet build ReunionMovement.Core.csproj -v q --nologo
dotnet build ReunionMovement.Utils.csproj -v q --nologo
```

产物在 `Unity/ReunionMovement/Temp/bin/Debug/`，本工具从这里加载。

## 用法

```bash
dotnet run --project Tools/HeadlessCheck
```

退出码：`0` 全通过，`1` 有失败，`2` 环境没准备好（缺 DLL / 找不到仓库根）。

## 覆盖范围

| 检查 | 状态 | 说明 |
|---|---|---|
| `Modules` 访问器（引擎未启动） | ✅ | `All` 为空、`TryGet` 返回 false、`Get` 抛 `InvalidOperationException` 且信息里带类型名 |
| `BootstrapOptions.IsTestScene` | ✅ | 前缀命中、关键字命中、正常场景不误判、空串不误判 |
| `ModuleCatalog` 排序与校验（5 条） | ✅ | 用 Reflection.Emit 造假模块，见下 |
| `GameEntryResolver` 入口解析 | ✅ | 框架程序集不再声明入口，纯框架加载时 `FindEntryType()` 返回 null；示例入口在 `Assembly-CSharp`，见下 |

### 为什么模块排序验不了

`ModuleCatalog.TryBuild` 需要**真实的模块实例**，而 14 个模块里多数继承自 `SingletonMgr<T> : MonoBehaviour`，
拿到 `Instance` 就等于 `new GameObject()` + `AddComponent` —— 这是 Unity 原生调用，纯 .NET 进程里必然抛
`SecurityException`。

解决办法不是跳过，而是**造假模块**：`ModuleCatalog` 只关心 `ICustomSystem` 和 `[ModuleOrder]` 两件事，
所以用 `Reflection.Emit` 在内存里造几个只实现接口 + 打上特性的最小类型就够了（见 `FakeModuleFactory.cs`）。

这样排序与校验逻辑都能真跑：

| 检查 | 验证的行为 |
|---|---|
| 按 `ModuleOrder` 升序 | 声明 30/10/20 → 排成 10/20/30 |
| 声明顺序不影响结果 | 打乱 5 个模块的声明顺序 → 结果不变 |
| 依赖优先于序号 | B(10) 依赖 A(20) → 输出仍是 A 在前 |
| 重复注册 | 同一实例注册两次 → 返回 false，错误信息含"重复" |
| 缺少依赖 | 依赖的模块没注册 → 返回 false，错误信息含"未注册" |

> 顺带记一条设计观察：模块单例在构造期就触碰 Unity（`GameObject`、`Resources.Load`），
> 这让"直接测真实模块"变成不可能。想彻底解决，得让模块构造保持无副作用（依赖在 `Init()` 里才取），
> 那是阶段 3 之后可以做的事。

### Reflection.Emit 的天花板：特性里的 `Type` 身份不稳定

造出来的假模块只能覆盖"单个依赖"的场景。以下两种写法过不去：

| 想造的用例 | 现象 | 原因 |
|---|---|---|
| 依赖成环（A↔B） | 报"依赖未注册"而不是"环" | 特性 blob 里引用未落地的 `TypeBuilder`，落地后拿到的 `Type` 与 `CreateType()` 的不是同一个标识 |
| 同一个依赖写两遍 | 同样报"依赖未注册" | 重复的 `Type` 在 blob 里解析出另一个 `Type` 实例，`IsInstanceOfType` 判不等 |

实测打印确认过：

```
[debug] consumer.DependsOn.Length=2
[debug]   [0] FakeModule1 sameAsProvider=False equalsProvider=False
```

**结论**：这两类用例改用真实编译的类型写，放在 `Tests/ModuleCatalogTests.cs` 里
（`Cycle_Is_Reported`、`Repeated_Dependency_Is_Not_A_Cycle`）。它们是真正的 C# 类，
没有 `Type` 身份问题，而且**同样能在本工具里跑**——因为测试代码就在 `ReunionMovement.Tests.dll` 里。

> 换句话说：要测"依赖关系"就写 NUnit 用例，别指望 Reflection.Emit。
> `Repeated_Dependency_Is_Not_A_Cycle` 这条一开始写成假模块，报的是缺依赖；
> 换成真实嵌套类之后立刻通过——**产品是对的，是工具错了**。

### `GameEntryResolver` 检查什么

框架程序集**不再**声明入口（示例入口在 `Game/StartGameEntry.cs`，编译进 `Assembly-CSharp`）。
HeadlessCheck 只加载框架程序集，所以 `FindEntryType()` 应为 `null` —— 这正是"包安装不会接管宿主启动"的保证。
Unity 侧另有两个用例：在 `Assembly-CSharp` 已加载时断言返回 `StartGame`，在 HeadlessCheck 里
通过 `Assert.Ignore` 自动跳过（工具会把 `IgnoreException` 记为 SKIP）。

## 顺带把 Unity 的用例也跑一遍

`ReunionMovement.Tests.dll` 里的用例大多不依赖 Unity。工具会加载它、按 NUnit 规则
（`[Test]` / `[TestCase]` / `[SetUp]` / `[TearDown]`）逐条执行，**需要 Unity 的自动算 SKIP**：

```
--- Unity Test Runner 的用例（本机可跑的部分） ---
  BootstrapTests                     通过 16 / 失败  0 / 跳过  0
  ModuleCatalogTests                 通过  9 / 失败  0 / 跳过  0
  ModulesAccessorTests               通过  5 / 失败  0 / 跳过  0
  NetworkCodecTests                  通过 12 / 失败  0 / 跳过  2
  ...
  PauseSystemTests                   通过  0 / 失败  0 / 跳过  8
  SaveSystemTests                    通过  0 / 失败  0 / 跳过  6

    小计: 通过 60 / 失败 0 / 跳过 54
```

判断依据不是"哪条看着像需要 Unity"，而是异常本身：

| 异常 | 处理 |
|---|---|
| `SecurityException` | SKIP（Unity 的 ECall 在纯 .NET 里必失败） |
| 栈里出现 `UnityEngine.` | SKIP，并把**那一帧**记下来当证据 |
| NUnit 断言失败 | FAIL |
| 其它 | FAIL |

想看每条被跳过的用例及其证据：

```bash
HEADLESS_VERBOSE=1 dotnet run --project Tools/HeadlessCheck
```

```
[SKIP] CommandArgTests.Bool_Parses_CaseInsensitive
       [UnityEngine.TestTools.Logging.LogScope.get_Current() in .../LogScope.cs:line 31]
```

上面这条说明该用例用了 `LogAssert`，确实只能在 Unity 里跑——不是"猜的"。

**这不是 Unity Test Runner 的替代品**：`Time`、`AudioListener`、`AssetDatabase`、R3 的
`ObservableSystem` 都得在 Unity 里初始化，那 54 条只能在编辑器里跑。它只是保证
**能在本机跑的那 60 条每次改完都真跑过**。
## 加一个新的检查

在 `Program.cs` 的 `Main` 里加一行：

```csharp
Run("我的检查", () => CheckSomething(core));
```

然后写一个 `private static void CheckSomething(Assembly core)`，用 `Assert(...)` 断言。
会碰到 Unity 原生调用的用例不要写进来——它只会变成 `[SKIP]`。

## 和 Unity Test Runner 的分工

| | HeadlessCheck | Unity Test Runner |
|---|---|---|
| 需要 Unity | 否 | 是 |
| 需要的输入 | `dotnet build` 产物 | 打开的编辑器 / batchmode + 授权 |
| 能验的东西 | 纯托管逻辑 | 全部（含 `Time`、`AudioListener`、`AssetDatabase`） |
| 跑一次多久 | ~2 秒 | 分钟级 |

**两者都要有**：前者保证改完就能自查，后者保证行为正确。
