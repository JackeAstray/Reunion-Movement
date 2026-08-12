# Addressables 集成文档（ReunionMovement）

> 本文档集是 ReunionMovement 中 Addressables 集成的**唯一权威说明**。所有文档均为中文，按用途组织，建议按"阅读顺序"通读一遍后按需查阅。

## 文档地图

| # | 文档 | 用途 | 阅读时机 |
| --- | --- | --- | --- |
| 1 | [Addressables集成设计方案.md](./Addressables集成设计方案.md) | 总体架构、分组策略、运行时设计、迁移路线、风险 | 第一次接入前（决策依据） |
| 2 | [快速开始.md](./快速开始.md) | 依赖、自动化配置、第一次跑通、代码示例 | 首次上手使用 |
| 3 | [本地测试指南.md](./本地测试指南.md) | 无 CDN 的本地验证（Editor / Bundle 两种模式） | 日常开发验证 |
| 4 | [远程部署指南.md](./远程部署指南.md) | CDN 部署、全平台打包、WebGL 服务器配置、版本更新 | 上线 / 发版时 |

> 打包分发（导出 .unitypackage、接收方使用步骤）见独立文档集 [Docs/Packaging/打包指南.md](../Packaging/打包指南.md)。

## 阅读顺序

```
首次接入：设计方案(1) → 快速开始(2)
日常开发：本地测试(3) → 快速开始(2)
上线发版：远程部署(4) → 打包分发(见 Docs/Packaging)
```

## 当前实现状态（2026-08-12）

| 模块 | 状态 | 说明 |
| --- | --- | --- |
| `AddressableSystem`（运行时封装） | ✅ 已实现 | 加载/实例化/场景/释放/降级/远程更新检查/缓存清理 |
| `AddressableKeys`（地址常量） | ✅ 已实现 | `BuiltIn/UI/...` 前缀 + Label 约定 |
| `Config`/`GameConfig` 配置 | ✅ 已实现 | `enableAddressables` / `addressablesMode` / 远程 URL |
| `AddressablesSetup`（自动配置） | ✅ 已实现 | `[InitializeOnLoad]` 自动建分组/Label/Profile，校验并修正 Remote 分组路径变量，默认激活 `DevLocal`；远程 Catalog **默认关闭**（`ReunionMovement → Addressables → 启用远程 Catalog（Phase 3 热更）` 手动开启） |
| `AddressablesMigrator`（资源迁移） | ✅ 已实现 | 复制+GUID 重映射+导入设置+Addressable 标记（需在 Unity 手动点菜单） |
| `AddressablesBuildWindow`（构建） | ✅ 已实现 | 当前平台构建 + 全平台子菜单切换 + version.json |
| `AddressablesCdnUploader`（自动上传） | ✅ 已实现 | 阿里云 OSS HTTP PUT + 签名，增量上传 `.bundle`+远程 catalog（`catalog_*.bin`/`.hash`，独立菜单触发，需配置 AK/SK） |
| `ReunionMovementPackageExporter`（打包） | ✅ 已实现 | 一键导出 .unitypackage（独立于 Addressables，见 `Docs/Packaging/打包指南.md`） |
| UI 双轨加载（`LoadWindowAsync`） | ✅ 已实现 | Addressables 优先 → Resources 降级 |
| SoundSystem 双轨加载（`GetAudioClipAsync`） | ✅ 已实现 | Addressables 优先（`Remote/Sounds/...`）→ Resources 降级，按来源释放 |
| SceneSystem 双轨加载（`LoadScene`） | ✅ 已实现 | Addressable 场景优先（`Remote/Scenes/...`）→ SceneManager 降级，切换释放旧场景 |
| 远程 URL 运行时重写 | ✅ 已实现 | `remoteBundleUrl`/`remoteCatalogUrl` 在 Remote 模式覆盖构建烘焙地址（`InternalIdTransformFunc`），同构建产物可部署任意 CDN |
| 远程 CDN 实测 | ⏳ 待验证 | 代码就绪，无 CDN，LocalOnly 本地可用 |
| UI 资源实际迁移 | ✅ 已迁移 | `BuiltIn_UI`：StartGameUIPlane / PopupUIPlane / TerminalUIPlane（含 Logo/材质/Shader 依赖） |
| 音频/纹理资源迁移 | ⏳ 待执行 | 需在 Unity 点「迁移/音频」「迁移/图片」菜单（`Remote_Sounds`/`Remote_Textures` 当前为空） |

## 关键术语速查

- **address（地址）**：运行时加载 key，如 `BuiltIn/UI/StartGameUIPlane`
- **分组（Group）**：`BuiltIn_*`（内置）/ `Remote_*`（远程可热更）
- **Label**：语义标签（`builtin`/`remote`/`ui`/`sound`/`texture`/`scene`/`data`），用于批量预载/清理
- **Profile**：`DevLocal`（本地模拟）/ `Publish`（CDN）
- **Play Mode Script**：Editor 内加载模式（Use Asset Database / Simulate / Use Existing Build）
