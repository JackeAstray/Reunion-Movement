using System;

namespace Gear.Framework.Runtime.UIInput
{
    /// <summary>
    /// UI 按键绑定配置数据类
    /// 存储键盘导航、提交、取消的自定义按键映射，以及摇杆死区/重复节奏等参数。
    /// 手柄、赛车方向盘、飞行摇杆的按键由 UIInputDeviceRegistry 按设备单独解析。
    /// 
    /// 使用示例：
    /// <code>
    /// // 创建默认配置
    /// var binding = UIInputBinding.CreateDefault();
    /// 
    /// // 修改方向键
    /// binding.SetNavigateKey(UINavigationDirection.Up, "upArrow");
    /// binding.submit = "enter";
    /// binding.cancel = "escape";
    /// 
    /// // 切换 UI 控制模式
    /// UIInputSystem.GetOrInstance().EnableUIControl();
    /// 
    /// // 关闭键盘/手柄/方向盘对 UI 的接管，但保留鼠标点击 UI
    /// UIInputSystem.GetOrInstance().DisableUIControl();
    /// </code>
    /// </summary>
    [Serializable]
    public class UIInputBinding
    {
        /// <summary>向上导航按键（默认 W）</summary>
        public string navigateUp = "w";

        /// <summary>向下导航按键（默认 S）</summary>
        public string navigateDown = "s";

        /// <summary>向左导航按键（默认 A）</summary>
        public string navigateLeft = "a";

        /// <summary>向右导航按键（默认 D）</summary>
        public string navigateRight = "d";

        /// <summary>提交/确认按键（默认 Enter）</summary>
        public string submit = "enter";

        /// <summary>提交按键显示名称</summary>
        public string submitDisplayName = "Enter";

        /// <summary>取消/返回按键（默认 Escape）</summary>
        public string cancel = "escape";

        /// <summary>取消按键显示名称</summary>
        public string cancelDisplayName = "Escape";

        /// <summary>手柄确认键（完整 control path，默认 A / buttonSouth）</summary>
        public string gamepadSubmit = "<Gamepad>/buttonSouth";

        /// <summary>手柄确认键显示名称</summary>
        public string gamepadSubmitDisplayName = "A / ✕";

        /// <summary>手柄取消键（完整 control path，默认 B / buttonEast）</summary>
        public string gamepadCancel = "<Gamepad>/buttonEast";

        /// <summary>手柄取消键显示名称</summary>
        public string gamepadCancelDisplayName = "B / ○";

        /// <summary>切换 UI 控制模式的按键（默认 Tab）：Gameplay 按下进入 UI，UI 模式下再按退出</summary>
        public string toggleToUI = "tab";

        /// <summary>键盘菜单键显示名称</summary>
        public string toggleToUIDisplayName = "Tab";

        /// <summary>切换 UI 控制模式的游戏手柄按键（完整 control path，默认 Start）：Gameplay 按下进入 UI，UI 模式下再按退出</summary>
        public string toggleToUIGamepad = "<Gamepad>/start";

        /// <summary>游戏手柄切换键显示名称</summary>
        public string toggleToUIGamepadDisplayName = "Start";

        /// <summary>切换 UI 控制模式的方向盘/摇杆按键（完整 control path，默认 start）：设备有 start 键时生效</summary>
        public string toggleToUIJoystick = "<Joystick>/start";

        /// <summary>方向盘/摇杆切换键显示名称</summary>
        public string toggleToUIJoystickDisplayName = "Start";

        /// <summary>从 UI 控制切换回角色控制的按键（默认 Escape，与 Cancel 共用时由模式判断）</summary>
        public string toggleToGameplay = "escape";

        /// <summary>模拟轴（摇杆/飞行摇杆）的 UI 导航死区（0~1）</summary>
        public float navigateDeadzone = 0.5f;

        /// <summary>按住方向后，开始自动重复前的等待时间（秒）。数字按键的每一次按下都会立即响应，不受此值限制</summary>
        public float navigateHoldDelay = 0.4f;

        /// <summary>按住方向后的自动重复间隔（秒）</summary>
        public float navigateRepeatRate = 0.1f;

        /// <summary>鼠标右键是否作为取消/返回（默认关闭，避免与游戏内右键操作冲突）</summary>
        public bool mouseRightClickAsCancel = false;

        /// <summary>
        /// 是否允许用转向轴做 UI 导航（默认开启）。
        /// Auto 来源下，只有未检测到小摇杆/十字键/自定义导航按钮时才会真正使用转向轴；
        /// 因此有可用小摇杆的方向盘不会被轻微打方向误触发。
        /// </summary>
        public bool wheelSteeringNavigation = true;

        /// <summary>转向轴导航是否反向（某些方向盘左转为正）</summary>
        public bool wheelSteeringInvert = false;

        /// <summary>转向轴触发导航的阈值（0~1，越大需要打得越多）</summary>
        public float wheelSteeringDeadzone = 0.25f;

        /// <summary>转向轴回正到该值以下后，重新允许下一次切换</summary>
        public float wheelSteeringReleaseThreshold = 0.12f;

        /// <summary>转向轴保持在阈值以上多久后开始自动重复（秒）</summary>
        public float wheelSteeringHoldDelay = 0.6f;

        /// <summary>转向轴自动重复间隔（秒）；&lt;= 0 表示不重复，一次打满只切换一格</summary>
        public float wheelSteeringRepeatRate = 0.2f;

        /// <summary>获取导航方向对应的按键字符串</summary>
        public string GetNavigateKey(UINavigationDirection direction)
        {
            return direction switch
            {
                UINavigationDirection.Up => navigateUp,
                UINavigationDirection.Down => navigateDown,
                UINavigationDirection.Left => navigateLeft,
                UINavigationDirection.Right => navigateRight,
                _ => string.Empty,
            };
        }

        /// <summary>
        /// 设置导航方向对应的按键字符串
        /// </summary>
        public void SetNavigateKey(UINavigationDirection direction, string key)
        {
            switch (direction)
            {
                case UINavigationDirection.Up: navigateUp = key; break;
                case UINavigationDirection.Down: navigateDown = key; break;
                case UINavigationDirection.Left: navigateLeft = key; break;
                case UINavigationDirection.Right: navigateRight = key; break;
            }
        }

        /// <summary>
        /// 创建默认绑定实例
        /// </summary>
        public static UIInputBinding CreateDefault()
        {
            return new UIInputBinding
            {
                navigateUp = "w",
                navigateDown = "s",
                navigateLeft = "a",
                navigateRight = "d",
                submit = "enter",
                submitDisplayName = "Enter",
                cancel = "escape",
                cancelDisplayName = "Escape",
                gamepadSubmit = "<Gamepad>/buttonSouth",
                gamepadSubmitDisplayName = "A / ✕",
                gamepadCancel = "<Gamepad>/buttonEast",
                gamepadCancelDisplayName = "B / ○",
                toggleToUI = "tab",
                toggleToUIDisplayName = "Tab",
                toggleToUIGamepad = "<Gamepad>/start",
                toggleToUIGamepadDisplayName = "Start",
                toggleToUIJoystick = "<Joystick>/start",
                toggleToUIJoystickDisplayName = "Start",
                toggleToGameplay = "escape",
                navigateDeadzone = 0.5f,
                navigateHoldDelay = 0.4f,
                navigateRepeatRate = 0.1f,
                mouseRightClickAsCancel = false,
                wheelSteeringNavigation = true,
                wheelSteeringInvert = false,
                wheelSteeringDeadzone = 0.25f,
                wheelSteeringReleaseThreshold = 0.12f,
                wheelSteeringHoldDelay = 0.6f,
                wheelSteeringRepeatRate = 0.2f,
            };
        }
    }

    /// <summary>
    /// UI 导航方向枚举
    /// </summary>
    public enum UINavigationDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// UI 控制模式 —— 决定键盘/手柄/方向盘/飞行摇杆是否接管 UI，鼠标指针始终保留点击能力
    /// 
    /// 使用示例：
    /// <code>
    /// // 打开背包或系统菜单时
    /// UIInputSystem.GetOrInstance().EnableUIControl();
    /// 
    /// // 关闭菜单回到角色控制时
    /// UIInputSystem.GetOrInstance().DisableUIControl();
    /// </code>
    /// </summary>
    public enum UIControlMode
    {
        /// <summary>角色控制模式：Player Action Map 激活，UI 导航关闭，但鼠标仍可点击 UI</summary>
        Gameplay,

        /// <summary>UI 控制模式：启用键盘/手柄 UI 导航，鼠标仍可点击 UI</summary>
        UIControl,
    }
}
