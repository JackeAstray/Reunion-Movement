# 阶段 2 遗留：把三个"MonoBehaviour 模块"改成纯 C#

> 对应 [重构方案](重构方案.md) 阶段 2 的遗留项，以及「十一、待验证清单」第 6 条。
> 本文是**执行方案**，尚未动手。理由见最后一节。

## 一、先修正一个判断

重构方案里对这件事的评估是：

> 要重做 MonoBehaviour 生命周期与协程，风险集中

实测之后，这个判断**过于悲观**。三个类加起来 1005 行，但：

| 检查项 | 结果 |
|---|---|
| 用了 `StartCoroutine` / `IEnumerator` | **0 处** |
| 用了 `MonoBehaviour.Invoke` | **0 处** |
| 用了 `gameObject` / `transform` / `GetComponent` | **0 处** |
| 用了 `enabled` / `isActiveAndEnabled` | **0 处** |
| 用了 `DontDestroyOnLoad` | 通过基类的 `IsPersistentAcrossScenes` 间接使用 |

也就是说：它们**只是"被 MonoBehaviours 基类顺带挂上了 Update"**，没有任何真正依赖 MonoBehaviour 的能力。
要搬的只有三件事：`Awake` 的初始化、`Update` 的兜底、`OnDestroy` 的清理。

## 二、现状

| 类 | 行数 | 接口 | `Awake` | `Update`（MonoBehaviour） | `OnDestroy` |
|---|---|---|---|---|---|
| `TimerMgr` | 178 | `ICustomSystem, ISystemUpdatable, ISystemDisposable` | — | `if (!IsEngineRunning) TickTimers(Time.deltaTime)` | `CancelAllTimers()` |
| `HttpMgr` | 528 | `ICustomSystem, ISystemUpdatable` | `Init(new UnityHttpService())` | `if (!IsEngineRunning) UpdateProgressPump()` | — |
| `NetworkMgr` | 299 | `ICustomSystem, ISystemUpdatable` | `OnInit(null)` | `if (IsEngineRunning) return; DrainRemoveQueue()` | `OnTermination()` |

三个类都继承 `SingletonMgr<T> : MonoBehaviour`，都覆盖了 `IsPersistentAcrossScenes => true`。

`ModuleRuntime.IsEngineRunning` 就是为这个"双驱动"打的补丁：

```csharp
// 引擎在驱动 → 跳过 MonoBehaviour.Update；引擎没在驱动 → 用它兜底
if (ModuleRuntime.IsEngineRunning) return;
```

它在 `Core/Base/`（Base 程序集）而不是 `Utils`，**唯一原因**是 `Utils` 不允许反向引用 `Core`
——为了一个 bool 状态，专门建了个类型 + 两条 `InternalsVisibleTo`。

## 三、目标形状

以 `TimerMgr` 为例，改造前后：

```csharp
// ---------- 现在 ----------
public class TimerMgr : SingletonMgr<TimerMgr>, ICustomSystem, ISystemUpdatable, ISystemDisposable
{
    protected override bool IsPersistentAcrossScenes => true;

    private void Update()                                   // ← 删
    {
        if (!ModuleRuntime.IsEngineRunning)                 // ← 删
            TickTimers(Time.deltaTime);
    }

    protected override void OnDestroy()                     // ← 删
    {
        CancelAllTimers();
        base.OnDestroy();
    }
}

// ---------- 改造后 ----------
public class TimerMgr : ICustomSystem, ISystemUpdatable, ISystemDisposable
{
    private static readonly Lazy<TimerMgr> instance = new(() => new TimerMgr());
    public static TimerMgr Instance => instance.Value;

    public UniTask Init() { initProgress = 100; return UniTask.CompletedTask; }

    void ISystemUpdatable.Update(float logicTime, float realTime) => TickTimers(logicTime);

    public void Clear() { CancelAllTimers(); }              // ← OnDestroy 的职责搬到这里
}
```

净变化：**删掉 2 个方法、1 个基类、1 个 bool 判断；`Clear()` 补上清理**。

顺带能删掉的一个防御性判断：

```csharp
// 现状：防御"引擎持有的实例不是单例"的异常装配场景
if (ReferenceEquals(this, Instance)) TickTimers(logicTime);
```

改成 `Lazy<T>` 后不可能出现第二个实例，这个 `ReferenceEquals` 可以先删——但删之前要确认没有别处
`new TimerMgr()`（构造函数目前是隐式 public）。

## 四、四个必须保住的语义

这四点如果漏了，编译照样过，行为会变。逐条写清楚：

### 1. `SingletonMgr<T>.IsInitialized` → 必须补 `IsCreated`

`NetworkClient.cs` 有两处这么写：

```csharp
if (SingletonMgr<NetworkMgr>.IsInitialized)     // 只在"已经存在"时才注册，不主动创建
    NetworkMgr.Instance.AddChannel(channel);
```

`Lazy<T>` 的 `Instance` 是**访问即创建**，语义正好相反：会把"没创建"变成"顺手创建一个"。
所以改造后必须提供：

```csharp
public static bool IsCreated => instance.IsValueCreated;
```

并把那两处改成 `NetworkMgr.IsCreated`。**这一条最容易漏**，因为它不报错、只是行为悄悄变了。

### 2. `Awake` → `Init`

`HttpMgr.Awake` 里 `Init(new UnityHttpService())`，`NetworkMgr.Awake` 里 `OnInit(null)`。

搬到 `Init()` 里时要注意：`HttpMgr.Init()` **已经存在**且已经有 `if (service == null)` 兜底，
所以正确做法是删掉 `Awake`，让 `Init()` 负责创建 service，而不是在 `Init()` 里再调一次 `Init(service)`（无限递归）。

### 3. `OnDestroy` → `Clear`

`TimerMgr.OnDestroy` → `CancelAllTimers()`，而它的 `Clear()` **已经就是** `CancelAllTimers()`，所以这块直接删 `OnDestroy` 即可。
`NetworkMgr` 的 `OnDestroy` → `OnTermination()`，但它**没有** `Clear()`，需要新增 `ISystemDisposable` 并把 `OnTermination` 接进去。

`Clear()` 会被调用多次（切账号、引擎重建），必须幂等。

### 4. 没有引擎时，它们不再自己动

这是**唯一真正的行为变化**：

| 场景 | 现在 | 改造后 |
|---|---|---|
| 引擎运行中 | `ISystemUpdatable.Update` 驱动 | 不变 |
| 引擎未启动 / 已销毁 | `MonoBehaviour.Update` 兜底 | **不驱动** |
| 编辑器里直接打开示例场景 | 兜底生效 | 取决于 `Bootstrap` 是否启动引擎 |

`Bootstrap` 在**任何**场景都会启动引擎（测试场景只是跳过场景跳转，不是跳过引擎），
所以理论上"引擎未运行"只出现在引擎启动完成之前、以及 `Bootstrap.Disable()` 之后。
但这条需要真机确认，见第七节。

## 五、执行计划（三步，每步一个提交）

### 第 1 步：`TimerMgr` 试点（最小、有测试）

改动：

1. 去掉 `SingletonMgr<TimerMgr>` 基类，改为 `Lazy<TimerMgr>` 单例
2. 删 `IsPersistentAcrossScenes`、`Update()`、`OnDestroy()`，以及 `Update` 里的 `ReferenceEquals(this, Instance)` 防御
3. **`TickTimers` 的参数保持 `logicTime` 不变**：现状引擎路径用的就是 `logicTime`（暂停时归零），
   MonoBehaviour 兜底路径用的是 `Time.deltaTime`。删掉兜底后，计时器在暂停期间**不再走**——
   这是行为变化，需要跟产品确认是不是想要的
4. `Clear()` 已经调用 `CancelAllTimers()`，无需改动
5. `Utils/SingletonResets.cs` 删掉 `TimerMgr.ResetStatics();`

验证：`Tests/TimerTests.cs`（5 个用例）+ 示例场景 `Timer Example.unity`。

### 第 2 步：`HttpMgr`

1. 去掉 `SingletonMgr`，改 `Lazy` 单例
2. 删 `Awake`，把 `new UnityHttpService()` 移到 `Init()` 的 `service == null` 分支
3. 删 `Update()` 兜底，删 `ModuleRuntime.IsEngineRunning`
4. 补 `ISystemDisposable.Clear()`（现在没有）
5. `SingletonResets` 删一行

验证：示例场景 `WebClient.unity`、`Download Example.unity`（`DownloadMgr` 依赖 `HttpMgr.GetTexture`）。

### 第 3 步：`NetworkMgr`

1. 同上改 `Lazy` 单例 + 补 `IsCreated`
2. `NetworkClient.cs` 两处 `SingletonMgr<NetworkMgr>.IsInitialized` 改成 `NetworkMgr.IsCreated`
3. 删 `Awake`（`OnInit(null)` 移到 `Init()`）、删 `Update`、`OnDestroy` 的 `OnTermination()` 移到 `Clear()`
4. `SingletonResets` 删一行

验证：`Network Example.unity`、`MulitNetworkChannel.unity`、`WebServer.unity`。

### 第 4 步（收尾）：删掉补丁

三个都改完之后：

| 删除项 | 位置 | 行数 |
|---|---|---|
| `ModuleRuntime.cs` 整个文件 | `Core/Base/` | 19 |
| 两条 `InternalsVisibleTo` + 注释 | `Core/Base/AssemblyInfo.cs` | 6（整个文件可删） |
| 4 处写入 `IsEngineRunning` | `GameEngine.cs` / `.Launch.cs` / `.Cleanup.cs` | 4 行 |
| 1 处 `SingletonResets` 相关项 | `Utils/SingletonResets.cs` | 3 行 |

**注意**：`AssemblyInfo.cs` 删之前要确认 `Base` 程序集没有别的 `InternalsVisibleTo` 需求
（`Utils` 是否还访问 Base 的 internal 成员？改造后可能不再需要）。

## 六、净收益

| 指标 | 变化 |
|---|---|
| `ModuleRuntime` 这个"为绕开分层规则而生"的类型 | 消失 |
| `InternalsVisibleTo`（安全边界上的洞） | 从 2 条减到 0 条 |
| "模块可能被驱动两次"的隐性规则 | 消失，只剩一条驱动链 |
| `XxxMgr.Instance` 是否创建对象的歧义 | 统一成 `Lazy<T>`（与其余 11 个模块一致） |
| 代码量 | 约 −80 行（三个类的兜底逻辑 + 补丁） |

真正的收益不是行数，是**心智模型**：改造前新人要理解"引擎驱动 + MonoBehaviour 兜底 + 一个跨程序集的 bool"，
改造后只剩"引擎驱动模块"。

## 七、必须在 Unity 里验证的清单

`dotnet build` 只能证明编译通过。以下每一条都要真机点：

| # | 场景 | 观察点 |
|---|---|---|
| 1 | 打开任意示例场景直接 Play | 引擎启动（日志有 `模块顺序: …`），计时器照常走 |
| 2 | `Timer Example.unity` | 计时器回调、循环/单次、取消都正确 |
| 3 | 暂停（`PauseSystem`）期间 | 计时器**应该**停（若产品要求暂停也走，则要改成 `realTime`） |
| 4 | `WebClient.unity` | 请求能发出、进度能刷新（`UpdateProgressPump` 依赖引擎驱动） |
| 5 | 请求进行中切场景 | 进度回调不炸（改造前 MonoBehaviour 保证对象存活） |
| 6 | `Network Example.unity` + 断线重连 | 移除队列被消费、通道正确销毁 |
| 7 | 连续 Play 两次（第二次） | 没有 `MissingReferenceException`（单例不再是场景对象） |
| 8 | 关闭 Domain Reload 后连 Play 两次 | 静态状态正确复位 |

第 4、5 条最危险：`HttpMgr` 的进度轮询**只在引擎运行时进行**，如果有人在引擎启动前发请求，进度会停住。

## 八、为什么现在不做

三个原因，按权重排序：

1. **验证成本**：八条验证清单每一条都要真机点，而当前环境无法 Play。改完只能说"编译通过"，
   这对一次动生命周期的重构来说不够。
2. **收益是"以后更好"**：`ModuleRuntime` 是个丑补丁，但它现在**工作正常**。删掉它不会让任何功能变好，
   只会让代码更干净——属于"该做，但不紧急"。
3. **有更便宜的替代**：想降低这块的上手成本，先写清楚（`05-常见陷阱` 已写"双驱动"这一节），
   比直接重写风险低得多。

**建议时机**：阶段 3（分层归位）之后。因为阶段 3 会重排 `Utils` 的目录与命名空间，
届时这三个文件本来就要动，两件事合并做，只需要验证一次。
