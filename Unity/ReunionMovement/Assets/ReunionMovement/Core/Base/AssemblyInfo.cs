using System.Runtime.CompilerServices;

// ModuleRuntime.IsEngineRunning 的 setter 为 internal：写入方只有 GameEngine（Core 程序集），
// Utils 程序集仅读取（public getter）。internal 限制可防止业务代码随手改写引擎状态。
[assembly: InternalsVisibleTo("ReunionMovement.Core")]
[assembly: InternalsVisibleTo("ReunionMovement.Utils")]
