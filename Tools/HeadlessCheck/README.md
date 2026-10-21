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
| `ModuleCatalog` 排序与校验 | ⏭ | 见下 |
| `GameEntryResolver` 入口解析 | ⏭ | 见下 |

### 为什么模块排序验不了

`ModuleCatalog.TryBuild` 需要**真实的模块实例**，而 14 个模块里多数继承自 `SingletonMgr<T> : MonoBehaviour`，
拿到 `Instance` 就等于 `new GameObject()` + `AddComponent` —— 这是 Unity 原生调用，纯 .NET 进程里必然抛
`SecurityException`。所以这几个用例会被标成 `[SKIP]` 而不是 `[FAIL]`：

```
[SKIP] 模块顺序（声明顺序 == 属性顺序）
       该路径会触到 Unity 原生调用（SecurityException），只能在 Unity 里跑
```

模块排序的覆盖由 Unity Test Runner 里的 `Tests/ModuleCatalogTests.cs`（9 个用例）负责。

> 顺带记一条设计观察：模块单例在构造期就触碰 Unity（`GameObject`、`Resources.Load`），
> 这让"纯逻辑可测"变成不可能。想彻底解决，得让模块构造保持无副作用（依赖在 `Init()` 里才取），
> 那是阶段 3 之后可以做的事。

### `GameEntryResolver` 为什么也跳过

它要遍历程序集上的 `ReunionGameEntryAttribute`，一旦 `typeof(StartGame)` 被实体化，
`StartGame` 的静态构造会去读 `Config`（`Resources.Load`）→ 同样撞上 Unity 原生调用。

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
