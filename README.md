# Reunion Movement

Unity 6000.3 游戏框架（Runtime 包）。

## 安装

在目标工程的 `Packages/manifest.json` 中加入（路径按实际导出位置调整）：

```json
"com.reunion.movement": "file:../Reunion-Movement/Build/Packages/com.reunion.movement"
```

或 Package Manager → `+` → `Add package from disk...`，选择本目录的 `package.json`。

## 依赖

- Addressables 2.9.1
- Input System 1.18.0
- Newtonsoft.Json 3.2.2
- uGUI 2.0.0
- URP 17.3.0

## 注意

- 包安装时，编辑器生成物写到 `Assets/ReunionMovementGenerated/`（包目录只读）。
- 运行时包不含示例场景、示例 UI、字体与第三方演示资源；请自备字体/界面。
- 入口：`[assembly: ReunionGameEntry(typeof(MyGameEntry))]`，或 `Bootstrap.Options.Entry`。
  没有入口时 Bootstrap 会报错并跳过启动，不会接管宿主工程。

更多文档见 `Documentation~/`。
