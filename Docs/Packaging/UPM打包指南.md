# ReunionMovement UPM 打包指南

> 目标：把框架作为标准 Unity Package（UPM）分发，支持 `file:` / git URL 安装、版本升级与按需导入；
> 同时保留 `Assets/ReunionMovement` 作为开发期源码目录。
> 旧的分发方式（整目录 `.unitypackage`）仍在，见 [.unitypackage 打包指南](打包指南.md)。

## 1. 导出

菜单：`ReunionMovement → 打包 → 导出 UPM 包（运行时 / 完整）`。
选择输出目录后生成 `<dir>/com.reunion.movement`。

命令行（不弹窗，输出到 `Unity/ReunionMovement/Build/Packages/`）：

```powershell
Unity -batchmode -quit -projectPath Unity/ReunionMovement `
  -executeMethod ReunionMovement.EditorTools.Package.ReunionUpmExporter.ExportRuntimeBatch

# 完整包
Unity -batchmode -quit -projectPath Unity/ReunionMovement `
  -executeMethod ReunionMovement.EditorTools.Package.ReunionUpmExporter.ExportFullBatch
```

实现：`Editor/Package/ReunionUpmExporter.cs`。

## 2. 两种 profile

| 内容 | Runtime | Full |
|---|---|---|
| 体积（当前源码） | 约 44 MB | 约 190 MB |
| 字体 `Fonts/` | 不含 | 含（129 MB） |
| 示例场景 `Scenes/` | 不含 | 含 |
| 示例 UI `UI/Select`、`Resources/Prefabs/UIs` | 不含 | 含 |
| `3rd` 演示资源 | 不含（保留 `3rd/WebGLSupport`） | 含 |
| `Editor/UIExamples` | 不含 | 含 |
| 适用 | 接入自己的游戏 | 跑示例 / 完整验收 |

两个 profile 都包含：运行时程序集、`Plugins`（UniTask / R3 / DOTween / ZString / kcp2k / Telepathy /
SimpleWebTransport）、`Resources` 其余资源、`Editor` 工具、`GenerateScript/UI`、`Documentation~`。
iOS 触觉原生实现 `Plugins/iOS/ReunionHaptics.mm` 会随包分发；Android 的 `VIBRATE` 权限由
`Editor/Build/AndroidVibratePermission.cs` 在构建时注入。

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
└── Documentation~/
    ├── ReunionMovement 使用指南.md
    ├── CustomSystem.cs.txt
    └── Docs/                         # 从仓库 Docs/ 复制
```

## 4. 安装到目标工程

`Packages/manifest.json`（路径相对目标工程的 `Packages/` 目录）：

```json
"com.reunion.movement": "file:../../Reunion-Movement/Unity/ReunionMovement/Build/Packages/com.reunion.movement"
```

或 Package Manager → `+` → `Add package from disk...` → 选择 `package.json`。

发布到可访问仓库后可用 git URL：

```json
"com.reunion.movement": "https://github.com/<you>/Reunion-Movement.git?path=/Build/Packages/com.reunion.movement#v1.1.0"
```

> `Build/` 默认被 `.gitignore` 忽略。要支持 git URL，需把包目录提交到 `Distribution/` 或独立包仓库，
> 或者把 `Build/Packages/com.reunion.movement` 作为 CI 产物上传到 Release。

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
5. 包尚未发布到公开仓库；当前输出在 `Build/`，不会被提交。
6. `.unitypackage` 导出仍保留（已排除 `Game/`）；两者不要同时导入同一个工程。

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

- `ExportRuntimeBatch` 产出 **43.5 MB / 1,851 文件**；
- 新建 Unity 6000.3 工程，`Packages/manifest.json` 加 `file:` 包 → 打开后 **0 编译错误**；
- 产出 `ReunionMovement.Core` / `Utils` / `GenerateScript.UI` / `DOTween.Modules` 等 30+ 程序集；
- Addressables 2.9.1 / Input System 1.18.0 / uGUI 2.0.0 / URP 17.3.0 由 `package.json` 依赖自动解析；
- 空工程探针 `GameEntryResolver.FindEntryType()` 返回 `null`（日志 `[Probe] entry=null`），
  确认包安装后不会接管宿主启动。

实测记录见 [便捷性与可移植性评估](../便捷性与可移植性评估.md) 的「落地记录」。
