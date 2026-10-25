# ReunionMovement UPM 打包指南

> 目标：把框架作为标准 Unity Package（UPM）分发，支持 `file:` / git URL 安装、版本升级与按需导入；
> 同时保留 `Assets/ReunionMovement` 作为开发期源码目录。
> 旧的分发方式（整目录 `.unitypackage`）仍在，见 [.unitypackage 打包指南](打包指南.md)。

## 1. 导出

菜单：`ReunionMovement → 打包 → 导出 UPM 包（运行时 / 完整 / OpenUPM 依赖）`。
选择输出目录后生成 `<dir>/com.reunion.movement`。

命令行（不弹窗，输出到 `Unity/ReunionMovement/Build/Packages/`）：

```powershell
Unity -batchmode -quit -projectPath Unity/ReunionMovement `
  -executeMethod ReunionMovement.EditorTools.Package.ReunionUpmExporter.ExportRuntimeBatch

# 完整包
Unity -batchmode -quit -projectPath Unity/ReunionMovement `
  -executeMethod ReunionMovement.EditorTools.Package.ReunionUpmExporter.ExportFullBatch

# OpenUPM 依赖包（不内置 UniTask/R3/ZString）
Unity -batchmode -quit -projectPath Unity/ReunionMovement `
  -executeMethod ReunionMovement.EditorTools.Package.ReunionUpmExporter.ExportOpenUpmBatch
```

实现：`Editor/Package/ReunionUpmExporter.cs`。

## 2. 三种 profile

| 内容 | Runtime | Full | OpenUPM 依赖 |
|---|---|---|---|
| 体积（当前源码） | 约 43 MB | 约 190 MB | 约 40 MB（不含 UniTask） |
| UniTask / R3 / ZString | 内置 | 内置 | UniTask 走 OpenUPM；R3 / ZString 内置 |
| 字体 `Fonts/` | 不含 | 含（129 MB） | 不含 |
| 示例场景 `Scenes/` | 不含 | 含 | 不含 |
| 示例 UI `UI/Select`、`Resources/Prefabs/UIs` | 不含 | 含 | 不含 |
| `3rd` 演示资源 | 不含（保留 `3rd/WebGLSupport`） | 含 | 不含（保留 `3rd/WebGLSupport`） |
| `Editor/UIExamples` | 不含 | 含 | 不含 |
| 适用 | 接入自己的游戏 | 跑示例 / 完整验收 | 接入方已在用 OpenUPM |

Runtime / Full 包含 `Plugins` 里的 UniTask / R3 / DOTween / ZString / kcp2k / Telepathy / SimpleWebTransport；
OpenUPM 模式不含 UniTask / R3 / ZString，其余相同。
两种运行时包都包含 `Resources` 其余资源、`Editor` 工具、`GenerateScript/UI`、`Documentation~`。
iOS 触觉原生实现 `Plugins/iOS/ReunionHaptics.mm` 会随包分发；Android 的 `VIBRATE` 权限由
`Editor/Build/AndroidVibratePermission.cs` 在构建时注入。

### OpenUPM 依赖模式

这个包**只不内置 UniTask**（R3 / ZString 仍内置）。宿主需要先加 scoped registry
（UniTask 在 OpenUPM，不在 Unity 官方 registry）：

```json
"scopedRegistries": [
  {
    "name": "OpenUPM",
    "url": "https://package.openupm.com",
    "scopes": [ "com.cysharp" ]
  }
]
```

然后再把 `com.reunion.movement` 加进 `Packages/manifest.json`。版本由包的 `package.json` 声明：
UniTask 2.5.11。DOTween / R3 / ZString 仍内置。

> R3 的 OpenUPM 包只带 `R3.Unity`、不带核心 `R3.dll`；ZString 官方包要求每个 asmdef 显式引用，
> 因此这两个没有跟着切。详见 [第三方依赖](../guides/第三方依赖.md)。

## 3. 包结构

```text
com.reunion.movement/
├── package.json
├── README.md
├── CHANGELOG.md
├── LICENSE.md
├── Common/ Core/ Utils/ UI/ GenerateScript/ Resources/ Plugins/ 3rd/
├── Editor/
│   ├── Shared/ReunionPaths.cs       # 框架根解析
│   ├── Toolbox/
│   └── ...
├── Samples~/HelloWorld/             # Package Manager → Import Sample
└── Documentation~/
    ├── ReunionMovement 使用指南.md
    ├── CustomSystem.cs.txt
    └── Docs/                         # 从仓库 Docs/ 复制
```

## 4. 安装到目标工程

### 4.1 一条 git URL（推荐）

包发布在仓库的 **`upm` 分支**上：该分支只有包内容（`package.json` 在分支根目录），不含仓库其余部分，
所以 URL 不需要 `?path=`：

```json
"com.reunion.movement": "https://github.com/JackeAstray/Reunion-Movement.git#upm"
```

需要锁定到某次发布时，把 `upm` 换成那次提交的短哈希（`#4f85b79`）。

维护者发布/更新包：

```powershell
# 让 Unity 重新导出 + 重建本地 upm 分支（不推远端，可 git branch -D upm 撤销）
powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -Export

# 确认无误后推远端
powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -Push

# 只检查"源码是否比包新"，不动 git
powershell -ExecutionPolicy Bypass -File Tools/upm-publish.ps1 -CheckOnly
```

脚本做三件事：调导出器重新出包（`-Export`）、把包内容写进 `upm` 分支的单次提交、校验源码是否比包新
（过期直接拒绝发布）。`upm` 分支是**发布产物**：每次发布重建为单个提交（force push），不要在上面手工提交。

### 4.2 本地路径（离线开发）

```json
"com.reunion.movement": "file:../../Reunion-Movement/Unity/ReunionMovement/Build/Packages/com.reunion.movement"
```

`Build/` 是 Unity 的构建目录、被 `.gitignore` 忽略，所以**别人 clone 下来的仓库里没有这个目录**，
先按第 1 节导出一次再用。

### 4.3 Package Manager → Add package from disk

`+` → `Add package from disk...` → 选择包目录里的 `package.json`。适合临时试用，不适合团队协作（路径写死在个人机器上）。

导入后在 Package Manager 的 **Samples** 页签导入 **Hello World**；它不依赖场景，Play 即可看到文本。

### 4.4 接入自己的游戏（两行）

```csharp
// 1) 在任意程序集声明入口（框架不会自动接管宿主启动）
[assembly: ReunionGameEntry(typeof(MyGameEntry))]

// 2) 选模块清单：Full() 全量 14 个；Minimal() 只要资源/Addressables/场景/UI
public class MyGameEntry : GameEntry
{
    public override IList<ICustomSystem> CreateModules() => ModulePresets.Minimal();
}
```

删模块不用背依赖表：缺的提供者会在启动时按 `[ModuleOrder(..., DependsOn)]` 自动补回
（`Bootstrap.Options.AutoExpandModuleDependencies`，默认开，启动日志会打印补入了谁）。

## 5. 包安装模式下的行为

- `ReunionPaths` 用 `ReunionMovement.Core.asmdef` 反查根目录，兼容 `Assets/ReunionMovement` 与
  `Packages/com.reunion.movement`。
- 读框架资源用 `InFramework`；写生成物用 `InWritable`。包安装时写入
  `Assets/ReunionMovementGenerated/`（包目录只读）。
- Excel 导表 / UI 生成器在包安装模式下会拒绝重新生成包内已存在的类型，避免 `Assembly-CSharp`
  与包程序集出现同名类型（CS0433）；新表名 / 新类名不受影响。

## 6. 已知限制

1. Runtime 不含字体、示例场景、示例 UI；需要这些请用 Full，或自行放入目标工程。
2. `Editor/UIExamples` 依赖 `UI/Select` 演示代码，Runtime 两者一起排除；Full 一起包含。
3. 两个 profile 都**不包含** `Game/StartGameEntry.cs`（示例入口）。宿主必须声明自己的
   `[assembly: ReunionGameEntry]`，或设置 `Bootstrap.Options.Entry`；否则 Bootstrap 会报错并跳过启动，
   不会接管宿主工程。
4. Runtime 包里的 `StartGame` 示例流程不可用（缺 `Temp` 场景和启动 UI prefab）；请使用自己的 GameEntry。
5. 包通过 `upm` 分支发布（`Tools/upm-publish.ps1`）；本地导出目录 `Build/` 仍被忽略、不进 git。
6. `.unitypackage` 导出仍保留（已排除 `Game/`）；两者不要同时导入同一个工程。
7. 包内仍带 vendored 的 UniTask / R3 / ZString / DOTween。宿主已装官方同名包时，
   用 `ReunionMovement → 开发 → 检查第三方依赖冲突` 检查；二者只能留一套。详见 [第三方依赖](../guides/第三方依赖.md)。

## 7. 验收

```powershell
# 1. 编译
dotnet build Unity/ReunionMovement/ReunionMovement.Core.csproj

# 2. Unity 侧（独占工程）
pwsh Tools/unity-verify.ps1

# 3. 导出
Unity -batchmode -quit -projectPath Unity/ReunionMovement `
  -executeMethod ReunionMovement.EditorTools.Package.ReunionUpmExporter.ExportRuntimeBatch

# 4. 空工程安装
#    新建 Unity 6000.3 工程 → Packages/manifest.json 加 file: 包 → 打开，Console 无红色报错
```

本次实测（2026-10-22）：

- `ExportRuntimeBatch` 产出 **42.6 MB / 1,855 文件**；
- 新建 Unity 6000.3 工程，`Packages/manifest.json` 加 `file:` 包 → 打开后 **0 编译错误**；
- 产出 `ReunionMovement.Core` / `Utils` / `GenerateScript.UI` / `DOTween.Modules` 等 30+ 程序集；
- Addressables 2.9.1 / Input System 1.18.0 / Newtonsoft.Json 3.2.2 / uGUI 2.0.0 / URP 17.3.0 由 `package.json` 依赖自动解析；
- 空工程探针 `GameEntryResolver.FindEntryType()` 返回 `null`（日志 `[Probe] entry=null`），
  确认包安装后不会接管宿主启动；
- OpenUPM profile：新建工程 + `scopedRegistries` 安装 → **0 编译错误**，官方 UniTask 2.5.11 解析成功；
  R3 / ZString 继续使用内置副本。

实测记录见 [便捷性与可移植性评估](../design/便捷性与可移植性评估.md) 的「落地记录」。
