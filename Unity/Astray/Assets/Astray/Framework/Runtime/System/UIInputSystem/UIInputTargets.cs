using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Astray.Framework.Runtime.UIInput
{
    /// <summary>
    /// 一次 UI 导航事件（由 UIInputSystem 的设备轮询器产生）。
    /// Step 已归一化为四向（-1/0/1），并已按设备类型做了死区与重复处理：
    /// - 数字输入（键盘、十字键、方向盘 hat）：每次按下立即产生一次事件，不受节流限制；
    /// - 模拟输入（摇杆、飞行摇杆）：越过死区立即产生一次，持续推住时按 HoldDelay/RepeatRate 重复。
    /// </summary>
    public readonly struct UINavigateEvent
    {
        /// <summary>来源设备；键盘/鼠标为 null。</summary>
        public readonly InputDevice Device;

        /// <summary>玩家槽位（0 为键盘/鼠标 + 第一个实体控制器）。</summary>
        public readonly int PlayerIndex;

        /// <summary>四向步进：x 右正左负，y 上正下负。</summary>
        public readonly Vector2Int Step;

        /// <summary>原始方向值（模拟输入时保留摇杆幅度，数字输入为 -1/0/1）。</summary>
        public readonly Vector2 Value;

        /// <summary>是否来自模拟轴。</summary>
        public readonly bool IsAnalog;

        /// <summary>是否为按住后的自动重复事件。</summary>
        public readonly bool IsRepeat;

        /// <summary>
        /// 是否来自方向盘的转向轴（没有十字键的方向盘，如 MOZA RS50）。
        /// 轮播会把左右转向解释为“上一项/下一项”，不依赖布局轴。
        /// </summary>
        public readonly bool IsWheelSteering;

        public UINavigateEvent(
            InputDevice device,
            int playerIndex,
            Vector2Int step,
            Vector2 value,
            bool isAnalog,
            bool isRepeat,
            bool isWheelSteering = false)
        {
            Device = device;
            PlayerIndex = playerIndex;
            Step = step;
            Value = value;
            IsAnalog = isAnalog;
            IsRepeat = isRepeat;
            IsWheelSteering = isWheelSteering;
        }
    }

    /// <summary>一次确认/取消事件。</summary>
    public readonly struct UIButtonEvent
    {
        public readonly InputDevice Device;
        public readonly int PlayerIndex;

        public UIButtonEvent(InputDevice device, int playerIndex)
        {
            Device = device;
            PlayerIndex = playerIndex;
        }
    }

    /// <summary>
    /// UI 导航目标。轮播、选择组等实现该接口后注册到 UIInputSystem，
    /// 即可按玩家槽位接收来自键盘/手柄/方向盘/飞行摇杆的输入。
    /// 返回 true 表示事件已被消费，UIInputSystem 不会再把它交给 EventSystem 焦点导航。
    /// </summary>
    public interface IUINavigationTarget
    {
        /// <summary>优先级，越大越先收到事件（仅用于多个 PlayerSlot=-1 的兜底目标排序）。</summary>
        int InputPriority { get; }

        /// <summary>玩家槽位：0/1... 为具体玩家，-1 为任意玩家。</summary>
        int PlayerSlot { get; }

        /// <summary>是否接受指定玩家槽位的输入（焦点域已按 PlayerSlot 路由，保留用于目标内部校验）。</summary>
        bool AcceptsPlayer(int playerIndex);

        bool HandleNavigate(in UINavigateEvent e);

        bool HandleSubmit(in UIButtonEvent e);

        bool HandleCancel(in UIButtonEvent e);
    }

    /// <summary>已连接 UI 控制设备的信息（供设置界面/按键提示展示）。</summary>
    public readonly struct UIInputDeviceInfo
    {
        public readonly InputDevice Device;
        public readonly int PlayerIndex;
        public readonly UIInputDeviceKind Kind;
        public readonly string DisplayName;
        public readonly string SubmitDisplayName;
        public readonly string CancelDisplayName;
        public readonly string NavigateDisplayName;
        public readonly string ToggleDisplayName;

        public UIInputDeviceInfo(
            InputDevice device,
            int playerIndex,
            UIInputDeviceKind kind,
            string displayName,
            string submitDisplayName,
            string cancelDisplayName,
            string navigateDisplayName,
            string toggleDisplayName)
        {
            Device = device;
            PlayerIndex = playerIndex;
            Kind = kind;
            DisplayName = displayName;
            SubmitDisplayName = submitDisplayName;
            CancelDisplayName = cancelDisplayName;
            NavigateDisplayName = navigateDisplayName;
            ToggleDisplayName = toggleDisplayName;
        }

        public override string ToString()
        {
            return $"[P{PlayerIndex}] {DisplayName} ({Kind}) 确认={SubmitDisplayName} 取消={CancelDisplayName} " +
                   $"导航={NavigateDisplayName} 菜单={ToggleDisplayName}";
        }
    }
}
