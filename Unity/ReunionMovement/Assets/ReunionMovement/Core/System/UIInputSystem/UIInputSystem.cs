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
using UnityEngine.InputSystem.UI;

namespace ReunionMovement.Core.UIInput
{
    /// <summary>
    /// UI 输入系统 —— 提供键盘/手柄控制 UGUI 导航的核心功能
    /// 包括：自动聚焦首个可选元素、焦点追踪、按键自定义重绑定
    /// </summary>
    public class UIInputSystem : ICustomSystem
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

        /// <summary>当前 UI 控制模式</summary>
        private UIControlMode currentMode = UIControlMode.Gameplay;

        /// <summary>当前 UI 控制模式（只读）</summary>
        public UIControlMode CurrentMode => currentMode;

        /// <summary>进入 UI 模式前的玩家操作映射状态缓存（用于恢复）</summary>
        private bool playerMapWasEnabled = false;

        /// <summary>UI 模式下的 Cancel 是否已被本帧处理（防止与切换键冲突）</summary>
        private bool cancelHandledThisFrame = false;

        #endregion

        #region R3 响应式事件（推荐新代码使用）

        /// <summary>焦点变更事件</summary>
        public readonly Subject<GameObject> SelectionChangedSubject = new Subject<GameObject>();

        /// <summary>按键绑定变更事件</summary>
        public readonly Subject<UIInputBinding> BindingChangedSubject = new Subject<UIInputBinding>();

        /// <summary>输入模式切换事件</summary>
        public readonly Subject<UIControlMode> UIControlModeChangedSubject = new Subject<UIControlMode>();

        /// <summary>导航操作事件（方向向量）</summary>
        public readonly Subject<Vector2> NavigateSubject = new Subject<Vector2>();

        /// <summary>提交操作事件</summary>
        public readonly Subject<Unit> SubmitSubject = new Subject<Unit>();

        /// <summary>取消操作事件</summary>
        public readonly Subject<Unit> CancelSubject = new Subject<Unit>();

        #endregion

        #region 初始化
        public async UniTask Init()
        {
            initProgress = 0;

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

            cancelHandledThisFrame = false;

            // 轮询检测切换键
            PollToggleKeys();

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

            // 释放 R3 Subject（自动断开所有订阅）
            SelectionChangedSubject?.Dispose();
            BindingChangedSubject?.Dispose();
            UIControlModeChangedSubject?.Dispose();
            NavigateSubject?.Dispose();
            SubmitSubject?.Dispose();
            CancelSubject?.Dispose();

            isInited = false;
        }
        #endregion

        #region 输入 Action 加载与绑定

        /// <summary>
        /// 加载 InputActionAsset（从 Settings 文件夹）
        /// </summary>
        private async UniTask LoadInputActionsAsync()
        {
            // 尝试从 UISystem 的 InputSystemUIInputModule 获取已赋值的 actions
            if (inputModule != null && inputModule.actionsAsset != null)
            {
                inputActions = inputModule.actionsAsset;
                return;
            }

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

            await UniTask.CompletedTask;
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
                // Cancel
                ApplyBindingOverride("UI", "Cancel", CurrentBinding.cancel);
            }
            catch (Exception ex)
            {
                Log.Error("UIInputSystem: 应用按键绑定失败: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 确保 Navigate action 同时支持 WASD 和方向键
        /// 默认 .inputactions 的 composite 已包含全部，仅在自定义键时追加快捷键
        /// </summary>
        private void EnsureNavigateBindings()
        {
            var action = inputActions.FindAction("UI/Navigate");
            if (action == null) return;

            // 检查当前 composite 中是否已包含自定义键，没有则追加
            EnsureKeyInAction(action, CurrentBinding.navigateUp);
            EnsureKeyInAction(action, CurrentBinding.navigateDown);
            EnsureKeyInAction(action, CurrentBinding.navigateLeft);
            EnsureKeyInAction(action, CurrentBinding.navigateRight);

            // 确保方向键也存在（如果被意外覆盖）
            EnsureKeyInAction(action, "upArrow");
            EnsureKeyInAction(action, "downArrow");
            EnsureKeyInAction(action, "leftArrow");
            EnsureKeyInAction(action, "rightArrow");
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
        /// 检查 action 中是否已有指定按键绑定，没有则追加
        /// </summary>
        private void EnsureKeyInAction(InputAction action, string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;

            var targetPath = $"<Keyboard>/{keyName}";
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == targetPath)
                    return; // 已存在，跳过
            }
            action.AddBinding(targetPath);
        }

        /// <summary>
        /// 对指定 Action 的指定 Binding 应用覆盖
        /// </summary>
        private void ApplyBindingOverride(string actionMap, string actionName, string bindingKey, string compositePart = null)
        {
            if (string.IsNullOrEmpty(bindingKey)) return;

            var action = inputActions.FindAction($"{actionMap}/{actionName}");
            if (action == null) return;

            // 查找对应 binding
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;

                // 如果指定了 composite part，匹配对应部分
                if (!string.IsNullOrEmpty(compositePart))
                {
                    // 对于 composite binding，需要找到键盘部分的 binding index
                    // 2D Vector composite: Up=0, Down=1, Left=2, Right=3
                }

                // 简单方案：找到第一个非 composite 的键盘 binding 并覆盖
                if (!binding.isComposite && binding.path.Contains("Keyboard"))
                {
                    action.ApplyBindingOverride(i, $"<Keyboard>/{bindingKey}");
                    return;
                }
            }

            // 如果没有找到键盘 binding，添加新的
            action.AddBinding($"<Keyboard>/{bindingKey}");
        }

        /// <summary>
        /// 对指定 Action 覆盖提交/取消按键
        /// </summary>
        private void ApplyBindingOverride(string actionMap, string actionName, string bindingKey)
        {
            if (string.IsNullOrEmpty(bindingKey)) return;

            var action = inputActions.FindAction($"{actionMap}/{actionName}");
            if (action == null) return;

            // 找到键盘 binding 并覆盖
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite && !binding.isPartOfComposite && binding.path.Contains("Keyboard"))
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

            isRebinding = true;

            // 对键盘设备执行重绑定
            var rebindOperation = action.PerformInteractiveRebinding()
                .WithControlsExcluding("<Mouse>/")
                .WithControlsExcluding("<Gamepad>/")
                .WithControlsExcluding("<Joystick>/")
                .WithControlsExcluding("<XRController>/")
                .WithControlsExcluding("<Touchscreen>/")
                .WithControlsExcluding("<Pen>/")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation =>
                {
                    isRebinding = false;

                    // 更新 CurrentBinding
                    var newKey = operation.selectedControl.path;
                    var keyName = InputControlPath.ToHumanReadableString(newKey);
                    UpdateBindingFromRebind(actionName, newKey, keyName);

                    // 保存绑定
                    SaveBindings();

                    onComplete?.Invoke(keyName);
                    BindingChangedSubject.OnNext(CurrentBinding);

                    operation.Dispose();
                })
                .OnCancel(operation =>
                {
                    isRebinding = false;
                    onCancel?.Invoke();
                    operation.Dispose();
                });

            rebindOperation.Start();
        }

        /// <summary>
        /// 取消当前重绑定
        /// </summary>
        public void CancelRebind()
        {
            isRebinding = false;
            // 注意：PerformInteractiveRebinding 取消由用户按 Escape 触发
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
            // 从 controlPath 中提取按键名称，如 "<Keyboard>/a" -> "a"
            var keyName = controlPath;
            var slashIndex = controlPath.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                keyName = controlPath.Substring(slashIndex + 1);
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
                    // Navigate 是 2D Vector composite，这里简化处理 ——
                    // 实际项目中应该支持分别重绑定上下左右
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
            BindingChangedSubject.OnNext(CurrentBinding);
        }

        /// <summary>
        /// 重置所有按键绑定为默认值
        /// </summary>
        public void ResetBindings()
        {
            CurrentBinding = new UIInputBinding();
            ApplyBindingsToActions();
            SaveBindings();
            BindingChangedSubject.OnNext(CurrentBinding);
            Log.Debug("UIInputSystem: 按键绑定已重置为默认值");
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
                toggleToGameplay = PlayerPrefs.GetString("ui_bind_toggle_gameplay", "escape"),
            };
        }

        /// <summary>
        /// 保存按键绑定到 PlayerPrefs
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
            PlayerPrefs.SetString("ui_bind_toggle_gameplay", CurrentBinding.toggleToGameplay);
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
        /// 将焦点压入栈并设置为当前选中（包装方法，包含边界检查）
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

            focusStack.Push(go);
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

            focusStack.Pop();

            GameObject restoreTarget = null;
            if (focusStack.Count > 0)
            {
                restoreTarget = focusStack.Peek();
            }
            else if (LastSelected != null && LastSelected.activeInHierarchy)
            {
                restoreTarget = LastSelected;
            }

            if (restoreTarget != null)
            {
                eventSystem?.SetSelectedGameObject(restoreTarget);
                CurrentSelected = restoreTarget;
            }
        }

        /// <summary>
        /// 手动设置当前选中的 GameObject（直接替换栈顶）
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
            if (inputActions == null) return;

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
                // 在 Gameplay 模式：检测进入 UI 模式的切换键
                if (Keyboard.current != null)
                {
                    var toggleKey = GetKeyFromName(CurrentBinding.toggleToUI);
                    if (toggleKey != Key.None && Keyboard.current[toggleKey].wasPressedThisFrame)
                    {
                        EnableUIControl();
                        return;
                    }
                }
            }
            else // UIControlMode.UIControl
            {
                // 在 UI 模式：检测退出 UI 模式的切换键
                if (Keyboard.current != null)
                {
                    var exitKey = GetKeyFromName(CurrentBinding.toggleToGameplay);
                    if (exitKey != Key.None && Keyboard.current[exitKey].wasPressedThisFrame)
                    {
                        // 标记 cancel 已被本帧处理，避免 OnCancel 事件重复触发
                        cancelHandledThisFrame = true;
                        DisableUIControl();
                        return;
                    }
                }
            }
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
                // 将当前焦点压栈（如果存在）
                if (CurrentSelected != null)
                {
                    focusStack.Push(CurrentSelected);
                }

                SetSelectedGameObject(firstSelected);
            }
        }

        /// <summary>
        /// UI 窗口关闭时 —— 恢复上一个焦点
        /// </summary>
        private void OnUIClosed(UIController controller)
        {
            // 延迟一帧恢复焦点，确保关闭的 UI 已完全移除
            // 使用 Unity 的延迟调用
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
            // 如果当前帧已被切换键消耗，不再触发 OnCancel 事件
            if (cancelHandledThisFrame) return;
            CancelSubject.OnNext(Unit.Default);
        }

        #endregion
    }
}
