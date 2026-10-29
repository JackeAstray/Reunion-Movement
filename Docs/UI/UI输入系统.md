# UI 输入系统（键盘 / 鼠标 / 手柄 / 赛车方向盘 / 飞行摇杆）

本文档说明 `UIInputSystem` 与 `CarouselSelectionController` 的多设备输入约定。

## 1. 支持的设备与默认按键

| 设备 | 导航 | 确认 | 取消 | 进入/退出 UI |
| --- | --- | --- | --- | --- |
| 键盘 / 鼠标 | WASD / 方向键 / 鼠标悬停与滚轮 | Enter | Escape（或右键，可配置） | Tab |
| 手柄 | 左/右摇杆、十字键 | A / ✕（buttonSouth） | B / ○（buttonEast） | Start |
| 赛车方向盘（G29/G920/G923、MOZA RS50、Fanatec、Thrustmaster 等） | 小摇杆 > 十字键 / hat；都没有时才用**转向轴** | **button1（A / ✕）** | Xbox 布局：**button2（B）**；罗技 PlayStation 布局：**button3（○）** | start / options / menu / share / button9–14 |
| 飞行摇杆 / HOTAS | 主摇杆 + hat | trigger / button1 | button2 | start / menu / options / button7 / button8 |
| 未知 HID 摇杆 | 有 hat 时用 hat + 主摇杆；无 hat 时**不自动用主摇杆** | 第一个按钮 | 第二个按钮 | start / menu / options / 第一个可用按钮 |

> 方向盘默认约定：**确认 = button1（A / ✕）**；取消键按布局区分：
> **Xbox 布局 = button2（B）**，**罗技 PlayStation 布局（G29/G923 PS）= button3（○）**。
> G923 的 HID 编号为 Cross=button1、Square=button2、Circle=button3，所以 PS 版不能再把 button2 当取消键。
> 不同型号的按钮编号可能不同，可在设置界面调用重绑定接口，或直接使用 `UIInputSystem.SetDeviceBinding`。

### 一个方向盘枚举成多个 HID 子设备

赛车方向盘在 Windows / Input System 里经常不是单个 `InputDevice`，而是底座、踏板、轮缘等多个 HID 子设备。
`UIInputSystem` 现在会：

- 按 `manufacturer#serial`（没有 serial 时按 product）把同一物理设备的子设备归到同一个玩家槽位；
- 过滤掉没有按钮/导航控件的纯轴子设备（例如踏板）；
- 在 Console 打印实际分组：

```text
[UIInputSystem] 设备路由：MOZA RS50 → P1（product=..., serial=..., group=...）
```

另外，键盘/鼠标固定算 P1；所以“一个方向盘 + 键盘”可能只显示一张 P1 卡片。
如果只想按实体控制器数量显示，关闭选择界面的 `Include Keyboard Mouse As P1`。

菜单键（切换 UI 控制模式 / 控制对象）不依赖键盘 Tab：手柄、方向盘、飞行摇杆都有自己的默认键。
如果设备上报了 `Menu` usage，会优先使用该键；否则按上表顺序取第一个存在的按钮。设备插入时 Console 会显示实际菜单键：

```text
[UIInputSystem] 检测到方向盘：MOZA RS50；确认=Button 1，取消=Button 2，菜单=Button 10，导航=小摇杆。
```

如果菜单键使用的是 `button10` / `button7` 这类索引兜底，日志会提示可能不是 Start/Options 键；请用下面的重绑定接口换成真实菜单键。
菜单键与确认/取消键重合时不会失效：切换模式的那一刻会抑制本帧输入，避免同一次按键既切模式又触发确认。

菜单键等价于键盘 Tab：在“UI 控制模式”（方向输入驱动菜单/选择框）和“角色控制模式”（方向输入驱动车辆/角色）之间切换；
它**不会自动关闭**已经打开的窗口。要关闭窗口请用取消键（方向盘 Xbox 布局 button2 / 罗技 PS 布局 button3 / 手柄 B / Esc，且选择框开启 `Close On Cancel`），
或在 `UIInputSystem.Instance.UIControlModeChangedSubject` 中根据模式打开/关闭自己的菜单窗口。

未识别的 HID 摇杆如果没有 hat，默认**不会**用主摇杆导航（否则未识别方向盘的转向轴会让菜单持续切换）。
这类设备请用 `SetDeviceNavigation` 绑定导航按钮；已识别为“飞行摇杆”的设备不受此限制。

设备插入时会在 Console 输出实际采用的按键：

```text
[UIInputSystem] 检测到方向盘：Logitech G923 Racing Wheel；确认=A / ✕ (Button 1)，取消=○ / Circle (Button 3)，导航=十字键 / Hat。
```

## 2. 焦点域与窗口层级

每个 `UIController` 窗口打开时会自动获得一个 `UIFocusScope`：

- 窗口打开/置顶时压栈，关闭时出栈；
- **只有栈顶焦点域**会接收键盘/手柄/方向盘/飞行摇杆输入；
- 焦点域内按 `PlayerSlot` 精确路由：每个玩家一个焦点目标栈（`CarouselSelectionGroup` 算一个），后注册的子面板优先，关闭/禁用后自动回退到父面板；
- 不再用 siblingIndex / InputPriority 猜测“哪个窗口在最上面”，弹窗打开后下层轮播不会再抢输入。

窗口级 UI 控制：

- `UIController` 上的 **Request UI Control** 决定窗口打开时是否进入 UI 控制模式；
- 轮播/选择组的 `Auto Enable UI Control` 只作为“没有 UIController 的纯 Canvas 场景”创建焦点域时的提示；
- 不再由每个轮播单独 `RequestUIControl`，避免“场景里激活一个轮播就锁死角色输入”。

`CarouselSelectionController` 现在是普通 `MonoBehaviour`：

- 不再和 `CharacterSelectUIPlane` 争第二个 `UIController`；
- 自己的 `uiName` 只用于“确认/取消关闭哪个窗口”，留空会向上找最近的 `UIController`；
- 新增 `onSelectionChanged` / `onCancelled` 事件，`closeOnConfirm` / `closeOnCancel` 改为属性。

### 子菜单与子面板

子菜单分两种做法，焦点域都已覆盖：

1. **独立子窗口（推荐）**：子菜单也是 `UIController` 窗口。它有自己的 `UIFocusScope`，
   打开后位于栈顶，只接收子窗口输入；关闭后自动恢复父窗口。
2. **同一窗口内的子面板**：给子面板根节点挂 `UIFocusScope`，打开时 `SetActive(true)`、
   关闭时 `SetActive(false)`，焦点域会自动压栈/出栈。也可以不挂：同一焦点域会按
   `PlayerSlot` 维护目标栈，子面板轮播后注册在栈顶，关闭/禁用后自动回退父轮播。

子面板只关闭自己：

- 在子面板的 `CarouselSelectionController` 上把 **Close Target** 指向子面板根节点；
- 确认/取消时只隐藏子面板，不会关闭父窗口；
- 留空保持原行为：优先关闭自身 `uiName` 对应的窗口，再向上找最近的 `UIController`。

注意事项：

- 父窗口关闭前请显式关闭子窗口/子面板，`UISystem` 不会自动递归关闭子级；
- 鼠标不受焦点域限制：模态子面板请加全屏射线遮挡，或关闭父面板的
  `GraphicRaycaster` / `CanvasGroup.blocksRaycasts`；
- 子面板的 `UIFocusScope` 默认 `Request UI Control = true`；如果父窗口已经是 UI 控制模式，
  不需要额外处理，关闭时会自动释放。

开始菜单示例见 [开始菜单.md](./开始菜单.md)；
本地多人 P1-P4 选择界面见 [本地多人选择界面.md](./本地多人选择界面.md)。

## 3. 快速连按与按住重复

- 数字输入（键盘、十字键、hat）：**每次按下立即响应**，不再有 `0.15s` 节流，快速连按不会丢输入。
- 模拟输入（摇杆）：越过死区立即响应一次；继续按住时按 `navigateHoldDelay` / `navigateRepeatRate` 自动重复。
- 参数位于 `UIInputBinding`，可通过 `UIInputSystem.Instance.CurrentBinding` 修改并 `SaveBindings()` 持久化。

### 没有十字键的方向盘（MOZA RS50 等）

方向盘导航默认使用 **Auto** 来源，优先级为：

1. 自定义导航按钮（`SetDeviceNavigation`）
2. 小摇杆 / 第二摇杆
3. 十字键 / hat
4. 转向轴

**只要检测到小摇杆或十字键，就不会再使用转向轴**，避免轻微打方向导致菜单频繁切换。

有些方向盘会以“手柄（Gamepad）”形式上报。这类设备的**左摇杆通常是转向轴**，系统会自动忽略它，
改用右摇杆（一般对应方向盘上的小摇杆）或十字键；不会因为打方向而切菜单。

如果只能使用转向轴：

- 向右打方向 = 下一项，向左打方向 = 上一项；
- 回正到 `wheelSteeringReleaseThreshold` 以下后，才能再次触发；
- 如果 UI 打开时方向盘本来就没回正，会先等待回正，不会立刻跳一格；
- 保持在阈值以上超过 `wheelSteeringHoldDelay` 后，按 `wheelSteeringRepeatRate` 自动重复；
- 矩阵选择框按行优先顺序遍历，保证只用转向也能选到全部选项。

设备插入时 Console 会列出导航候选与小摇杆控件名，例如：

```text
[UIInputSystem] 方向盘导航候选：十字键=True，小摇杆=True，转向轴=True；当前来源=Auto
[UIInputSystem] 十字键/hat 控件：<Joystick>/hat
[UIInputSystem] 小摇杆控件：<Joystick>/hat2
```

转向轴优先识别 `steer` / `steering` / `wheel` 命名的轴，没有时才使用 X 轴；
如果方向盘的 X/Y 实际是小摇杆、转向在独立命名轴上，主摇杆也会被识别为小摇杆，而不是转向轴。

如果自动选择不是你想要的，可以强制指定：

```csharp
// 只用小摇杆
UIInputSystem.Instance.SetWheelNavigationSource(device, WheelNavigationSource.ThumbStick);

// 只用十字键 / 只用转向轴 / 只用自定义按钮 / 禁用
UIInputSystem.Instance.SetWheelNavigationSource(device, WheelNavigationSource.Hat);
UIInputSystem.Instance.SetWheelNavigationSource(device, WheelNavigationSource.Steering);
UIInputSystem.Instance.SetWheelNavigationSource(device, WheelNavigationSource.CustomButtons);
UIInputSystem.Instance.SetWheelNavigationSource(device, WheelNavigationSource.None);
```

如果小摇杆在 Auto 下没有被识别（例如它只上报 4 个按钮），用下面的自定义按钮方式绑定它。

相关参数：

```csharp
var b = UIInputSystem.Instance.CurrentBinding;
b.wheelSteeringNavigation = true;      // 关闭后不再用转向轴导航
b.wheelSteeringInvert = false;         // 某些方向盘左转为正时设为 true
b.wheelSteeringDeadzone = 0.25f;       // 触发阈值（0~1）
b.wheelSteeringReleaseThreshold = 0.12f;
b.wheelSteeringHoldDelay = 0.6f;
b.wheelSteeringRepeatRate = 0.2f;      // <= 0 表示一次打满只切换一格
UIInputSystem.Instance.SaveBindings();
```

如果转向轴也不可用，或更习惯用按钮导航，可以把方向盘上的旋钮/拨片/面板按钮绑定为上下左右：

```csharp
UIInputSystem.Instance.SetDeviceNavigation(
    device,
    upControl:    "<Joystick>/button5",   // 例如旋钮上
    downControl:  "<Joystick>/button6",   // 旋钮下
    leftControl:  "<Joystick>/button7",
    rightControl: "<Joystick>/button8");

UIInputSystem.Instance.ResetDeviceNavigation(device); // 恢复 hat / 转向轴
```

配置了自定义导航按钮后，会优先使用按钮；未按按钮时仍可用小摇杆/十字键兜底，但不会再使用转向轴。如果只想用按钮，可设置 `WheelNavigationSource.CustomButtons`。

## 4. 一个窗口内多个选择框 / 多个方向盘

> `CarouselSelectionController` 是普通 `MonoBehaviour`，不要让它再继承/挂第二个 `UIController`。
> 同一个窗口 GameObject 上只保留 `CharacterSelectUIPlane` 这类窗口控制器；轮播自己的 `uiName`
> 只用于“确认/取消时关闭哪个窗口”，留空则会向上查找最近的 `UIController`。

给每个 `CarouselSelectionController` 设置 **Player Slot**：

- `0`：键盘/鼠标 + 第一个实体控制器（第一个手柄或方向盘）
- `1`：第二个实体控制器
- `-1`：任意玩家（没有具体槽位目标时兜底）

焦点域按 `PlayerSlot` 精确路由；同一焦点域里同一槽位使用**目标栈**：后注册的目标优先，
关闭/禁用后自动回退到前一个，因此父面板 + 子面板可以共用同一个槽位。
同一玩家需要同时操作多个并列选择框时，请用后面的 `CarouselSelectionGroup`。

多方向盘时建议：

1. 每个选择框设置不同的 Player Slot；
2. 关闭选择框的 **Set Event System Focus**，避免多个选择框互相抢焦点；
3. 关闭每个选择框的 **Close On Confirm**，改为订阅 `onConfirmed`，等所有玩家都确认后再统一关闭窗口；
4. 需要固定“哪只方向盘对应哪个槽位”时，在设置界面调用：

```csharp
UIInputSystem.Instance.AssignPlayerSlot(device, slot: 0); // slot < 0 恢复自动分配
```

设备按 `product + serial` 持久化，因此 Windows 重新枚举设备后仍能保持分配。
`GetConnectedDeviceInfos()` 可列出当前所有设备、槽位、确认/取消键显示名，供设置界面使用。

## 5. 轮播布局设置（Options / Columns / Row Sizes）

`CarouselSelectionController` **不负责摆放按钮**，只把 `Options` 数组解释成导航矩阵。
按钮位置由 `GridLayoutGroup`、`Horizontal/VerticalLayoutGroup` 或手动 `RectTransform` 决定。
设置顺序：先摆好按钮 → 再按视觉顺序填 `Options` → 最后填行表。

`Options` 必须是视觉上的**行优先**顺序：从上到下，每行从左到右；`Previews` 使用相同顺序。

| 布局 | 设置 | 说明 |
|---|---|---|
| 线性 | `Columns = 1` + `Navigate Axis` | 一排或一列；只响应指定轴，另一轴作为兜底 |
| 均匀矩阵 | `Columns > 1`，`Row Sizes` 留空 | 按 `Columns` 均匀分列，最后一行可以不满 |
| 不规则矩阵 | `Columns > 1`，`Row Sizes` 填每行数量 | 如 `3,2,2,3`；总和必须等于 `Options` 数量 |

`Row Sizes` 是**每行有几个选项**，不是列号。总和与 `Options` 数量不一致时会回退到
`Columns` 均匀分列并打印警告。

当前 `CharacterSelectUIPlaneScene` 的按钮实际排布是：

```text
[0] [1] [2]
[3]     [4]
[5]     [6]
[7] [8] [9]
```

即 `Row Sizes = 3,2,2,3`（`Columns` 保持 `3`）。如果填成 `4,2,4`，总和虽然也是 10，
但会把前 4 个当成第一行，导致上下移动错位。

设置后可在 Inspector 中右键 `CarouselSelectionController` 组件标题 → **打印布局索引**：

- 打印每个索引的按钮名、坐标、当前 `row/col`；
- 按按钮坐标推导真实行数，直接给出建议的 `Row Sizes`；
- 如果 `Options` 顺序不是行优先，会打印按坐标排序后的索引顺序，先按它重排 `Options`。

导航规则：

- 左右：按 `Options` 顺序线性移动，行尾会接到下一行行首（不会卡在行尾）；
- 上下：按行表跨行；目标行同一列不可导航时就近落到该行可用项；
- `options` 中为 `null` 或不可交互的项会被自动跳过。

## 6. 同一玩家操作多个选择框

同时挂 `CarouselSelectionGroup`，把多个 `CarouselSelectionController` 拖入数组：

- 主轴（横向列表的左右、纵向列表的上下）操作当前选择框的选项；
- 副轴（横向列表的上下、纵向列表的左右）在多个选择框之间切换；
- 矩阵选择框（`columns > 1`）会消费所有方向，不会自动切换选择框。

鼠标直接指向某个选择框时，组会自动把它设为活动选择框。

## 7. 重新绑定设备按键

```csharp
// 交互式重绑定：按下方向盘上想使用的按键即可
UIInputSystem.Instance.StartRebind("Submit", displayName => { /* 更新提示文本 */ });
UIInputSystem.Instance.StartRebind("Cancel", displayName => { /* ... */ });

// 只重绑定“菜单键”：只接受指定设备上的按键；device 传 null 表示任意设备
UIInputSystem.Instance.StartDeviceToggleRebind(device,
    displayName => { /* 显示新的菜单键 */ },
    () => { /* 用户取消或按了别的设备 */ });

// 也可以走统一入口（注意 device 是第二个参数）
UIInputSystem.Instance.StartRebind("Toggle", device, displayName => { /* ... */ });

// 程序化设置（controlPath 可来自 InputControl.path）
UIInputSystem.Instance.SetDeviceBinding(device, "Submit", "<Joystick>/button1");
UIInputSystem.Instance.SetDeviceBinding(device, "Toggle", "<Joystick>/button10");
UIInputSystem.Instance.ResetDeviceBindings(device);
```

键盘/手柄的确认取消已经拆成独立字段：

- 键盘：`CurrentBinding.submit` / `CurrentBinding.cancel`
- 手柄：`CurrentBinding.gamepadSubmit` / `CurrentBinding.gamepadCancel`

`StartRebind("Submit", ...)` 会根据捕获到的设备写入对应字段；`SetDeviceBinding(keyboard/gamepad, ...)` 同理。
方向盘/飞行摇杆的覆盖按设备持久化，不会影响其它控制器；
手柄/键盘的菜单键是全局设置，写入 `UIInputBinding.toggleToUIGamepad` / `toggleToUI` 并随 `SaveBindings()` 持久化。
`GetConnectedDeviceInfos()` 返回的 `ToggleDisplayName` 可用于设置界面显示当前菜单键。

## 8. 相关文件

- `Assets/Astray/Core/System/UIInputSystem/UIInputSystem.cs`：设备轮询、玩家槽位、焦点域路由、事件派发
- `Assets/Astray/Core/System/UIInputSystem/UIFocusScope.cs`：窗口焦点域，负责窗口栈顶与 PlayerSlot 路由
- `Assets/Astray/Core/System/UIInputSystem/UIInputDeviceProfile.cs`：设备识别、默认按键、按设备重绑定
- `Assets/Astray/Core/System/UIInputSystem/UIInputTargets.cs`：导航目标接口与事件结构
- `Assets/Astray/UI/Select/CarouselSelectionController.cs`：轮播选择框
- `Assets/Astray/UI/Select/CarouselSelectionGroup.cs`：同一玩家的多选择框切换
- `Assets/Astray/UI/Select/CarouselMenuStack.cs`：开始菜单的层级跳转
- `Assets/Astray/UI/Select/SettingsPanelController.cs`：单页设置的滑动条/下拉框/开关与导航
- `Assets/Astray/UI/Select/StartMenuActions.cs`：开始菜单退出
- `Assets/Astray/UI/Select/LocalMultiplayerSelectionManager.cs`：P1-Pn 位置/颜色/准备与读秒
- `Assets/Astray/UI/Select/LocalMultiplayerPlayerCard.cs`：单个玩家卡片与输入目标
- `Assets/Astray/UI/Select/LocalMultiplayerTrackView.cs`：赛道位置槽的 P#/颜色标记
- `Assets/Astray/UI/Select/CarouselPointerBridge.cs`：鼠标悬停/滚轮转发
