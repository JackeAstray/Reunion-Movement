# Reunion Movement

A Unity game framework focused on a **pure C# lifecycle**, modular systems, a four-transport network layer,
Addressables hot update, and SDF-based UI.

**Unity**: 6000.3.10f1 (Unity 6 / URP 17.3)<br>
**Chinese README**: [README.md](README.md)

## Platform support

| Platform | Status | Notes |
|---|---|---|
| Windows (x64) | ✅ Verified | Local build: 170.8 MB / 0 errors |
| WebGL | 🟡 Configured | CI job + WebGL guards; real-browser verification pending |
| Android | ✅ Builds | APK 75 MB / 0 errors; `VIBRATE` permission verified; device launch pending |
| iOS | 🟡 Code ready | Haptics `.mm` added; Xcode link needs macOS |
| macOS / Linux | 🟡 Script ready | Build menu / CLI provided; not executed yet |

**Unity version**: this repository targets `6000.3.10f1`. Older Unity versions require downgrading
URP and replacing APIs such as `FindObjectsByType`; this is not verified.

## Quick start

1. Open `Unity/ReunionMovement` with Unity Hub.
2. Wait for the first import and compilation to finish.
3. Open `Assets/ReunionMovement/Scenes/StartGame.unity` and press Play.
4. `Bootstrap` creates the engine, initializes the registered modules, loads the boot scene and opens
   the start-up UI.

The example entry lives in `Game/StartGameEntry.cs` and is **not** shipped in the UPM package.
For your own game, declare:

```csharp
[assembly: ReunionGameEntry(typeof(MyGame.MyGameEntry))]
```

or set `Bootstrap.Options.Entry`. Without an entry, `Bootstrap` logs an error and skips startup instead
of taking over the host project.

## Install as a UPM package

Export a package from the editor menu:

```
ReunionMovement → 打包 → 导出 UPM 包（运行时 / 完整 / OpenUPM 依赖）
```

The default output is `Unity/ReunionMovement/Build/Packages/com.reunion.movement`.
Then add it to the target project's `Packages/manifest.json`:

```json
"com.reunion.movement": "file:../../Reunion-Movement/Unity/ReunionMovement/Build/Packages/com.reunion.movement"
```

Or use Package Manager → `+` → *Add package from disk...* and select its `package.json`.
The package ships a **Hello World** sample (Samples tab); it needs no scene and draws a label at runtime.

Profiles:

- **Runtime** — self-contained, no demo scenes/fonts, ~43 MB.
- **Full** — includes demo scenes, fonts and third-party demo assets, ~190 MB.
- **OpenUPM** — UniTask comes from OpenUPM (scoped registry required); R3/ZString stay vendored.

See [Docs/Packaging/UPM打包指南.md](Docs/Packaging/UPM打包指南.md) for details.

## Project layout

```
Unity/ReunionMovement/Assets/ReunionMovement/
├── Common/      Logging, config, enums, singleton base
├── Core/        Bootstrap, GameEngine, System/*
├── Utils/       Runtime systems, utilities, reusable UI
├── UI/          Project UI (examples / business)
├── Editor/      Excel, packaging, Addressables pipeline, toolbox
├── Plugins/     UniTask, R3, DOTween, ZString, kcp2k, Telepathy, SimpleWebTransport
├── 3rd/         Joystick Pack, WebGLSupport, Animated Loading Icons
├── Scenes/      Boot / loading / demo scenes
├── Resources/   Runtime assets
├── Game/        Example entry declaration (not shipped in packages)
└── GenerateScript/  Generated UI / config code
```

## Documentation

Documentation is currently Chinese-first:

- [Docs/00-快速开始.md](Docs/00-快速开始.md) — quick start
- [Docs/01-架构总览.md](Docs/01-架构总览.md) — architecture and startup sequence
- [Docs/02-新增一个系统.md](Docs/02-新增一个系统.md) — add a system
- [Docs/03-新增一个窗口.md](Docs/03-新增一个窗口.md) — add a window
- [Docs/04-接入自己的游戏.md](Docs/04-接入自己的游戏.md) — integrate your game
- [Docs/05-常见陷阱.md](Docs/05-常见陷阱.md) — pitfalls
- [Docs/Packaging/UPM打包指南.md](Docs/Packaging/UPM打包指南.md) — UPM packaging
- [Docs/guides/跨平台构建指南.md](Docs/guides/跨平台构建指南.md) — multi-platform builds and `link.xml`
- [Docs/guides/第三方依赖.md](Docs/guides/第三方依赖.md) — third-party dependencies and conflicts

## Dependencies

| Dependency | Version | Purpose |
|---|---|---|
| com.unity.inputsystem | 1.18.0 | New Input System |
| com.unity.addressables | 2.9.1 | Remote content and memory management |
| com.unity.render-pipelines.universal | 17.3.0 | URP |
| com.unity.ugui | 2.0.0 | uGUI |
| com.unity.nuget.newtonsoft-json | 3.2.2 | JSON |
| UniTask / R3 / ZString / DOTween | vendored | Async, reactive, zero-alloc strings, tweening |
| kcp2k / Telepathy / SimpleWebTransport | vendored | KCP / TCP / WebSocket transports |

## License

[Apache-2.0](LICENSE).
