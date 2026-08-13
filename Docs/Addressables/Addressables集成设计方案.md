# Addressables 集成设计方案（ReunionMovement）

> 说明：本文档为设计交付物。资源数量等为 2026-08-11 工作区实测数据；凡标有【估算】的数值（如 Bundle 体积、迁移工时）均为工程估算值，落地前需以实际构建结果校准。

## 0. 文档信息

| 项目 | 内容 |
| --- | --- |
| 目标项目 | ReunionMovement（Unity WebGL 为主，兼容移动端 / PC） |
| 目标平台 | WebGL（Build/index.html 已有输出） |
| 相关版本 | Addressables 2.9.1（已安装）、UniTask.Addressables（已随 UniTask 提供） |
| 现状 | Addressables 包已装但从未配置（无 AddressableAssetsData 目录） |
| 涉及系统 | ResourcesSystem、StartGame、Config/GameConfig、UISystem、SoundSystem、SceneSystem、UIToolkitSystem、DownloadMgr |

---

## 1. 现状分析

### 1.1 已具备的基础
- `com.unity.addressables@2.9.1` 已在 `Packages/manifest.json` 中声明。
- `UniTask` 的 `Runtime/External/Addressables/AddressablesAsyncExtensions.cs` 已存在（`UNITASK_ADDRESSABLES_SUPPORT` 宏启用），运行时可用 `handle.ToUniTask()` 等扩展。
- `ResourcesSystem.cs` 已含一个 `#region Addressables 集成`，提供 4 个基础方法：
  - `LoadAddressableAsync<T>(string key)`
  - `InstantiateAddressableAsync(string key, Transform parent)`
  - `ReleaseAddressableInstance(GameObject)`
  - `ReleaseAddressableAsset<T>(T)`
- 模块化架构：`ICustomSystem` + `GameEntry.CreateModules()` 按序注册，`ResourcesSystem` 位于索引 0（最高依赖）。

### 1.2 缺失项（本次设计要补齐的）
| 缺失项 | 说明 |
| --- | --- |
| 配置数据 | 无 `AddressableAssetsData`，无分组、无 Profile、无构建脚本 |
| 统一地址管理 | 现有封装直接收 `string key`，无常量类 / Label 约定，易出现魔法字符串 |
| 运行时系统 | 无独立的 AddressableSystem（加载进度、取消、降级、缓存清理、更新检查） |
| 远程更新 | 未打通 WebGL CDN 部署、catalog 版本对比、增量下载 |
| 迁移路线 | Resources 与 Addressables 双轨并存的过渡方案未定义 |
| 构建/发布工具 | 无 Editor 一键构建、无部署清单生成 |

### 1.3 现有资源规模（实测）
`Assets/ReunionMovement/Resources/` 下共约 139 个资源文件：

| 类型 | 数量 | 建议归属 |
| --- | --- | --- |
| .prefab | 17 | BuiltIn（核心逻辑）/ Remote（可选） |
| .png | 52 | Remote（图片/图集，WebGL 首包体积大头） |
| .mat | 21 | 随依赖自动打入所属 Bundle |
| .asset | 5 | BuiltIn（GameConfig 等配置） |
| .shader | 19 | BuiltIn（Shader 建议常驻内置） |
| .ogg | 24 | Remote（音频体积大，适合热更） |
| .wav | 1 | Remote |

另有 `ResourcesFile/Texture/`（远程下载用的纹理资源）与现有 `DownloadMgr`（HTTP 动态下载），二者职责与 Addressables 不同，见 §7.5 职责边界。

---

## 2. 设计目标与原则

1. **统一资源入口**：游戏代码只面对 `AddressableSystem` 一个异步资源入口，屏蔽底层 Addressables/Resources 差异。
2. **WebGL 远程更新**：大体积资源（音频、图片、场景、数值）走 Remote Bundle，支持启动时版本检查与按需/整包下载。
3. **渐进式迁移**：不推倒现有 Resources 体系，按阶段双轨并存，最终收口到 Addressables；`ResourcesSystem` 保留为降级兜底。
4. **与现有风格一致**：沿用 `ICustomSystem` 注册、UniTask 异步、`Log.*` 日志、中文注释、`Config` 配置。
5. **生命周期闭环**：借鉴既有 Code Review 结论，所有加载必须可取消、可释放、可统计，杜绝泄漏。

---

## 3. 总体架构

```
┌─────────────────────────────────────────────────────────────┐
│                       业务层（UI / Sound / Scene / 逻辑）       │
│   UISystem   SoundSystem   SceneSystem   UIToolkitSystem ... │
└──────────────────────────────┬──────────────────────────────┘
                               │ UniTask 异步
┌──────────────────────────────▼──────────────────────────────┐
│              AddressableSystem（新增 ICustomSystem）           │
│  LoadAsset / Instantiate / LoadScene / Release / CheckUpdate │
│  缓存策略 · 进度 · 取消 · 降级(→Resources) · 版本检查 · 日志     │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│            Addressables（引擎层，自带引用计数/依赖分析）          │
│        Local Bundles（内置）     Remote Bundles（远程 CDN）      │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│          资源仓库：Resources/(待迁)  AddressableAssets/        │
│          Build/WebGL 输出  ·  CDN 服务器（Nginx 同域）          │
└─────────────────────────────────────────────────────────────┘
```

### 3.1 系统职责边界（与 ResourcesSystem 划分）
| 系统 | 职责 | 不负责 |
| --- | --- | --- |
| `AddressableSystem`（新） | 受管资源的异步加载/实例化/释放、远程更新、版本检查、Bundle 缓存清理 | Resources 同步加载、引用计数缓存 |
| `ResourcesSystem`（现有） | Resources 目录同步/异步加载 + 引用计数缓存、图集缓存 | Addressables 生命周期（Addressables 自管） |
| `DownloadMgr`（现有） | 运行时动态 URL 内容（外部图片、用户上传文件） | 构建期受管资源 |

> 现有 `ResourcesSystem` 中 4 个 Addressables 方法将**迁移**到 `AddressableSystem`，`ResourcesSystem` 保持纯 Resources 职责（含降级兜底）。

---

## 4. 目录与分组设计

### 4.1 物理目录约定（新建）
```
Assets/
└── AddressableAssets/                    # 新增：只放 Addressable 资源（不放进 Resources）
    ├── BuiltIn/
    │   ├── UI/            # 核心 UI Prefab
    │   ├── Prefabs/       # 对象池/特效/相机等核心 Prefab
    │   ├── Fonts/         # 字体
    │   └── ScriptableObjects/  # 配置类
    └── Remote/
        ├── Sounds/        # ogg/wav
        ├── Textures/      # png/图集
        ├── Scenes/        # 可热更场景
        └── AutoDatabase/  # JSON 数值
```
> 迁移策略：资源**复制**到上述目录再标记 Addressable，而不是原地把 Resources 下的资源标记为 Addressable（避免 Addressables 构建把 Resources 也打进包、且便于将来直接删除 Resources 目录）。

### 4.2 分组（AssetGroup）设计
| 分组名 | 包含内容 | 部署 | 构建路径 | 理由 |
| --- | --- | --- | --- | --- |
| `BuiltIn_Config` | GameConfig、初始化配置 | Local | 内置 | 启动必需，缺了游戏起不来 |
| `BuiltIn_UI` | 核心 UI Prefab、首屏 | Local | 内置 | 首屏体验，必须秒开 |
| `BuiltIn_Prefabs` | 对象池、特效、相机、终端 | Local | 内置 | 逻辑强依赖，避免网络等待 |
| `BuiltIn_Fonts` | 字体 | Local | 内置 | 体积极小，避免字体闪烁 |
| `BuiltIn_Shaders` | Shader | Local | 内置 | Shader 建议内置防变体丢失 |
| `Remote_Sounds` | 24×ogg + 1×wav | Remote | CDN | 体积大头，可热更 |
| `Remote_Textures` | 52×png、图集 | Remote | CDN | 体积大头，可热更 |
| `Remote_Scenes` | 游戏场景 | Remote | CDN | 场景级热更 |
| `Remote_AutoDatabase` | JSON 数值表 | Remote | CDN | 数值/活动热更 |

> 分组顺序即构建顺序：`BuiltIn_*` 在前、`Remote_*` 在后。依赖资源（mat、shader 等）由 Addressables 自动分析打进所属 Bundle，无需手动归类。

### 4.3 Label 约定
- 每个分组自动带同名 Label（`BuiltIn_UI`、`Remote_Sounds`…）。
- 附加语义 Label：`builtin` / `remote`（部署策略）、`ui` / `sound` / `texture` / `scene` / `data`（功能）。
- 查询约定：加载单个资源用 **key（路径或 AssetReference）**；批量预载/清理用 **Label**。

---

## 5. 运行时模块设计：`AddressableSystem`

### 5.1 注册位置
`StartGame.CreateModules()`：插入到 `ResourcesSystem`（索引 0）之后、`SceneSystem`（索引 1）之前：

```
modules.Add(ResourcesSystem.Instance);   // 0: 资源（同步/兜底）
modules.Add(AddressableSystem.Instance); // 1: Addressables（受管异步/远程）【新增】
modules.Add(SceneSystem.Instance);       // 2: 场景管理（原 1，顺延）
...
```

### 5.2 类骨架与 API
```csharp
namespace ReunionMovement.Core.Resources
{
    /// <summary>
    /// Addressables 统一封装：受管资源异步加载/实例化/释放 + 远程更新。
    /// 所有 API 均基于 UniTask，支持进度与取消；失败可降级到 ResourcesSystem。
    /// </summary>
    public class AddressableSystem : ICustomSystem, ISystemDisposable
    {
        public static AddressableSystem Instance { get; }  // Lazy 单例，风格同 ResourcesSystem

        // 初始化：Addressables.InitializeAsync + 可选启动预载 + 版本检查（Remote 模式）
        public UniTask Init();
        public UniTask InitAsync(IProgress<float> progress = null, CancellationToken ct = default);

        // 基础加载 / 释放
        public UniTask<T> LoadAssetAsync<T>(object key,
            IProgress<float> progress = null, CancellationToken ct = default) where T : Object;
        public UniTask<GameObject> InstantiateAsync(object key, Transform parent = null,
            IProgress<float> progress = null, CancellationToken ct = default);
        public void ReleaseAsset<T>(T asset) where T : Object;
        public void ReleaseInstance(GameObject instance);

        // 降级加载：Addressables 失败 → Resources.Load（需在 Config 开启）
        public UniTask<T> LoadWithFallbackAsync<T>(string addrKey, string resourcePath,
            IProgress<float> progress = null, CancellationToken ct = default) where T : Object;

        // 场景
        public UniTask<SceneInstance> LoadSceneAsync(object key, LoadSceneMode mode = LoadSceneMode.Single,
            IProgress<float> progress = null, CancellationToken ct = default);

        // 远程更新（Remote 模式才有效）
        public UniTask<UpdateResult> CheckUpdateAsync(CancellationToken ct = default);
        public UniTask UpdateContentAsync(UpdateResult result, IProgress<float> progress = null,
            CancellationToken ct = default);

        // 缓存 / 内存
        public void CleanBundleCache();      // Addressables.CleanBundleCache
        public UniTask CleanUnusedAsync();   // Resources.UnloadUnusedAssets 合并调用
        public void Clear();                 // 释放句柄 + 缓存清理（ISystemDisposable）

        // 统计与诊断
        public string GetDebugStats();       // 句柄数 / 缓存大小 / 下载进度
    }
}
```

### 5.3 关键实现要点
1. **句柄管理**：内部维护 `Dictionary<object, AsyncOperationHandle>` 活跃句柄表，`Release` 时校验句柄有效性（`IsValid()`）再 `Release`，避免双重释放 / 释放无效句柄。
2. **进度与取消**：全部基于 `UniTask.Addressables` 扩展 `handle.ToUniTask(progress, cancellationToken)`；取消时统一走 `Release` 收尾（防止取消后句柄泄漏）。
3. **降级策略**：`LoadWithFallbackAsync` 先试 Addressables；失败（Status != Succeeded 或 Key 不存在）时回退 `ResourcesSystem.LoadAsync` 并记录 `Log.Warning`。降级开关由 `Config` 控制（默认开启，便于双轨期平滑过渡）。
4. **引用计数**：Addressables 自带引用计数，**不再套用** `ResourcesSystem` 的 resourceTable/resourceRefCount，避免双重计数。
5. **远程更新流程**：
   ```
   InitAsync → Addressables.InitializeAsync
             → CheckUpdateAsync: 对比本地 CatalogHash 与远程 CatalogHash
             → 有更新: UpdateContentAsync 下载远程 Catalog + Bundle（带进度）
             → 完成: Addressables 自动切换到新 Catalog
   ```
   WebGL 下 Bundle 走 IndexedDB 缓存，二次启动命中缓存免下载。
6. **错误处理**：统一 `try/catch` + `Log.Error("Addressables {op} 失败: {key}, {ex}")`，回调/返回值语义与现有 `ResourcesSystem` 保持一致（失败返回 null / 抛异常）。

### 5.4 地址管理：`AddressableKeys`
新建静态常量类，消除魔法字符串（与 `Config` 的 const 风格一致）：

```csharp
namespace ReunionMovement.Core.Resources
{
    public static class AddressableKeys
    {
        // UI（路径与 Config.UIPath 对齐，未来 GameConfig 可覆盖前缀）
        public const string UIRoot         = "BuiltIn/UI/";
        public const string UIStartGame    = UIRoot + "StartGamePanel";
        // Prefabs
        public const string PrefabEnemy    = "BuiltIn/Prefabs/Enemy";
        public const string PrefabEffect   = "BuiltIn/Prefabs/Effect";
        // 场景
        public const string SceneGame      = "Remote/Scenes/GameScene";
        // Label
        public const string LabelRemote    = "remote";
        public const string LabelBuiltIn   = "builtin";
        public const string LabelUI        = "ui";
        public const string LabelSound     = "sound";
        public const string LabelTexture   = "texture";
    }
}
```

---

## 6. 构建与部署设计

### 6.1 Profile（构建配置）
| Profile 名 | Local.LoadPath | Remote.LoadPath |
| --- | --- | --- |
| `DevLocal`（**默认激活**） | `{UnityEngine.AddressableAssets.Addressables.BuildPath}` | `http://localhost:8080/StreamingAssets` |
| `Publish` | `{UnityEngine.AddressableAssets.Addressables.BuildPath}` | `https://cdn.example.com/reunion/{version}/` |

- 默认激活 `DevLocal`（`AddressablesSetup` 自动切换），避免停留在 `Default`（其 `Remote.LoadPath` 为 `<undefined>`）。
- **远程 Catalog 默认关闭**（`m_BuildRemoteCatalog=false`）：`AddressablesSetup` 不主动开启；Phase 3 部署 CDN 前执行菜单 `ReunionMovement → Addressables → 配置/启用远程 Catalog（Phase 3 热更）` 手动开启（自动把 `RemoteCatalogBuildPath/LoadPath` 指向 Remote 变量）。
- `AddressablesSetup` 对已存在的 Remote 分组会**校验并修正** BuildPath/LoadPath 指向 Remote 变量（修复历史/手动创建分组遗留的 Local 路径错误）。
- `Remote.LoadPath` 的 base URL + `{version}` 写入 `GameConfig`（`remoteBundleUrl`、`remoteCatalogUrl`），构建时由 Editor 工具回填，避免硬编码。
- 移动端/PC 的 Remote Bundle 走 `Caching`；WebGL 走浏览器 IndexedDB。

### 6.2 Editor 工具（新增 `Assets/ReunionMovement/Editor/Addressables/`）
| 脚本 | 职责 |
| --- | --- |
| `AddressablesSetup.cs` | 一键创建 `AddressableAssetsData`、分组、Label、Profile；`[InitializeOnLoad]` 检测缺失时自动建；已存在分组也校验/修正 Remote 路径；默认激活 `DevLocal`；提供「配置/启用远程 Catalog」菜单（默认关闭） |
| `AddressablesBuildWindow.cs` | 菜单 `ReunionMovement/Addressables/一键构建`：构建 Addressables → 输出 catalog hash → 写版本清单 `Build/Addressables/version.json` |
| `AddressablesDeployReport.cs` | 生成部署清单（哪些 Bundle 需上传 CDN、md5、大小），供 CI/人工上传 |

### 6.3 WebGL 部署清单
1. `Build/` 为 WebGL 产物，`StreamingAssets` 内为内置 Bundle。
2. Remote Bundle 上传到 CDN（与 `index.html` **同域**，避免 CORS 与跨域 Worker 限制；若跨域需配 `Access-Control-Allow-Origin` + `Access-Control-Allow-Headers`）。
3. 启动时 `CheckUpdateAsync` 对比 catalog，命中新版本才下载，非全量覆盖。
4. 服务器（Nginx）需支持：`Content-Type: application/octet-stream`、缓存头（Bundle 带 hash 文件名，可强缓存）。

---

## 7. 与现有系统集成改造点

| 系统/文件 | 改造点 | 工作量 |
| --- | --- | --- |
| `Core/System/ResourcesSystem/ResourcesSystem.cs` | 删除 `#region Addressables 集成`（4 方法迁至 AddressableSystem），保留 Resources 职责 | 小 |
| `Core/StartGame.cs` | `CreateModules()` 插入 `AddressableSystem`；`OnGameStartAsync` 前加 `AddressableSystem.InitAsync` | 小 |
| `Common/Config/Config.cs` + `Core/GameConfig.cs` | 新增 `enableAddressables`、`remoteBundleUrl`、`remoteCatalogUrl`、`addressablesMode(Off/LocalOnly/Remote)` | 小 |
| `Core/System/UISystem/UISystem.cs` | UI Prefab 加载走 `AddressableSystem.LoadAssetAsync`（`Config.UIPath` 前缀），失败降级 Resources | 中 |
| `Core/System/SoundSystem/` | 音频加载走 Addressables（Remote_Sounds），预载常用音效 | 中 |
| `Core/System/SceneSystem/` | `LoadScene` 增加 Addressable 模式（Remote_Scenes），保留原有加载路径 | 中 |
| `Core/System/UIToolkitSystem/` | UXML/USS 走 Addressables（可选，晚一期） | 小 |
| `Utils/Download/DownloadMgr.cs` | **不改**——职责边界保持（见 §7.5） | 无 |
| `Utils/ObjectPool/` | 池资源源改为 `AddressableSystem.InstantiateAsync` | 小 |

### 7.1 UISystem 改造示意
```csharp
// 改造前
var prefab = ResourcesSystem.Instance.Load<GameObject>(Config.UIPath + uiName);
// 改造后（双轨：Addressable 优先，Resources 兜底）
var prefab = await AddressableSystem.Instance.LoadWithFallbackAsync<GameObject>(
    AddressableKeys.UIRoot + uiName, Config.UIPath + uiName);
```

### 7.2 对象池接入
`ObjectPool` 的源对象改为 Addressable 实例化，池回收改为 `AddressableSystem.ReleaseInstance`（注意：池复用与 Addressables 引用计数的对应关系需在 Phase 2 专项验证，避免计数错乱）。

### 7.3 启动流程（改造后时序）
```
Bootstrap → GameEngine
  └─ OnBeforeInitAsync：Config.EnsureLoaded()（仍走 Resources，保证启动必需）
  └─ CreateModules：ResourcesSystem(0) → AddressableSystem(1) → ...
  └─ AddressableSystem.InitAsync：InitializeAsync → [Remote] CheckUpdate → 预载 BuiltIn
  └─ OnGameStartAsync：LoadScene（Addressable 模式）
```

### 7.4 降级矩阵
| 场景 | 行为 |
| --- | --- |
| Addressables 未初始化 / 初始化失败 | `InitAsync` 失败不阻断启动，降级纯 Resources 模式，UI 弹"资源不可用"提示 |
| Remote 加载失败（断网） | 若该资源 BuiltIn 有副本 → 自动降级 Resources；否则报错并记录，不崩溃 |
| Bundle 缓存损坏 | `CleanBundleCache` 后重试一次 |

### 7.5 与 DownloadMgr 的职责边界（重要）
- **Addressables 管**：构建期受管资源（Prefab、音频、图集、场景、数值表）——有 catalog、有版本、可整体更新。
- **DownloadMgr 管**：运行时才知道的 URL 内容（外部图片、用户头像、公告图）——无版本、按需拉取 + LRU 缓存。
- 二者**不重叠**：不要把 DownloadMgr 下载的临时文件塞进 Addressables，也不要用 DownloadMgr 去拉 Bundle（交给 Addressables 自身下载器，WebGL 上它会走 IndexedDB 缓存）。

---

## 8. 迁移路线（分阶段，可灰度回滚）

### Phase 0：基础设施（约 0.5~1 天）【估算】
- 目标：Addressables 可配置、可构建、可打包。
- 产出：`AddressablesSetup.cs`（自动建分组/Profile/Label）、`AddressableAssetsData`、`AddressableKeys`、`AddressableSystem` 骨架（本地模式）。
- 验收：Editor 菜单一键构建成功；`Build/Addressables/version.json` 生成；`AddressableSystem.InitAsync` 在 Editor 内通过。

### Phase 1：资源迁移（约 1 天）【估算】
- 目标：资源进 Addressables（不改业务代码）。
- 操作：按 §4.1 复制资源到 `AddressableAssets/` → 按 §4.2 分组 → 构建验证每个组 Bundle 归属正确。
- 验收：构建报告显示 BuiltIn/Remote Bundle 划分符合预期；本地模式 `LoadAssetAsync` 可加载全部迁移资源。

### Phase 2：运行时接入（约 2~3 天）【估算】
- 目标：业务代码切换入口，双轨降级生效。
- 操作：接入 §7 全部改造点；UISystem/SoundSystem/SceneSystem 双轨验证；对象池计数专项。
- 验收：`addressablesMode=Off` 时行为与改造前完全一致（回归基线）；`LocalOnly` 时全部走 Addressables 且内存/释放无泄漏（结合既有 Code Review 关注点）。

### Phase 3：远程更新（约 2 天）【估算】
- 目标：WebGL CDN 热更闭环。
- 操作：Profile `Publish` 配置、`GameConfig` 远程 URL、`CheckUpdateAsync/UpdateContentAsync`、Nginx 部署、版本清单。
- 验收：修改一个 Remote 资源 → 构建 → 上传 CDN → 旧包启动能检测到更新并下载；断网时行为符合 §7.4 降级矩阵；IndexedDB 二次启动命中缓存。

### Phase 4：收尾清理（约 1 天）【估算】
- 目标：去重、瘦身、文档、验收。
- 操作：删除已迁移的 Resources 重复资源（保留 `ScriptableObjects/GameConfig` 等启动必需项）；`ResourcesSystem` 收敛；性能/内存压测（WebGL 首包体积对比前后）。
- 验收：WebGL 首包体积较迁移前下降【估算 30%+，以实际构建为准】；全部验收项通过（见 §10）。

---

## 9. 风险与注意事项

| 风险 | 等级 | 应对 |
| --- | --- | --- |
| WebGL 内存受限，Bundle 峰值加载 OOM | 高 | 分帧加载、Remote 资源按需+预载窗口、WebGL 限制纹理压缩 |
| CORS / 跨域 Worker 限制 | 高 | Remote Bundle 与 index.html 同域部署；跨域则配 CORS 头 |
| Release 泄漏（既有 Code Review 重点关注项） | 高 | 句柄表 + 有效性校验；对象池计数专项；`GetDebugStats` 可观测 |
| 双轨期间资源重复、路径不一致 | 中 | 迁移走"复制"而非"原地标记"；`LoadWithFallbackAsync` 统一入口 |
| Catalog/版本管理错误导致强更失败 | 中 | `version.json` + hash 对比；保留旧版本可回滚 |
| Shader 变体随 Remote Bundle 丢失 | 中 | Shader 常驻 BuiltIn；构建勾选 Strict Mode 校验 |
| 首次启动新增初始化/检查延迟 | 中 | BuiltIn 秒开、Remote 异步检查不阻塞首屏 |
| ResourcesSystem 与 AddressableSystem 双重缓存 | 低 | 明确职责边界；Resources 仅兜底 |

---

## 10. 验收清单（Phase 3 结束全量核对）

- [ ] Editor 一键构建 Addressables 成功，`version.json` 含 catalog hash
- [ ] BuiltIn 资源启动 0 网络依赖，首屏可进
- [ ] Remote 资源更新后旧包可检测、可下载、可生效
- [ ] 断网/初始化失败按降级矩阵行为，不崩溃、有提示
- [ ] 对象池实例化/回收引用计数无泄漏（`GetDebugStats` 句柄归零）
- [ ] 场景切换 Addressable 场景加载/卸载正常
- [ ] WebGL 首包体积较迁移前下降（目标【估算】≥30%）
- [ ] 删除迁移后 Resources 重复资源，构建无报错
- [ ] 项目原有回归（UI 打开、音频、下载图片）全部通过

---

## 11. 交付物清单

| 交付物 | 类型 | 归属 |
| --- | --- | --- |
| 本设计文档 | 文档 | Docs/Addressables/ |
| `AddressableSystem.cs` | 运行时 | Core/System/AddressableSystem/ |
| `AddressableKeys.cs` | 运行时 | Core/System/AddressableSystem/ |
| `AddressablesSetup.cs` | Editor | Editor/Addressables/ |
| `AddressablesBuildWindow.cs` | Editor | Editor/Addressables/ |
| `AddressablesDeployReport.cs` | Editor | Editor/Addressables/ |
| `GameConfig` 字段扩展 | 配置 | Core/GameConfig.cs + Config.cs |
| `StartGame` 模块注册 | 集成 | Core/StartGame.cs |
