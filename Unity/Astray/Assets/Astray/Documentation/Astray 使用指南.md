# Astray 使用指南

本文件随 `Assets/Astray` 一起分发。源码仓库中的 `Docs/` 包含更完整的专题文档。

## 打开工具箱

Unity 菜单：`Astray → 工具箱`。

打开窗口只读取状态，不自动安装 Package、创建配置、迁移、构建或上传。菜单按钮在编译、导入、Player 构建和 Play Mode 下拒绝执行；延迟执行前会再次检查，不会自动重试。

## 最快运行

1. 处理工具箱中的必需项错误；
2. 定位 `GameConfig`；缺失时点击“创建 GameConfig…”并确认，已有资产不会被覆盖；
3. 保持 `enableHotUpdate` 关闭；
4. 下拉选择示例，点击“打开所选场景”，按提示保存当前场景；
5. 进入 Play Mode。

此路径使用 Resources，不要求 CDN 或 Addressables 迁移。GameConfig 缺失时框架使用静态默认值，但无法在 Inspector 调整配置。

## Addressables 与远程资源

1. 初始化或补齐配置：创建/补齐分组、Label、Profile；
2. 迁移全部资源：处理 UI、音频、图片、配置、预制体、场景和字体；不会删除源 Resources；
3. 构建当前平台的 Content；
4. 核实 CDN、Profile、Catalog 和 OSS 配置后再上传。

检查读取当前注册的 Settings，支持自定义位置，并核对预期的 9 个分组、7 个 Label 及 DevLocal / Publish 名称。名称齐全不代表 URL、Schema 路径或构建内容已验证。

注意：“一键流水线”固定构建 **WebGL**，可能切换平台、启用远程 Catalog，并在 OSS 凭据已配置时上传到外部服务。工具箱会在执行前确认。只需本地内容时请使用分步入口。

## 扩展自己的模块

工具箱“扩展模块”区提供“复制模块模板”和“定位注册入口”。

1. 复制随包模板 [CustomSystem.cs.txt](CustomSystem.cs.txt)，保存为插件 `Core/System/CustomSystem/CustomSystem.cs`；模板是纯 C# 模块，不是 MonoBehaviour。
2. 保持默认命名空间 `Astray.Core.Custom`，或重命名类并同步修改单例字段、构造函数和注册代码。
3. 在 `Core/StartGame.cs` 添加 `using Astray.Core.Custom;`。
4. 在 `CreateModules()` 的 `return modules;` 前添加 `modules.Add(CustomSystem.Instance);`。
5. 如新增一个模块，可将列表初始容量从 14 改为 15（不是功能必需）。

模块必须实现 `InitProgress`（0–100）和 `UniTask Init()`；Update / Clear 是可选接口。模板包含三者，按需删除不需要的接口及方法。Init 完成前把进度设为 100，Clear 中解除订阅、取消任务并释放资源。

列表顺序决定初始化顺序。依赖模块必须先注册，ResourcesSystem 保持最前。工具箱不会自动改注册列表，也不会创建运行时模块实例。

推荐目录沿用 `Astray.Core` 程序集，无需新 asmdef。不要直接保存到无 asmdef 的普通 Assets 目录：那会进入 Assembly-CSharp，而 Core 无法引用它。也不要随意拆出一个反向依赖 Core 的程序集再让 Core 引用它，以免循环引用。

## 常见问题

- Package 缺失：先通过 Package Manager 安装所需 Addressables / Input System，处理 Console 编译错误，再刷新。
- 配置检查暂不可用：等待包解析、资产导入和编译完成后刷新。
- 菜单无法执行：先处理脚本错误并确认依赖安装完整；工具箱独立程序集不保证其他工具也能编译。
- 文档按钮打开本文件：当前项目只有随包简明指南；完整专题文档位于源码仓库 `Docs/`。
