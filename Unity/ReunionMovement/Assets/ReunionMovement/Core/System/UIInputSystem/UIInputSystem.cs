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
    /// <summary>
    /// UI 输入系统 —— 统一轮询键盘、鼠标、手柄、赛车方向盘、飞行摇杆并驱动 UGUI 导航。
    /// 包括：多设备玩家槽位、方向盘/飞行摇杆默认按键与按设备重绑定、自动聚焦、焦点追踪。
    /// 导航/确认/取消由本系统的轮询器派发；InputSystemUIInputModule 只保留指针动作，
    /// 避免同一次按键被 EventSystem 与本系统重复处理。
    /// </summary>
    public class UIInputSystem : ICustomSystem, ISystemUpdatable, ISystemDisposable
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

        #region 输入 Action 加载与绑定

        /// <summary>
        /// 加载 InputActionAsset（从 Settings 文件夹）
        /// </summary>
        private async UniTask LoadInputActionsAsync()
        {
            // 尝试从 UISystem 的 InputSystemUIInputModule 获取已赋值的 actions。
            // 与 Resources 路径一致：Instantiate 克隆后使用，避免 ApplyBindingOverride/AddBinding
            // 直接修改共享资产（域重载关闭时跨 Play 会话残留、其他系统共用该资产被污染）。
            if (inputModule != null && inputModule.actionsAsset != null)
            {
                inputActions = UnityEngine.Object.Instantiate(inputModule.actionsAsset);
            }
            else
            {
                // 否则通过 ResourcesSystem 加载（需放在 Resources 文件夹下）
                var loaded = ResourcesSystem.Instance.Load<InputActionAsset>("InputSystem_Actions");
                if (loaded != null)
                {
                    try
                    {
                        inputActions = UnityEngine.Object.Instantiate(loaded);
                    }
                    finally
                    {
                        ResourcesSystem.Instance.DeleteAssetCache("InputSystem_Actions");
                    }
                    if (inputModule != null)
                    {
                        inputModule.actionsAsset = inputActions;
                    }
                }
                else
                {
                    Log.Warning("UIInputSystem: 无法加载 InputSystem_Actions.inputactions，键盘导航将依赖默认绑定");
                }
            }

            // 恢复官方格式绑定快照（InputActionAsset.ToJson/LoadFromJson，完整保留手柄/composite override）
            RestoreBindingsFromAssetJson();

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 从官方 JSON 快照恢复 InputActionAsset 绑定覆盖（Input System 标准持久化方案）。
        /// 快照不存在时静默跳过；恢复后 Init 的 ApplyBindingsToActions 仍会用 CurrentBinding 覆盖
        /// （两者来源一致，不会打架）。
        /// </summary>
        private void RestoreBindingsFromAssetJson()
        {
            if (inputActions == null) return;
            string json = PlayerPrefs.GetString("ui_bind_asset_json", null);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                inputActions.LoadFromJson(json);
                Log.Debug("UIInputSystem: 已从官方 JSON 快照恢复绑定覆盖");
            }
            catch (Exception ex)
            {
                Log.Warning("UIInputSystem: 绑定 JSON 快照恢复失败（将使用 PlayerPrefs 简化字段）: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 将 CurrentBinding 中的自定义按键应用到 InputAction
        /// 注意：Navigate 的默认 .inputactions 已包含 WASD + 方向键，此处不做覆盖仅确保存在
        /// </summary>
        public void ApplyBindingsToActions()
        {
            if (inputActions == null) return;

            try
            {
                // Navigate: 默认 2D Vector composite 已含 WASD + 方向键 + 手柄，
                // 仅在用户自定义键与默认不同时才追加（不覆盖原有绑定）
                EnsureNavigateBindings();
                // Submit: 确保 Enter + NumpadEnter 都可用
                EnsureSubmitBindings();
                // 应用自定义 Submit/Cancel 覆盖（此前遗漏 Submit → SetBinding/交互重绑定不生效、重启后丢失）
                ApplyBindingOverride("UI", "Submit", CurrentBinding.submit);
                ApplyBindingOverride("UI", "Cancel", CurrentBinding.cancel);
            }
            catch (Exception ex)
            {
                Log.Error("UIInputSystem: 应用按键绑定失败: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 确保 Navigate action 同时支持 WASD 和方向键
        /// 默认 .inputactions 的 composite 已包含全部，仅在自定义键时覆盖对应方向 part
        /// </summary>
        private void EnsureNavigateBindings()
        {
            var action = inputActions.FindAction("UI/Navigate");
            if (action == null) return;

            // 修复：Vector2 复合动作不能通过追加独立绑定来添加方向（单键无法解析出方向向量，
            // 原 EnsureKeyInAction 的追加实际无效）。自定义方向键必须覆盖 2DVector composite
            // 中对应命名的键盘 part（asset 中名为 up/down/left/right，多个同名 part 共存）。
            OverrideNavigatePart(action, "up", CurrentBinding.navigateUp);
            OverrideNavigatePart(action, "down", CurrentBinding.navigateDown);
            OverrideNavigatePart(action, "left", CurrentBinding.navigateLeft);
            OverrideNavigatePart(action, "right", CurrentBinding.navigateRight);
        }

        /// <summary>
        /// 覆盖 2DVector composite 中指定方向 part 的按键路径（覆盖第一个匹配的键盘 part）。
        /// 同名 part 有多个（如 up 有 w 与 upArrow）时只覆盖第一个，方向键仍保留。幂等：已是目标键则跳过。
        /// </summary>
        private void OverrideNavigatePart(InputAction action, string partName, string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;
            var targetPath = $"<Keyboard>/{keyName}";

            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite) continue;

                for (int j = i + 1; j < action.bindings.Count; j++)
                {
                    var part = action.bindings[j];
                    if (!part.isPartOfComposite) break;
                    if (!part.path.Contains("Keyboard")) continue;
                    if (!string.Equals(part.name, partName, StringComparison.OrdinalIgnoreCase)) continue;

                    if (part.path == targetPath) return; // 已是目标键，无需覆盖
                    action.ApplyBindingOverride(j, targetPath);
                    return;
                }
            }
        }

        /// <summary>
        /// 确保 Submit action 同时支持 Enter 和 NumpadEnter
        /// </summary>
        private void EnsureSubmitBindings()
        {
            var action = inputActions.FindAction("UI/Submit");
            if (action == null) return;

            // 确保 numpadEnter 绑定存在（不在 {Submit} 通配符范围内）
            bool hasNumpadEnter = false;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == "<Keyboard>/numpadEnter")
                {
                    hasNumpadEnter = true;
                    break;
                }
            }
            if (!hasNumpadEnter)
            {
                action.AddBinding("<Keyboard>/numpadEnter");
            }
        }

        /// <summary>
        /// 对指定 Action 覆盖提交/取消按键
        /// </summary>
        private void ApplyBindingOverride(string actionMap, string actionName, string bindingKey)
        {
            if (string.IsNullOrEmpty(bindingKey)) return;

            var action = inputActions.FindAction($"{actionMap}/{actionName}");
            if (action == null) return;

            // 完整路径（如 "<Gamepad>/buttonSouth"，交互式重绑定手柄产生）按路径处理：
            // 覆盖同设备的已有绑定，否则追加
            if (bindingKey.StartsWith("<"))
            {
                int close = bindingKey.IndexOf('>');
                string device = close > 0 ? bindingKey.Substring(0, close + 1) : bindingKey;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (!binding.isComposite && !binding.isPartOfComposite
                        && binding.path != null && binding.path.StartsWith(device, StringComparison.Ordinal))
                    {
                        action.ApplyBindingOverride(i, bindingKey);
                        return;
                    }
                }
                action.AddBinding(bindingKey);
                return;
            }

            // 键盘按键名（如 "enter"）：覆盖第一个键盘绑定
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite && !binding.isPartOfComposite
                    && binding.path != null && binding.path.Contains("Keyboard"))
                {
                    action.ApplyBindingOverride(i, $"<Keyboard>/{bindingKey}");
                    return;
                }
            }

            action.AddBinding($"<Keyboard>/{bindingKey}");
        }

        #endregion

        #region 按键重绑定（运行时自定义按键）

        /// <summary>
        /// 开始按键重绑定流程
        /// </summary>
        /// <param name="actionName">Action 名称（如 "Navigate"、"Submit"、"Cancel"）</param>
        /// <param name="onComplete">重绑定完成回调（参数：新按键显示名称）</param>
        /// <param name="onCancel">取消回调</param>
        public void StartRebind(string actionName, Action<string> onComplete, Action onCancel = null)
        {
            StartRebind(actionName, null, onComplete, onCancel);
        }

        /// <summary>
        /// 开始按键重绑定流程，并限定只接受指定设备上的按键（目前用于 Toggle/Menu 菜单键）。
        /// </summary>
        public void StartRebind(string actionName, InputDevice device, Action<string> onComplete, Action onCancel = null)
        {
            if (string.Equals(actionName, "Toggle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "Menu", StringComparison.OrdinalIgnoreCase))
            {
                StartDeviceToggleRebind(device, onComplete, onCancel);
                return;
            }

            if (device != null)
            {
                Log.Warning("UIInputSystem: StartRebind 的 device 参数目前只支持 Toggle/Menu；{0} 将按全局重绑定处理。", actionName);
            }

            if (inputActions == null || isRebinding)
            {
                onCancel?.Invoke();
                return;
            }

            var action = inputActions.FindAction($"UI/{actionName}");
            if (action == null)
            {
                Log.Warning("UIInputSystem: 找不到 UI/{0} Action", actionName);
                onCancel?.Invoke();
                return;
            }

            // Navigate 是 2D Vector composite（含 up/down/left/right 四个 part），
            // 单次交互式重绑定无法确定捕获的按键属于哪个方向，
            // 若强行使用会只写入 navigateUp 造成方向键设置混乱。
            // 拒绝并引导调用方使用按方向的 SetBinding。
            if (string.Equals(actionName, "Navigate", StringComparison.OrdinalIgnoreCase) &&
                action.expectedControlType == "Vector2")
            {
                Log.Warning("UIInputSystem: Navigate 为 2D Vector 复合动作，不支持交互式重绑定。" +
                            "请改用 SetBinding(\"NavigateUp\"/\"NavigateDown\"/\"NavigateLeft\"/\"NavigateRight\", key)。");
                onCancel?.Invoke();
                return;
            }

            // PerformInteractiveRebinding 要求目标 Action 处于禁用状态
            if (action.enabled)
            {
                action.Disable();
            }

            isRebinding = true;

            // 幂等包装：操作自身 OnCancel 与外部 CancelRebind 都可能触发，保证用户 onCancel 恰好一次
            bool cancelInvoked = false;
            Action guardedCancel = () =>
            {
                if (cancelInvoked) return;
                cancelInvoked = true;
                onCancel?.Invoke();
            };
            activeRebindCancelCallback = guardedCancel;

            // 交互式重绑定：允许键盘、手柄、赛车方向盘、飞行摇杆（方向盘通过 Joystick 设备上报）；
            // 仅排除鼠标/触屏/XR 等不适合做 UI 动作的设备。
            var rebindOperation = action.PerformInteractiveRebinding()
                .WithControlsExcluding("<Mouse>/")
                .WithControlsExcluding("<XRController>/")
                .WithControlsExcluding("<Touchscreen>/")
                .WithControlsExcluding("<Pen>/")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    activeRebindCancelCallback = null;

                    var control = operation.selectedControl;
                    var newKey = control.path;
                    var keyName = InputControlPath.ToHumanReadableString(
                        newKey,
                        InputControlPath.HumanReadableStringOptions.OmitDevice |
                        InputControlPath.HumanReadableStringOptions.UseShortNames);

                    if (control.device is Joystick joystick)
                    {
                        // 方向盘/飞行摇杆按设备 product 保存，避免影响其他控制器
                        if (string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(actionName, "Toggle", StringComparison.OrdinalIgnoreCase))
                        {
                            UIInputDeviceRegistry.SetOverride(joystick, actionName, newKey, keyName);
                        }
                        else
                        {
                            UpdateBindingFromRebind(actionName, newKey, keyName);
                        }
                    }
                    else if (control.device is Gamepad)
                    {
                        // 手柄使用独立字段，不再和键盘共用 submit/cancel
                        if (string.Equals(actionName, "Submit", StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentBinding.gamepadSubmit = newKey;
                            CurrentBinding.gamepadSubmitDisplayName = keyName;
                        }
                        else if (string.Equals(actionName, "Cancel", StringComparison.OrdinalIgnoreCase))
                        {
                            CurrentBinding.gamepadCancel = newKey;
                            CurrentBinding.gamepadCancelDisplayName = keyName;
                        }
                        else
                        {
                            UpdateBindingFromRebind(actionName, newKey, keyName);
                        }
                    }
                    else
                    {
                        UpdateBindingFromRebind(actionName, newKey, keyName);
                    }

                    // 保存绑定
                    SaveBindings();

                    onComplete?.Invoke(keyName);
                    // Clear() 可能已 Dispose 并置 null Subject，判空避免 NRE
                    BindingChangedSubject?.OnNext(CurrentBinding);

                    operation.Dispose();
                })
                .OnCancel(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    // 通过幂等包装调用用户取消回调（先取出并清空，防重入）
                    var cancelCb = activeRebindCancelCallback;
                    activeRebindCancelCallback = null;
                    cancelCb?.Invoke();
                    operation.Dispose();
                });

            activeRebindOperation = rebindOperation;
            rebindOperation.Start();
        }

        /// <summary>
        /// 取消当前重绑定（真正 Dispose 底层 operation，停止监听按键）。
        /// 注意：部分 Unity 版本 Dispose 进行中的 operation 不会触发 OnCancel 回调，
        /// 因此这里在 Dispose 后兜底调用取消回调 —— guardedCancel 幂等守卫保证恰好一次。
        /// </summary>
        public void CancelRebind()
        {
            var operation = activeRebindOperation;
            activeRebindOperation = null;
            var cancelCb = activeRebindCancelCallback;
            activeRebindCancelCallback = null;
            isRebinding = false;
            inputSuppressedUntil = Time.unscaledTime + 0.15f;

            operation?.Dispose();
            cancelCb?.Invoke();
        }

        /// <summary>
        /// 重绑定“打开/关闭菜单”键。device 为 null 时接受任意设备；指定设备时只接受该设备上的按键。
        /// 方向盘/飞行摇杆按设备持久化；手柄/键盘写入全局菜单键。
        /// </summary>
        public void StartDeviceToggleRebind(InputDevice device, Action<string> onComplete, Action onCancel = null)
        {
            if (inputActions == null || isRebinding)
            {
                onCancel?.Invoke();
                return;
            }

            // 借用 UI/Submit 作为“捕获任意按钮”的探针，但不允许它修改 Submit 绑定。
            var captureAction = inputActions.FindAction("UI/Submit");
            if (captureAction == null)
            {
                Log.Warning("UIInputSystem: 找不到用于捕获菜单键的 UI/Submit Action");
                onCancel?.Invoke();
                return;
            }

            // PerformInteractiveRebinding 要求目标 Action 处于禁用状态
            if (captureAction.enabled)
            {
                captureAction.Disable();
            }

            isRebinding = true;
            bool cancelInvoked = false;
            Action guardedCancel = () =>
            {
                if (cancelInvoked) return;
                cancelInvoked = true;
                onCancel?.Invoke();
            };
            activeRebindCancelCallback = guardedCancel;

            var rebindOperation = captureAction.PerformInteractiveRebinding()
                .WithControlsExcluding("<Mouse>/")
                .WithControlsExcluding("<XRController>/")
                .WithControlsExcluding("<Touchscreen>/")
                .WithControlsExcluding("<Pen>/")
                .OnMatchWaitForAnother(0.1f)
                .OnApplyBinding((_, __) => { /* 只捕获，不改 UI/Submit */ })
                .OnComplete(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    activeRebindCancelCallback = null;

                    var control = operation.selectedControl;
                    if (control == null || control.device == null ||
                        (device != null && control.device != device))
                    {
                        guardedCancel();
                        operation.Dispose();
                        return;
                    }

                    string path = control.path;
                    string display = InputControlPath.ToHumanReadableString(
                        path,
                        InputControlPath.HumanReadableStringOptions.OmitDevice |
                        InputControlPath.HumanReadableStringOptions.UseShortNames);

                    if (control.device is Joystick)
                    {
                        UIInputDeviceRegistry.SetOverride(control.device, "Toggle", path, display);
                    }
                    else if (control.device is Gamepad)
                    {
                        CurrentBinding.toggleToUIGamepad = path;
                        CurrentBinding.toggleToUIGamepadDisplayName = display;
                        SaveBindings();
                    }
                    else if (control.device is Keyboard)
                    {
                        string leaf = path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)
                            ? path.Substring(path.LastIndexOf('/') + 1)
                            : path;
                        CurrentBinding.toggleToUI = leaf;
                        CurrentBinding.toggleToUIDisplayName = display;
                        SaveBindings();
                    }
                    else
                    {
                        guardedCancel();
                        operation.Dispose();
                        return;
                    }

                    BindingChangedSubject?.OnNext(CurrentBinding);
                    onComplete?.Invoke(display);
                    operation.Dispose();
                })
                .OnCancel(operation =>
                {
                    isRebinding = false;
                    inputSuppressedUntil = Time.unscaledTime + 0.15f;
                    activeRebindOperation = null;
                    var cancelCb = activeRebindCancelCallback;
                    activeRebindCancelCallback = null;
                    cancelCb?.Invoke();
                    operation.Dispose();
                });

            activeRebindOperation = rebindOperation;
            rebindOperation.Start();
        }

        /// <summary>
        /// 是否正在重绑定
        /// </summary>
        public bool IsRebinding => isRebinding;

        /// <summary>
        /// 根据重绑定结果更新 CurrentBinding
        /// </summary>
        private void UpdateBindingFromRebind(string actionName, string controlPath, string displayName)
        {
            // 键盘按键提取尾段（如 "<Keyboard>/a" → "a"，与 SetBinding/历史存档格式一致）；
            // 非键盘控件（如手柄）保留完整路径（如 "<Gamepad>/buttonSouth"），ApplyBindingOverride 按路径处理
            var keyName = controlPath;
            if (controlPath.Contains("<Keyboard>"))
            {
                var slashIndex = controlPath.LastIndexOf('/');
                if (slashIndex >= 0)
                {
                    keyName = controlPath.Substring(slashIndex + 1);
                }
            }

            switch (actionName)
            {
                case "Submit":
                    CurrentBinding.submit = keyName;
                    CurrentBinding.submitDisplayName = displayName;
                    break;
                case "Cancel":
                    CurrentBinding.cancel = keyName;
                    CurrentBinding.cancelDisplayName = displayName;
                    break;
                case "Navigate":
                    // Navigate 是 2D Vector composite，正常路径已被 StartRebind 拒绝
                    // （交互式重绑定无法确定方向）。此分支仅作防御，保持向后兼容。
                    CurrentBinding.navigateUp = keyName;
                    break;
            }
        }

        /// <summary>
        /// 程序化设置指定操作的自定义按键（不通过交互式重绑定）
        /// </summary>
        public void SetBinding(string actionName, Key key)
        {
            var keyName = key.ToString().ToLower();
            switch (actionName)
            {
                case "Submit":
                    CurrentBinding.submit = keyName;
                    CurrentBinding.submitDisplayName = key.ToString();
                    break;
                case "Cancel":
                    CurrentBinding.cancel = keyName;
                    CurrentBinding.cancelDisplayName = key.ToString();
                    break;
                case "NavigateUp":
                    CurrentBinding.navigateUp = keyName;
                    break;
                case "NavigateDown":
                    CurrentBinding.navigateDown = keyName;
                    break;
                case "NavigateLeft":
                    CurrentBinding.navigateLeft = keyName;
                    break;
                case "NavigateRight":
                    CurrentBinding.navigateRight = keyName;
                    break;
            }

            ApplyBindingsToActions();
            SaveBindings();
            // Clear 后 Subject 为 null，判空避免 NRE（与 StartRebind 回调中的 ?. 一致）
            BindingChangedSubject?.OnNext(CurrentBinding);

            WarnBindingConflicts();
        }

        /// <summary>
        /// 重置所有按键绑定为默认值
        /// </summary>
        public void ResetBindings()
        {
            CurrentBinding = new UIInputBinding();
            UIInputDeviceRegistry.ClearAllOverrides();
            ApplyBindingsToActions();
            SaveBindings();
            BindingChangedSubject?.OnNext(CurrentBinding);
            Log.Debug("UIInputSystem: 按键绑定已重置为默认值（含方向盘/飞行摇杆）");
        }

        /// <summary>
        /// 检测并告警按键冲突：同一按键绑定到多个操作（Submit/Cancel/切换键等）时提示，
        /// 避免玩家按键互相覆盖导致输入行为异常。
        /// </summary>
        private void WarnBindingConflicts()
        {
            // 简易分组：keyName → 绑定该键的操作名列表（低频调用，分配可接受）
            var map = new Dictionary<string, List<string>>();
            void Register(string keyName, string actionName)
            {
                if (string.IsNullOrEmpty(keyName)) return;
                if (!map.TryGetValue(keyName, out var list))
                {
                    list = new List<string>(2);
                    map[keyName] = list;
                }
                list.Add(actionName);
            }

            // 键盘/手柄/摇杆各自独立命名空间，避免 Enter 与 buttonSouth 被误判为冲突
            Register("K:" + CurrentBinding.submit, "Keyboard.Submit");
            Register("K:" + CurrentBinding.cancel, "Keyboard.Cancel");
            Register("K:" + CurrentBinding.toggleToUI, "Keyboard.Toggle");
            Register("K:" + CurrentBinding.toggleToGameplay, "Keyboard.ToggleGameplay");
            Register("G:" + CurrentBinding.gamepadSubmit, "Gamepad.Submit");
            Register("G:" + CurrentBinding.gamepadCancel, "Gamepad.Cancel");
            Register("G:" + CurrentBinding.toggleToUIGamepad, "Gamepad.Toggle");
            Register("J:" + CurrentBinding.toggleToUIJoystick, "Joystick.Toggle");

            foreach (var kv in map)
            {
                if (kv.Value.Count > 1)
                {
                    Log.Warning("UIInputSystem: 按键 [{0}] 同时绑定到 {1}，存在冲突，可能导致输入行为异常",
                        kv.Key, string.Join("/", kv.Value));
                }
            }
        }

        #endregion

        #region 按键绑定持久化

        /// <summary>
        /// 从 PlayerPrefs 加载按键绑定
        /// </summary>
        public void LoadBindings()
        {
            // PlayerPrefs 在 WebGL 上同样可用：Unity 的 WebGL 播放器启动时会以 runDependency 阻塞，
            // 等 IndexedDB 载入完成（FS.syncfs(populate:true)）后才运行 C#，故不再按平台跳过。
            CurrentBinding = new UIInputBinding
            {
                navigateUp = PlayerPrefs.GetString("ui_bind_nav_up", "w"),
                navigateDown = PlayerPrefs.GetString("ui_bind_nav_down", "s"),
                navigateLeft = PlayerPrefs.GetString("ui_bind_nav_left", "a"),
                navigateRight = PlayerPrefs.GetString("ui_bind_nav_right", "d"),
                submit = PlayerPrefs.GetString("ui_bind_submit", "enter"),
                submitDisplayName = PlayerPrefs.GetString("ui_bind_submit_display", "Enter"),
                cancel = PlayerPrefs.GetString("ui_bind_cancel", "escape"),
                cancelDisplayName = PlayerPrefs.GetString("ui_bind_cancel_display", "Escape"),
                gamepadSubmit = PlayerPrefs.GetString("ui_bind_gamepad_submit", "<Gamepad>/buttonSouth"),
                gamepadSubmitDisplayName = PlayerPrefs.GetString("ui_bind_gamepad_submit_display", "A / ✕"),
                gamepadCancel = PlayerPrefs.GetString("ui_bind_gamepad_cancel", "<Gamepad>/buttonEast"),
                gamepadCancelDisplayName = PlayerPrefs.GetString("ui_bind_gamepad_cancel_display", "B / ○"),
                toggleToUI = PlayerPrefs.GetString("ui_bind_toggle_ui", "tab"),
                toggleToUIDisplayName = PlayerPrefs.GetString("ui_bind_toggle_ui_display", "Tab"),
                toggleToUIGamepad = PlayerPrefs.GetString("ui_bind_toggle_ui_gamepad", "<Gamepad>/start"),
                toggleToUIGamepadDisplayName = PlayerPrefs.GetString("ui_bind_toggle_ui_gamepad_display", "Start"),
                toggleToUIJoystick = PlayerPrefs.GetString("ui_bind_toggle_ui_joystick", "<Joystick>/start"),
                toggleToUIJoystickDisplayName = PlayerPrefs.GetString("ui_bind_toggle_ui_joystick_display", "Start"),
                toggleToGameplay = PlayerPrefs.GetString("ui_bind_toggle_gameplay", "escape"),
                navigateDeadzone = PlayerPrefs.GetFloat("ui_bind_nav_deadzone", 0.5f),
                navigateHoldDelay = PlayerPrefs.GetFloat("ui_bind_nav_hold_delay", 0.4f),
                navigateRepeatRate = PlayerPrefs.GetFloat("ui_bind_nav_repeat_rate", 0.1f),
                mouseRightClickAsCancel = PlayerPrefs.GetInt("ui_bind_mouse_right_cancel", 0) != 0,
                wheelSteeringNavigation = PlayerPrefs.GetInt("ui_bind_wheel_steering_nav", 1) != 0,
                wheelSteeringInvert = PlayerPrefs.GetInt("ui_bind_wheel_steering_invert", 0) != 0,
                wheelSteeringDeadzone = PlayerPrefs.GetFloat("ui_bind_wheel_steering_deadzone", 0.25f),
                wheelSteeringReleaseThreshold = PlayerPrefs.GetFloat("ui_bind_wheel_steering_release", 0.12f),
                wheelSteeringHoldDelay = PlayerPrefs.GetFloat("ui_bind_wheel_steering_hold", 0.6f),
                wheelSteeringRepeatRate = PlayerPrefs.GetFloat("ui_bind_wheel_steering_repeat", 0.2f),
            };

            // 旧版本把键盘和手柄的确认/取消混在同一个字段里：检测到旧存档是手柄路径时迁移到手柄字段
            MigrateLegacyGamepadBinding(
                ref CurrentBinding.submit, ref CurrentBinding.submitDisplayName,
                ref CurrentBinding.gamepadSubmit, ref CurrentBinding.gamepadSubmitDisplayName,
                "enter", "Enter");
            MigrateLegacyGamepadBinding(
                ref CurrentBinding.cancel, ref CurrentBinding.cancelDisplayName,
                ref CurrentBinding.gamepadCancel, ref CurrentBinding.gamepadCancelDisplayName,
                "escape", "Escape");
        }

        private static void MigrateLegacyGamepadBinding(
            ref string keyboardValue, ref string keyboardDisplay,
            ref string gamepadValue, ref string gamepadDisplay,
            string defaultKey, string defaultDisplay)
        {
            if (!LooksLikeGamepadBinding(keyboardValue)) return;

            gamepadValue = keyboardValue;
            gamepadDisplay = keyboardDisplay;
            keyboardValue = defaultKey;
            keyboardDisplay = defaultDisplay;
        }

        private static bool LooksLikeGamepadBinding(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase)) return true;

            string name = value;
            if (name.StartsWith("button", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("button".Length);
            }
            return Enum.TryParse<GamepadButton>(name, true, out _);
        }

        /// <summary>
        /// 保存按键绑定到 PlayerPrefs（简化字段 + 官方 JSON 快照双写）。
        /// 官方 JSON 快照（InputActionAsset.ToJson）完整保留手柄/composite override，
        /// 供 LoadInputActionsAsync 恢复；简化字段保持向后兼容。
        /// </summary>
        public void SaveBindings()
        {
            PlayerPrefs.SetString("ui_bind_nav_up", CurrentBinding.navigateUp);
            PlayerPrefs.SetString("ui_bind_nav_down", CurrentBinding.navigateDown);
            PlayerPrefs.SetString("ui_bind_nav_left", CurrentBinding.navigateLeft);
            PlayerPrefs.SetString("ui_bind_nav_right", CurrentBinding.navigateRight);
            PlayerPrefs.SetString("ui_bind_submit", CurrentBinding.submit);
            PlayerPrefs.SetString("ui_bind_submit_display", CurrentBinding.submitDisplayName);
            PlayerPrefs.SetString("ui_bind_cancel", CurrentBinding.cancel);
            PlayerPrefs.SetString("ui_bind_cancel_display", CurrentBinding.cancelDisplayName);
            PlayerPrefs.SetString("ui_bind_gamepad_submit", CurrentBinding.gamepadSubmit);
            PlayerPrefs.SetString("ui_bind_gamepad_submit_display", CurrentBinding.gamepadSubmitDisplayName);
            PlayerPrefs.SetString("ui_bind_gamepad_cancel", CurrentBinding.gamepadCancel);
            PlayerPrefs.SetString("ui_bind_gamepad_cancel_display", CurrentBinding.gamepadCancelDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_ui", CurrentBinding.toggleToUI);
            PlayerPrefs.SetString("ui_bind_toggle_ui_display", CurrentBinding.toggleToUIDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_ui_gamepad", CurrentBinding.toggleToUIGamepad);
            PlayerPrefs.SetString("ui_bind_toggle_ui_gamepad_display", CurrentBinding.toggleToUIGamepadDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_ui_joystick", CurrentBinding.toggleToUIJoystick);
            PlayerPrefs.SetString("ui_bind_toggle_ui_joystick_display", CurrentBinding.toggleToUIJoystickDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_gameplay", CurrentBinding.toggleToGameplay);
            PlayerPrefs.SetFloat("ui_bind_nav_deadzone", CurrentBinding.navigateDeadzone);
            PlayerPrefs.SetFloat("ui_bind_nav_hold_delay", CurrentBinding.navigateHoldDelay);
            PlayerPrefs.SetFloat("ui_bind_nav_repeat_rate", CurrentBinding.navigateRepeatRate);
            PlayerPrefs.SetInt("ui_bind_mouse_right_cancel", CurrentBinding.mouseRightClickAsCancel ? 1 : 0);
            PlayerPrefs.SetInt("ui_bind_wheel_steering_nav", CurrentBinding.wheelSteeringNavigation ? 1 : 0);
            PlayerPrefs.SetInt("ui_bind_wheel_steering_invert", CurrentBinding.wheelSteeringInvert ? 1 : 0);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_deadzone", CurrentBinding.wheelSteeringDeadzone);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_release", CurrentBinding.wheelSteeringReleaseThreshold);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_hold", CurrentBinding.wheelSteeringHoldDelay);
            PlayerPrefs.SetFloat("ui_bind_wheel_steering_repeat", CurrentBinding.wheelSteeringRepeatRate);

            // 官方 JSON 快照：重置时删除（ResetBindings 后不应残留旧覆盖）
            if (inputActions != null)
            {
                try
                {
                    PlayerPrefs.SetString("ui_bind_asset_json", inputActions.ToJson());
                }
                catch (Exception ex)
                {
                    Log.Warning("UIInputSystem: 绑定 JSON 快照保存失败: {0}", ex.Message);
                }
            }
            else
            {
                PlayerPrefs.DeleteKey("ui_bind_asset_json");
            }

            PlayerPrefs.Save();
        }

        #endregion

        #region 焦点管理

        /// <summary>
        /// 注册 UI 窗口的默认首选项
        /// </summary>
        /// <param name="uiName">UI 窗口名称</param>
        /// <param name="firstSelected">默认选中的 GameObject</param>
        public void RegisterFirstSelected(string uiName, GameObject firstSelected)
        {
            if (firstSelected == null) return;
            firstSelectedRegistry[uiName] = firstSelected;
        }

        /// <summary>
        /// 取消注册 UI 窗口的默认首选项
        /// </summary>
        public void UnregisterFirstSelected(string uiName)
        {
            firstSelectedRegistry.Remove(uiName);
        }

        /// <summary>
        /// 将焦点压入栈并设置为当前选中（包装方法，包含边界检查）。
        /// 注意：压入的是【当前选中】的旧焦点，开窗→设新焦点只入栈一次，
        /// PopFocus 弹出并恢复该旧焦点，保证 Push/Pop 严格配对。
        /// </summary>
        public void PushFocus(GameObject go)
        {
            if (eventSystem == null) return;

            // 检查目标是否可选
            if (go != null)
            {
                var selectable = go.GetComponent<UnityEngine.UI.Selectable>();
                if (selectable == null || !selectable.interactable) return;
            }

            // 幂等：目标与当前一致时不重复压栈
            if (go == CurrentSelected) return;

            // 先压入旧焦点，再设置新焦点
            if (CurrentSelected != null)
            {
                focusStack.Push(CurrentSelected);
            }

            eventSystem.SetSelectedGameObject(go);
            CurrentSelected = go;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (focusStack.Count > 32)
            {
                Log.Warning("UIInputSystem: 焦点栈深度异常 ({0})，可能存在 Push/Pop 不对称调用", focusStack.Count);
            }
#endif
        }

        /// <summary>
        /// 弹出当前焦点并恢复到上一个（包装方法，包含边界检查）
        /// </summary>
        public void PopFocus()
        {
            if (focusStack.Count == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Warning("UIInputSystem: PopFocus 调用时焦点栈为空，可能存在不对称的 Push/Pop");
#endif
                return;
            }

            // 弹出的是开窗时压入的旧焦点
            GameObject restoreTarget = focusStack.Pop();

            // 旧焦点可能已销毁/失活，向下找最近的有效焦点
            while (restoreTarget != null && !restoreTarget.activeInHierarchy && focusStack.Count > 0)
            {
                restoreTarget = focusStack.Pop();
            }

            if (restoreTarget != null && restoreTarget.activeInHierarchy)
            {
                SetEventSystemSelection(restoreTarget);
                CurrentSelected = restoreTarget;
            }
            else if (LastSelected != null && LastSelected.activeInHierarchy)
            {
                SetEventSystemSelection(LastSelected);
                CurrentSelected = LastSelected;
            }
            else
            {
                SetEventSystemSelection(null);
                CurrentSelected = null;
            }
        }

        /// <summary>设置 EventSystem 焦点；使用 Unity 的 == 判空以识别已销毁对象。</summary>
        private void SetEventSystemSelection(GameObject go)
        {
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(go);
            }
        }

        /// <summary>
        /// 手动设置当前选中的 GameObject（压入当前旧焦点后设置新焦点）
        /// </summary>
        public void SetSelectedGameObject(GameObject go)
        {
            PushFocus(go);
        }

        /// <summary>
        /// 恢复上一个焦点（弹窗关闭时调用）
        /// </summary>
        public void RestorePreviousFocus()
        {
            PopFocus();
        }

        /// <summary>
        /// 清除当前焦点（取消所有选中）
        /// </summary>
        public void ClearFocus()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (focusStack.Count > 0)
            {
                Log.Debug("UIInputSystem: ClearFocus 清除了 {0} 个焦点记录", focusStack.Count);
            }
#endif
            focusStack.Clear();
            SetEventSystemSelection(null);
            CurrentSelected = null;
        }

        /// <summary>
        /// 获取当前选中 GameObject 上的 Selectable
        /// </summary>
        public UnityEngine.UI.Selectable GetCurrentSelectable()
        {
            return CurrentSelected?.GetComponent<UnityEngine.UI.Selectable>();
        }

        #endregion

        #region UI 控制模式切换（Gameplay ↔ UIControl）

        /// <summary>
        /// 启用 UI 控制模式
        /// 关闭 Player 输入，启用本系统的键盘/手柄/方向盘/飞行摇杆 UI 导航，并自动聚焦当前打开的 UI 窗口
        /// </summary>
        public void EnableUIControl()
        {
            if (currentMode == UIControlMode.UIControl) return;

            EnsureInputModuleReferences();

            // 记录 Player map 状态（InputActions 加载失败时仍允许使用本系统的设备轮询）
            var playerMap = inputActions != null ? inputActions.FindActionMap("Player") : null;
            if (playerMap != null)
            {
                playerMapWasEnabled = playerMap.enabled;
                playerMap.Disable();
            }

            // 先确保 InputModule 处于启用状态：组件 OnEnable 会重新启用它引用的所有动作，
            // 因此必须在其后关闭 move/submit/cancel，避免重复响应。
            if (inputModule != null)
            {
                inputModule.enabled = true;
            }

            SetUIActionsEnabled(enablePointerActions: true);

            // 清理上一轮 UI 会话的重复/转向状态，避免按住键进入 UI 时立即多跳一格
            repeatStates.Clear();
            steeringStates.Clear();

            currentMode = UIControlMode.UIControl;

            // 尝试聚焦当前打开的 UI 窗口
            TryFocusCurrentUI();

            Log.Debug("UIInputSystem: 启用 UI 控制模式");
            try { UIControlModeChangedSubject.OnNext(UIControlMode.UIControl); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] UIControlModeChangedSubject 订阅者异常（已隔离）: {0}", ex.Message); }
        }

        /// <summary>
        /// 禁用 UI 控制模式
        /// 恢复 Player 输入，仅保留鼠标/触摸对 UI 的点击能力，并清除 UI 焦点
        /// </summary>
        public void DisableUIControl()
        {
            if (currentMode == UIControlMode.Gameplay) return;

            EnsureInputModuleReferences();

            // 清除 UI 焦点
            ClearFocus();

            // 退出 UI 控制时清空重复状态，避免下次进入时沿用旧的按住计时
            repeatStates.Clear();
            steeringStates.Clear();

            // 保持 InputSystemUIInputModule 激活，使鼠标/触摸仍可驱动 UI。
            // 先启用组件（可能重新启用其动作），再关闭导航/提交/取消。
            if (inputModule != null)
            {
                inputModule.enabled = true;
            }

            // 保留鼠标/触摸指针事件，避免 Gameplay 模式下 UI 完全不可点击。
            // 仅关闭导航/提交/取消，让键盘/手柄/方向盘不再驱动 UI 焦点。
            SetUIActionsEnabled(enablePointerActions: true);

            // 恢复 Player map
            var playerMap = inputActions != null ? inputActions.FindActionMap("Player") : null;
            if (playerMap != null && playerMapWasEnabled)
            {
                playerMap.Enable();
            }

            currentMode = UIControlMode.Gameplay;

            Log.Debug("UIInputSystem: 禁用 UI 控制模式");
            try { UIControlModeChangedSubject.OnNext(UIControlMode.Gameplay); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] UIControlModeChangedSubject 订阅者异常（已隔离）: {0}", ex.Message); }
        }

        /// <summary>
        /// 在 Gameplay 和 UIControl 模式之间切换
        /// </summary>
        public void ToggleUIControl()
        {
            if (currentMode == UIControlMode.Gameplay)
            {
                EnableUIControl();
            }
            else
            {
                DisableUIControl();
            }
        }

        /// <summary>
        /// 尝试聚焦当前打开的 UI 窗口中的首个可选元素
        /// </summary>
        private void TryFocusCurrentUI()
        {
            if (eventSystem == null) return;

            // 遍历所有已打开的 UI 窗口，按优先级找第一个可聚焦的
            var openWindows = UISystem.Instance?.GetAllOpenWindows();
            if (openWindows != null)
            {
                foreach (var window in openWindows)
                {
                    if (window == null || !window.gameObject.activeInHierarchy) continue;

                    var first = window.firstSelected;
                    if (first == null)
                    {
                        firstSelectedRegistry.TryGetValue(window.uiName, out first);
                    }
                    if (first == null)
                    {
                        first = FindFirstSelectable(window.gameObject);
                    }

                    if (first != null)
                    {
                        eventSystem.SetSelectedGameObject(first);
                        CurrentSelected = first;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 每帧轮询切换键，处理 Gameplay ↔ UI 模式切换
        /// </summary>
        private void PollToggleKeys()
        {
            // 重绑定期间不响应模式切换键
            if (isRebinding || Time.unscaledTime < inputSuppressedUntil) return;

            bool enterUI = currentMode == UIControlMode.Gameplay;

            // 键盘切换键（默认 Tab，进出对称）
            if (Keyboard.current != null)
            {
                var toggleKey = GetCachedKey(ref cachedToggleToUIStr, ref cachedToggleToUIKey, CurrentBinding.toggleToUI);
                if (toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
                {
                    ToggleFromInput(enterUI);
                    return;
                }
            }

            // 所有手柄的 Start（不再只取 Gamepad.current，支持多手柄/多方向盘同时接入）
            var gamepads = Gamepad.all;
            var gamepadButton = GetCachedGamepadButton(ref cachedToggleToUIGamepadStr, ref cachedToggleToUIGamepadButton, CurrentBinding.toggleToUIGamepad);
            for (int i = 0; i < gamepads.Count; i++)
            {
                if (gamepadButton.HasValue && gamepads[i][gamepadButton.Value].wasPressedThisFrame)
                {
                    ToggleFromInput(enterUI);
                    return;
                }

                // 菜单键被重绑为 dpad/trigger 等无法映射到 GamepadButton 的控件时，按路径查找
                if (!gamepadButton.HasValue && !string.IsNullOrEmpty(CurrentBinding.toggleToUIGamepad))
                {
                    var control = InputControlPath.TryFindControl<ButtonControl>(
                        gamepads[i], CurrentBinding.toggleToUIGamepad);
                    if (control != null && control.wasPressedThisFrame)
                    {
                        ToggleFromInput(enterUI);
                        return;
                    }
                }
            }

            // 方向盘/飞行摇杆的 Start（设备有 start 键时生效）
            if (devicesDirty)
            {
                RefreshRoutedDevices();
            }
            for (int i = 0; i < routedDevices.Count; i++)
            {
                if (routedDevices[i] is Joystick joystick)
                {
                    var toggle = GetJoystickToggle(joystick);
                    if (toggle != null && toggle.wasPressedThisFrame)
                    {
                        ToggleFromInput(enterUI);
                        return;
                    }
                }
            }

            if (enterUI) return;

            // 退出键（默认 Escape）：与 Cancel 共键时优先交给窗口/轮播处理，
            // 只有在没有可取消目标时才退出 UI 控制模式。
            if (Keyboard.current != null)
            {
                var exitKey = GetCachedKey(ref cachedToggleToGameplayStr, ref cachedToggleToGameplayKey, CurrentBinding.toggleToGameplay);
                if (exitKey != Key.None && Keyboard.current[exitKey].wasPressedThisFrame)
                {
                    var cancelKey = GetKeyFromName(CurrentBinding.cancel);
                    if (exitKey == cancelKey && (HasOpenUI() || HasActiveNavigationTarget(KeyboardMousePlayerIndex)))
                    {
                        return;
                    }
                    DisableUIControl();
                }
            }
        }

        private void ToggleFromInput(bool enterUI)
        {
            // 菜单键与确认/取消键可能是同一个物理键：切换模式后抑制本帧剩余输入，
            // 避免“按菜单键打开 UI”的同一次按下又确认/关闭了某个选项。
            // 只抑制一帧左右，避免打开 UI 后立刻按方向键被吞掉。
            inputSuppressedUntil = Time.unscaledTime + 0.05f;

            if (enterUI)
            {
                EnableUIControl();
            }
            else
            {
                DisableUIControl();
            }
        }

        private ButtonControl GetJoystickToggle(Joystick joystick)
        {
            if (joystick == null) return null;
            if (UIInputDeviceRegistry.TryGetToggle(joystick, out var toggle)) return toggle;

            // 设备没有 start 时，允许通过全局绑定指定任意按钮
            if (!string.IsNullOrEmpty(CurrentBinding.toggleToUIJoystick))
            {
                return InputControlPath.TryFindControl<ButtonControl>(joystick, CurrentBinding.toggleToUIJoystick);
            }
            return null;
        }

        /// <summary>
        /// 按键名 → Key 的免分配缓存：仅当字符串值变化时才重新解析，
        /// 避免每帧 ToLower().Trim() 产生字符串分配。
        /// </summary>
        private Key GetCachedKey(ref string cachedStr, ref Key cachedKey, string current)
        {
            if (cachedStr != current)
            {
                cachedStr = current;
                cachedKey = GetKeyFromName(current);
            }
            return cachedKey;
        }

        /// <summary>
        /// 将按键名称字符串转换为 Key 枚举值
        /// </summary>
        private Key GetKeyFromName(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return Key.None;

            // 兼容旧存档中的完整路径：<Keyboard>/enter → enter
            if (keyName.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                keyName = keyName.Substring(keyName.LastIndexOf('/') + 1);
            }

            // 常用别名映射
            var normalized = keyName.ToLower().Trim();
            if (normalized == "enter" || normalized == "return") return Key.Enter;
            if (normalized == "escape" || normalized == "esc") return Key.Escape;
            if (normalized == "tab") return Key.Tab;
            if (normalized == "space") return Key.Space;

            // 尝试直接解析
            if (Enum.TryParse<Key>(keyName, true, out var result))
            {
                return result;
            }

            return Key.None;
        }

        /// <summary>
        /// 游戏手柄按键路径（如 "&lt;Gamepad&gt;/start"）→ GamepadButton 的免分配缓存：
        /// 仅当字符串值变化时才重新解析，避免每帧字符串分配。
        /// </summary>
        private GamepadButton? GetCachedGamepadButton(ref string cachedStr, ref GamepadButton? cachedButton, string current)
        {
            if (cachedStr != current)
            {
                cachedStr = current;
                cachedButton = ParseGamepadButton(current);
            }
            return cachedButton;
        }

        /// <summary>
        /// 解析游戏手柄按键完整路径（如 "&lt;Gamepad&gt;/start"）为 GamepadButton；
        /// 支持 "&lt;Gamepad&gt;/xxx" 与裸名 "xxx" 两种写法，无法解析时返回 null。
        /// </summary>
        private GamepadButton? ParseGamepadButton(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.LastIndexOf('/');
            var name = slash >= 0 ? path.Substring(slash + 1) : path;
            if (name.StartsWith("button", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("button".Length);
            }
            if (Enum.TryParse<GamepadButton>(name, true, out var result))
            {
                return result;
            }
            return null;
        }

        #endregion

        #region UISystem 生命周期事件处理

        /// <summary>R3 订阅句柄（用于取消订阅）</summary>
        private IDisposable uiOpenSubscription;
        private IDisposable uiCloseSubscription;

        private void RegisterUIEvents()
        {
            // R3 原生订阅方式
            uiOpenSubscription = UISystem.Instance.OnOpenSubject.Subscribe(OnUIOpened);
            uiCloseSubscription = UISystem.Instance.OnCloseSubject.Subscribe(OnUIClosed);
        }

        private void UnregisterUIEvents()
        {
            uiOpenSubscription?.Dispose();
            uiOpenSubscription = null;
            uiCloseSubscription?.Dispose();
            uiCloseSubscription = null;
        }

        /// <summary>
        /// UI 窗口打开时 —— 自动聚焦该窗口注册的首选项
        /// </summary>
        private void OnUIOpened(UIController controller)
        {
            if (controller == null) return;

            // 仅在 UI 模式下才自动聚焦；Gameplay 模式下窗口打开不抢焦点
            if (currentMode != UIControlMode.UIControl) return;

            // 优先从 UIController 获取 firstSelected
            var firstSelected = controller.firstSelected;

            // 如果 UIController 未设置，尝试从注册表获取
            if (firstSelected == null)
            {
                firstSelectedRegistry.TryGetValue(controller.uiName, out firstSelected);
            }

            // 如果仍未找到，自动查找该 UI 下的第一个 Selectable
            if (firstSelected == null)
            {
                firstSelected = FindFirstSelectable(controller.gameObject);
            }

            if (firstSelected != null)
            {
                // PushFocus 内部会压入当前旧焦点并设置新焦点（单次压栈）。
                // 不要在此处再手动 Push，否则每次开窗多压一条，焦点栈无界增长。
                SetSelectedGameObject(firstSelected);
            }
        }

        /// <summary>
        /// UI 窗口关闭时 —— 恢复上一个焦点
        /// </summary>
        private void OnUIClosed(UIController controller)
        {
            // 与 OnUIOpened 对称：仅当窗口是在 UI 模式下打开（压栈过焦点）时才恢复焦点，
            // 避免 Gameplay 模式下关窗误弹栈导致焦点栈错位（Pop 多于 Push）。
            if (currentMode != UIControlMode.UIControl) return;

            // 延迟一帧恢复焦点：当帧窗口尚未完全从 EventSystem 注销，
            // 立即恢复可能被 InputSystemUIInputModule 当帧的导航状态覆写
            if (focusStack.Count > 0)
            {
                RestoreFocusNextFrameAsync().Forget();
            }
        }

        /// <summary>延迟一帧恢复焦点（延迟期间若模式切换/Clear 则放弃恢复）</summary>
        private async UniTaskVoid RestoreFocusNextFrameAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (currentMode != UIControlMode.UIControl) return;
            if (focusStack.Count > 0)
            {
                RestorePreviousFocus();
            }
        }

        /// <summary>
        /// 在 GameObject 及其子对象中查找第一个可交互的 Selectable
        /// </summary>
        private GameObject FindFirstSelectable(GameObject root)
        {
            if (root == null) return null;

            var selectables = root.GetComponentsInChildren<UnityEngine.UI.Selectable>();
            foreach (var s in selectables)
            {
                if (s.interactable && s.gameObject.activeInHierarchy)
                {
                    return s.gameObject;
                }
            }
            return null;
        }

        #endregion

        #region 输入事件回调与设备轮询

        private void RegisterInputCallbacks()
        {
            // 导航/确认/取消不再走 InputAction 回调：改为每帧直接轮询设备。
            // 这样键盘、手柄、赛车方向盘、飞行摇杆可以统一处理，并按玩家槽位路由；
            // InputModule 只保留鼠标/触摸指针动作，避免 EventSystem 与轮播双重响应。
            if (inputActions != null)
            {
                var playerMap = inputActions.FindActionMap("Player");
                playerMap?.Enable();
            }

            SetUIActionsEnabled(enablePointerActions: true);
            currentMode = UIControlMode.Gameplay;
        }

        /// <summary>
        /// UISystem 可能在 UIInputSystem.Init 之后才创建/替换 EventSystem，使用前补一次引用解析。
        /// </summary>
        private void EnsureInputModuleReferences()
        {
            var current = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = current ?? UISystem.Instance?.EventSystem;
            }
            else if (!eventSystem.isActiveAndEnabled && current != null && current != eventSystem)
            {
                eventSystem = current;
            }

            if (eventSystem != null &&
                (inputModule == null || inputModule.gameObject != eventSystem.gameObject))
            {
                inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            }
        }

        private void SetUIActionsEnabled(bool enablePointerActions)
        {
            // 自己的资产副本：保持指针动作与 Player map 可用；导航动作关闭（由轮询器接管）。
            if (inputActions != null)
            {
                SetActionEnabled(inputActions, "UI/Point", enablePointerActions);
                SetActionEnabled(inputActions, "UI/Click", enablePointerActions);
                SetActionEnabled(inputActions, "UI/RightClick", enablePointerActions);
                SetActionEnabled(inputActions, "UI/MiddleClick", enablePointerActions);
                SetActionEnabled(inputActions, "UI/ScrollWheel", enablePointerActions);
                SetActionEnabled(inputActions, "UI/TrackedDevicePosition", enablePointerActions);
                SetActionEnabled(inputActions, "UI/TrackedDeviceOrientation", enablePointerActions);

                // 始终关闭：键盘/手柄/方向盘导航由 PollInputDevices 产生，
                // 若这里再启用 InputAction，会与轮询器重复响应。
                SetActionEnabled(inputActions, "UI/Navigate", false);
                SetActionEnabled(inputActions, "UI/Submit", false);
                SetActionEnabled(inputActions, "UI/Cancel", false);
            }

            // 真正驱动 EventSystem 的是 InputModule 自己的资产，必须单独关闭导航动作。
            if (inputModule != null)
            {
                inputModule.move?.action?.Disable();
                inputModule.submit?.action?.Disable();
                inputModule.cancel?.action?.Disable();

                if (enablePointerActions)
                {
                    inputModule.point?.action?.Enable();
                    inputModule.leftClick?.action?.Enable();
                    inputModule.rightClick?.action?.Enable();
                    inputModule.middleClick?.action?.Enable();
                    inputModule.scrollWheel?.action?.Enable();
                    inputModule.trackedDevicePosition?.action?.Enable();
                    inputModule.trackedDeviceOrientation?.action?.Enable();
                }
            }
        }

        private static void SetActionEnabled(InputActionAsset asset, string actionPath, bool enabled)
        {
            var action = asset?.FindAction(actionPath);
            if (action == null) return;

            if (enabled)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }

        private void UnregisterInputCallbacks()
        {
            // 轮询模式无需解除 InputAction 回调；只需确保模块导航保持关闭。
            if (inputModule == null) return;
            inputModule.move?.action?.Disable();
            inputModule.submit?.action?.Disable();
            inputModule.cancel?.action?.Disable();
        }

        #region 设备轮询与事件派发

        /// <summary>每帧轮询键盘/鼠标/手柄/方向盘/飞行摇杆，产生带玩家槽位的 UI 事件。</summary>
        private void PollInputDevices()
        {
            // 重绑定期间捕获的按键不应同时触发 UI 动作
            if (isRebinding || Time.unscaledTime < inputSuppressedUntil) return;

            float now = Time.unscaledTime;

            PollKeyboard(now);
            PollMouse(now);

            for (int i = 0; i < routedDevices.Count; i++)
            {
                var device = routedDevices[i];
                if (device == null || !device.added) continue;

                int player = GetPlayerIndex(device);
                if (device is Gamepad gamepad)
                {
                    PollGamepad(gamepad, player, now);
                }
                else if (device is Joystick joystick)
                {
                    PollJoystick(joystick, player, now);
                }
            }
        }

        private void PollKeyboard(float now)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 输入框获得焦点时，方向键/WASD 用于文本编辑，不能被 UI 导航吞掉
            if (IsTextInputFocused())
            {
                ResetRepeat(-1);
            }
            else
            {
                Vector2Int step = Vector2Int.zero;
                var upKey = GetCachedKey(ref cachedNavUpStr, ref cachedNavUpKey, CurrentBinding.navigateUp);
                var downKey = GetCachedKey(ref cachedNavDownStr, ref cachedNavDownKey, CurrentBinding.navigateDown);
                var leftKey = GetCachedKey(ref cachedNavLeftStr, ref cachedNavLeftKey, CurrentBinding.navigateLeft);
                var rightKey = GetCachedKey(ref cachedNavRightStr, ref cachedNavRightKey, CurrentBinding.navigateRight);

                if (IsKeyActive(keyboard, upKey) || IsKeyActive(keyboard, Key.UpArrow)) step.y += 1;
                if (IsKeyActive(keyboard, downKey) || IsKeyActive(keyboard, Key.DownArrow)) step.y -= 1;
                if (IsKeyActive(keyboard, leftKey) || IsKeyActive(keyboard, Key.LeftArrow)) step.x -= 1;
                if (IsKeyActive(keyboard, rightKey) || IsKeyActive(keyboard, Key.RightArrow)) step.x += 1;

                if (step == Vector2Int.zero)
                {
                    ResetRepeat(-1);
                }
                else
                {
                    EmitDigitalStep(null, KeyboardMousePlayerIndex, step, now, -1);
                }
            }

            var submitKey = GetCachedKey(ref cachedSubmitStr, ref cachedSubmitKey, CurrentBinding.submit);
            if (IsSubmitKeyPressed(keyboard, submitKey))
            {
                DispatchSubmit(null, KeyboardMousePlayerIndex);
            }

            var cancelKey = GetCachedKey(ref cachedCancelStr, ref cachedCancelKey, CurrentBinding.cancel);
            if (IsKeyPressedThisFrame(keyboard, cancelKey))
            {
                DispatchCancel(null, KeyboardMousePlayerIndex);
            }
        }

        private void PollMouse(float now)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (CurrentBinding.mouseRightClickAsCancel && mouse.rightButton.wasPressedThisFrame)
            {
                DispatchCancel(null, KeyboardMousePlayerIndex);
            }
        }

        private void PollGamepad(Gamepad gamepad, int player, float now)
        {
            Vector2Int digital = Vector2Int.zero;
            var dpad = gamepad.dpad;
            if (IsButtonActive(dpad.up)) digital.y += 1;
            if (IsButtonActive(dpad.down)) digital.y -= 1;
            if (IsButtonActive(dpad.left)) digital.x -= 1;
            if (IsButtonActive(dpad.right)) digital.x += 1;

            // 方向盘伪装成手柄时，允许用 SetDeviceNavigation 把它的面板按钮/小摇杆按钮绑成方向
            if (UIInputDeviceRegistry.TryGetNavigationButtons(
                gamepad, out var navUp, out var navDown, out var navLeft, out var navRight))
            {
                if (IsButtonActive(navUp)) digital.y += 1;
                if (IsButtonActive(navDown)) digital.y -= 1;
                if (IsButtonActive(navLeft)) digital.x -= 1;
                if (IsButtonActive(navRight)) digital.x += 1;
            }

            // 自定义按钮可能与十字键重合，避免一次按下被算成两步
            digital.x = Mathf.Clamp(digital.x, -1, 1);
            digital.y = Mathf.Clamp(digital.y, -1, 1);

            // 有些方向盘会伪装成 Gamepad：左摇杆是转向轴，不能用于 UI 导航；
            // 这类设备改用右摇杆（通常是小摇杆），没有右摇杆输入时只用十字键。
            bool wheelLike = UIInputDeviceRegistry.LooksLikeRacingWheel(gamepad);
            Vector2 stick;
            if (wheelLike)
            {
                stick = gamepad.rightStick.ReadValue();
            }
            else
            {
                stick = gamepad.leftStick.ReadValue();
                // 左摇杆优先；左摇杆回中时允许右摇杆导航
                if (Mathf.Abs(stick.x) < CurrentBinding.navigateDeadzone &&
                    Mathf.Abs(stick.y) < CurrentBinding.navigateDeadzone)
                {
                    Vector2 right = gamepad.rightStick.ReadValue();
                    if (Mathf.Abs(right.x) >= CurrentBinding.navigateDeadzone ||
                        Mathf.Abs(right.y) >= CurrentBinding.navigateDeadzone)
                    {
                        stick = right;
                    }
                }
            }
            Vector2Int analog = ToCardinal(stick, CurrentBinding.navigateDeadzone);

            if (digital != Vector2Int.zero)
            {
                EmitDigitalStep(gamepad, player, digital, now, gamepad.deviceId);
            }
            else if (analog != Vector2Int.zero)
            {
                EmitAnalogStep(gamepad, player, analog, stick, now, gamepad.deviceId);
            }
            else
            {
                ResetRepeat(gamepad.deviceId);
            }

            var explicitSubmit = GetCachedGamepadButton(
                ref cachedGamepadSubmitStr, ref cachedGamepadSubmitButton, CurrentBinding.gamepadSubmit);
            ButtonControl submit = explicitSubmit.HasValue
                ? null
                : InputControlPath.TryFindControl<ButtonControl>(gamepad, CurrentBinding.gamepadSubmit);
            submit ??= GetGamepadActionButton(gamepad, explicitSubmit, "Submit", GamepadButton.South, gamepadSubmitCache);
            if (submit != null && submit.wasPressedThisFrame)
            {
                DispatchSubmit(gamepad, player);
            }

            var explicitCancel = GetCachedGamepadButton(
                ref cachedGamepadCancelStr, ref cachedGamepadCancelButton, CurrentBinding.gamepadCancel);
            ButtonControl cancel = explicitCancel.HasValue
                ? null
                : InputControlPath.TryFindControl<ButtonControl>(gamepad, CurrentBinding.gamepadCancel);
            cancel ??= GetGamepadActionButton(gamepad, explicitCancel, "Cancel", GamepadButton.East, gamepadCancelCache);
            if (cancel != null && cancel.wasPressedThisFrame)
            {
                DispatchCancel(gamepad, player);
            }
        }

        private void PollJoystick(Joystick joystick, int player, float now)
        {
            var profile = UIInputDeviceRegistry.GetProfile(joystick);
            if (profile.Kind == UIInputDeviceKind.RacingWheel)
            {
                PollRacingWheel(joystick, profile, player, now);
            }
            else
            {
                PollGenericJoystick(joystick, profile, player, now);
            }

            if (UIInputDeviceRegistry.TryGetSubmit(joystick, out var submit) && submit.wasPressedThisFrame)
            {
                DispatchSubmit(joystick, player);
            }
            if (UIInputDeviceRegistry.TryGetCancel(joystick, out var cancel) && cancel.wasPressedThisFrame)
            {
                DispatchCancel(joystick, player);
            }
        }

        /// <summary>
        /// 方向盘导航。Auto 优先级：自定义按钮 &gt; 小摇杆 &gt; 十字键/hat &gt; 转向轴。
        /// 只要检测到小摇杆/hat，就不会再使用转向轴，避免轻微打方向导致菜单频繁切换。
        /// </summary>
        private void PollRacingWheel(Joystick joystick, UIInputDeviceProfile profile, int player, float now)
        {
            var hat = UIInputDeviceRegistry.GetHat(joystick);
            bool hasCustom = UIInputDeviceRegistry.TryGetNavigationButtons(
                joystick, out var navUp, out var navDown, out var navLeft, out var navRight);
            bool hasThumb = UIInputDeviceRegistry.TryGetThumbStick(
                joystick, out var thumb, out var thumbX, out var thumbY);
            bool thumbIsAxesOnly = thumb == null && thumbX != null && thumbY != null;

            bool useCustom;
            bool useThumb;
            bool useHat;
            bool useSteering;

            switch (profile.WheelNavigationSource)
            {
                case WheelNavigationSource.Hat:
                    useCustom = false; useThumb = false; useHat = true; useSteering = false;
                    break;
                case WheelNavigationSource.ThumbStick:
                    useCustom = false; useThumb = true; useHat = false; useSteering = false;
                    break;
                case WheelNavigationSource.Steering:
                    useCustom = false; useThumb = false; useHat = false; useSteering = true;
                    break;
                case WheelNavigationSource.CustomButtons:
                    useCustom = true; useThumb = false; useHat = false; useSteering = false;
                    break;
                case WheelNavigationSource.None:
                    useCustom = false; useThumb = false; useHat = false; useSteering = false;
                    break;
                default: // Auto
                    // 优先级：自定义按钮 > 小摇杆 > 十字键 > 转向轴。
                    // 自定义按钮/小摇杆/十字键可以互为兜底；只要存在其一，就不使用转向轴。
                    // rx/ry 轴对只有在没有 hat 时才作为小摇杆自动使用，避免把离合/手刹轴误当摇杆。
                    useCustom = hasCustom;
                    useThumb = hasThumb && (thumb != null || hat == null);
                    useHat = hat != null;
                    useSteering = !hasCustom && !useThumb && hat == null;
                    break;
            }

            // 全局开关仍然可以关闭转向轴导航
            if (useSteering) useSteering = CurrentBinding.wheelSteeringNavigation;

            Vector2Int digital = Vector2Int.zero;
            if (useCustom)
            {
                if (IsButtonActive(navUp)) digital.y += 1;
                if (IsButtonActive(navDown)) digital.y -= 1;
                if (IsButtonActive(navLeft)) digital.x -= 1;
                if (IsButtonActive(navRight)) digital.x += 1;
            }

            Vector2Int thumbStep = Vector2Int.zero;
            Vector2 thumbValue = Vector2.zero;
            bool thumbIsDigital = false;
            if (digital == Vector2Int.zero && useThumb)
            {
                ReadThumbStickStep(thumb, thumbX, thumbY, out thumbStep, out thumbValue, out thumbIsDigital);
            }

            // 小摇杆优先；小摇杆无输入时再用十字键兜底
            if (digital == Vector2Int.zero && thumbStep == Vector2Int.zero && useHat)
            {
                digital = ReadHatStep(hat, 0.5f);
            }

            if (digital != Vector2Int.zero)
            {
                EmitDigitalStep(joystick, player, digital, now, joystick.deviceId);
            }
            else if (thumbStep != Vector2Int.zero)
            {
                if (thumbIsDigital)
                {
                    EmitDigitalStep(joystick, player, thumbStep, now, joystick.deviceId);
                }
                else
                {
                    EmitAnalogStep(joystick, player, thumbStep, thumbValue, now, joystick.deviceId);
                }
            }
            else
            {
                ResetRepeat(joystick.deviceId);
            }

            if (useSteering && digital == Vector2Int.zero && thumbStep == Vector2Int.zero)
            {
                var steering = UIInputDeviceRegistry.GetSteeringAxis(joystick);
                if (steering != null)
                {
                    PollWheelSteering(joystick, steering, player, now);
                }
                else
                {
                    steeringStates.Remove(joystick.deviceId);
                }
            }
            else
            {
                steeringStates.Remove(joystick.deviceId);
            }
        }

        /// <summary>飞行摇杆/普通摇杆：自定义按钮 &gt; hat &gt; 主轴。</summary>
        private void PollGenericJoystick(Joystick joystick, UIInputDeviceProfile profile, int player, float now)
        {
            var hat = UIInputDeviceRegistry.GetHat(joystick);
            Vector2Int digital = Vector2Int.zero;

            if (UIInputDeviceRegistry.TryGetNavigationButtons(
                joystick, out var navUp, out var navDown, out var navLeft, out var navRight))
            {
                if (IsButtonActive(navUp)) digital.y += 1;
                if (IsButtonActive(navDown)) digital.y -= 1;
                if (IsButtonActive(navLeft)) digital.x -= 1;
                if (IsButtonActive(navRight)) digital.x += 1;
            }

            if (digital == Vector2Int.zero)
            {
                digital = ReadHatStep(hat, 0.5f);
            }

            // 飞行摇杆（已识别）用主轴导航；未知 HID 摇杆只有存在 hat 时才用主轴，
            // 避免把未识别方向盘的转向轴当成导航轴，导致菜单持续切换。
            bool useStick = profile.UseStickForNavigation &&
                            (profile.Kind == UIInputDeviceKind.FlightStick || hat != null);

            Vector2 stickValue = Vector2.zero;
            Vector2Int analog = Vector2Int.zero;
            if (useStick)
            {
                var stick = UIInputDeviceRegistry.GetStick(joystick);
                if (stick != null)
                {
                    stickValue = stick.ReadValue();
                    analog = ToCardinal(stickValue, CurrentBinding.navigateDeadzone);
                }
            }

            if (digital != Vector2Int.zero)
            {
                EmitDigitalStep(joystick, player, digital, now, joystick.deviceId);
            }
            else if (analog != Vector2Int.zero)
            {
                EmitAnalogStep(joystick, player, analog, stickValue, now, joystick.deviceId);
            }
            else
            {
                ResetRepeat(joystick.deviceId);
            }
        }

        private void ReadThumbStickStep(
            Vector2Control thumb, AxisControl axisX, AxisControl axisY,
            out Vector2Int step, out Vector2 value, out bool isDigital)
        {
            step = Vector2Int.zero;
            value = Vector2.zero;
            isDigital = false;

            if (thumb is DpadControl dpad)
            {
                step = ReadHatStep(dpad, 0.5f);
                isDigital = true;
                return;
            }

            if (thumb != null)
            {
                value = thumb.ReadValue();
                step = ToCardinal(value, CurrentBinding.navigateDeadzone);
                return;
            }

            if (axisX != null && axisY != null)
            {
                value = new Vector2(axisX.ReadValue(), axisY.ReadValue());
                step = ToCardinal(value, CurrentBinding.navigateDeadzone);
            }
        }

        /// <summary>
        /// 转向轴导航：左右打方向触发一次，回正后重新允许触发。
        /// 适用于 MOZA RS50 等没有十字键的方向盘：向右 = 下一项，向左 = 上一项。
        /// 保持在阈值以上可按配置自动重复。
        /// </summary>
        private void PollWheelSteering(Joystick joystick, AxisControl steering, int player, float now)
        {
            if (steering == null) return;

            float value = steering.ReadValue();
            if (CurrentBinding.wheelSteeringInvert) value = -value;

            float engage = Mathf.Clamp(CurrentBinding.wheelSteeringDeadzone, 0.01f, 1f);
            float release = Mathf.Clamp(CurrentBinding.wheelSteeringReleaseThreshold, 0f, engage);
            float holdDelay = Mathf.Max(0f, CurrentBinding.wheelSteeringHoldDelay);
            float repeatRate = CurrentBinding.wheelSteeringRepeatRate;

            if (!steeringStates.TryGetValue(joystick.deviceId, out var state))
            {
                // UI 打开时方向盘可能本来就没回正：先不武装，等回正后再允许切换，
                // 避免打开菜单的瞬间立刻跳一格。
                bool nearCenter = value > -engage && value < engage;
                state = new SteeringState { PositiveArmed = nearCenter, NegativeArmed = nearCenter };
            }

            int direction = 0;
            bool repeat = false;

            if (value >= engage)
            {
                if (state.PositiveArmed)
                {
                    state.PositiveArmed = false;
                    state.RepeatDirection = 1;
                    state.NextRepeatTime = now + holdDelay;
                    direction = 1;
                }
                else if (state.RepeatDirection == 1 && repeatRate > 0f && now >= state.NextRepeatTime)
                {
                    state.NextRepeatTime = now + repeatRate;
                    direction = 1;
                    repeat = true;
                }
            }
            else if (value <= release)
            {
                state.PositiveArmed = true;
            }

            if (value <= -engage)
            {
                if (state.NegativeArmed)
                {
                    state.NegativeArmed = false;
                    state.RepeatDirection = -1;
                    state.NextRepeatTime = now + holdDelay;
                    direction = -1;
                }
                else if (state.RepeatDirection == -1 && repeatRate > 0f && now >= state.NextRepeatTime)
                {
                    state.NextRepeatTime = now + repeatRate;
                    direction = -1;
                    repeat = true;
                }
            }
            else if (value >= -release)
            {
                state.NegativeArmed = true;
            }

            if (value > -engage && value < engage)
            {
                state.RepeatDirection = 0;
            }

            steeringStates[joystick.deviceId] = state;

            if (direction != 0)
            {
                DispatchNavigate(
                    joystick, player,
                    new Vector2Int(direction, 0),
                    new Vector2(value, 0f),
                    true, repeat, isWheelSteering: true);
            }
        }

        private static Vector2Int ReadHatStep(Vector2Control hat, float threshold)
        {
            if (hat == null) return Vector2Int.zero;

            if (hat is DpadControl dpad)
            {
                Vector2Int digital = Vector2Int.zero;
                if (IsButtonActive(dpad.up)) digital.y += 1;
                if (IsButtonActive(dpad.down)) digital.y -= 1;
                if (IsButtonActive(dpad.left)) digital.x -= 1;
                if (IsButtonActive(dpad.right)) digital.x += 1;
                return digital;
            }

            return ToCardinal(hat.ReadValue(), threshold);
        }

        private static Vector2Int ToCardinal(Vector2 value, float deadzone)
        {
            float ax = Mathf.Abs(value.x);
            float ay = Mathf.Abs(value.y);
            if (ax < deadzone && ay < deadzone) return Vector2Int.zero;

            if (ax >= ay)
            {
                return new Vector2Int(value.x > 0f ? 1 : -1, 0);
            }
            return new Vector2Int(0, value.y > 0f ? 1 : -1);
        }

        private static ButtonControl GetGamepadActionButton(
            Gamepad gamepad, GamepadButton? explicitButton, string usage, GamepadButton fallback,
            Dictionary<int, ButtonControl> cache)
        {
            if (explicitButton.HasValue)
            {
                return gamepad[explicitButton.Value];
            }

            if (cache.TryGetValue(gamepad.deviceId, out var cached) && cached != null)
            {
                return cached;
            }

            // 优先使用 Input System 的标准 usage（兼容 Switch Pro 等 A/B 位置不同的手柄）
            var byUsage = InputControlPath.TryFindControl<ButtonControl>(gamepad, "*/{" + usage + "}");
            var result = byUsage ?? gamepad[fallback];
            cache[gamepad.deviceId] = result;
            return result;
        }

        /// <summary>按下状态：包含本帧刚按下（快速点击时 isPressed 可能已回落）。</summary>
        private static bool IsKeyActive(Keyboard keyboard, Key key)
        {
            if (key == Key.None) return false;
            var control = keyboard[key];
            return control.isPressed || control.wasPressedThisFrame;
        }

        /// <summary>数字按钮：包含本帧刚按下（快速点击/松开时 isPressed 可能已回落）。</summary>
        private static bool IsButtonActive(ButtonControl button)
        {
            return button != null && (button.isPressed || button.wasPressedThisFrame);
        }

        private static bool IsKeyPressedThisFrame(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        private bool IsTextInputFocused()
        {
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null) return false;

            return selected.GetComponent<UnityEngine.UI.InputField>() != null ||
                   selected.GetComponent<TMPro.TMP_InputField>() != null;
        }

        private static bool IsSubmitKeyPressed(Keyboard keyboard, Key key)
        {
            if (key == Key.None) return false;
            if (keyboard[key].wasPressedThisFrame) return true;

            // 小键盘 Enter 与主 Enter 等价
            return key == Key.Enter && keyboard[Key.NumpadEnter].wasPressedThisFrame;
        }

        /// <summary>数字输入：每次按下立即派发一次；按住后按 HoldDelay/RepeatRate 重复。</summary>
        private void EmitDigitalStep(InputDevice device, int player, Vector2Int step, float now, int stateKey)
        {
            if (!repeatStates.TryGetValue(stateKey, out var state) || state.LastStep != step)
            {
                state.LastStep = step;
                state.NextRepeatTime = now + Mathf.Max(0f, CurrentBinding.navigateHoldDelay);
                repeatStates[stateKey] = state;
                DispatchNavigate(device, player, step, new Vector2(step.x, step.y), false, false);
                return;
            }

            if (now < state.NextRepeatTime) return;

            state.NextRepeatTime = now + Mathf.Max(0.01f, CurrentBinding.navigateRepeatRate);
            repeatStates[stateKey] = state;
            DispatchNavigate(device, player, step, new Vector2(step.x, step.y), false, true);
        }

        /// <summary>模拟轴输入：方向变化立即派发；同方向按 HoldDelay/RepeatRate 重复。</summary>
        private void EmitAnalogStep(InputDevice device, int player, Vector2Int step, Vector2 value, float now, int stateKey)
        {
            if (!repeatStates.TryGetValue(stateKey, out var state) || state.LastStep != step)
            {
                state.LastStep = step;
                state.NextRepeatTime = now + Mathf.Max(0f, CurrentBinding.navigateHoldDelay);
                repeatStates[stateKey] = state;
                DispatchNavigate(device, player, step, value, true, false);
                return;
            }

            if (now < state.NextRepeatTime) return;

            state.NextRepeatTime = now + Mathf.Max(0.01f, CurrentBinding.navigateRepeatRate);
            repeatStates[stateKey] = state;
            DispatchNavigate(device, player, step, value, true, true);
        }

        private void ResetRepeat(int stateKey)
        {
            if (repeatStates.Count > 0)
            {
                repeatStates.Remove(stateKey);
            }
        }

        private void DispatchNavigate(
            InputDevice device, int player, Vector2Int step, Vector2 value,
            bool analog, bool repeat, bool isWheelSteering = false)
        {
            var e = new UINavigateEvent(device, player, step, value, analog, repeat, isWheelSteering);

            try { NavigateEventSubject?.OnNext(e); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] NavigateEventSubject 订阅者异常（已隔离）: {0}", ex.Message); }

            // 旧接口只代表主玩家（键盘/第一个实体控制器），避免第二个方向盘的输入误驱动旧 UI
            if (player == KeyboardMousePlayerIndex)
            {
                try { NavigateSubject?.OnNext(value); }
                catch (Exception ex) { Log.Warning("[UIInputSystem] NavigateSubject 订阅者异常（已隔离）: {0}", ex.Message); }
            }

            if (DispatchNavigateToTargets(e)) return;

            if (player == KeyboardMousePlayerIndex)
            {
                MoveFocus(step);
            }
        }

        private void DispatchSubmit(InputDevice device, int player)
        {
            var e = new UIButtonEvent(device, player);

            // 新事件用于观察/提示；真正消费由导航目标完成
            try { SubmitEventSubject?.OnNext(e); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] SubmitEventSubject 订阅者异常（已隔离）: {0}", ex.Message); }

            if (DispatchSubmitToTargets(e)) return;

            // 没有目标消费时才走旧接口与 EventSystem 回退，避免同一次确认被执行两次
            if (player == KeyboardMousePlayerIndex)
            {
                try { SubmitSubject?.OnNext(Unit.Default); }
                catch (Exception ex) { Log.Warning("[UIInputSystem] SubmitSubject 订阅者异常（已隔离）: {0}", ex.Message); }

                SubmitFocus();
            }
        }

        private void DispatchCancel(InputDevice device, int player)
        {
            var e = new UIButtonEvent(device, player);

            try { CancelEventSubject?.OnNext(e); }
            catch (Exception ex) { Log.Warning("[UIInputSystem] CancelEventSubject 订阅者异常（已隔离）: {0}", ex.Message); }

            if (DispatchCancelToTargets(e)) return;

            if (player == KeyboardMousePlayerIndex)
            {
                try { CancelSubject?.OnNext(Unit.Default); }
                catch (Exception ex) { Log.Warning("[UIInputSystem] CancelSubject 订阅者异常（已隔离）: {0}", ex.Message); }

                CancelFocus();
            }
        }

        private bool DispatchNavigateToTargets(in UINavigateEvent e)
        {
            var scope = GetTopFocusScope();
            if (scope == null) return false;

            try
            {
                return scope.HandleNavigate(e);
            }
            catch (Exception ex)
            {
                Log.Warning("[UIInputSystem] 焦点域处理导航异常（已隔离）: {0}", ex.Message);
                return false;
            }
        }

        private bool DispatchSubmitToTargets(in UIButtonEvent e)
        {
            var scope = GetTopFocusScope();
            if (scope == null) return false;

            try
            {
                return scope.HandleSubmit(e);
            }
            catch (Exception ex)
            {
                Log.Warning("[UIInputSystem] 焦点域处理确认异常（已隔离）: {0}", ex.Message);
                return false;
            }
        }

        private bool DispatchCancelToTargets(in UIButtonEvent e)
        {
            var scope = GetTopFocusScope();
            if (scope == null) return false;

            try
            {
                return scope.HandleCancel(e);
            }
            catch (Exception ex)
            {
                Log.Warning("[UIInputSystem] 焦点域处理取消异常（已隔离）: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// EventSystem 回退前确认当前焦点属于栈顶焦点域，
        /// 避免上层窗口没有可导航目标时把输入落到下层窗口。
        /// </summary>
        private bool IsSelectionInTopScope()
        {
            var scope = GetTopFocusScope();
            if (scope == null) return true;

            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            return selected != null && scope.Contains(selected.transform);
        }

        /// <summary>把导航事件交给 EventSystem 的当前焦点（普通 Button/Toggle 等）。</summary>
        private void MoveFocus(Vector2Int step)
        {
            if (eventSystem == null || step == Vector2Int.zero) return;
            if (!IsSelectionInTopScope()) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;

            MoveDirection moveDir;
            if (Mathf.Abs(step.x) >= Mathf.Abs(step.y))
            {
                moveDir = step.x > 0 ? MoveDirection.Right : MoveDirection.Left;
            }
            else
            {
                moveDir = step.y > 0 ? MoveDirection.Up : MoveDirection.Down;
            }

            var axisEvent = new AxisEventData(eventSystem)
            {
                moveVector = new Vector2(step.x, step.y),
                moveDir = moveDir,
            };
            ExecuteEvents.Execute(selected, axisEvent, ExecuteEvents.moveHandler);
        }

        private void SubmitFocus()
        {
            if (eventSystem == null || !IsSelectionInTopScope()) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;
            ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        }

        private void CancelFocus()
        {
            if (eventSystem == null || !IsSelectionInTopScope()) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;
            ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.cancelHandler);
        }

        private bool HasActiveNavigationTarget(int player)
        {
            var scope = GetTopFocusScope();
            return scope != null && scope.HasTargetForPlayer(player);
        }

        #endregion

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
