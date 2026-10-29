using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Astray.Framework.Runtime.UIInput
{
    /// <summary>UI 输入设备类别。</summary>
    public enum UIInputDeviceKind
    {
        KeyboardMouse,
        Gamepad,
        RacingWheel,
        FlightStick,
        Joystick,
    }

    /// <summary>
    /// 方向盘 UI 导航来源。
    /// Auto 的优先级：自定义按钮 &gt; 小摇杆 &gt; 十字键/hat &gt; 转向轴。
    /// 有更合适的输入时不会再使用转向轴，避免轻微打方向导致菜单频繁切换。
    /// </summary>
    public enum WheelNavigationSource
    {
        Auto = 0,
        Hat = 1,
        ThumbStick = 2,
        Steering = 3,
        CustomButtons = 4,
        None = 5,
    }

    /// <summary>
    /// 单个输入设备的 UI 按键/导航配置（只读）。
    /// 方向盘的默认约定：确认 = button1（A / ✕），取消 = button2（Xbox B）。
    /// 罗技 PlayStation 版（G29/G923 PS）的圆圈在 button3，会自动纠正为“取消 = button3（○）”。
    /// 飞行摇杆/普通摇杆：确认 = trigger/button1，取消 = button2，导航 = stick + hat。
    /// </summary>
    public sealed class UIInputDeviceProfile
    {
        public UIInputDeviceKind Kind { get; }
        public string DisplayName { get; }
        public string SubmitControl { get; }
        public string CancelControl { get; }
        public string ToggleControl { get; }
        public string SubmitDisplayName { get; }
        public string CancelDisplayName { get; }
        public string NavigateDisplayName { get; }

        /// <summary>是否允许用 stick（x/y 轴）做 UI 导航。方向盘为 false，避免转向轴误触发菜单。</summary>
        public bool UseStickForNavigation { get; }

        /// <summary>自定义导航按钮（可选）。为空时使用 hat / 摇杆 / 转向轴。</summary>
        public string NavigateUpControl { get; }
        public string NavigateDownControl { get; }
        public string NavigateLeftControl { get; }
        public string NavigateRightControl { get; }

        /// <summary>方向盘导航来源（Auto 时自动选择小摇杆/hat/转向轴）。</summary>
        public WheelNavigationSource WheelNavigationSource { get; }

        public UIInputDeviceProfile(
            UIInputDeviceKind kind,
            string displayName,
            string submitControl,
            string cancelControl,
            string toggleControl,
            string submitDisplayName,
            string cancelDisplayName,
            string navigateDisplayName,
            bool useStickForNavigation,
            string navigateUpControl = null,
            string navigateDownControl = null,
            string navigateLeftControl = null,
            string navigateRightControl = null,
            WheelNavigationSource wheelNavigationSource = WheelNavigationSource.Auto)
        {
            Kind = kind;
            DisplayName = displayName;
            SubmitControl = submitControl;
            CancelControl = cancelControl;
            ToggleControl = toggleControl;
            SubmitDisplayName = submitDisplayName;
            CancelDisplayName = cancelDisplayName;
            NavigateDisplayName = navigateDisplayName;
            UseStickForNavigation = useStickForNavigation;
            NavigateUpControl = navigateUpControl;
            NavigateDownControl = navigateDownControl;
            NavigateLeftControl = navigateLeftControl;
            NavigateRightControl = navigateRightControl;
            WheelNavigationSource = wheelNavigationSource;
        }
    }

    [Serializable]
    internal sealed class UIInputDeviceBindingOverride
    {
        public string deviceKey;
        public string submitControl;
        public string cancelControl;
        public string toggleControl;
        public string submitDisplayName;
        public string cancelDisplayName;
        public string navigateUpControl;
        public string navigateDownControl;
        public string navigateLeftControl;
        public string navigateRightControl;
        public int wheelNavigationSource;
    }

    [Serializable]
    internal sealed class UIInputDeviceBindingOverrideList
    {
        public List<UIInputDeviceBindingOverride> items = new List<UIInputDeviceBindingOverride>();
    }

    /// <summary>
    /// 设备识别与默认按键表。
    /// 仅依赖设备描述字符串分类，因此同一套逻辑可用于手柄、赛车方向盘、飞行摇杆和普通 HID 摇杆。
    /// 用户重绑定的结果按设备 product 持久化到 PlayerPrefs。
    /// </summary>
    public static class UIInputDeviceRegistry
    {
        private const string OverridePrefsKey = "ui_input_device_bindings_v1";

        // 顺序很重要：飞行摇杆关键词先于方向盘关键词，
        // 避免 "Thrustmaster T.16000M" 被 "thrustmaster" 误判成方向盘。
        private static readonly string[] FlightKeywords =
        {
            "flight", "hotas", "flightstick", "flight stick", "t.16000", "t16000",
            "warthog", "x52", "x55", "x56", "gladiator", "virpil", "vkb", "winwing",
            "stecs", "sidestick", "throttle",
        };

        private static readonly string[] WheelKeywords =
        {
            "racing wheel", "steering wheel", "steering", "driving force", "force feedback", "ffb",
            "wheel base", "wheelbase", "direct drive",
            "logitech g25", "logitech g27", "logitech g29", "logitech g920", "logitech g923",
            "g25", "g27", "g29", "g920", "g923",
            "moza", "rs50", "moza r5", "moza r9", "moza r12", "moza r16",
            "fanatec", "csl", "club sport", "dd1", "dd2", "podium",
            "thrustmaster", "t300", "t500", "t150", "t248", "tmx", "ts-xw", "tx racing",
            "simagic", "simucube", "asetek", "vrs",
        };

        // 罗技 PlayStation 布局（G29/G923 PS 版）的 HID 编号：
        // Square=button2、Circle=button3，因此取消键不能再用 “button2,button3” 的默认顺序。
        private static readonly string[] PlayStationWheelKeywords =
        {
            "g29", "g923", "playstation", "ps4", "ps5",
        };

        private static readonly string[] XboxWheelKeywords =
        {
            "g920", "xbox",
        };

        private static readonly UIInputDeviceProfile WheelProfile =
            new UIInputDeviceProfile(
                UIInputDeviceKind.RacingWheel, "赛车方向盘",
                "button1,trigger", "button2,button3",
                "start,options,menu,share,button10,button9,button11,button12,button13,button14",
                "A / ✕ (Button 1)", "B / ○ (Button 2)", "十字键 / Hat",
                useStickForNavigation: false);

        private static readonly UIInputDeviceProfile FlightProfile =
            new UIInputDeviceProfile(
                UIInputDeviceKind.FlightStick, "飞行摇杆",
                "trigger,button1", "button2,button3",
                "start,menu,options,button7,button8",
                "Trigger / 扳机", "Button 2", "摇杆 / Hat",
                useStickForNavigation: true);

        private static readonly UIInputDeviceProfile JoystickProfile =
            new UIInputDeviceProfile(
                UIInputDeviceKind.Joystick, "摇杆",
                "trigger,button1", "button2,button3",
                "start,menu,options,button7,button8",
                "Trigger / Button 1", "Button 2", "摇杆 / Hat",
                useStickForNavigation: true);

        private static readonly UIInputDeviceProfile GamepadProfile =
            new UIInputDeviceProfile(
                UIInputDeviceKind.Gamepad, "手柄",
                "buttonSouth", "buttonEast", "start",
                "A / ✕", "B / ○", "左摇杆 / 十字键",
                useStickForNavigation: true);

        private static readonly UIInputDeviceProfile KeyboardMouseProfile =
            new UIInputDeviceProfile(
                UIInputDeviceKind.KeyboardMouse, "键盘 / 鼠标",
                "enter", "escape", "tab",
                "Enter", "Escape", "WASD / 方向键",
                useStickForNavigation: false);

        private sealed class DeviceCache
        {
            public UIInputDeviceKind Kind;
            public UIInputDeviceProfile Profile;
            public string DeviceKey;
            public Vector2Control Hat;
            public StickControl Stick;
            public AxisControl Steering;
            public Vector2Control ThumbStick;
            public AxisControl ThumbX;
            public AxisControl ThumbY;
            public ButtonControl NavUp;
            public ButtonControl NavDown;
            public ButtonControl NavLeft;
            public ButtonControl NavRight;
            public ButtonControl Submit;
            public ButtonControl Cancel;
            public ButtonControl Toggle;
            public bool SubmitResolved;
            public bool CancelResolved;
            public bool ToggleResolved;
            public bool NavigationResolved;
            public bool IsWheelLike;
        }

        private static readonly Dictionary<int, DeviceCache> s_cache = new Dictionary<int, DeviceCache>(8);
        private static readonly List<ButtonControl> s_directButtons = new List<ButtonControl>(16);
        private static UIInputDeviceBindingOverrideList s_overrides;
        private static bool s_overridesLoaded;

        /// <summary>把任意设备归类为键盘鼠标/手柄/方向盘/飞行摇杆/普通摇杆。</summary>
        public static UIInputDeviceKind Classify(InputDevice device)
        {
            if (device == null || device is Keyboard || device is Mouse || device is Touchscreen || device is Pen)
            {
                return UIInputDeviceKind.KeyboardMouse;
            }
            if (device is Gamepad)
            {
                return UIInputDeviceKind.Gamepad;
            }

            string key = GetDeviceSearchKey(device);
            if (ContainsAny(key, FlightKeywords)) return UIInputDeviceKind.FlightStick;
            if (ContainsAny(key, WheelKeywords)) return UIInputDeviceKind.RacingWheel;
            return UIInputDeviceKind.Joystick;
        }

        /// <summary>
        /// 设备描述是否像赛车方向盘。用于识别“伪装成 Gamepad”的方向盘：
        /// 这类设备的左摇杆通常是转向轴，不能直接用于 UI 导航。
        /// </summary>
        public static bool LooksLikeRacingWheel(InputDevice device)
        {
            if (device == null) return false;
            return GetCache(device).IsWheelLike;
        }

        /// <summary>获取设备的 UI 配置（含玩家重绑定覆盖）。</summary>
        public static UIInputDeviceProfile GetProfile(InputDevice device)
        {
            if (device == null) return KeyboardMouseProfile;

            var cache = GetCache(device);
            return cache.Profile;
        }

        /// <summary>设备分类的展示名称（如 Logitech G923）。</summary>
        public static string GetDeviceDisplayName(InputDevice device)
        {
            if (device == null) return "键盘 / 鼠标";
            string product = device.description.product;
            if (!string.IsNullOrEmpty(product)) return product;
            return string.IsNullOrEmpty(device.displayName) ? device.name : device.displayName;
        }

        /// <summary>确认键控件（解析失败时回退到设备上第一个直接按钮）。</summary>
        public static bool TryGetSubmit(InputDevice device, out ButtonControl control)
        {
            var cache = GetCache(device);
            if (!cache.SubmitResolved)
            {
                cache.Submit = ResolveButton(device, cache.Profile.SubmitControl)
                    ?? GetDirectButton(device, 0);
                cache.SubmitResolved = true;
            }
            control = cache.Submit;
            return control != null;
        }

        /// <summary>取消键控件（解析失败时回退到设备上第二个直接按钮）。</summary>
        public static bool TryGetCancel(InputDevice device, out ButtonControl control)
        {
            var cache = GetCache(device);

            // 确保确认键已解析，避免单按钮设备把同一按钮同时用作确认与取消
            if (!cache.SubmitResolved && device != null)
            {
                TryGetSubmit(device, out _);
                cache = GetCache(device);
            }

            if (!cache.CancelResolved)
            {
                var cancel = ResolveButton(device, cache.Profile.CancelControl)
                    ?? GetDirectButton(device, 1)
                    ?? GetDirectButton(device, 0);

                if (cancel != null && cancel == cache.Submit)
                {
                    cancel = GetDirectButton(device, 2);
                    if (cancel == cache.Submit) cancel = null;
                }
                cache.Cancel = cancel;
                cache.CancelResolved = true;
            }
            control = cache.Cancel;
            return control != null;
        }

        /// <summary>切换 UI 模式的按键（默认 start，找不到时返回 false）。</summary>
        public static bool TryGetToggle(InputDevice device, out ButtonControl control)
        {
            var cache = GetCache(device);
            if (!cache.ToggleResolved)
            {
                // 先按型号默认键解析，再使用 Input System 的 {Menu} usage 兜底
                cache.Toggle = ResolveButton(device, cache.Profile.ToggleControl)
                    ?? InputControlPath.TryFindControl<ButtonControl>(device, "*/{Menu}");
                cache.ToggleResolved = true;
            }
            control = cache.Toggle;
            return control != null;
        }

        /// <summary>十字键/hat（可能为空）。</summary>
        public static Vector2Control GetHat(InputDevice device)
        {
            return GetCache(device).Hat;
        }

        /// <summary>主摇杆轴（方向盘上的 stick 实际是转向轴，调用方应按 profile.UseStickForNavigation 决定是否使用）。</summary>
        public static StickControl GetStick(InputDevice device)
        {
            return GetCache(device).Stick;
        }

        /// <summary>
        /// 方向盘转向轴（X 轴）。没有十字键的方向盘用它做 UI 导航。
        /// 大多数 HID 方向盘把转向放在 X 轴，Input System 会把 X/Y 合并成 stick。
        /// </summary>
        public static AxisControl GetSteeringAxis(InputDevice device)
        {
            return GetCache(device).Steering;
        }

        /// <summary>
        /// 方向盘上的小摇杆/第二摇杆。返回 true 表示检测到可用的 2D 控件或 rx/ry 轴对。
        /// </summary>
        public static bool TryGetThumbStick(
            InputDevice device,
            out Vector2Control stick,
            out AxisControl axisX,
            out AxisControl axisY)
        {
            var cache = GetCache(device);
            stick = cache.ThumbStick;
            axisX = cache.ThumbX;
            axisY = cache.ThumbY;
            return stick != null || (axisX != null && axisY != null);
        }

        /// <summary>解析用户为设备配置的自定义导航按钮（未配置时全部为 null）。</summary>
        public static bool TryGetNavigationButtons(
            InputDevice device,
            out ButtonControl up,
            out ButtonControl down,
            out ButtonControl left,
            out ButtonControl right)
        {
            var cache = GetCache(device);
            if (!cache.NavigationResolved)
            {
                cache.NavUp = ResolveButton(device, cache.Profile.NavigateUpControl);
                cache.NavDown = ResolveButton(device, cache.Profile.NavigateDownControl);
                cache.NavLeft = ResolveButton(device, cache.Profile.NavigateLeftControl);
                cache.NavRight = ResolveButton(device, cache.Profile.NavigateRightControl);
                cache.NavigationResolved = true;
            }

            up = cache.NavUp;
            down = cache.NavDown;
            left = cache.NavLeft;
            right = cache.NavRight;
            return up != null || down != null || left != null || right != null;
        }

        /// <summary>该设备是否至少有一种 UI 导航输入（hat / 摇杆 / 转向轴 / 自定义按钮）。</summary>
        public static bool HasNavigationInput(InputDevice device)
        {
            var cache = GetCache(device);
            bool hasCustom = TryGetNavigationButtons(device, out _, out _, out _, out _);
            bool hasThumb = TryGetThumbStick(device, out _, out _, out _);

            if (cache.Profile.Kind == UIInputDeviceKind.RacingWheel)
            {
                switch (cache.Profile.WheelNavigationSource)
                {
                    case WheelNavigationSource.None: return false;
                    case WheelNavigationSource.CustomButtons: return hasCustom;
                    case WheelNavigationSource.ThumbStick: return hasThumb;
                    case WheelNavigationSource.Hat: return cache.Hat != null;
                    case WheelNavigationSource.Steering: return cache.Steering != null;
                }

                return hasCustom || hasThumb || cache.Hat != null || cache.Steering != null;
            }

            return hasCustom ||
                   cache.Hat != null ||
                   (cache.Stick != null && cache.Profile.UseStickForNavigation);
        }

        /// <summary>当前动作的显示名称（如 "A / ✕ (Button 1)"）。</summary>
        public static string GetActionDisplayName(InputDevice device, string actionName)
        {
            if (device == null) return actionName;
            var cache = GetCache(device);
            switch (actionName)
            {
                case "Submit":
                    return cache.Profile.SubmitDisplayName;
                case "Cancel":
                    return cache.Profile.CancelDisplayName;
                case "Toggle":
                    if (TryGetToggle(device, out var toggle) && toggle != null)
                    {
                        if (!string.IsNullOrEmpty(toggle.displayName) && toggle.displayName != "Button")
                        {
                            return toggle.displayName;
                        }
                        return InputControlPath.ToHumanReadableString(
                            toggle.path,
                            InputControlPath.HumanReadableStringOptions.OmitDevice |
                            InputControlPath.HumanReadableStringOptions.UseShortNames);
                    }

                    return "未检测到（可重绑）";
                default:
                    if (cache.Profile.Kind == UIInputDeviceKind.RacingWheel)
                    {
                        switch (cache.Profile.WheelNavigationSource)
                        {
                            case WheelNavigationSource.None:
                                return "已禁用";
                            case WheelNavigationSource.CustomButtons:
                                return TryGetNavigationButtons(device, out _, out _, out _, out _)
                                    ? "自定义导航按钮" : "自定义按钮（未绑定）";
                            case WheelNavigationSource.ThumbStick:
                                return TryGetThumbStick(device, out _, out _, out _)
                                    ? "小摇杆" : "小摇杆（未检测到）";
                            case WheelNavigationSource.Hat:
                                return cache.Hat != null ? "十字键 / Hat" : "十字键（未检测到）";
                            case WheelNavigationSource.Steering:
                                return cache.Steering != null
                                    ? "转向轴（左右打方向，回正后再切换）" : "转向轴（未检测到）";
                        }

                        // Auto：优先级 自定义按钮 > 小摇杆 > 十字键 > 转向轴
                        if (TryGetNavigationButtons(device, out _, out _, out _, out _)) return "自定义导航按钮";
                        if (TryGetThumbStick(device, out var thumb, out _, out _) &&
                            (thumb != null || cache.Hat == null))
                        {
                            return "小摇杆";
                        }
                        if (cache.Hat != null) return "十字键 / Hat";
                        if (cache.Steering != null) return "转向轴（左右打方向，回正后再切换）";
                        return "无（未检测到十字键 / 小摇杆 / 转向轴）";
                    }

                    if (TryGetNavigationButtons(device, out _, out _, out _, out _)) return "自定义导航按钮";
                    if (cache.Hat != null) return cache.Profile.NavigateDisplayName;
                    if (cache.Stick != null && cache.Profile.UseStickForNavigation) return "摇杆";
                    return "无（未检测到 hat / 摇杆）";
            }
        }

        /// <summary>
        /// 为某设备写入 UI 按键覆盖（方向盘/飞行摇杆重绑定使用）。
        /// actionName 支持 "Submit" / "Cancel" / "Toggle"。
        /// </summary>
        public static void SetOverride(InputDevice device, string actionName, string controlPath, string displayName = null)
        {
            if (device == null || string.IsNullOrEmpty(controlPath)) return;

            bool supported =
                string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "Toggle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "NavigateUp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "NavigateDown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "NavigateLeft", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "NavigateRight", StringComparison.OrdinalIgnoreCase);
            if (!supported) return;

            EnsureOverridesLoaded();
            string key = GetDeviceKey(device);
            var entry = s_overrides.items.Find(e => e.deviceKey == key);
            if (entry == null)
            {
                entry = new UIInputDeviceBindingOverride { deviceKey = key };
                s_overrides.items.Add(entry);
            }

            string display = string.IsNullOrEmpty(displayName)
                ? GetControlPathDisplayName(device, controlPath)
                : displayName;

            if (string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase))
            {
                entry.submitControl = controlPath;
                entry.submitDisplayName = display;
            }
            else if (string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                entry.cancelControl = controlPath;
                entry.cancelDisplayName = display;
            }
            else if (string.Equals(actionName, "NavigateUp", StringComparison.OrdinalIgnoreCase))
            {
                entry.navigateUpControl = controlPath;
                entry.wheelNavigationSource = (int)WheelNavigationSource.Auto;
            }
            else if (string.Equals(actionName, "NavigateDown", StringComparison.OrdinalIgnoreCase))
            {
                entry.navigateDownControl = controlPath;
                entry.wheelNavigationSource = (int)WheelNavigationSource.Auto;
            }
            else if (string.Equals(actionName, "NavigateLeft", StringComparison.OrdinalIgnoreCase))
            {
                entry.navigateLeftControl = controlPath;
                entry.wheelNavigationSource = (int)WheelNavigationSource.Auto;
            }
            else if (string.Equals(actionName, "NavigateRight", StringComparison.OrdinalIgnoreCase))
            {
                entry.navigateRightControl = controlPath;
                entry.wheelNavigationSource = (int)WheelNavigationSource.Auto;
            }
            else
            {
                entry.toggleControl = controlPath;
            }

            SaveOverrides();
            s_cache.Clear();
        }

        /// <summary>
        /// 为设备设置自定义导航按钮（传 null/空字符串表示该方向不绑定）。
        /// 适用于没有十字键的方向盘：把旋钮左右、拨片或面板按钮绑成上下左右。
        /// </summary>
        public static void SetNavigationButtons(
            InputDevice device,
            string upControl,
            string downControl,
            string leftControl,
            string rightControl)
        {
            if (device == null) return;

            if (string.IsNullOrEmpty(upControl) && string.IsNullOrEmpty(downControl) &&
                string.IsNullOrEmpty(leftControl) && string.IsNullOrEmpty(rightControl))
            {
                ClearNavigationButtons(device);
                return;
            }

            EnsureOverridesLoaded();
            string key = GetDeviceKey(device);
            var entry = s_overrides.items.Find(e => e.deviceKey == key);
            if (entry == null)
            {
                entry = new UIInputDeviceBindingOverride { deviceKey = key };
                s_overrides.items.Add(entry);
            }

            entry.navigateUpControl = string.IsNullOrEmpty(upControl) ? null : upControl;
            entry.navigateDownControl = string.IsNullOrEmpty(downControl) ? null : downControl;
            entry.navigateLeftControl = string.IsNullOrEmpty(leftControl) ? null : leftControl;
            entry.navigateRightControl = string.IsNullOrEmpty(rightControl) ? null : rightControl;
            entry.wheelNavigationSource = (int)WheelNavigationSource.Auto;

            SaveOverrides();
            s_cache.Clear();
        }

        /// <summary>
        /// 强制指定方向盘导航来源。
        /// Auto 会优先使用自定义按钮/小摇杆/十字键，只有都不存在时才使用转向轴。
        /// </summary>
        public static void SetWheelNavigationSource(InputDevice device, WheelNavigationSource source)
        {
            if (device == null) return;

            EnsureOverridesLoaded();
            string key = GetDeviceKey(device);
            var entry = s_overrides.items.Find(e => e.deviceKey == key);
            if (entry == null)
            {
                entry = new UIInputDeviceBindingOverride { deviceKey = key };
                s_overrides.items.Add(entry);
            }

            entry.wheelNavigationSource = (int)source;
            SaveOverrides();
            s_cache.Clear();
        }

        /// <summary>清除设备的自定义导航按钮（恢复 hat / 转向轴导航）。</summary>
        public static void ClearNavigationButtons(InputDevice device)
        {
            if (device == null) return;
            EnsureOverridesLoaded();
            string key = GetDeviceKey(device);
            var entry = s_overrides.items.Find(e => e.deviceKey == key);
            if (entry == null) return;

            entry.navigateUpControl = null;
            entry.navigateDownControl = null;
            entry.navigateLeftControl = null;
            entry.navigateRightControl = null;
            if (entry.wheelNavigationSource == (int)WheelNavigationSource.CustomButtons)
            {
                entry.wheelNavigationSource = (int)WheelNavigationSource.Auto;
            }
            SaveOverrides();
            s_cache.Clear();
        }

        /// <summary>清除某设备的所有 UI 按键覆盖。</summary>
        public static void ClearOverride(InputDevice device)
        {
            if (device == null) return;
            EnsureOverridesLoaded();
            string key = GetDeviceKey(device);
            if (s_overrides.items.RemoveAll(e => e.deviceKey == key) > 0)
            {
                SaveOverrides();
                s_cache.Clear();
            }
        }

        /// <summary>清除所有设备的 UI 按键覆盖。</summary>
        public static void ClearAllOverrides()
        {
            EnsureOverridesLoaded();
            if (s_overrides.items.Count == 0) return;
            s_overrides.items.Clear();
            SaveOverrides();
            s_cache.Clear();
        }

        /// <summary>是否已经为该设备保存过按键覆盖。</summary>
        public static bool HasOverride(InputDevice device)
        {
            if (device == null) return false;
            EnsureOverridesLoaded();
            string key = GetDeviceKey(device);
            return s_overrides.items.Exists(e => e.deviceKey == key);
        }

        /// <summary>移除缓存（设备移除/重绑定后调用）。</summary>
        public static void Invalidate(InputDevice device)
        {
            if (device == null) return;
            s_cache.Remove(device.deviceId);
        }

        public static void InvalidateAll()
        {
            s_cache.Clear();
        }

        private static DeviceCache GetCache(InputDevice device)
        {
            if (device == null) return new DeviceCache
            {
                Kind = UIInputDeviceKind.KeyboardMouse,
                Profile = KeyboardMouseProfile,
                SubmitResolved = true,
                CancelResolved = true,
                ToggleResolved = true,
            };

            if (s_cache.TryGetValue(device.deviceId, out var cache) && cache.Profile != null)
            {
                return cache;
            }

            var kind = Classify(device);
            string searchKey = GetDeviceSearchKey(device);
            bool isWheelLike = !ContainsAny(searchKey, FlightKeywords) && ContainsAny(searchKey, WheelKeywords);
            EnsureOverridesLoaded();
            string deviceKey = ComputeDeviceKey(device);
            var entry = s_overrides.items.Find(e => e.deviceKey == deviceKey);

            var baseProfile = kind switch
            {
                UIInputDeviceKind.Gamepad => GamepadProfile,
                UIInputDeviceKind.RacingWheel => WheelProfile,
                UIInputDeviceKind.FlightStick => FlightProfile,
                UIInputDeviceKind.Joystick => JoystickProfile,
                _ => KeyboardMouseProfile,
            };

            var wheelSource = WheelNavigationSource.Auto;
            if (entry != null && Enum.IsDefined(typeof(WheelNavigationSource), entry.wheelNavigationSource))
            {
                wheelSource = (WheelNavigationSource)entry.wheelNavigationSource;
            }

            string displayName = GetDeviceDisplayName(device);

            // 默认方向盘取消键是 button2（Xbox B / PS 方框）。
            // G29/G923 的 PlayStation 版把圆圈放在 button3，需要按型号纠正，
            // 否则 UI 会提示/使用方块而不是圆圈。
            bool playStationWheel = kind == UIInputDeviceKind.RacingWheel &&
                                    ContainsAny(searchKey, PlayStationWheelKeywords) &&
                                    !ContainsAny(searchKey, XboxWheelKeywords);
            string wheelCancelControl = playStationWheel ? "button3,button2" : baseProfile.CancelControl;
            string wheelCancelDisplay = playStationWheel ? "○ / Circle (Button 3)" : baseProfile.CancelDisplayName;

            var profile = new UIInputDeviceProfile(
                kind,
                $"{baseProfile.DisplayName}（{displayName}）",
                entry != null && !string.IsNullOrEmpty(entry.submitControl) ? entry.submitControl : baseProfile.SubmitControl,
                entry != null && !string.IsNullOrEmpty(entry.cancelControl) ? entry.cancelControl : wheelCancelControl,
                entry != null && !string.IsNullOrEmpty(entry.toggleControl) ? entry.toggleControl : baseProfile.ToggleControl,
                entry != null && !string.IsNullOrEmpty(entry.submitDisplayName) ? entry.submitDisplayName : baseProfile.SubmitDisplayName,
                entry != null && !string.IsNullOrEmpty(entry.cancelDisplayName) ? entry.cancelDisplayName : wheelCancelDisplay,
                baseProfile.NavigateDisplayName,
                baseProfile.UseStickForNavigation,
                entry?.navigateUpControl,
                entry?.navigateDownControl,
                entry?.navigateLeftControl,
                entry?.navigateRightControl,
                wheelSource);

            var hat = ResolveHat(device);
            var primaryStick = device is Joystick joystick ? joystick.stick : null;
            var steering = ResolveSteering(device);

            // 若转向是显式命名轴，主 stick 就不是转向轴，可以当小摇杆使用
            bool primaryStickIsSteering = steering == null ||
                (device is Joystick j && j.stick != null && steering == j.stick.x);

            Vector2Control thumbStick = null;
            AxisControl thumbX = null;
            AxisControl thumbY = null;
            if (device is Joystick)
            {
                thumbStick = ResolveThumbStick(device, primaryStick, hat, primaryStickIsSteering);
                thumbX = thumbStick == null ? device.TryGetChildControl<AxisControl>("rx") : null;
                thumbY = thumbStick == null ? device.TryGetChildControl<AxisControl>("ry") : null;
                if (thumbStick == null && (thumbX == null || thumbY == null))
                {
                    thumbX = device.TryGetChildControl<AxisControl>("x2");
                    thumbY = device.TryGetChildControl<AxisControl>("y2");
                }
            }

            cache = new DeviceCache
            {
                Kind = kind,
                Profile = profile,
                DeviceKey = deviceKey,
                Hat = hat,
                Stick = primaryStick,
                Steering = steering,
                ThumbStick = thumbStick,
                ThumbX = thumbX,
                ThumbY = thumbY,
                IsWheelLike = isWheelLike,
            };

            // 按键控件按需解析（TryGetSubmit/TryGetCancel/TryGetToggle），
            // 避免未知设备在没有按钮时缓存 null 后永远不再尝试兜底。
            s_cache[device.deviceId] = cache;
            return cache;
        }

        private static Vector2Control ResolveHat(InputDevice device)
        {
            if (device is Joystick joystick && joystick.hatswitch != null)
            {
                return joystick.hatswitch;
            }
            var dpad = device.TryGetChildControl<DpadControl>("dpad");
            if (dpad != null) return dpad;
            return device.TryGetChildControl<DpadControl>("hat");
        }

        private static AxisControl ResolveSteering(InputDevice device)
        {
            // 优先显式命名的转向轴：部分 HID 方向盘把转向放在 steer/wheel，
            // 而 x/y 反而是小摇杆；若先取 stick.x 会把小摇杆误当转向。
            var named = device.TryGetChildControl<AxisControl>("steer")
                ?? device.TryGetChildControl<AxisControl>("steering")
                ?? device.TryGetChildControl<AxisControl>("wheel");
            if (named != null) return named;

            // 常见情况：HID 方向盘把转向放在 X 轴，Input System 合并成 stick。
            if (device is Joystick joystick && joystick.stick != null)
            {
                return joystick.stick.x;
            }

            return device.TryGetChildControl<AxisControl>("x");
        }

        /// <summary>
        /// 方向盘上的小摇杆/第二摇杆/第二个 hat。
        /// Input System 通常会把它识别成另一个 Vector2Control；如果是独立的 rx/ry 轴，则由调用方读取轴对。
        /// </summary>
        private static Vector2Control ResolveThumbStick(
            InputDevice device, Vector2Control primaryStick, Vector2Control primaryHat, bool primaryStickIsSteering)
        {
            if (device == null) return null;

            Vector2Control fallback = null;
            var controls = device.allControls;
            for (int i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                if (control.parent != device) continue;
                if (!(control is Vector2Control vector)) continue;
                if (vector == primaryHat) continue;

                if (vector == primaryStick)
                {
                    if (primaryStickIsSteering) continue;
                    // 转向在显式命名轴上时，主 stick 实际是小摇杆
                    return vector;
                }

                string name = (vector.name ?? string.Empty).ToLowerInvariant();
                if (name.Contains("thumb") || name.Contains("aux") || name.Contains("stick2") ||
                    name.Contains("stick3") || name.Contains("right") || name.Contains("pad") ||
                    name.Contains("hat"))
                {
                    return vector;
                }

                if (fallback == null) fallback = vector;
            }

            return fallback;
        }

        private static ButtonControl ResolveButton(InputDevice device, string controlSpec)
        {
            if (device == null || string.IsNullOrEmpty(controlSpec)) return null;

            string[] parts = controlSpec.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim();
                if (name.Length == 0) continue;

                // 完整路径（<Joystick>/button1）与裸控件名（button1/start）都支持
                ButtonControl control = InputControlPath.TryFindControl<ButtonControl>(device, name);
                if (control != null) return control;

                control = device.TryGetChildControl<ButtonControl>(name);
                if (control != null) return control;

                string leaf = GetLeafName(name);
                if (!string.Equals(leaf, name, StringComparison.Ordinal))
                {
                    control = device.TryGetChildControl<ButtonControl>(leaf);
                    if (control != null) return control;
                }
            }
            return null;
        }

        /// <summary>设备直连按钮（排除 hat 子按钮），用于未知按键命名设备的兜底。</summary>
        private static ButtonControl GetDirectButton(InputDevice device, int index)
        {
            if (device == null || index < 0) return null;

            s_directButtons.Clear();
            ButtonControl trigger = null;

            var controls = device.allControls;
            for (int i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                if (control.parent != device || !(control is ButtonControl button)) continue;

                // trigger 可能是 button1 的别名，排在最后，避免同一物理键同时充当确认与取消
                if (string.Equals(button.name, "trigger", StringComparison.OrdinalIgnoreCase))
                {
                    trigger = button;
                }
                else
                {
                    s_directButtons.Add(button);
                }
            }

            if (trigger != null)
            {
                s_directButtons.Add(trigger);
            }

            return index < s_directButtons.Count ? s_directButtons[index] : null;
        }

        private static string GetControlPathDisplayName(InputDevice device, string controlPath)
        {
            var control = InputControlPath.TryFindControl(device, controlPath);
            if (control == null) return controlPath;

            return InputControlPath.ToHumanReadableString(
                control.path,
                InputControlPath.HumanReadableStringOptions.OmitDevice |
                InputControlPath.HumanReadableStringOptions.UseShortNames);
        }

        private static string GetLeafName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        /// <summary>
        /// 设备的稳定键（优先 product + serial），用于把按键/玩家槽位绑定持久化到该设备。
        /// 加入 serial 可区分两个同型号方向盘（如两只 G923）。
        /// 结果缓存在 DeviceCache，避免每帧拼接字符串。
        /// </summary>
        public static string GetDeviceKey(InputDevice device)
        {
            if (device == null) return "keyboard_mouse";
            return GetCache(device).DeviceKey;
        }

        /// <summary>
        /// 玩家分组键：同一个物理设备的多个 HID 子设备（底座、踏板、轮缘等）
        /// 应占用同一个玩家槽位，不能因为枚举出多个 InputDevice 就多出 P2/P3。
        /// 有 serial 时按 manufacturer#serial 分组；没有 serial 时退回 product 键。
        /// </summary>
        public static string GetPlayerGroupKey(InputDevice device)
        {
            if (device == null) return "keyboard_mouse";

            var description = device.description;
            string serial = description.serial;
            if (!string.IsNullOrEmpty(serial))
            {
                string manufacturer = description.manufacturer ?? string.Empty;
                return manufacturer + "#" + serial;
            }

            return GetDeviceKey(device);
        }

        private static string ComputeDeviceKey(InputDevice device)
        {
            string product = device.description.product;
            string serial = device.description.serial;

            if (!string.IsNullOrEmpty(product))
            {
                return string.IsNullOrEmpty(serial) ? product : product + "#" + serial;
            }
            if (!string.IsNullOrEmpty(device.displayName)) return device.displayName;
            if (!string.IsNullOrEmpty(device.name)) return device.name;
            return device.layout;
        }

        private static string GetDeviceSearchKey(InputDevice device)
        {
            // 不要拼接 layout/name：HID 摇杆的 layout 通常就是 "Joystick"，
            // 否则所有方向盘都会被误判成飞行摇杆。
            string product = device.description.product ?? string.Empty;
            string manufacturer = device.description.manufacturer ?? string.Empty;
            string display = device.displayName ?? string.Empty;

            return string.Concat(product, " ", manufacturer, " ", display).ToLowerInvariant();
        }

        private static bool ContainsAny(string source, string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (source.Contains(keywords[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnsureOverridesLoaded()
        {
            if (s_overridesLoaded) return;
            s_overridesLoaded = true;

            string json = PlayerPrefs.GetString(OverridePrefsKey, null);
            if (string.IsNullOrEmpty(json))
            {
                s_overrides = new UIInputDeviceBindingOverrideList();
                return;
            }

            try
            {
                s_overrides = JsonUtility.FromJson<UIInputDeviceBindingOverrideList>(json)
                              ?? new UIInputDeviceBindingOverrideList();
                if (s_overrides.items == null)
                {
                    s_overrides.items = new List<UIInputDeviceBindingOverride>();
                }
            }
            catch (Exception)
            {
                s_overrides = new UIInputDeviceBindingOverrideList();
            }
        }

        private static void SaveOverrides()
        {
            EnsureOverridesLoaded();
            PlayerPrefs.SetString(OverridePrefsKey, JsonUtility.ToJson(s_overrides));
            PlayerPrefs.Save();
        }
    }
}
