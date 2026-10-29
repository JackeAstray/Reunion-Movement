using Astray.Common;
using Astray.Framework.Runtime.Base;
using Astray.Framework.Runtime.Resources;
using Astray.Framework.Runtime.UI;
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

namespace Astray.Framework.Runtime.UIInput
{
    /// <summary>
    /// UI 输入系统 —— 统一轮询键盘、鼠标、手柄、赛车方向盘、飞行摇杆并驱动 UGUI 导航。
    /// 包括：多设备玩家槽位、方向盘/飞行摇杆默认按键与按设备重绑定、自动聚焦、焦点追踪。
    /// 导航/确认/取消由本系统的轮询器派发；InputSystemUIInputModule 只保留指针动作，
    /// 避免同一次按键被 EventSystem 与本系统重复处理。
    /// </summary>
    [ModuleOrder(100, typeof(Astray.Framework.Runtime.UI.UISystem), typeof(Astray.Framework.Runtime.Resources.ResourcesSystem))]
    public partial class UIInputSystem : ICustomSystem, ISystemUpdatable, ISystemDisposable
    {
        #region 单例与初始化
        private static readonly Lazy<UIInputSystem> instance = new(() => new UIInputSystem());
        public static UIInputSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        /// <summary>初始化代数：引擎 Clear/Init 循环后递增，导航目标据此重新注册/申请 UI 模式。</summary>
        public int InitVersion { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion
        #region 字段

        /// <summary>InputActionAsset 引用（UI 操作映射）</summary>
        private InputActionAsset inputActions;

        /// <summary>EventSystem 引用</summary>
        private EventSystem eventSystem;

        /// <summary>InputSystemUIInputModule 引用</summary>
        private InputSystemUIInputModule inputModule;

        /// <summary>当前选中的 GameObject</summary>
        public GameObject CurrentSelected { get; private set; }

        /// <summary>上一次选中的 GameObject（用于窗口关闭后恢复焦点）</summary>
        public GameObject LastSelected { get; private set; }

        /// <summary>焦点选择栈（用于多层 UI 的焦点管理）</summary>
        private readonly Stack<GameObject> focusStack = new Stack<GameObject>();

        /// <summary>当前按键绑定配置</summary>
        public UIInputBinding CurrentBinding { get; private set; } = new UIInputBinding();

        /// <summary>各 UI 窗口注册的默认首选项</summary>
        private readonly Dictionary<string, GameObject> firstSelectedRegistry = new Dictionary<string, GameObject>(32);

        /// <summary>重绑定进行中标记</summary>
        private bool isRebinding = false;

        /// <summary>重绑定结束后的输入抑制截止时间（避免捕获按键的同一帧又触发确认/取消）</summary>
        private float inputSuppressedUntil = -1f;

        /// <summary>进行中的重绑定操作（持有引用以便 CancelRebind 能真正取消底层监听）</summary>
        private InputActionRebindingExtensions.RebindingOperation activeRebindOperation;

        /// <summary>进行中的重绑定取消回调（幂等包装，保证外部 CancelRebind 与操作自身 OnCancel 都只触发一次用户回调）</summary>
        private Action activeRebindCancelCallback;

        /// <summary>当前 UI 控制模式</summary>
        private UIControlMode currentMode = UIControlMode.Gameplay;

        /// <summary>当前 UI 控制模式（只读）</summary>
        public UIControlMode CurrentMode => currentMode;

        /// <summary>进入 UI 模式前的玩家操作映射状态缓存（用于恢复）</summary>
        private bool playerMapWasEnabled = false;

        // ---- 按键解析缓存（避免每帧字符串分配）----
        private string cachedToggleToUIStr;
        private Key cachedToggleToUIKey = Key.None;
        private string cachedToggleToUIGamepadStr;
        private GamepadButton? cachedToggleToUIGamepadButton;
        private string cachedToggleToGameplayStr;
        private Key cachedToggleToGameplayKey = Key.None;

        private string cachedNavUpStr;
        private Key cachedNavUpKey = Key.None;
        private string cachedNavDownStr;
        private Key cachedNavDownKey = Key.None;
        private string cachedNavLeftStr;
        private Key cachedNavLeftKey = Key.None;
        private string cachedNavRightStr;
        private Key cachedNavRightKey = Key.None;
        private string cachedSubmitStr;
        private Key cachedSubmitKey = Key.None;
        private string cachedCancelStr;
        private Key cachedCancelKey = Key.None;

        private string cachedGamepadSubmitStr;
        private GamepadButton? cachedGamepadSubmitButton;
        private string cachedGamepadCancelStr;
        private GamepadButton? cachedGamepadCancelButton;
        private readonly Dictionary<int, ButtonControl> gamepadSubmitCache = new Dictionary<int, ButtonControl>(4);
        private readonly Dictionary<int, ButtonControl> gamepadCancelCache = new Dictionary<int, ButtonControl>(4);

        // ---- 多设备路由 ----
        /// <summary>已接入路由的实体控制器（手柄/方向盘/飞行摇杆），顺序即玩家槽位顺序</summary>
        private readonly List<InputDevice> routedDevices = new List<InputDevice>(8);

        // 设备分组复用缓冲：同一个物理方向盘可能枚举成底座/踏板/轮缘多个 InputDevice
        private readonly HashSet<string> playerGroupKeyBuffer = new HashSet<string>(8);
        private readonly HashSet<string> deviceInfoGroupBuffer = new HashSet<string>(8);

        /// <summary>当前活跃的焦点域（UI 窗口），最后一个为栈顶</summary>
        private readonly List<UIFocusScope> focusScopes = new List<UIFocusScope>(4);

        /// <summary>导航目标 → 所属焦点域，用于 Unregister 时找回正确的 scope</summary>
        private readonly Dictionary<IUINavigationTarget, UIFocusScope> targetScopes =
            new Dictionary<IUINavigationTarget, UIFocusScope>(16);

        /// <summary>按住方向时的重复状态（key = 设备 deviceId，键盘为 0）</summary>
        private readonly Dictionary<int, DirectionRepeatState> repeatStates = new Dictionary<int, DirectionRepeatState>(8);

        /// <summary>方向盘转向轴导航状态（key = 设备 deviceId）</summary>
        private readonly Dictionary<int, SteeringState> steeringStates = new Dictionary<int, SteeringState>(4);

        /// <summary>需要自动启用 UI 控制的持有者（轮播等），用于多窗口引用计数</summary>
        private readonly HashSet<object> uiControlRequests = new HashSet<object>();

        /// <summary>键盘/鼠标所属玩家槽位（固定 0）</summary>
        private const int KeyboardMousePlayerIndex = 0;

        private bool devicesDirty = true;

        // ---- 玩家槽位持久化（按设备 product 记录，插入顺序变化后仍保持分配） ----
        private const string PlayerSlotPrefsKey = "ui_input_player_slots_v1";
        private Dictionary<string, int> playerSlotOverrides;
        private bool playerSlotsLoaded;

        [Serializable]
        private sealed class PlayerSlotEntry
        {
            public string deviceKey;
            public int slot;
        }

        [Serializable]
        private sealed class PlayerSlotList
        {
            public List<PlayerSlotEntry> items = new List<PlayerSlotEntry>();
        }

        private struct DirectionRepeatState
        {
            public Vector2Int LastStep;
            public float NextRepeatTime;
        }

        private struct SteeringState
        {
            public bool PositiveArmed;
            public bool NegativeArmed;
            public int RepeatDirection;
            public float NextRepeatTime;
        }

        #endregion
        #region R3 响应式事件（推荐新代码使用）

        /// <summary>焦点变更事件</summary>
        public Subject<GameObject> SelectionChangedSubject { get; private set; } = new Subject<GameObject>();

        /// <summary>按键绑定变更事件</summary>
        public Subject<UIInputBinding> BindingChangedSubject { get; private set; } = new Subject<UIInputBinding>();

        /// <summary>输入模式切换事件</summary>
        public Subject<UIControlMode> UIControlModeChangedSubject { get; private set; } = new Subject<UIControlMode>();

        /// <summary>导航操作事件（方向向量）</summary>
        public Subject<Vector2> NavigateSubject { get; private set; } = new Subject<Vector2>();

        /// <summary>提交操作事件</summary>
        public Subject<Unit> SubmitSubject { get; private set; } = new Subject<Unit>();

        /// <summary>取消操作事件</summary>
        public Subject<Unit> CancelSubject { get; private set; } = new Subject<Unit>();

        /// <summary>多设备导航事件（包含设备与玩家槽位，轮播/选择组内部使用；旧代码仍可用 NavigateSubject）</summary>
        public Subject<UINavigateEvent> NavigateEventSubject { get; private set; } = new Subject<UINavigateEvent>();

        /// <summary>多设备确认事件</summary>
        public Subject<UIButtonEvent> SubmitEventSubject { get; private set; } = new Subject<UIButtonEvent>();

        /// <summary>多设备取消事件</summary>
        public Subject<UIButtonEvent> CancelEventSubject { get; private set; } = new Subject<UIButtonEvent>();

        #endregion
        #region 初始化
        public async UniTask Init()
        {
            initProgress = 0;
            InitVersion++;

            // 设备控件缓存可能来自上一次 Play（关闭域重载时），重新初始化时清空
            UIInputDeviceRegistry.InvalidateAll();

            // 重建 R3 Subject（Clear() 已 Dispose 并置 null，重初始化时必须重建）
            SelectionChangedSubject ??= new Subject<GameObject>();
            BindingChangedSubject ??= new Subject<UIInputBinding>();
            UIControlModeChangedSubject ??= new Subject<UIControlMode>();
            NavigateSubject ??= new Subject<Vector2>();
            SubmitSubject ??= new Subject<Unit>();
            CancelSubject ??= new Subject<Unit>();
            NavigateEventSubject ??= new Subject<UINavigateEvent>();
            SubmitEventSubject ??= new Subject<UIButtonEvent>();
            CancelEventSubject ??= new Subject<UIButtonEvent>();

            // 1. 加载按键绑定配置
            LoadBindings();
            initProgress = 20;

            // 2. 获取 EventSystem 和 InputModule 引用
            eventSystem = UISystem.Instance?.EventSystem;
            if (eventSystem != null)
            {
                inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            }
            initProgress = 40;

            // 3. 加载 InputActionAsset 并应用自定义绑定
            await LoadInputActionsAsync();
            ApplyBindingsToActions();
            initProgress = 70;

            // 4. 注册 UISystem 生命周期事件
            RegisterUIEvents();
            initProgress = 85;

            // 5. 注册输入事件回调（导航/确认/取消改为轮询，InputModule 只保留指针动作）
            RegisterInputCallbacks();

            // 6. 设备热插拔与玩家槽位
            InputSystem.onDeviceChange -= OnInputDeviceChanged;
            InputSystem.onDeviceChange += OnInputDeviceChanged;
            RefreshRoutedDevices();
            for (int i = 0; i < routedDevices.Count; i++)
            {
                LogDeviceMapping(routedDevices[i]);
            }
            initProgress = 100;

            // Clear() 会清空 uiControlRequests；初始化时把仍活跃的焦点域请求补回来
            for (int i = 0; i < focusScopes.Count; i++)
            {
                var scope = focusScopes[i];
                if (scope != null && scope.IsActive && scope.RequestUIControl)
                {
                    uiControlRequests.Add(scope);
                }
            }

            isInited = true;

            // 轮播等目标在系统初始化前就注册了自动 UI 控制请求，初始化完成后补启用
            if (uiControlRequests.Count > 0 && currentMode != UIControlMode.UIControl)
            {
                EnableUIControl();
            }

            Log.Debug("UIInputSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {
            if (!isInited) return;

            // 轮询检测切换键
            PollToggleKeys();

            // 仅在 UI 模式下轮询设备输入
            if (currentMode != UIControlMode.UIControl) return;

            if (eventSystem == null || inputModule == null)
            {
                var previousModule = inputModule;
                EnsureInputModuleReferences();
                // 迟到的 EventSystem/InputModule：先启用组件，再关闭它的导航动作，
                // 否则它的 move/submit/cancel 会和本系统的轮询重复响应。
                if (inputModule != null && inputModule != previousModule)
                {
                    inputModule.enabled = true;
                    SetUIActionsEnabled(enablePointerActions: true);
                }
            }

            if (devicesDirty)
            {
                RefreshRoutedDevices();
            }

            PollInputDevices();

            // 轮询检测焦点变化（EventSystem 的 currentSelectedGameObject 可能在 InputSystemUIInputModule 内部更新）
            var current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (current != CurrentSelected)
            {
                var previous = CurrentSelected;
                CurrentSelected = current;
                try { SelectionChangedSubject.OnNext(current); }
                catch (Exception ex) { Log.Warning("[UIInputSystem] SelectionChangedSubject 订阅者异常（已隔离）: {0}", ex.Message); }

                if (current != null)
                {
                    LastSelected = current;
                }
            }
        }

        public void Clear()
        {
            Log.Debug("UIInputSystem 清除数据");

            UnregisterUIEvents();
            UnregisterInputCallbacks();
            InputSystem.onDeviceChange -= OnInputDeviceChanged;
            UIInputDeviceRegistry.InvalidateAll();

            // 切回 Gameplay 模式以恢复 Player map
            if (currentMode == UIControlMode.UIControl)
            {
                DisableUIControl();
            }

            focusStack.Clear();
            firstSelectedRegistry.Clear();
            // 不清理 focusScopes/targetScopes：场景中的窗口/轮播可能在引擎重初始化后仍然存活，
            // 它们已注册过；清理后不会自动重建。GetTopFocusScope 会跳过已销毁的焦点域。
            for (int i = focusScopes.Count - 1; i >= 0; i--)
            {
                if (focusScopes[i] == null) focusScopes.RemoveAt(i);
            }
            routedDevices.Clear();
            repeatStates.Clear();
            steeringStates.Clear();
            gamepadSubmitCache.Clear();
            gamepadCancelCache.Clear();
            uiControlRequests.Clear();
            devicesDirty = true;
            CurrentSelected = null;
            LastSelected = null;

            // 复位重绑定状态：取消进行中的操作并复位标记，避免重初始化后 StartRebind 被永久拒绝
            CancelRebind();
            isRebinding = false;

            // 释放 R3 Subject（自动断开所有订阅），并置 null 以便 Init 中 ??= 重建
            SelectionChangedSubject?.Dispose();
            SelectionChangedSubject = null;
            BindingChangedSubject?.Dispose();
            BindingChangedSubject = null;
            UIControlModeChangedSubject?.Dispose();
            UIControlModeChangedSubject = null;
            NavigateSubject?.Dispose();
            NavigateSubject = null;
            SubmitSubject?.Dispose();
            SubmitSubject = null;
            CancelSubject?.Dispose();
            CancelSubject = null;
            NavigateEventSubject?.Dispose();
            NavigateEventSubject = null;
            SubmitEventSubject?.Dispose();
            SubmitEventSubject = null;
            CancelEventSubject?.Dispose();
            CancelEventSubject = null;

            // 销毁 Instantiate 克隆的 InputActionAsset：LoadInputActionsAsync 每次克隆一份，
            // 不销毁会随 Init/Clear 循环泄漏 ScriptableObject（不随场景卸载，驻留到 UnloadUnusedAssets）
            if (inputActions != null)
            {
                if (inputModule != null && inputModule.actionsAsset == inputActions)
                {
                    inputModule.actionsAsset = null;
                }
                UnityEngine.Object.Destroy(inputActions);
                inputActions = null;
            }

            isInited = false;
        }
        #endregion

        #region 内部查询
        /// <summary>
        /// 是否存在已打开的 UI 窗口（用于区分 Esc 应优先取消窗口还是退出 UI 模式）。
        /// 使用零分配的 HasAnyOpenWindow，避免每次按键分配 List。
        /// </summary>
        private bool HasOpenUI()
        {
            try
            {
                return UISystem.Instance != null && UISystem.Instance.HasAnyOpenWindow();
            }
            catch (Exception ex)
            {
                // 记录异常而非静默吞掉：极端时序（引擎重建中）下可能抛错，至少留痕
                Log.Warning("UIInputSystem.HasOpenUI 访问异常（按无窗口处理）: {0}", ex.Message);
                return false;
            }
        }
        #endregion
    }
}
