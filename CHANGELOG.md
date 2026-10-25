# Changelog

## [1.3.0] - 2026-10-25

### Changed（破坏性）
- 窗口打开改为类型化入口：`OpenWindowAsync<TWindow, TArgs>` / `UIRouter.PushAsync` / `PushForResultAsync`
- 删除 `UINames`、`poolableWindowNames`、`CallUI`、`GetFromArgs`、`SetWindow`
- 字符串版 `OpenWindow` / `OpenWindowAsync(string)` / `ToggleWindow` / `LoadWindow*` 收为 internal
- `OnOpen()` 与 `BeforeOpen(Action)` 不再带 `object[]`；`OnSet` 更名 `OnReopen()`

### Added
- `[UIWindow(Id, Layer, Modal, Pooled)]` 描述符 + `UIWindowCatalog`（启动预热、零反射查询）
- `UIRouter`：导航栈、ESC 返回、`WindowResult<T>` 结果回传、模态阻断（可选变暗）
- 窗口生命周期状态机 `UIWindowState`（非法转换告警）
- `SendWindowArgs/SendWindowArgsAsync` 命令通道与 `TerminalWindowArgs`
- 生成器 `CreateUIPlane` 自动写 `[UIWindow("Id")]`，新窗口默认声明式

## [1.2.0] - 2026-10-25

### Added
- `ModulePresets.Full / Minimal / Without<T> / Of`：模块清单从「背依赖表」变成选一行。
- `ModuleCatalog.ExpandDependencies`：启动时按 `[ModuleOrder(..., DependsOn)]` 自动补回缺失的被依赖模块，
  由 `BootstrapOptions.AutoExpandModuleDependencies`（默认开）控制。

### Changed

- Addressables 默认关闭：仓库不再带 `AddressableAssetsData`，`AddressablesSetup` 不再自动执行；
  只有显式启用（工具箱「Addressables」页 / 菜单）时才检查包安装并按需初始化。
- 菜单顶层按段位固化：工具箱 → 打包 → 构建 → 内容 → Addressables → 开发；`导出 unitypackage` 收进 `打包`。

## [1.1.0] - 2026-10-22

### Added
- 标准 UPM 包结构（package.json / README / CHANGELOG / LICENSE / Documentation~）。
- 运行时 / 完整 / OpenUPM 依赖三种导出 profile。

### Changed
- 编辑器工具通过 `ReunionPaths` 解析框架根，不再硬编码 `Assets/ReunionMovement`。
- 没有入口声明时 Bootstrap 不再隐式回退 `StartGame`；可用 `BootstrapOptions.Entry` 显式指定。
