# Utilities —— 纯工具与可复用 UI 组件

**放**：无状态、给谁都能用的函数与组件，**不依赖** `Framework/Runtime` 与 `Framework/Modules`。

| 子目录 | 内容 |
|---|---|
| `Util/` | `StringUtil`、`FileOperationUtil`、`AlgorithmUtil`、`SaveSystem` 等 |
| `UI/` | `ImageEx`、`LoopScrollRect`、`TreeView`、`Ripple`、`ButtonExtensions` |

**不放**：需要被 `GameEngine` 驱动的系统（去 `Framework/Modules/`）、引擎与 UI 核心（去 `Framework/Runtime/`）。

**注意**：程序集是 `Astray.Utilities`，命名空间是 `Astray.Utilities.*`
（2026-10-29 已从 `Astray.Common.Util.*` / `Astray.UI.*` 归位）。
