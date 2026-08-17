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
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace ReunionMovement.Core.UIInput
{
    /// <summary>
    /// UI 输入系统 —— 提供键盘/手柄控制 UGUI 导航的核心功能
    /// 包括：自动聚焦首个可选元素、焦点追踪、按键自定义重绑定
    /// </summary>
    public class UIInputSystem : ICustomSystem, ISystemUpdatable, ISystemDisposable
    {
        #region 单例与初始化
        private static readonly Lazy<UIInputSystem> instance = new(() => new UIInputSystem());
        public static UIInputSystem Instance => instance.Value;

        public bool isInited { get; private set; }
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

        /// <summary>切换键退出 UI 模式时消耗 Cancel 的帧号（-1=无）。OnCancelPerformed 据此跳过同帧的 Cancel 触发</summary>
        private int cancelConsumedFrame = -1;
        /// <summary>本帧 Cancel 已被打开的窗口消费的帧号（-1=无）。防止同帧 PollToggleKeys 误退出 UI 模式</summary>
        private int cancelConsumedByWindowFrame = -1;

        // ---- 切换键解析缓存（避免每帧字符串分配）----
        private string cachedToggleToUIStr;
        private Key cachedToggleToUIKey = Key.None;
        private string cachedToggleToUIGamepadStr;
        private GamepadButton? cachedToggleToUIGamepadButton;
        private string cachedToggleToGameplayStr;
        private Key cachedToggleToGameplayKey = Key.None;

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

        #endregion

        #region 初始化
        public async UniTask Init()
        {
            initProgress = 0;

            // 重建 R3 Subject（Clear() 已 Dispose 并置 null，重初始化时必须重建）
            SelectionChangedSubject ??= new Subject<GameObject>();
            BindingChangedSubject ??= new Subject<UIInputBinding>();
            UIControlModeChangedSubject ??= new Subject<UIControlMode>();
            NavigateSubject ??= new Subject<Vector2>();
            SubmitSubject ??= new Subject<Unit>();
            CancelSubject ??= new Subject<Unit>();

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

            // 5. 注册输入事件回调
            RegisterInputCallbacks();
            initProgress = 100;

            isInited = true;
            Log.Debug("UIInputSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {
            if (!isInited) return;

            // 轮询检测切换键
            PollToggleKeys();

            // 取消帧标记无需每帧复位：帧号会自然过期（跨帧后 == Time.frameCount 恒不成立），
            // 因此不再依赖“输入回调早于 Update 执行”的顺序假设。

            // 仅在 UI 模式下轮询焦点变化
            if (currentMode != UIControlMode.UIControl) return;

            // 轮询检测焦点变化（EventSystem 的 currentSelectedGameObject 可能在 InputSystemUIInputModule 内部更新）
            var current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (current != CurrentSelected)
            {
                var previous = CurrentSelected;
                CurrentSelected = current;
                SelectionChangedSubject.OnNext(current);

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

            // 切回 Gameplay 模式以恢复 Player map
            if (currentMode == UIControlMode.UIControl)
            {
                DisableUIControl();
            }

            focusStack.Clear();
            firstSelectedRegistry.Clear();
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
                    inputActions = UnityEngine.Object.Instantiate(loaded);
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

            // 交互式重绑定：允许键盘与手柄（Submit/Cancel 支持手柄按键）；排除指针/触屏等非按键设备
            var rebindOperation = action.PerformInteractiveRebinding()
                .WithControlsExcluding("<Mouse>/")
                .WithControlsExcluding("<Joystick>/")
                .WithControlsExcluding("<XRController>/")
                .WithControlsExcluding("<Touchscreen>/")
                .WithControlsExcluding("<Pen>/")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation =>
                {
                    isRebinding = false;
                    activeRebindOperation = null;
                    activeRebindCancelCallback = null;

                    // 更新 CurrentBinding
                    var newKey = operation.selectedControl.path;
                    var keyName = InputControlPath.ToHumanReadableString(newKey);
                    UpdateBindingFromRebind(actionName, newKey, keyName);

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

            operation?.Dispose();
            cancelCb?.Invoke();
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
            ApplyBindingsToActions();
            SaveBindings();
            BindingChangedSubject?.OnNext(CurrentBinding);
            Log.Debug("UIInputSystem: 按键绑定已重置为默认值");
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

            Register(CurrentBinding.submit, "Submit");
            Register(CurrentBinding.cancel, "Cancel");
            Register(CurrentBinding.toggleToUI, "ToggleToUI");
            Register(CurrentBinding.toggleToUIGamepad, "ToggleToUIGamepad");
            Register(CurrentBinding.toggleToGameplay, "ToggleToGameplay");

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
                toggleToUI = PlayerPrefs.GetString("ui_bind_toggle_ui", "tab"),
                toggleToUIGamepad = PlayerPrefs.GetString("ui_bind_toggle_ui_gamepad", "<Gamepad>/start"),
                toggleToUIGamepadDisplayName = PlayerPrefs.GetString("ui_bind_toggle_ui_gamepad_display", "Start"),
                toggleToGameplay = PlayerPrefs.GetString("ui_bind_toggle_gameplay", "escape"),
            };
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
            PlayerPrefs.SetString("ui_bind_toggle_ui", CurrentBinding.toggleToUI);
            PlayerPrefs.SetString("ui_bind_toggle_ui_gamepad", CurrentBinding.toggleToUIGamepad);
            PlayerPrefs.SetString("ui_bind_toggle_ui_gamepad_display", CurrentBinding.toggleToUIGamepadDisplayName);
            PlayerPrefs.SetString("ui_bind_toggle_gameplay", CurrentBinding.toggleToGameplay);

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
                eventSystem?.SetSelectedGameObject(restoreTarget);
                CurrentSelected = restoreTarget;
            }
            else if (LastSelected != null && LastSelected.activeInHierarchy)
            {
                eventSystem?.SetSelectedGameObject(LastSelected);
                CurrentSelected = LastSelected;
            }
            else
            {
                eventSystem?.SetSelectedGameObject(null);
                CurrentSelected = null;
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
            eventSystem?.SetSelectedGameObject(null);
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
        /// 关闭 Player 输入，启用键盘/手柄 UI 导航，并自动聚焦当前打开的 UI 窗口
        /// </summary>
        public void EnableUIControl()
        {
            if (currentMode == UIControlMode.UIControl) return;
            if (inputActions == null) return;

            // 记录 Player map 状态
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                playerMapWasEnabled = playerMap.enabled;
                playerMap.Disable();
            }

            SetUIActionsEnabled(enablePointerActions: true, enableNavigationActions: true);

            // 启用 InputSystemUIInputModule
            if (inputModule != null)
            {
                inputModule.enabled = true;
            }

            currentMode = UIControlMode.UIControl;

            // 尝试聚焦当前打开的 UI 窗口
            TryFocusCurrentUI();

            Log.Debug("UIInputSystem: 启用 UI 控制模式");
            UIControlModeChangedSubject.OnNext(UIControlMode.UIControl);
        }

        /// <summary>
        /// 禁用 UI 控制模式
        /// 恢复 Player 输入，仅保留鼠标/触摸对 UI 的点击能力，并清除 UI 焦点
        /// </summary>
        public void DisableUIControl()
        {
            if (currentMode == UIControlMode.Gameplay) return;
            if (inputActions == null)
            {
                // 输入未初始化：无法真正关闭导航，但需同步模式状态避免残留
                currentMode = UIControlMode.Gameplay;
                return;
            }

            // 清除 UI 焦点
            ClearFocus();

            // 保留鼠标/触摸指针事件，避免 Gameplay 模式下 UI 完全不可点击。
            // 仅关闭导航/提交/取消，让键盘/手柄不再驱动 UI 焦点。
            SetUIActionsEnabled(enablePointerActions: true, enableNavigationActions: false);

            // 保持 InputSystemUIInputModule 激活，使鼠标/触摸仍可驱动 UI。
            if (inputModule != null)
            {
                inputModule.enabled = true;
            }

            // 恢复 Player map
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null && playerMapWasEnabled)
            {
                playerMap.Enable();
            }

            currentMode = UIControlMode.Gameplay;

            Log.Debug("UIInputSystem: 禁用 UI 控制模式");
            UIControlModeChangedSubject.OnNext(UIControlMode.Gameplay);
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
            if (currentMode == UIControlMode.Gameplay)
            {
                // 在 Gameplay 模式：检测进入 UI 模式的切换键（键盘，默认 Tab）
                if (Keyboard.current != null)
                {
                    var toggleKey = GetCachedKey(ref cachedToggleToUIStr, ref cachedToggleToUIKey, CurrentBinding.toggleToUI);
                    if (toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
                    {
                        EnableUIControl();
                        return;
                    }
                }

                // 手柄切换键（默认 Start）：插着手柄时无需键盘即可进入 UI
                if (Gamepad.current != null)
                {
                    var gamepadButton = GetCachedGamepadButton(ref cachedToggleToUIGamepadStr, ref cachedToggleToUIGamepadButton, CurrentBinding.toggleToUIGamepad);
                    if (gamepadButton.HasValue && Gamepad.current[gamepadButton.Value].wasPressedThisFrame)
                    {
                        EnableUIControl();
                        return;
                    }
                }
            }
            else // UIControlMode.UIControl
            {
                // 在 UI 模式：再按一次 Tab/Start 同样退出 UI 模式（与进入按键对称的 toggle）。
                // 手柄玩家按 Start 打开界面后，再按 Start 即可关闭 UI 控制回到角色控制，无需键盘 Escape。
                if (Keyboard.current != null)
                {
                    var toggleKey = GetCachedKey(ref cachedToggleToUIStr, ref cachedToggleToUIKey, CurrentBinding.toggleToUI);
                    if (toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
                    {
                        DisableUIControl();
                        return;
                    }
                }

                if (Gamepad.current != null)
                {
                    var gamepadButton = GetCachedGamepadButton(ref cachedToggleToUIGamepadStr, ref cachedToggleToUIGamepadButton, CurrentBinding.toggleToUIGamepad);
                    if (gamepadButton.HasValue && Gamepad.current[gamepadButton.Value].wasPressedThisFrame)
                    {
                        DisableUIControl();
                        return;
                    }
                }

                // 在 UI 模式：检测退出 UI 模式的切换键（Escape）
                if (Keyboard.current != null)
                {
                    var exitKey = GetCachedKey(ref cachedToggleToGameplayStr, ref cachedToggleToGameplayKey, CurrentBinding.toggleToGameplay);
                    if (exitKey != Key.None && Keyboard.current[exitKey].wasPressedThisFrame)
                    {
                        // 修复 Esc 冲突：当退出键与 Cancel 键相同（默认都是 escape）且
                        // 有打开的窗口（或本帧 Cancel 已被窗口消费）时，让 Cancel 优先关闭
                        // 窗口，不退出 UI 模式；仅在没有窗口需要取消时才切换到 Gameplay。
                        var cancelKey = GetKeyFromName(CurrentBinding.cancel);
                        if (exitKey == cancelKey && (HasOpenUI() || cancelConsumedByWindowFrame == Time.frameCount))
                        {
                            return;
                        }

                        // 记录本帧已被切换键消耗的帧号：无论 OnCancelPerformed 在
                        // 本帧的输入阶段（早于 Update）还是晚于 Update 触发，
                        // 都会跳过同帧 CancelSubject，避免同一次 Esc 既退出 UI 模式又关窗。
                        cancelConsumedFrame = Time.frameCount;
                        DisableUIControl();
                        return;
                    }
                }
            }
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

        #region 输入事件回调

        private void RegisterInputCallbacks()
        {
            if (inputActions == null) return;

            var navigateAction = inputActions.FindAction("UI/Navigate");
            var submitAction = inputActions.FindAction("UI/Submit");
            var cancelAction = inputActions.FindAction("UI/Cancel");

            if (navigateAction != null)
            {
                navigateAction.performed += OnNavigatePerformed;
            }
            if (submitAction != null)
            {
                submitAction.performed += OnSubmitPerformed;
            }
            if (cancelAction != null)
            {
                cancelAction.performed += OnCancelPerformed;
            }

            // 默认启动为 Gameplay 模式：启用 Player map，仅关闭 UI 导航/提交/取消；
            // 保留鼠标/触摸点击所需的 Point/Click/Scroll 等动作。
            var playerMap = inputActions.FindActionMap("Player");

            playerMap?.Enable();
            SetUIActionsEnabled(enablePointerActions: true, enableNavigationActions: false);

            // 保持 EventSystem 的 InputSystemUIInputModule 激活，
            // 否则 UISystem 刚创建的运行时组件会立刻显示为未激活状态。
            // Gameplay 模式下通过禁用 UI ActionMap 来屏蔽键盘/手柄导航，
            // 但保留鼠标/触摸驱动的 UI 交互能力用于调试场景和运行时面板。

            currentMode = UIControlMode.Gameplay;
        }

        private void SetUIActionsEnabled(bool enablePointerActions, bool enableNavigationActions)
        {
            if (inputActions == null) return;

            SetActionEnabled("UI/Point", enablePointerActions);
            SetActionEnabled("UI/LeftClick", enablePointerActions);
            SetActionEnabled("UI/RightClick", enablePointerActions);
            SetActionEnabled("UI/MiddleClick", enablePointerActions);
            SetActionEnabled("UI/ScrollWheel", enablePointerActions);
            SetActionEnabled("UI/TrackedDevicePosition", enablePointerActions);
            SetActionEnabled("UI/TrackedDeviceOrientation", enablePointerActions);

            SetActionEnabled("UI/Navigate", enableNavigationActions);
            SetActionEnabled("UI/Submit", enableNavigationActions);
            SetActionEnabled("UI/Cancel", enableNavigationActions);
        }

        private void SetActionEnabled(string actionPath, bool enabled)
        {
            var action = inputActions?.FindAction(actionPath);
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
            if (inputActions == null) return;

            var navigateAction = inputActions.FindAction("UI/Navigate");
            var submitAction = inputActions.FindAction("UI/Submit");
            var cancelAction = inputActions.FindAction("UI/Cancel");

            if (navigateAction != null)
            {
                navigateAction.performed -= OnNavigatePerformed;
            }
            if (submitAction != null)
            {
                submitAction.performed -= OnSubmitPerformed;
            }
            if (cancelAction != null)
            {
                cancelAction.performed -= OnCancelPerformed;
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext ctx)
        {
            var value = ctx.ReadValue<Vector2>();
            NavigateSubject.OnNext(value);
        }

        private void OnSubmitPerformed(InputAction.CallbackContext ctx)
        {
            SubmitSubject.OnNext(Unit.Default);
        }

        private void OnCancelPerformed(InputAction.CallbackContext ctx)
        {
            // 若本帧 Cancel 已被切换键（退出 UI 模式）消耗，不再触发 CancelSubject。
            // 使用帧号判断（Time.frameCount 同帧内恒等），消除对
            // “输入回调早于 Update 执行”的顺序依赖。
            if (cancelConsumedFrame == Time.frameCount) return;

            // 有打开的窗口时记录本帧 Cancel 已由窗口消费的帧号，
            // 防止同帧 PollToggleKeys 检测到窗口已关闭后误退出 UI 模式
            if (HasOpenUI())
            {
                cancelConsumedByWindowFrame = Time.frameCount;
            }
            CancelSubject.OnNext(Unit.Default);
        }

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
