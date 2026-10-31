# Changelog

## [1.3.0] - 2026-10-31

这一版集中在"把静默错误变成可观测的失败"，以及修正发布身份。

### Fixed

- **UPM 包身份**：导出器固定 `com.jackeastray.gear@1.3.0`，`package.json` 校验 name/version；
  `upm` 分支曾遗留 `com.reunion.movement@1.3.0` 旧包，发布脚本现在会拒绝推送错误包名。
- **网络重连分帧**：`NetworkClient.CreateChannel` 在重建连接前重置 `NetworkStreamAssembler`，
  避免旧连接的半帧与新流首字节拼帧（`LengthPrefixed` / `RawTcp` 下会静默错帧）。
- **通道被替换**：`NetworkClient.Tick` 检测 `channel.IsConnect`，同名通道被 `NetworkMgr` 替换后
  会收敛状态并触发重连，不再停在 Connected。
- **可靠重发**：`DrainReliableResendQueue` 改为 Peek → 发送成功 → Dequeue，重连补发失败不再丢消息。
- **RPC 失败语义**：新增保留消息 ID `0xFFF7` 与 `NetworkRpcException`；未鉴权/未注册/处理器异常
  回错误帧，客户端不再把"空响应"当成功。
- **握手主密钥缺失**：服务端在未配置主密钥时主动断开连接，不再留下"TCP 已连、握手永不完成"的半连接。
- **下载完整性**：成功落盘写 `<file>.done`（内容为字节数）；`DownloadMgr.skipIfExists` 只认
  "标记存在且大小一致"的文件，中断产生的截断文件会重新下载/续传。
  旧缓存没有标记时默认重下；确认完整时可临时开启 `DownloadMgr.treatLegacyFileAsComplete`
  一次性补写标记（见字段注释）。
- **HttpMgr 清理**：实现 `ISystemDisposable.Clear()`，引擎重启/模块移除时 Abort 在途与排队请求。
- **UI 状态机**：`Open→Open`（重复打开刷新）与 `Closed→Closed`（池化 `OnClose→OnDespawned`）
  改为合法转换，不再刷假告警。
- **ESC 与输入框**：文本输入聚焦时 `DispatchCancel` 直接消费，不再冒泡到 `UIRouter.Back()` 误关窗口。
- **返回键顺序**：`UIRouter.Back()` 优先关闭最上层模态，其次导航栈顶；`CanGoBack` 计入模态栈。
- **玩家槽位持久化**：跨会话只持久化稳定设备键；同型号无 serial 手柄的 `#dupN` 槽位只在本次会话内生效，
  并迁移/丢弃旧版本落盘的不稳定键。
- **死代码**：`UISystem.OnSet` 合并到"已打开窗口再次打开"的真实路径，`OnSetSubject` 恢复可触发；
  `[UIWindow(Static = true)]` 真正生效（关闭保留实例）。
- **UI Toolkit**：缺失 PanelSettings 时的兜底 `CreateInstance` 对象在 `Clear()` 中销毁，不再泄漏。

### Changed

- 巨型文件按 partial 拆分：`NetworkClient`（RPC）、`NetworkServer`（RPC）、`UWRExecutor`（完成标记）、
  `UIInputSystem`（玩家槽位）、`CarouselSelectionController`（导航）各自独立成文件，主文件回落到守卫基线以内。
- 新增 `Tools/SyntaxCheck`（Roslyn 语法门禁，不依赖 Unity），CI 在 PR 上无条件执行。
- 新增 `Tools/upm-smoke-test.ps1`：空工程 `file:` 安装 + 编译烟测，自托管 main 验证已接入。
- `NetworkClientConfig.enableRpcErrorFrames` / `NetworkServerConfig.enableRpcErrorFrames`：
  混合版本部署时服务端可关错误帧，回退"空响应"以兼容旧客户端。
- `DownloadMgr.treatLegacyFileAsComplete`：旧缓存无 `.done` 时可选一次性补标记，避免弱网用户被重下。
- 新增 `UI/Gear.UI.asmdef` 与 `Editor/Gear.Editor.Framework.asmdef`：UI 与编辑器工具不再落
  `Assembly-CSharp` / `Assembly-CSharp-Editor`，架构守卫能看到这两层依赖。
- 发布：`upm` 已推送为 `com.jackeastray.gear@1.3.0`；历史已重写并把 GIF/`.unitypackage`
  迁入 LFS（23 个对象）。旧包备份标签 `upm-backup-20261031`；协作者需要重新 clone。

### Added

- `NetworkRpcFrames.EncodeError / TryDecodeError`、`NetworkRpcException` 与对应测试。
- `UWRExecutor.HasCompletedDownload` / `MarkCompleted` 与 `DownloadIntegrityTests`。
- `UIWindowCatalogTests`、`NetworkCodecTests` 的分帧重置 / 保留 ID / RPC 错误帧用例。
- `UIModalStack`（纯 C# 模态栈）与 `UIModalStackTests`；`NetworkClient.IsChannelLost` 与
  `NetworkClientLivenessTests`。
- 根目录 `THIRD-PARTY-NOTICES.md`，并随 UPM 包导出为 `Third Party Notices.md`。
- `Docs/design/S-验收清单.md`：S 的验收标准、已通过命令与剩余阻塞项。

### Docs

- 修正守卫数量（6 条 + link.xml）、测试数量、asmdef 数量、`.git` 体积等过期数字。
- 修正 `Docs/03` 的池化条件（AND 而非 OR）、`panelSize` 未实现的承诺、ESC 行为说明。
- UPM 指南新增"发布前检查"与包身份核对步骤。

## [1.2.0] - 2026-10-25

### Added

- `ModulePresets.Full / Minimal / Without<T> / Of`：模块清单从「背依赖表」变成选一行。
- `ModuleCatalog.ExpandDependencies`：启动时按 `[ModuleOrder(..., DependsOn)]` 自动补回缺失的被依赖模块。

### Changed

- Addressables 默认关闭：仓库不再带 `Assets/AddressableAssetsData`，只有显式启用时才初始化。
- 菜单顶层按段位固化：工具箱 → 打包 → 构建 → 内容 → Addressables → 开发。

## [1.1.0] - 2026-10-22

### Added

- 标准 UPM 包结构（package.json / README / CHANGELOG / LICENSE / Documentation~）。
- 运行时 / 完整 / OpenUPM 依赖三种导出 profile。

### Changed

- 编辑器工具通过 `GearPaths` 解析框架根，不再硬编码 `Assets/Gear`。
- 没有入口声明时 Bootstrap 不再隐式回退 `StartGame`；可用 `BootstrapOptions.Entry` 显式指定。
