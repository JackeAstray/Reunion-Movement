using System;

namespace ReunionMovement.Core.UIInput
{
    /// <summary>
    /// UI 按键绑定配置数据类
    /// 存储键盘导航、提交、取消的自定义按键映射
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
    /// UIInputSystem.Instance.EnableUIControl();
    /// 
    /// // 关闭键盘/手柄对 UI 的接管，但保留鼠标点击 UI
    /// UIInputSystem.Instance.DisableUIControl();
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

        /// <summary>从角色控制切换到 UI 控制的按键（默认 Tab）</summary>
        public string toggleToUI = "tab";

        /// <summary>从 UI 控制切换回角色控制的按键（默认 Escape，与 Cancel 共用时由模式判断）</summary>
        public string toggleToGameplay = "escape";

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
                toggleToUI = "tab",
                toggleToGameplay = "escape",
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
    /// UI 控制模式 —— 只决定键盘/手柄是否接管 UI，鼠标指针始终保留点击能力
    /// 
    /// 使用示例：
    /// <code>
    /// // 打开背包或系统菜单时
    /// UIInputSystem.Instance.EnableUIControl();
    /// 
    /// // 关闭菜单回到角色控制时
    /// UIInputSystem.Instance.DisableUIControl();
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
