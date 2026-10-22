# Reunion-Movement 整合运动

Unity 游戏框架 —— 纯 C# 生命周期、模块化系统、四传输网络层、Addressables 热更、SDF 形状 UI。

**Unity**：6000.3.10f1（Unity 6 / URP 17.3）<br>
**定位**：重构版 LLAFramework。去掉历史包袱后从零搭建，保留经过验证的能力。

---

## 快速开始

1. 用 Unity Hub 打开 `Unity/ReunionMovement`。
2. 等首次导入完成（Package 解析 + 编译，Console 无红色报错）。
3. 打开场景 `Assets/ReunionMovement/Scenes/StartGame.unity`。
4. 按 Play。`Bootstrap` 会自动创建引擎、初始化 14 个模块、加载 `Temp` 场景并打开启动界面。
5. 想单独看某个功能：打开 `Assets/ReunionMovement/Scenes/Example/` 下对应场景再 Play（场景名命中测试规则时会跳过自动跳转）。

> 更细的步骤、地址见 [Docs/00-快速开始.md](Docs/00-快速开始.md)。
> 包内还有一份随框架分发的简明指南：`Assets/ReunionMovement/Documentation/ReunionMovement 使用指南.md`。

## 目录地图

```
Assets/ReunionMovement/
├── Common/      最底层：日志、配置、枚举、单例基类        （无 asmdef 依赖）
├── Core/        引擎与系统：Bootstrap、GameEngine、System/*  ← 从这里开始读
├── Utils/       运行时系统 + 通用工具 + 可复用 UI 组件
├── UI/          项目自带的界面逻辑（示例 / 业务混合）
├── Editor/      编辑器工具：导表、打包、Addressables 流水线、工具箱
├── Plugins/     第三方：UniTask、R3、DOTween、ZString、kcp2k、Telepathy、SimpleWebTransport
├── 3rd/         外部素材与插件：Joystick Pack、WebGLSupport、Animated Loading Icons
├── Scenes/      启动 / 加载 / 业务场景 + Example 示例场景
├── Resources/   运行时资源（材质、预制体、音频、着色器、UI）
├── Game/        示例入口声明（Assembly-CSharp，不进 UPM / .unitypackage）
└── GenerateScript/  代码生成产物（UI 面板、配置数据）
```

每个代码目录下都有一份 `README.md`，写明"放什么 / 不放什么"。

## 架构与重构

| 文档 | 内容 |
|---|---|
| [Docs/00-快速开始.md](Docs/00-快速开始.md) | 10 分钟跑起来，第一个改动 |
| [Docs/01-架构总览.md](Docs/01-架构总览.md) | 分层、启动时序、模块表 |
| [Docs/重构方案.md](Docs/重构方案.md) | 现状体检、问题清单、六阶段改造计划 |
| [Docs/便捷性与可移植性评估.md](Docs/便捷性与可移植性评估.md) | 调用 / 跨平台 / 插件便携性的现状与改造路线 |
| [Docs/Packaging/UPM打包指南.md](Docs/Packaging/UPM打包指南.md) | 导出 / 安装 UPM 包（运行时 / 完整） |
| [Docs/跨平台构建指南.md](Docs/跨平台构建指南.md) | 多平台构建、link.xml 与平台备注 |
| [Docs/第三方依赖.md](Docs/第三方依赖.md) | 第三方版本、冲突检测与二选一 |
| [Docs/features.md](Docs/features.md) | 完整特性清单与示例 GIF |

## 依赖

| 依赖 | 版本 | 用途 |
|---|---|---|
| com.unity.inputsystem | 1.18.0 | 新输入系统（键盘 / 手柄 / UI 导航） |
| com.unity.addressables | 2.9.1 | 资源远程更新与内存管理 |
| com.unity.render-pipelines.universal | 17.3.0 | URP 渲染管线 |
| com.unity.ugui | 2.0.0 | uGUI |
| UniTask | Plugins | 高性能异步 / 协程替代 |
| R3 | Plugins | 响应式编程（事件 / Subject） |
| ZString | Plugins | 零分配字符串格式化 |
| DOTween | Plugins | 补间动画 |
| kcp2k / Telepathy / SimpleWebTransport | Plugins | KCP / TCP / WebSocket 网络传输 |

**必开**：TextMesh Pro、New Input System（Project Settings → Player → Active Input Handling 需包含 Input System Package）。

**低版本兼容**：低于 2022.3 的版本在示例中 TextMesh Pro 可能报错，需重新生成 Font Asset（不影响框架逻辑）；更低版本（如 2019）可能需要手动解决少量语法差异。

## 特性

零分配日志、响应式事件总线、统一生命周期引擎、四传输网络层、断点续传、热更完整性保护、SDF 形状 UI、循环滚动列表、UI Toolkit 面板、WebGL 体积优化、崩溃上报、通用存档、性能监控、对象池、暂停系统、轮播选择……

完整清单见 [Docs/features.md](Docs/features.md)。

## 感谢

感谢 [Shadertoy](https://www.shadertoy.com/) 上的作者所做的贡献，以及以下开源项目：

- [mob-sakai/UIEffect](https://github.com/mob-sakai/UIEffect)
- [scanfing/HttpFileServer](https://github.com/scanfing/HttpFileServer)
- [Cysharp/UniTask](https://github.com/Cysharp/UniTask)
- [Cysharp/R3](https://github.com/Cysharp/R3)

## 许可

见 [LICENSE](LICENSE)。
