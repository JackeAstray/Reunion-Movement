# Hello World

最小可运行示例：不依赖场景或预制体，`GameEntry` 在运行时创建一个 uGUI 文本。

## 使用

1. Package Manager → Gear → Samples → **Hello World** → Import。
2. 打开任意场景（空场景即可），Play。
3. 屏幕中央出现 `Hello, Gear!`。

> 示例通过 `[assembly: GearGameEntry(typeof(HelloWorldEntry))]` 声明入口。
> 如果你的工程已经声明了自己的入口，请不要导入这个 Sample，或删掉示例里的这行声明。
> 非框架程序集的入口优先于示例。

## 文件

- `HelloWorldEntry.cs` —— 最小 `GameEntry`，`CreateModules()` 返回空列表，`OnGameStartAsync()` 创建 Canvas + Text。
- `Gear.Samples.HelloWorld.asmdef` —— 只引用框架核心与 uGUI。

## 说明

这个示例刻意不依赖 `Resources` / Addressables / 场景，用来验证：

- `Bootstrap` 能解析到程序集级入口；
- `GameEntry` 的三个生命周期点（`CreateModules` / `OnBeforeInitAsync` / `OnGameStartAsync`）；
- 即使 `CreateModules()` 返回空列表，引擎也能正常进入 `Running` 并执行业务启动逻辑。

真实项目通常会在 `CreateModules()` 里注册 `ResourcesSystem` / `SceneSystem` / `UISystem` 等模块。
