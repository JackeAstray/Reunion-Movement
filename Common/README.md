# Common —— 最底层基础设施

**放**：零业务的通用件。日志（`Log` / `LogHelper` / `GameLogger`）、配置（`Config` / `GameConfig`）、枚举、单例基类（`SingletonMgr`）。

**不放**：任何依赖 `Framework/Runtime` 或 `Framework/Modules` 的代码。本程序集没有 asmdef 引用，只能使用 UnityEngine 本身。

**典型文件**：`Log/Log.cs`、`Config/Config.cs`、`Singleton/SingletonMgr.cs`

**判断标准**：这段代码需要知道"游戏"吗？需要 → 不属于这里。
