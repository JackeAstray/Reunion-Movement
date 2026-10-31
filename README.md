# Gear 齿轮

Unity 游戏框架 —— 纯 C# 生命周期、模块化系统、四传输网络层、Addressables 热更、SDF 形状 UI。

**Unity**：6000.3.10f1（Unity 6 / URP 17.3）<br>
**English**：[README.en.md](README.en.md)<br>
**定位**：重构版 LLAFramework。去掉历史包袱后从零搭建，保留经过验证的能力。

---

## 快速开始

1. 用 Unity Hub 打开 `Unity/Gear`。
2. 等首次导入完成（Package 解析 + 编译，Console 无红色报错）。
3. 打开场景 `Assets/Gear/Scenes/StartGame.unity`。
4. 按 Play。`Bootstrap` 会自动创建引擎、初始化 14 个模块、加载 `Temp` 场景并打开启动界面。
5. 想单独看某个功能：打开 `Assets/Gear/Scenes/Example/` 下对应场景再 Play（场景名命中测试规则时会跳过自动跳转）。

> 更细的步骤、地址见 [Docs/00-快速开始.md](Docs/00-快速开始.md)。
> 包内还有一份随框架分发的简明指南：`Assets/Gear/Documentation/Gear 使用指南.md`。

## 作为 UPM 包接入（一条 URL）

在目标工程的 `Packages/manifest.json` 里加一行（包发布在 `upm` 分支，不需要 `?path=`）：

```json
"com.jackeastray.gear": "https://github.com/JackeAstray/Gear.git#upm"
```

包标识固定为 **`com.jackeastray.gear`**（当前 `1.3.0`）。`upm` 分支是**发布产物**，每次发布重建为单个提交；
如果你安装到的包名是 `com.reunion.movement`，说明该分支还是重构前的旧包，请让维护者按
[UPM 打包指南](Docs/Packaging/UPM打包指南.md) 重新发布。

然后在自己的程序集里声明入口并选模块清单：

```csharp
[assembly: GearGameEntry(typeof(MyGameEntry))]

public class MyGameEntry : GameEntry
{
    // Full() 全量 14 个；Minimal() 只要资源 / Addressables / 场景 / UI
    public override IList<ICustomSystem> CreateModules() => ModulePresets.Full();
}
```

框架不会接管宿主启动（没有入口声明就报错并跳过）；删模块也不用背依赖表 ——
缺的提供者会按 `[ModuleOrder(..., DependsOn)]` 自动补回，`Bootstrap.Options.AutoExpandModuleDependencies` 可关。
发布/更新包：`powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -Export`（再加 `-Push` 推远端）。
详见 [Docs/Packaging/UPM打包指南.md](Docs/Packaging/UPM打包指南.md)。

## 目录地图

```
Assets/Gear/
├── Common/      最底层：日志、配置、枚举、单例基类        （无 asmdef 依赖）
├── Framework/
│   ├── Base/      只有接口与状态（Gear.Base）
│   ├── Runtime/   引擎与系统：Bootstrap、GameEngine、System/*  ← 从这里开始读
│   └── Modules/   运行时系统：Network/Http/Download/Timer/…
├── Utilities/   纯工具 + 可复用 UI 组件（ImageEx、LoopScrollRect…）
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

## 文档

全部文档的入口是 **[Docs/README.md](Docs/README.md)**（按"上手 → 指南 → 设计 → 专题 → 归档"分组）。

| 文档 | 内容 |
|---|---|
| [Docs/00-快速开始.md](Docs/00-快速开始.md) | 10 分钟跑起来，第一个改动 |
| [Docs/01-架构总览.md](Docs/01-架构总览.md) | 分层、启动时序、模块表 |
| [Docs/guides/工具箱.md](Docs/guides/工具箱.md) | 工具箱七个 Tab + 菜单结构 |
| [Docs/design/重构方案.md](Docs/design/重构方案.md) | 现状体检、问题清单、六阶段改造计划 |
| [Docs/design/便捷性与可移植性评估.md](Docs/design/便捷性与可移植性评估.md) | 调用 / 跨平台 / 插件便携性的现状与改造路线 |
| [Docs/Packaging/UPM打包指南.md](Docs/Packaging/UPM打包指南.md) | 导出 / 安装 UPM 包（运行时 / 完整） |
| [Docs/guides/跨平台构建指南.md](Docs/guides/跨平台构建指南.md) | 多平台构建、link.xml 与平台备注 |
| [Docs/guides/第三方依赖.md](Docs/guides/第三方依赖.md) | 第三方版本、冲突检测与二选一 |
| [Docs/features.md](Docs/features.md) | 完整特性清单与示例 GIF |

历史阶段记录（已完成，不再维护）在 [Docs/archive/](Docs/archive/)。

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

**Addressables 默认关闭**：仓库不带 `Assets/AddressableAssetsData`，运行时 `AddressablesMode.Off`、全程走 Resources（启动更快）。
需要热更时在工具箱 `Addressables` 页点「启用」，它才会检查包安装并创建分组/Label/Profile —— 详见 [Addressables 快速开始](Docs/Addressables/快速开始.md)。

> 「关闭」指**不启用热更流程**，不是可以不装包：`com.unity.addressables` 仍是硬依赖
> （`Gear.Framework.Runtime.asmdef` 引用了 `Unity.Addressables`）。不想装它需要自行裁剪程序集与相关代码。

## 平台支持

| 平台 | 状态 | 说明 |
|---|---|---|
| Windows (x64) | ✅ 已验证 | 本机构建 170.8 MB / 0 错误 |
| WebGL | ✅ 构建通过 | 本机构建 44.3 MB / 0 错误（00:12:55）；浏览器真机验证待做 |
| Android | ✅ 构建通过 | APK 76 MB / 0 错误（00:09:41）；`VIBRATE` 注入已确认；真机启动待验证 |
| iOS | 🟡 代码就绪 | 触觉 `.mm` 已补；Xcode 链接需 macOS |
| macOS / Linux | 🟡 脚本就绪 | 构建菜单 / 命令行已提供；未实跑 |

**Unity 版本**：仓库使用 `6000.3.10f1`。低于 2022.3 需要自行降级 URP 并处理 `FindObjectsByType` 等 API（未验证）。

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

Gear 本体见 [LICENSE](LICENSE)（Apache-2.0）。随仓库分发的第三方源码、DLL、字体与美术资源
保留各自许可，清单见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
对外发布前请按该清单确认 DOTween、Asset Store 资源与字体的再分发条款。
