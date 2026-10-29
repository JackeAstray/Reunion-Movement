using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.UI;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace ReunionMovement.Core.UIInput
{
    public partial class UIInputSystem
    {
        #region 导航目标与焦点域

        /// <summary>
        /// 注册 UI 导航目标（轮播/选择组在 OnEnable 时调用）。
        /// 目标会注册到最近的 UIFocusScope（按 UIController/Canvas 自动创建），
        /// 只有栈顶焦点域会收到输入。
        /// </summary>
        public void RegisterNavigationTarget(IUINavigationTarget target, bool requestUIControl = true)
        {
            if (target == null) return;
            if (target is UnityEngine.Object targetObject && targetObject == null) return;

            // 重复注册时先移除旧映射，避免 PlayerSlot 变化后残留
            if (targetScopes.TryGetValue(target, out var previous) && previous != null)
            {
                previous.Unregister(target);
                targetScopes.Remove(target);
            }

            var scope = target is Component component
                ? GetOrCreateFocusScope(component.transform, requestUIControl)
                : GetTopFocusScope();

            if (scope == null)
            {
                Log.Warning("[UIInputSystem] 无法为 {0} 创建焦点域（缺少 UIController/Canvas）", target.GetType().Name);
                return;
            }

            scope.Register(target);
            targetScopes[target] = scope;
        }

        /// <summary>取消注册 UI 导航目标。</summary>
        public void UnregisterNavigationTarget(IUINavigationTarget target)
        {
            if (target == null) return;

            if (targetScopes.TryGetValue(target, out var scope))
            {
                if (scope != null) scope.Unregister(target);
                targetScopes.Remove(target);
                return;
            }

            for (int i = 0; i < focusScopes.Count; i++)
            {
                var focusScope = focusScopes[i];
                if (focusScope != null) focusScope.Unregister(target);
            }
        }

        /// <summary>找到目标所属窗口的焦点域；没有则按 UIController/Canvas 自动创建。</summary>
        public UIFocusScope GetOrCreateFocusScope(Transform context, bool requestUIControl = true)
        {
            if (context == null) return GetTopFocusScope();

            var scope = context.GetComponentInParent<UIFocusScope>(true);
            var controller = context.GetComponentInParent<UIController>(true);

            // 子面板手动挂了 UIFocusScope：它比父窗口控制器更近，优先使用
            if (scope != null && (controller == null || IsSelfOrDescendant(controller.transform, scope.transform)))
            {
                // 已存在的焦点域由窗口（UIController）决定是否请求 UI 控制，轮播不覆盖
                return scope;
            }

            // 最近的是子窗口 UIController 而它还没有焦点域：在子窗口上创建，
            // 不能借用父窗口的焦点域，否则子窗口输入会落到父窗口。
            var canvas = context.GetComponentInParent<Canvas>(true);
            var host = controller != null
                ? controller.gameObject
                : (canvas != null ? canvas.gameObject : context.root.gameObject);

            if (host == null) return null;

            scope = host.GetComponent<UIFocusScope>();
            if (scope == null)
            {
                scope = host.AddComponent<UIFocusScope>();
                scope.RequestUIControl = requestUIControl;
            }
            return scope;
        }

        /// <summary>node 是否为 ancestor 本身或 ancestor 的后代。</summary>
        private static bool IsSelfOrDescendant(Transform ancestor, Transform node)
        {
            return ancestor != null && node != null && (node == ancestor || node.IsChildOf(ancestor));
        }

        /// <summary>窗口打开：压入焦点域并置顶。</summary>
        public void PushFocusScope(UIFocusScope scope)
        {
            if (scope == null) return;

            focusScopes.Remove(scope);
            focusScopes.Add(scope);

            if (scope.RequestUIControl)
            {
                RequestUIControl(scope);
            }
        }

        /// <summary>窗口关闭：移出焦点域并释放 UI 控制请求。</summary>
        public void PopFocusScope(UIFocusScope scope)
        {
            if (scope == null) return;

            focusScopes.Remove(scope);
            ReleaseUIControl(scope);
        }

        /// <summary>窗口重新打开/置顶时调用，保证它是最上层焦点域。</summary>
        public void BringFocusScopeToFront(UIFocusScope scope)
        {
            if (scope == null) return;

            focusScopes.Remove(scope);
            focusScopes.Add(scope);

            if (scope.RequestUIControl)
            {
                RequestUIControl(scope);
            }
        }

        private UIFocusScope GetTopFocusScope()
        {
            for (int i = focusScopes.Count - 1; i >= 0; i--)
            {
                var scope = focusScopes[i];
                if (scope == null)
                {
                    focusScopes.RemoveAt(i);
                    continue;
                }
                if (!scope.IsActive)
                {
                    ReleaseUIControl(scope);
                    focusScopes.RemoveAt(i);
                    continue;
                }
                return scope;
            }
            return null;
        }


        /// <summary>
        /// 设备 → 玩家槽位。
        /// 键盘/鼠标固定为主玩家 0；实体控制器优先使用 AssignPlayerSlot 保存的槽位，
        /// 未分配时按连接顺序占用 0、1、2……
        /// 因此单方向盘 + 键盘时两者都控制玩家 0；双方向盘时第二个自动成为玩家 1。
        /// </summary>
        public int GetPlayerIndex(InputDevice device)
        {
            if (device == null || device is Keyboard || device is Mouse) return KeyboardMousePlayerIndex;

            int assigned = GetAssignedPlayerSlot(device);
            if (assigned >= 0) return assigned;

            int groupAssigned = GetAssignedPlayerSlotForGroup(device);
            if (groupAssigned >= 0) return groupAssigned;

            return GetAutoPlayerSlot(device);
        }

        /// <summary>同一物理设备的其它子设备被显式分配了槽位时，整个设备组跟随该槽位。</summary>
        private int GetAssignedPlayerSlotForGroup(InputDevice device)
        {
            string groupKey = UIInputDeviceRegistry.GetPlayerGroupKey(device);
            EnsurePlayerSlotsLoaded();

            for (int i = 0; i < routedDevices.Count; i++)
            {
                var candidate = routedDevices[i];
                if (candidate == null) continue;
                if (UIInputDeviceRegistry.GetPlayerGroupKey(candidate) != groupKey) continue;

                string key = UIInputDeviceRegistry.GetDeviceKey(candidate);
                if (playerSlotOverrides.TryGetValue(key, out int slot)) return slot;
            }

            return -1;
        }

        /// <summary>
        /// 自动槽位按“物理设备分组键”分配，而不是按 routedDevices 的索引。
        /// 方向盘常枚举成多个 InputDevice（底座/踏板/轮缘），它们必须落在同一个玩家槽位。
        /// </summary>
        private int GetAutoPlayerSlot(InputDevice device)
        {
            string groupKey = UIInputDeviceRegistry.GetPlayerGroupKey(device);
            playerGroupKeyBuffer.Clear();

            int slot = 0;
            for (int i = 0; i < routedDevices.Count; i++)
            {
                var candidate = routedDevices[i];
                if (candidate == null) continue;

                string candidateKey = UIInputDeviceRegistry.GetPlayerGroupKey(candidate);
                if (!playerGroupKeyBuffer.Add(candidateKey)) continue;

                // 跳过已被其它设备显式占用的槽位
                while (IsExplicitSlotOccupied(slot))
                {
                    slot++;
                }

                if (candidateKey == groupKey) return slot;
                slot++;
            }

            return KeyboardMousePlayerIndex;
        }

        private bool IsExplicitSlotOccupied(int slot)
        {
            EnsurePlayerSlotsLoaded();
            for (int i = 0; i < routedDevices.Count; i++)
            {
                var device = routedDevices[i];
                if (device == null) continue;
                string key = UIInputDeviceRegistry.GetDeviceKey(device);
                if (playerSlotOverrides.TryGetValue(key, out int assigned) && assigned == slot)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>玩家槽位对应的实体控制器（没有实体控制器时返回 null，键盘/鼠标仍可控制玩家 0）。</summary>
        public InputDevice GetPlayerDevice(int playerIndex)
        {
            if (playerIndex < 0) return null;

            // 以实际派发用的 GetPlayerIndex 为准，避免“设备被显式分配到别的槽位”时返回错设备
            for (int i = 0; i < routedDevices.Count; i++)
            {
                var device = routedDevices[i];
                if (device == null) continue;
                if (GetPlayerIndex(device) == playerIndex)
                {
                    return device;
                }
            }

            return null;
        }

        /// <summary>当前可用的玩家数量（至少 1，对应键盘/鼠标）。</summary>
        public int PlayerCount
        {
            get
            {
                int max = 0;
                for (int i = 0; i < routedDevices.Count; i++)
                {
                    var device = routedDevices[i];
                    if (device == null) continue;
                    max = Mathf.Max(max, GetPlayerIndex(device) + 1);
                }

                EnsurePlayerSlotsLoaded();
                foreach (var kv in playerSlotOverrides)
                {
                    if (kv.Value + 1 > max) max = kv.Value + 1;
                }
                return Mathf.Max(1, max);
            }
        }

        /// <summary>读取设备被显式分配的玩家槽位（-1 表示按连接顺序自动分配）。</summary>
        public int GetAssignedPlayerSlot(InputDevice device)
        {
            if (device == null) return -1;
            EnsurePlayerSlotsLoaded();
            string key = UIInputDeviceRegistry.GetDeviceKey(device);
            return playerSlotOverrides.TryGetValue(key, out int slot) ? slot : -1;
        }

        /// <summary>
        /// 把设备固定到指定玩家槽位（按设备 product 持久化）。
        /// 多方向盘场景建议在设置界面中显式分配，避免 Windows 设备枚举顺序变化导致选错选择框。
        /// slot &lt; 0 表示恢复自动分配。
        /// </summary>
        public void AssignPlayerSlot(InputDevice device, int slot)
        {
            if (device == null) return;
            EnsurePlayerSlotsLoaded();

            string key = UIInputDeviceRegistry.GetDeviceKey(device);
            if (slot < 0)
            {
                playerSlotOverrides.Remove(key);
            }
            else
            {
                playerSlotOverrides[key] = slot;
            }
            SavePlayerSlots();
        }

        /// <summary>清除所有设备的玩家槽位固定分配。</summary>
        public void ClearPlayerSlotAssignments()
        {
            EnsurePlayerSlotsLoaded();
            if (playerSlotOverrides.Count == 0) return;
            playerSlotOverrides.Clear();
            SavePlayerSlots();
        }

        private void EnsurePlayerSlotsLoaded()
        {
            if (playerSlotsLoaded) return;
            playerSlotsLoaded = true;
            playerSlotOverrides = new Dictionary<string, int>(8);

            string json = PlayerPrefs.GetString(PlayerSlotPrefsKey, null);
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var list = JsonUtility.FromJson<PlayerSlotList>(json);
                if (list?.items == null) return;
                for (int i = 0; i < list.items.Count; i++)
                {
                    var entry = list.items[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.deviceKey))
                    {
                        playerSlotOverrides[entry.deviceKey] = entry.slot;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("UIInputSystem: 玩家槽位配置读取失败: {0}", ex.Message);
            }
        }

        private void SavePlayerSlots()
        {
            EnsurePlayerSlotsLoaded();
            var list = new PlayerSlotList();
            foreach (var kv in playerSlotOverrides)
            {
                list.items.Add(new PlayerSlotEntry { deviceKey = kv.Key, slot = kv.Value });
            }
            PlayerPrefs.SetString(PlayerSlotPrefsKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }

        /// <summary>已接入的实体控制器数量。</summary>
        public int RoutedDeviceCount => routedDevices.Count;

        /// <summary>获取当前所有 UI 控制设备的信息（用于设置界面/按键提示）。</summary>
        public List<UIInputDeviceInfo> GetConnectedDeviceInfos(List<UIInputDeviceInfo> result = null)
        {
            result ??= new List<UIInputDeviceInfo>(8);
            result.Clear();
            deviceInfoGroupBuffer.Clear();

            // 键盘字段独立；解析失败时显示默认键，避免设置界面出现空文本
            string keyboardSubmitDisplay = GetKeyFromName(CurrentBinding.submit) != Key.None &&
                                           !string.IsNullOrEmpty(CurrentBinding.submitDisplayName)
                ? CurrentBinding.submitDisplayName
                : "Enter";
            string keyboardCancelDisplay = GetKeyFromName(CurrentBinding.cancel) != Key.None &&
                                           !string.IsNullOrEmpty(CurrentBinding.cancelDisplayName)
                ? CurrentBinding.cancelDisplayName
                : "Escape";

            result.Add(new UIInputDeviceInfo(
                null,
                KeyboardMousePlayerIndex,
                UIInputDeviceKind.KeyboardMouse,
                "键盘 / 鼠标",
                keyboardSubmitDisplay,
                keyboardCancelDisplay,
                "WASD / 方向键",
                CurrentBinding.toggleToUIDisplayName));

            for (int i = 0; i < routedDevices.Count; i++)
            {
                var device = routedDevices[i];
                if (device == null || !device.added) continue;

                // 同一物理设备的多个 HID 子设备只显示一行
                string groupKey = UIInputDeviceRegistry.GetPlayerGroupKey(device);
                if (!deviceInfoGroupBuffer.Add(groupKey)) continue;

                var profile = UIInputDeviceRegistry.GetProfile(device);
                bool wheelMode = profile.Kind == UIInputDeviceKind.Gamepad &&
                                 UIInputDeviceRegistry.LooksLikeRacingWheel(device);
                var kind = wheelMode ? UIInputDeviceKind.RacingWheel : profile.Kind;
                string navigateDisplay = wheelMode
                    ? "右摇杆 / 十字键"
                    : UIInputDeviceRegistry.GetActionDisplayName(device, "Navigate");
                bool isGamepad = profile.Kind == UIInputDeviceKind.Gamepad;
                string submitDisplay = isGamepad
                    ? CurrentBinding.gamepadSubmitDisplayName
                    : UIInputDeviceRegistry.GetActionDisplayName(device, "Submit");
                string cancelDisplay = isGamepad
                    ? CurrentBinding.gamepadCancelDisplayName
                    : UIInputDeviceRegistry.GetActionDisplayName(device, "Cancel");
                result.Add(new UIInputDeviceInfo(
                    device,
                    GetPlayerIndex(device),
                    kind,
                    UIInputDeviceRegistry.GetDeviceDisplayName(device),
                    submitDisplay,
                    cancelDisplay,
                    navigateDisplay,
                    UIInputDeviceRegistry.GetActionDisplayName(device, "Toggle")));
            }

            return result;
        }

        /// <summary>为指定设备设置确认/取消/切换键（方向盘/飞行摇杆重绑定使用）。</summary>
        public void SetDeviceBinding(InputDevice device, string actionName, string controlPath)
        {
            bool isToggle = string.Equals(actionName, "Toggle", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(actionName, "Menu", StringComparison.OrdinalIgnoreCase);

            string display = string.IsNullOrEmpty(controlPath)
                ? null
                : InputControlPath.ToHumanReadableString(
                    controlPath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice |
                    InputControlPath.HumanReadableStringOptions.UseShortNames);

            string keyboardValue = string.IsNullOrEmpty(controlPath)
                ? controlPath
                : (controlPath.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)
                    ? controlPath.Substring(controlPath.LastIndexOf('/') + 1)
                    : controlPath);

            bool changedGlobal = false;

            // 手柄/键盘的确认、取消、菜单键是全局字段（所有同类设备共用），
            // 写入设备注册表不会生效，因此这里直接写 CurrentBinding。
            if (device is Gamepad)
            {
                if (isToggle)
                {
                    CurrentBinding.toggleToUIGamepad = controlPath;
                    CurrentBinding.toggleToUIGamepadDisplayName = display;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.gamepadSubmit = controlPath;
                    CurrentBinding.gamepadSubmitDisplayName = display;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.gamepadCancel = controlPath;
                    CurrentBinding.gamepadCancelDisplayName = display;
                    changedGlobal = true;
                }
                else
                {
                    Log.Warning("UIInputSystem: 手柄的方向导航固定为十字键/摇杆，忽略 SetDeviceBinding(\"{0}\")。", actionName);
                }
            }
            else if (device is Keyboard)
            {
                if (isToggle)
                {
                    CurrentBinding.toggleToUI = keyboardValue;
                    CurrentBinding.toggleToUIDisplayName = display;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.submit = keyboardValue;
                    CurrentBinding.submitDisplayName = display;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.cancel = keyboardValue;
                    CurrentBinding.cancelDisplayName = display;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "NavigateUp", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.navigateUp = keyboardValue;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "NavigateDown", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.navigateDown = keyboardValue;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "NavigateLeft", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.navigateLeft = keyboardValue;
                    changedGlobal = true;
                }
                else if (string.Equals(actionName, "NavigateRight", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentBinding.navigateRight = keyboardValue;
                    changedGlobal = true;
                }
                else
                {
                    UIInputDeviceRegistry.SetOverride(device, actionName, controlPath);
                }
            }
            else
            {
                // "Menu" 是 "Toggle" 的别名，设备注册表只认 "Toggle"
                UIInputDeviceRegistry.SetOverride(device, isToggle ? "Toggle" : actionName, controlPath, display);
            }

            if (changedGlobal)
            {
                SaveBindings();
            }

            BindingChangedSubject?.OnNext(CurrentBinding);
        }

        /// <summary>恢复指定设备的默认 UI 按键。</summary>
        public void ResetDeviceBindings(InputDevice device)
        {
            UIInputDeviceRegistry.ClearOverride(device);

            // 手柄/键盘使用全局字段，重置时一并恢复默认值
            if (device is Gamepad)
            {
                var defaults = UIInputBinding.CreateDefault();
                CurrentBinding.gamepadSubmit = defaults.gamepadSubmit;
                CurrentBinding.gamepadSubmitDisplayName = defaults.gamepadSubmitDisplayName;
                CurrentBinding.gamepadCancel = defaults.gamepadCancel;
                CurrentBinding.gamepadCancelDisplayName = defaults.gamepadCancelDisplayName;
                CurrentBinding.toggleToUIGamepad = defaults.toggleToUIGamepad;
                CurrentBinding.toggleToUIGamepadDisplayName = defaults.toggleToUIGamepadDisplayName;
                SaveBindings();
            }
            else if (device is Keyboard)
            {
                var defaults = UIInputBinding.CreateDefault();
                CurrentBinding.submit = defaults.submit;
                CurrentBinding.submitDisplayName = defaults.submitDisplayName;
                CurrentBinding.cancel = defaults.cancel;
                CurrentBinding.cancelDisplayName = defaults.cancelDisplayName;
                CurrentBinding.toggleToUI = defaults.toggleToUI;
                CurrentBinding.toggleToUIDisplayName = defaults.toggleToUIDisplayName;
                SaveBindings();
            }

            BindingChangedSubject?.OnNext(CurrentBinding);
        }


        /// <summary>
        /// 为设备配置自定义导航按钮（适用于没有十字键的方向盘，如 MOZA RS50）。
        /// 例如把轮缘上的旋钮左右/拨片绑定为 left/right，传 null 表示该方向不绑定。
        /// 配置后优先使用这些按钮，不再使用转向轴导航。
        /// </summary>
        public void SetDeviceNavigation(
            InputDevice device,
            string upControl,
            string downControl,
            string leftControl,
            string rightControl)
        {
            UIInputDeviceRegistry.SetNavigationButtons(device, upControl, downControl, leftControl, rightControl);
            if (device != null)
            {
                steeringStates.Remove(device.deviceId);
            }
            BindingChangedSubject?.OnNext(CurrentBinding);
        }

        /// <summary>清除设备的自定义导航按钮，恢复 hat / 小摇杆 / 转向轴导航。</summary>
        public void ResetDeviceNavigation(InputDevice device)
        {
            UIInputDeviceRegistry.ClearNavigationButtons(device);
            BindingChangedSubject?.OnNext(CurrentBinding);
        }

        /// <summary>
        /// 强制指定方向盘导航来源。
        /// 默认 Auto：自定义按钮 &gt; 小摇杆 &gt; 十字键/hat &gt; 转向轴。
        /// 例如 RS50 想固定用小摇杆：
        /// SetWheelNavigationSource(device, WheelNavigationSource.ThumbStick)。
        /// </summary>
        public void SetWheelNavigationSource(InputDevice device, WheelNavigationSource source)
        {
            UIInputDeviceRegistry.SetWheelNavigationSource(device, source);
            if (device != null)
            {
                steeringStates.Remove(device.deviceId);
            }
            BindingChangedSubject?.OnNext(CurrentBinding);
        }

        /// <summary>自动启用 UI 控制（引用计数）。轮播等界面在 OnEnable 调用，OnDisable 调用 ReleaseUIControl。</summary>
        public void RequestUIControl(object owner)
        {
            if (owner == null) return;
            uiControlRequests.Add(owner);
            if (isInited && currentMode != UIControlMode.UIControl)
            {
                EnableUIControl();
            }
        }

        /// <summary>释放自动启用请求；最后一个持有者释放后回到 Gameplay 模式。</summary>
        public void ReleaseUIControl(object owner)
        {
            if (owner == null) return;
            if (!uiControlRequests.Remove(owner)) return;
            if (uiControlRequests.Count == 0 && currentMode == UIControlMode.UIControl)
            {
                DisableUIControl();
            }
        }

        private void OnInputDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            devicesDirty = true;

            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
            {
                UIInputDeviceRegistry.Invalidate(device);
                if (device != null)
                {
                    repeatStates.Remove(device.deviceId);
                    steeringStates.Remove(device.deviceId);
                    gamepadSubmitCache.Remove(device.deviceId);
                    gamepadCancelCache.Remove(device.deviceId);
                }
                return;
            }

            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                UIInputDeviceRegistry.Invalidate(device);
                // 先刷新槽位再输出日志，确保日志中的 P{n} 与后续实际派发一致
                RefreshRoutedDevices();
                LogDeviceMapping(device);
            }
        }

        /// <summary>输出设备实际采用的确认/取消/导航键，便于排查不同型号方向盘的按钮编号。</summary>
        private void LogDeviceMapping(InputDevice device)
        {
            if (!(device is Gamepad) && !(device is Joystick)) return;
            if (!routedDevices.Contains(device)) return; // 纯轴子设备已被过滤，不打印成玩家

            var profile = UIInputDeviceRegistry.GetProfile(device);
            bool wheelMode = profile.Kind == UIInputDeviceKind.Gamepad &&
                             UIInputDeviceRegistry.LooksLikeRacingWheel(device);
            string kindName = wheelMode
                ? "方向盘（手柄模式）"
                : profile.Kind switch
                {
                    UIInputDeviceKind.RacingWheel => "方向盘",
                    UIInputDeviceKind.FlightStick => "飞行摇杆",
                    UIInputDeviceKind.Gamepad => "手柄",
                    _ => "摇杆",
                };
            bool isGamepad = device is Gamepad;

            // 排查“一个方向盘识别成多个玩家”：显示 product/serial 与分组键
            Log.Info("[UIInputSystem] 设备路由：{0} → P{1}（product={2}，serial={3}，group={4}）",
                UIInputDeviceRegistry.GetDeviceDisplayName(device),
                GetPlayerIndex(device) + 1,
                device.description.product,
                device.description.serial,
                UIInputDeviceRegistry.GetPlayerGroupKey(device));

            Log.Info(
                "[UIInputSystem] P{0} 检测到{1}：{2}；确认={3}，取消={4}，导航={5}，菜单={6}。可在设置中按设备重新绑定。",
                GetPlayerIndex(device),
                kindName,
                UIInputDeviceRegistry.GetDeviceDisplayName(device),
                isGamepad ? CurrentBinding.gamepadSubmitDisplayName : UIInputDeviceRegistry.GetActionDisplayName(device, "Submit"),
                isGamepad ? CurrentBinding.gamepadCancelDisplayName : UIInputDeviceRegistry.GetActionDisplayName(device, "Cancel"),
                UIInputDeviceRegistry.GetActionDisplayName(device, "Navigate"),
                isGamepad ? CurrentBinding.toggleToUIGamepadDisplayName : UIInputDeviceRegistry.GetActionDisplayName(device, "Toggle"));

            if (wheelMode)
            {
                Log.Info("[UIInputSystem] 方向盘以手柄模式上报：导航使用右摇杆/十字键，左摇杆（转向轴）已忽略。");
            }

            ButtonControl submitControl;
            ButtonControl cancelControl;
            ButtonControl toggleControl;
            if (device is Gamepad gamepad)
            {
                submitControl = InputControlPath.TryFindControl<ButtonControl>(gamepad, CurrentBinding.gamepadSubmit);
                cancelControl = InputControlPath.TryFindControl<ButtonControl>(gamepad, CurrentBinding.gamepadCancel);
                toggleControl = InputControlPath.TryFindControl<ButtonControl>(gamepad, CurrentBinding.toggleToUIGamepad);
            }
            else
            {
                UIInputDeviceRegistry.TryGetSubmit(device, out submitControl);
                UIInputDeviceRegistry.TryGetCancel(device, out cancelControl);
                UIInputDeviceRegistry.TryGetToggle(device, out toggleControl);
            }

            Log.Info(
                "[UIInputSystem] {0} 实际控件路径：确认={1}，取消={2}，菜单={3}",
                UIInputDeviceRegistry.GetDeviceDisplayName(device),
                submitControl != null ? submitControl.path : "未检测到",
                cancelControl != null ? cancelControl.path : "未检测到",
                toggleControl != null ? toggleControl.path : "未检测到");

            var toggle = toggleControl;
            if (toggle != null)
            {
                bool conflicts = submitControl == toggle || cancelControl == toggle;
                if (conflicts)
                {
                    Log.Warning(
                        "[UIInputSystem] 设备 {0} 的菜单键与确认/取消键相同，切换模式时会抑制本帧输入。" +
                        "建议在设置中把菜单键重绑定到其它按钮。",
                        UIInputDeviceRegistry.GetDeviceDisplayName(device));
                }
                else if (toggle.name.StartsWith("button", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Info(
                        "[UIInputSystem] 设备 {0} 的菜单键使用 {1} 兜底；如果它不是 Start/Options 键，" +
                        "请用 StartRebind(\"Toggle\", ...) 重绑。",
                        UIInputDeviceRegistry.GetDeviceDisplayName(device), toggle.name);
                }
            }

            if (profile.Kind == UIInputDeviceKind.RacingWheel)
            {
                bool hasHat = UIInputDeviceRegistry.GetHat(device) != null;
                bool hasThumb = UIInputDeviceRegistry.TryGetThumbStick(device, out var thumb, out var thumbX, out var thumbY);
                bool hasSteering = UIInputDeviceRegistry.GetSteeringAxis(device) != null;

                Log.Info(
                    "[UIInputSystem] 方向盘导航候选：十字键={0}，小摇杆={1}，转向轴={2}；当前来源={3}",
                    hasHat, hasThumb, hasSteering, profile.WheelNavigationSource);

                if (hasHat)
                {
                    Log.Info("[UIInputSystem] 十字键/hat 控件：{0}", UIInputDeviceRegistry.GetHat(device)?.path);
                }

                if (hasThumb)
                {
                    Log.Info("[UIInputSystem] 小摇杆控件：{0}",
                        thumb != null ? thumb.path : $"{thumbX?.name} + {thumbY?.name}");
                }
            }

            if (profile.Kind == UIInputDeviceKind.RacingWheel &&
                profile.WheelNavigationSource != WheelNavigationSource.None &&
                !UIInputDeviceRegistry.HasNavigationInput(device))
            {
                Log.Warning(
                    "[UIInputSystem] 方向盘 {0} 未检测到十字键/小摇杆/转向轴，无法用它导航 UI。" +
                    "请调用 SetDeviceNavigation 绑定按钮，或使用键盘/手柄导航。",
                    UIInputDeviceRegistry.GetDeviceDisplayName(device));
            }

            if (profile.Kind == UIInputDeviceKind.Joystick &&
                UIInputDeviceRegistry.GetHat(device) == null)
            {
                Log.Warning(
                    "[UIInputSystem] 未识别的摇杆 {0} 没有 hat，主摇杆不会用于 UI 导航（避免把方向盘转向轴当导航）。" +
                    "如果这是方向盘/飞行摇杆，请用 SetDeviceNavigation 绑定导航按钮。",
                    UIInputDeviceRegistry.GetDeviceDisplayName(device));
            }
        }

        private void RefreshRoutedDevices()
        {
            devicesDirty = false;

            for (int i = routedDevices.Count - 1; i >= 0; i--)
            {
                var device = routedDevices[i];
                if (device == null || !device.added)
                {
                    routedDevices.RemoveAt(i);
                }
            }

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (device == null || !device.added) continue;
                if (device is Gamepad || device is Joystick)
                {
                    // 踏板块等纯轴子设备没有按钮/导航控件，不应该单独算一个玩家
                    if (!HasUiControls(device)) continue;

                    if (!routedDevices.Contains(device))
                    {
                        routedDevices.Add(device);
                    }
                }
            }
        }

        /// <summary>设备是否有可用于 UI 的确认/导航控件；纯踏板/油门轴子设备返回 false。</summary>
        private static bool HasUiControls(InputDevice device)
        {
            if (device is Gamepad) return true;
            if (UIInputDeviceRegistry.TryGetSubmit(device, out _)) return true;
            if (UIInputDeviceRegistry.HasNavigationInput(device)) return true;
            return false;
        }

        #endregion
    }
}
