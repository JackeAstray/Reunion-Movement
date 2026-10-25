# UI 系统评估：现状、方案对比与演进建议

> **已归档（2026-10-25）**：本文是阶段 2 / 阶段 3 时期的历史记录，相关改造已完成，不再维护；其中的路径与命令可能已过时，以当前代码为准。

> 结论先说：**项目里已经有一套更先进的方案（`UIToolkitSystem`），问题不是"没有"，而是"两套并存且没有边界"。**
> 所以最值得做的是**定主栈 + 划边界**，而不是再引入第三套。

## 一、现状测绘（都是实测数字，不是估计）

### 1. 两套 UI 栈并存

| | UGUI 栈 | UI Toolkit 栈 |
|---|---|---|
| 入口 | `UISystem`（`ICustomSystem`，order 90） | `UIToolkitSystem`（`ICustomSystem`，order 110） |
| 管理对象 | 窗口 Prefab（`UILoadState` + `UIController`） | `UIToolkitPanel` + UXML/USS |
| 资源 | `Resources/Prefabs/UIs/*.prefab` → Addressables 优先 | `Resources/UI/UIToolkit/*.uxml` + `PanelSettings` |
| 渲染 | 每个窗口自带 `Canvas`（`CreateUIPlane` 第 147–163 行生成） | 单个全局 `UIDocument` + `PanelSettings` |
| 数量 | **3 个预制体 / 3 个控制器**（Popup / StartGame / Terminal） | **2 个 UXML**（MainMenu / StartGame）+ 2 个 USS |
| 事件 | 无 | `OnPanelOpenSubject` / `OnPanelCloseSubject` |

关键发现：**运行时 UGUI 的规模很小**——3 个窗口，不是几百个。这直接改变了"迁移成本"的结论。

### 2. `UIToolkitSystem` 已经具备的能力

```csharp
public UIDocument GlobalDocument { get; }          // 全局 UIDocument
public VisualElement RootVisualElement { get; }    // 直接挂 UI 的入口
public T OpenPanel<T>(string panelName, object data = null) where T : UIToolkitPanel, new();
public T OpenPanel<T>(object data = null)          // 按类型名解析
public Subject<UIToolkitPanel> OnPanelOpenSubject / OnPanelCloseSubject
```

也就是说：面板基类、按名/按类型打开、开关事件、主题样式（`Global.uss`）、缩放策略（`PanelSettings`）都已经有了。

## 二、UGUI 这套的七个具体问题

按"要不要修、修起来贵不贵"排序。每条都标了代码位置。

### P1 每个窗口一个 Canvas（影响最大，改起来最便宜）

`CreateUIPlane.cs:147` 给每个窗口预制体根节点 `AddComponent<Canvas>()` + `CanvasScaler`。

- **代价**：每个 Canvas 是独立的批处理边界；N 个窗口 = N 段 draw call，且任一窗口内容变动只重建自己（这点是好处），但**排序**和**批次合并**都变差。
- **改法**：根 Canvas 保留（`uiRoot` 下已经有一个），窗口改成挂 `CanvasGroup`（用于淡入淡出 / 交互开关），层级顺序由 `panelType` 决定的四个子根（`mainUI / normalUI / headInfoUI / tipsUI`）保证。
- **验收**：Frame Debugger 里同一面板下的窗口能合批；窗口淡入淡出仍正常。

### P2 "类名 = 预制体名"的隐式契约

```csharp
public async UniTask<UILoadState> OpenWindowAsync<T>(params object[] args) where T : UIController
    => await OpenWindowAsync(typeof(T).Name, args);
```

- **代价**：把 `StartGameUIPlane` 类改名，窗口就在运行期悄悄打不开（`typeof(T).Name` 找不到同名资源）。编译器完全帮不上忙。
- **改法**：给 `UIController` 加 `[WindowId("StartGameUIPlane")]`，`OpenWindowAsync<T>` 优先读特性、回退类名；或者用 source generator 生成 `UINames` 常量（项目里已经有 `ReunionMovement.SourceGenerator.dll`）。
- **验收**：故意把类改名，窗口仍能打开（特性生效）。

### P3 `GetControl(Type, uri)` 的字符串路径

`UIController.GetControl` 用字符串 uri 在层级里找控件，并且需要手动 `InvalidateControlCache()`。

- **代价**：路径写错只在运行时 `null`；动态增删子节点后忘记清缓存会拿到已销毁对象。
- **改法**：优先用 Inspector 序列化字段直接引用控件；确实需要批量查找时，由生成器按命名规范生成访问器。

### P4 双轨加载的边界不清晰

异步 `OpenWindowAsync` 走 Addressables → 失败降级 Resources；同步 `OpenWindow` **只能** Resources（WebGL 不支持 `WaitForCompletion`）。

- **代价**：同一个窗口在两条路径下"能不能打开"不一致；调用方要自己判断用哪个。
- **改法**：把同步 API 标记为"仅编辑器/调试"，运行时代码统一 `await`。或者在启动时按窗口清单预加载。

### P5 生命周期有 7 个 virtual，但没有状态约束

`OnInit / BeforeOpen / OnOpen / OnSet / OnClose / OnSpawned / OnDespawned`。

- **代价**：`OnSpawned`（池化复用时）与 `OnOpen`（每次打开）的边界靠自觉；忘了写 `OnSpawned` 的清理逻辑就会出现"第二次打开带着上次的数据"。
- **改法**：在文档里钉死语义（当前 `03-新增一个窗口` 只提了 `OnOpen`），或者在基类里加断言/日志。

### P6 池化白名单手工维护

`UISystem.poolableWindowNames` 默认**空**，也就是默认不池化。

- **代价**：想池化要记得改代码；改了以后窗口的 `OnDespawned` 必须正确。
- **改法**：把"是否池化"下放到 `UIWindowAsset`（序列化到预制体上），`UISystem` 读资产而不是读字符串清单。

### P7 两套栈没有边界约定

现在既能 `UISystem.OpenWindowAsync`，也能 `UIToolkitSystem.OpenPanel<T>`，新人不知道新界面该用哪个。

- **代价**：同类需求两种写法，风格分裂；样式/缩放/输入焦点管理各一套。
- **改法**：写一页约定（见下面"建议路线"第 0 步）。

## 三、三个方案对比

| | A 定主栈 + 渐进改良（推荐） | B 全面迁移到 UI Toolkit | C 维持双栈，只修 P1/P2 |
|---|---|---|---|
| 做法 | 新 UI 一律 UI Toolkit；UGUI 只留 3 个历史窗口并修 P1/P2 | 3 个窗口全部重写成 UXML/USS，删除 UGUI 路径 | 不动栈的选择，只做局部优化 |
| 改动面 | 约定 + 2 处代码（Canvas、WindowId）+ 新页面用 Toolkit | 3 预制体 → UXML、3 控制器 → Panel、删 `UISystem` 一半代码 | 2 处代码 |
| 风险 | 低 | **中高**：world-space / 3D 跟随 UI 在 UI Toolkit 里代价更高；动画工具链要换 | 低 |
| 收益 | 新代码统一、旧代码成本可控 | 彻底一套栈 | 只解决性能与改名坑 |
| 工期 | 2–4 人日 | 8–12 人日（3 个窗口 + 全局样式统一 + 回归） | 1 人日 |
| 适合 | 继续做中等规模 UI | 确定不再需要 world-space HUD | 短期止血 |

**不建议现在就选 B**，理由具体到本项目：

1. `headInfoUI` 这一层（头顶血条/飘字）天然是 world-space，UI Toolkit 的 world-space 支持不如 UGUI 成熟；
2. 项目里 UGUI 窗口只有 3 个，**全量迁移省下的维护成本抵不过回归风险**；
3. `UIToolkitSystem` 的 `OpenPanel<T>` 还是 `new()` 约束（每次新建实例），面板复用/对象池还没做——迁过去等于把 UGUI 已经解决的池化问题再解一遍。

## 四、建议路线（三步，每步独立可验收）

### 第 0 步：写约定（半天，零风险）

在 `03-新增一个窗口.md` 顶部加一条决策表：

| 你要做的界面 | 用哪个 |
|---|---|
| 全屏菜单 / 设置 / 背包等以列表与表单为主的界面 | **UI Toolkit**（`UIToolkitSystem.OpenPanel<T>`） |
| 需要 3D 跟随（血条、名字板）、需要和场景物件混排 | **UGUI**（`UISystem.OpenWindowAsync`） |
| 已有的 Popup / StartGame / Terminal | 保持 UGUI，不重写 |

### 第 1 步：修 P1 + P2（1–2 人日）

- 窗口预制体去掉自带 `Canvas`，改 `CanvasGroup`；`CreateUIPlane` 生成逻辑同步改（**只影响新建窗口**，老预制体手工改一次）；
- `UIController` 加 `WindowIdAttribute`，`OpenWindowAsync<T>` 优先读它；
- 验收：3 个老窗口正常打开关闭、Frame Debugger 里同面板可合批、改类名不炸。

### 第 2 步：把"新 UI 用 Toolkit"落到实处（1–2 人日）

- 给 `UIToolkitPanel` 补一个池化/复用路径（现在是 `new()`）；
- 补一个 `UIToolkitPanel` 的典型页面模板（等价于 `CreateUIPlane` 的 UXML 版），并接进工具箱的「UI 窗口生成器」。

## 五、验收指标

| 指标 | 现在 | 目标 |
|---|---|---|
| 同一面板下窗口的 draw call 段数 | 每窗口 1 段 | 按 CanvasGroup 合批 |
| 改窗口类名导致打不开的概率 | 100% | 0（`WindowId` 特性兜底） |
| 新界面的栈选择 | 两种写法随意 | 一张决策表 |
| UI Toolkit 面板复用 | 无（每次 new） | 可池化 |

## 六、和这次重构的关系

UI 系统不在「阶段 0–5」的范围内（那几阶段只做了模块契约与巨型类拆分）。
本文是**独立评估**，没有改动任何 UI 运行时代码——只改了编辑器工具箱。
真要动手时，第 1 步（P1+P2）风险最低，可以单独一个 PR。
