# Third-Party Notices / 第三方组件声明

Gear 本体以 **Apache-2.0** 授权（见 `LICENSE`）。仓库内随附的第三方源码、二进制与美术资源
**不适用** Apache-2.0，各自保留原许可证。下表按仓库实际内置内容整理。

> 本文件用于满足再分发时的署名与许可告知义务，不构成法律意见。
> 对外发布（UPM / .unitypackage / 商业交付）前，请按目标平台与分发方式再次核对各组件条款，
> 尤其是标注为「需人工确认」的条目。

## 1. 运行时库

| 组件 | 版本 | 位置 | 上游 | 许可证 |
|---|---|---|---|---|
| UniTask | 2.5.11 | `Plugins/UniTask` | https://github.com/Cysharp/UniTask | MIT |
| R3 | 1.3.1 | `Plugins/R3`（DLL） | https://github.com/Cysharp/R3 | MIT |
| ZString | 2.6.0 | `Plugins/ZString`（DLL） | https://github.com/Cysharp/ZString | MIT |
| Newtonsoft.Json | 3.2.2（UPM 包，未内置） | `com.unity.nuget.newtonsoft-json` | https://github.com/JamesNK/Newtonsoft.Json | MIT |
| DOTween | 1.0.0.0（DLL，无版本号） | `Plugins/DOTween` | http://dotween.demigiant.com | **DOTween 自有许可证（非 Apache/MIT），再分发前需人工确认** |
| kcp2k | 随源码 | `Plugins/KCP/kcp2k` | https://github.com/vis2k/kcp2k | MIT（见 `LICENSE.md`） |
| Telepathy | 随源码 | `Plugins/Telepathy` | https://github.com/vis2k/Telepathy | MIT（见 `LICENSE.md`） |
| SimpleWebTransport | 随源码 | `Plugins/SimpleWebTransport` | https://github.com/vis2k/SimpleWebTransport | MIT（见 `LICENSE.md`） |
| WebGLSupport / WebGLInput | 随源码 | `3rd/WebGLSupport` | https://github.com/kou-yeung/WebGLInput | MIT（以仓库内声明为准） |

## 2. 编辑器 / 示例资源

| 组件 | 位置 | 许可证 / 说明 |
|---|---|---|
| UIEffect（mob-sakai） | `Utilities/UI/...`（按上游改写） | MIT |
| Joystick Pack | `3rd/Joystick Pack` | Unity Asset Store EULA（**不可单独再分发**） |
| Animated Loading Icons | `3rd/Animated Loading Icons` | Unity Asset Store EULA（**不可单独再分发**） |
| 示例图片 / GIF | `Images/` | 仅用于文档演示；第三方素材保留原权利 |

## 3. 字体

| 字体 | 位置 | 许可证 / 说明 |
|---|---|---|
| 思源黑体 / 思源宋体（Source Han Sans / Serif） | `Fonts/思源` | SIL Open Font License 1.1 |
| HarmonyOS Sans | `Fonts/HarmonyOS Sans` | HarmonyOS Sans 字体许可（华为） |
| 「只支持中文」合集内各字体 | `Fonts/只支持中文` | 各字体自有许可；**再分发前逐个确认** |

## 4. Unity 官方包

`com.unity.*` 依赖（Addressables、Input System、URP、uGUI、Test Framework 等）通过
`Packages/manifest.json` 声明，适用 Unity Companion License 或各包自带许可，
不随本仓库源码再分发（UPM 包仅声明依赖）。

## 5. 再分发检查清单

- [ ] 保留本文件与各组件目录下的 `LICENSE*` 原文。
- [ ] 确认 DOTween 的分发方式符合其许可证（商业项目通常需要 DOTween Pro 授权）。
- [ ] 从发行包中移除 `3rd/Joystick Pack`、`3rd/Animated Loading Icons` 等 Asset Store 资源，
      除非目标接收方已各自持有授权。
- [ ] 字体按 `Fonts/` 内各目录的许可单独确认；不确定时从 Runtime 包中排除（导出器已默认排除 `Fonts/`）。
- [ ] 在产品的关于页/文档中保留本声明与 Apache-2.0 许可文本。
