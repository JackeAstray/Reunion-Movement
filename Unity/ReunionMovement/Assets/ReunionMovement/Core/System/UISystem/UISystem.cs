using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// UI系统 —— UI 生命周期事件使用 R3 Subject 替代 static event
    /// </summary>
    public class UISystem : ICustomSystem, ISystemDisposable
    {
        #region 单例与初始化
        private static readonly Lazy<UISystem> instance = new(() => new UISystem());
        public static UISystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        // UI加载状态缓存（用于跟踪每个UI窗口的加载状态）
        private Dictionary<string, UILoadState> uiStateCache = new Dictionary<string, UILoadState>(32);

        // 同名窗口并发加载去重：加载期间先登记 TCS，后续调用方等待同一任务，
        // 避免双实例化 + uiStateCache.Add 键冲突抛异常导致孤儿窗口
        private readonly Dictionary<string, UniTaskCompletionSource<UILoadState>> loadingWindows =
            new Dictionary<string, UniTaskCompletionSource<UILoadState>>();

        #region R3 响应式事件（推荐新代码使用）

        /// <summary>UI 初始化完成事件</summary>
        public Subject<UIController> OnInitSubject { get; private set; } = new Subject<UIController>();

        /// <summary>UI 打开事件</summary>
        public Subject<UIController> OnOpenSubject { get; private set; } = new Subject<UIController>();

        /// <summary>UI 设置事件</summary>
        public Subject<UIController> OnSetSubject { get; private set; } = new Subject<UIController>();

        /// <summary>UI 关闭事件</summary>
        public Subject<UIController> OnCloseSubject { get; private set; } = new Subject<UIController>();

        #endregion

        public EventSystem EventSystem;
        public GameObject uiRoot { get; private set; }
        public GameObject mainUIRoot { get; private set; }
        public GameObject normalUIRoot { get; private set; }
        public GameObject headInfoUIRoot { get; private set; }
        public GameObject tipsUIRoot { get; private set; }

        public async UniTask Init()
        {
            initProgress = 0;

            // 重建可能已被 Clear() 释放的 R3 Subject（支持模块重初始化）
            OnInitSubject ??= new Subject<UIController>();
            OnOpenSubject ??= new Subject<UIController>();
            OnSetSubject ??= new Subject<UIController>();
            OnCloseSubject ??= new Subject<UIController>();

            await CreateRoot();

            initProgress = 100;
            isInited = true;
            Log.Debug("UISystem 初始化完成");
        }

        public void Clear()
        {
            Log.Debug("UISystem 清除数据");
            isInited = false;
            uiStateCache.Clear();
            uiControllerTypeCache.Clear();
            // 取消在途的窗口加载（其完成回调会登记到已清空的缓存中）
            foreach (var kvp in loadingWindows)
            {
                kvp.Value.TrySetCanceled();
            }
            loadingWindows.Clear();
            // 释放 R3 Subject（自动断开所有订阅）并置 null 以支持重初始化
            OnInitSubject?.Dispose();
            OnInitSubject = null;
            OnOpenSubject?.Dispose();
            OnOpenSubject = null;
            OnSetSubject?.Dispose();
            OnSetSubject = null;
            OnCloseSubject?.Dispose();
            OnCloseSubject = null;

            // 销毁 UI 根节点（与 SoundSystem/UIToolkitSystem 的 Clear 保持一致），
            // 避免引擎重初始化时残留一套孤儿 UI 与重复根节点（UIRoot/EventSystem 均为 DontDestroyOnLoad）
            DestroyRoot();
        }

        /// <summary>
        /// 销毁所有 UI 根节点与 EventSystem（均为 DontDestroyOnLoad 常驻对象，必须显式销毁）
        /// </summary>
        private void DestroyRoot()
        {
            if (uiRoot != null)
            {
                UnityEngine.Object.Destroy(uiRoot);
                uiRoot = null;
            }
            // 子根节点随 UIRoot 一起销毁，这里仅清空引用
            mainUIRoot = null;
            normalUIRoot = null;
            headInfoUIRoot = null;
            tipsUIRoot = null;

            if (EventSystem != null && EventSystem.gameObject != null)
            {
                UnityEngine.Object.Destroy(EventSystem.gameObject);
                EventSystem = null;
            }
        }

        /// <summary>
        /// 正在加载的UI统计
        /// </summary>
        private int loadingUICount = 0;

        public int LoadingUICount
        {
            get => loadingUICount;
            set => loadingUICount = value;
        }

        /// <summary>
        /// 创建根节点
        /// </summary>
        private async UniTask CreateRoot()
        {
            uiRoot = new GameObject("UIRoot");
            mainUIRoot = new GameObject("MainUIRoot");
            normalUIRoot = new GameObject("NormalUIRoot");
            headInfoUIRoot = new GameObject("HeadInfoUIRoot");
            tipsUIRoot = new GameObject("TipsUIRoot");
            mainUIRoot.transform.SetParent(uiRoot.transform, true);
            normalUIRoot.transform.SetParent(uiRoot.transform, true);
            headInfoUIRoot.transform.SetParent(uiRoot.transform, true);
            tipsUIRoot.transform.SetParent(uiRoot.transform, true);

            GameObject.DontDestroyOnLoad(uiRoot);

            // 延迟一帧创建 EventSystem：RuntimeInitializeLoadType.BeforeSceneLoad 阶段
            // Input System 包可能尚未初始化完毕，此时 Instantiate 预制体上的
            // InputSystemUIInputModule.OnEnable() 会静默失败。
            // 等一帧后 Input System 就绪，再创建即可正常工作。
            await UniTask.Yield(PlayerLoopTiming.Update);

            // 销毁已有的 EventSystem，从预制体加载干净的实例
            var existingES = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existingES != null)
            {
                UnityEngine.Object.DestroyImmediate(existingES.gameObject);
            }

            var esPrefab = ResourcesSystem.Instance.Load<GameObject>("Prefabs/EventSystem/EventSystem");
            if (esPrefab != null)
            {
                var esGo = UnityEngine.Object.Instantiate(esPrefab);
                esGo.name = "EventSystem";
                EventSystem = esGo.GetComponent<EventSystem>();
                GameObject.DontDestroyOnLoad(esGo);

                // ============================================================
                // 关键修复：InputSystemUIInputModule 在 Instantiate 时 OnEnable
                // 可能因 InputSystem 未完全就绪而静默失败。
                // 单纯 toggle enabled 不可靠（Unity 可能在同一帧内合并 enable 操作），
                // 这里采用"销毁旧组件 + AddComponent 新建"的方式，与 CreateUIPlane.cs
                // 中已验证可行的方案一致。
                // ============================================================
                var oldModule = esGo.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (oldModule != null)
                {
                    // 记录预制体上的配置值
                    var savedActionsAsset = oldModule.actionsAsset;
                    var savedDeselectOnBgClick = oldModule.deselectOnBackgroundClick;
                    var savedPointerBehavior = oldModule.pointerBehavior;
                    var savedMoveRepeatDelay = oldModule.moveRepeatDelay;
                    var savedMoveRepeatRate = oldModule.moveRepeatRate;
                    var savedScrollDeltaPerTick = oldModule.scrollDeltaPerTick;

                    // 销毁预制体上初始化失败的旧模块
                    UnityEngine.Object.DestroyImmediate(oldModule);

                    // 等待一帧，确保 DestroyImmediate 完全生效且 InputSystem 进一步就绪
                    await UniTask.Yield(PlayerLoopTiming.Update);

                    // 用 AddComponent 创建全新模块 —— 此时 OnEnable 会正常注册到 InputSystem
                    var newModule = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                    newModule.deselectOnBackgroundClick = savedDeselectOnBgClick;
                    newModule.pointerBehavior = savedPointerBehavior;
                    newModule.moveRepeatDelay = savedMoveRepeatDelay;
                    newModule.moveRepeatRate = savedMoveRepeatRate;
                    newModule.scrollDeltaPerTick = savedScrollDeltaPerTick;

                    // 还原 actionsAsset；若为空则从 Resources 加载独立副本
                    if (savedActionsAsset != null)
                    {
                        newModule.actionsAsset = UnityEngine.Object.Instantiate(savedActionsAsset);
                    }
                    else
                    {
                        // 多路径兜底加载 InputActionAsset
                        var inputActions = UnityEngine.Resources.Load<UnityEngine.InputSystem.InputActionAsset>("InputSystem_Actions");
                        if (inputActions == null)
                            inputActions = UnityEngine.Resources.Load<UnityEngine.InputSystem.InputActionAsset>("InputSystem_Actions.default");
                        if (inputActions != null)
                        {
                            newModule.actionsAsset = UnityEngine.Object.Instantiate(inputActions);
                        }
                        else
                        {
                            // 最终兜底：从 InputSystem 包内置资源尝试加载
                            #if UNITY_EDITOR
                            Log.Error("[UISystem] 严重：未找到 InputSystem_Actions.inputactions！" +
                                "UI 鼠标/触屏/手柄输入将完全失效。请确保 Input System 包的默认 InputActionAsset 存在于 Resources 文件夹中。" +
                                "可通过 Window → Analysis → Input Debugger 确认 InputSystem 状态。");
                            #else
                            Log.Error("[UISystem] 未找到 InputSystem_Actions.inputactions，UI 输入不可用。请检查打包资源。");
                            #endif
                        }
                    }

                    // 关键：Instantiate 克隆出的 InputActionAsset 所有 ActionMap 默认禁用！
                    // InputSystemUIInputModule 不会自动 Enable，必须手动启用，否则鼠标/触屏/键盘事件全部无法接收。
                    // 这就是"手动拖入场景可用，代码加载不可用"的最终根因。
                    if (newModule.actionsAsset != null)
                    {
                        newModule.actionsAsset.Enable();
                    }

                    newModule.enabled = true;
                    Log.Debug("[UISystem] EventSystem 预制体加载完成（已重建 InputSystemUIInputModule），enabled={0}, actionsAsset={1}",
                        newModule.enabled,
                        newModule.actionsAsset != null ? newModule.actionsAsset.name : "NULL");
                }
                else
                {
                    Log.Warning("[UISystem] EventSystem 预制体上未找到 InputSystemUIInputModule 组件，鼠标/触屏 UI 交互可能不可用");
                }
            }
            else
            {
                Log.Error("[UISystem] 未找到 EventSystem 预制体: Resources/Prefabs/EventSystem/EventSystem.prefab");
            }

            initProgress = 50;
        }


        #region UI操作
        /// <summary>
        /// 初始化UI
        /// </summary>
        /// <param name="uiObj"></param>
        private void InitUIAsset(GameObject uiObj)
        {
            if (!uiObj)
            {
                Log.Error("UI对象为空。");
                return;
            }
            var windowAsset = uiObj.GetComponent<UIWindowAsset>();
            if (windowAsset == null)
            {
                Log.Error("UI对象 {0} 缺少 UIWindowAsset 组件！", uiObj.name);
                return;
            }
            var parent = windowAsset.panelType switch
            {
                PanelType.MainUI => mainUIRoot.transform,
                PanelType.NormalUI => normalUIRoot.transform,
                PanelType.HeadInfoUI => headInfoUIRoot.transform,
                PanelType.TipsUI => tipsUIRoot.transform,
                _ => uiRoot.transform
            };
            uiObj.transform.SetParent(parent);
            if (parent == uiRoot.transform)
            {
                Log.Error("没有默认PanelType: {0}", windowAsset.panelType);
            }
        }

        /// <summary>
        /// 加载UI
        /// </summary>
        /// <param name="name"></param>
        /// <param name="openWhenFinish"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public UILoadState LoadWindow(string name, bool openWhenFinish, params object[] args)
        {
            if (uiStateCache.TryGetValue(name, out var existingState))
            {
                return existingState;
            }

            // 同步路径：仅走 Resources（Addressables 无法真正同步加载，WebGL 不支持 WaitForCompletion）。
            // 推荐使用 LoadWindowAsync 走 Addressables 双轨。
            GameObject uiObj = ResourcesSystem.Instance.InstantiateAsset<GameObject>(Config.UIPath + name);
            Log.Debug("[UISystem] LoadWindow({0}) Instantiate → {1}, activeSelf={2}", name, (uiObj ? uiObj.name : "NULL"), uiObj?.activeSelf);
            if (uiObj == null)
            {
                return null;
            }

            return SetupLoadedWindow(uiObj, name, openWhenFinish, args);
        }

        /// <summary>
        /// [推荐] 异步加载 UI —— Addressables 优先（AddressableKeys.UIRoot + name），
        /// 失败自动降级 Resources（Config.UIPath + name）。
        /// 实例化完成后立即释放源 Prefab 的 Addressables 引用，避免泄漏。
        /// </summary>
        public async UniTask<UILoadState> LoadWindowAsync(string name, bool openWhenFinish, params object[] args)
        {
            if (uiStateCache.TryGetValue(name, out var existingState))
            {
                return existingState;
            }

            // 并发去重：已有同名窗口在途加载，等待同一结果，避免双实例化与缓存键冲突。
            // Clear() 会 TrySetCanceled 在途加载任务：按“加载失败”语义返回 null，
            // 不向调用方抛 OperationCanceledException（SuppressCancellationThrow 返回 (IsCanceled, Result) 元组）。
            if (loadingWindows.TryGetValue(name, out var pending))
            {
                var (_, result) = await pending.Task.SuppressCancellationThrow();
                return result;
            }

            var loadTcs = new UniTaskCompletionSource<UILoadState>();
            loadingWindows[name] = loadTcs;
            try
            {
                GameObject prefab = null;
                bool fromAddressables = false;

                // 双轨：Addressables 优先
                if (Config.AddressablesMode != AddressablesMode.Off)
                {
                    prefab = await AddressableSystem.Instance.LoadAssetAsync<GameObject>(AddressableKeys.UIRoot + name);
                    fromAddressables = prefab != null;
                    if (prefab != null)
                    {
                        Log.Debug("[UISystem] LoadWindowAsync({0}) 从 Addressables 加载成功: {1}", name, AddressableKeys.UIRoot + name);
                    }
                }

                // Clear() 防护：await 期间系统可能被清理（UI 根节点已销毁、缓存已清空），
                // 继续 Instantiate + 登记会把孤儿窗口写进新缓存或触发空引用
                if (uiRoot == null)
                {
                    Log.Warning("[UISystem] LoadWindowAsync({0}) 中止：加载期间系统已清理", name);
                    loadTcs.TrySetResult(null);
                    return null;
                }

                // 降级：Resources
                if (prefab == null)
                {
                    prefab = ResourcesSystem.Instance.Load<GameObject>(Config.UIPath + name);
                    if (prefab != null)
                    {
                        Log.Debug("[UISystem] LoadWindowAsync({0}) 降级 Resources 加载成功: {1}", name, Config.UIPath + name);
                    }
                }

                if (prefab == null)
                {
                    Log.Error("[UISystem] LoadWindowAsync({0}) 加载失败（Addressables + Resources 均未命中）", name);
                    loadTcs.TrySetResult(null);
                    return null;
                }

                var uiObj = UnityEngine.Object.Instantiate(prefab);
                // 实例化后释放源 Prefab 的 Addressables 引用（Resources 路径无引用计数，无需释放）
                if (fromAddressables)
                {
                    AddressableSystem.Instance.ReleaseAsset(prefab);
                }

                var result = SetupLoadedWindow(uiObj, name, openWhenFinish, args);
                loadTcs.TrySetResult(result);
                return result;
            }
            catch (Exception ex)
            {
                loadTcs.TrySetException(ex);
                throw;
            }
            finally
            {
                // 仅移除自己的条目：Clear() 后可能有新的同名加载登记了新 TCS，
                // 无条件 Remove 会误删新条目导致其等待者永久挂起
                if (loadingWindows.TryGetValue(name, out var cur) && ReferenceEquals(cur, loadTcs))
                {
                    loadingWindows.Remove(name);
                }
            }
        }

        /// <summary>
        /// 对已实例化的 UI GameObject 完成初始化并登记（LoadWindow / LoadWindowAsync 共用）。
        /// </summary>
        private UILoadState SetupLoadedWindow(GameObject uiObj, string name, bool openWhenFinish, object[] args)
        {
            uiObj.name = name;
            InitUIAsset(uiObj);
            uiObj.transform.localRotation = Quaternion.identity;
            uiObj.transform.localScale = Vector3.one;

            var uiController = uiObj.GetComponent<UIController>() ?? CreateUIController(uiObj, name);
            if (uiController == null)
            {
                Log.Error("加载 UI {0} 失败，找不到或无法创建 UIController 脚本！", name);
                UnityEngine.Object.Destroy(uiObj);
                return null;
            }
            Log.Debug("[UISystem] LoadWindow({0}) uiController={1}", name, uiController.GetType().Name);

            var uiLoadState = new UILoadState(name)
            {
                uiWindow = uiController,
                openWhenFinish = openWhenFinish,
                openArgs = args,
                isOnInit = true
            };
            uiLoadState.uiWindow.uiName = name;
            uiStateCache.Add(name, uiLoadState);

            InitWindow(uiLoadState, uiLoadState.uiWindow, uiLoadState.openWhenFinish, uiLoadState.openArgs);
            Log.Debug("[UISystem] LoadWindow({0}) 完成, activeSelf={1}", name, uiController.gameObject.activeSelf);

            return uiLoadState;
        }

        /// <summary>
        /// 初始化UI
        /// </summary>
        private void InitWindow(UILoadState uiState, UIController uiBase, bool open, params object[] args)
        {
            // 先关闭防止初始化过程中的闪烁，open=true 时 OnOpen 会重新激活
            uiBase.gameObject.SetActive(false);

            uiBase.OnInit();

            Log.Debug("OnInit UI {0}", uiBase.gameObject.name);

            // 订阅者异常隔离：坏订阅者不应中断 InitWindow 后续的开窗流程
            try
            {
                OnInitSubject.OnNext(uiBase);
            }
            catch (Exception ex)
            {
                Log.Error("OnInitSubject 订阅者异常（已隔离）: {0}", ex.Message);
            }

            if (open)
            {
                OnOpen(uiState, args);
                // 防御：OnOpen 可能因 BeforeOpen 被子类覆盖而延迟激活，此处兜底
                if (!uiBase.gameObject.activeSelf)
                {
                    Log.Warning("UI {0} OnOpen 后仍未激活，强制激活", uiBase.gameObject.name);
                    uiBase.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 和UI通讯
        /// 等待并获取UI实例，执行callback
        /// 源起Loadindg UI， 在加载过程中，进度条设置方法会失效
        /// 如果是DynamicWindow,，使用前务必先要Open!
        /// </summary>
        /// <param name="uiName"></param>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void CallUI(string uiName, Action<UIController, object[]> callback, params object[] args)
        {
            UILoadState uiState;

            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                // 只加载，不打开
                uiState = LoadWindow(uiName, false);
                if (uiState != null)
                {
                    uiStateCache[uiName] = uiState;
                }
            }

            uiState?.DoCallback(callback, args);
        }

        /// <summary>
        /// 打开窗口
        /// </summary>
        /// <param name="uiState"></param>
        /// <param name="args"></param>
        private void OnOpen(UILoadState uiState, params object[] args)
        {
            // LoadWindow 为同步加载，不存在“加载中”状态，无需 isLoading 分支
            UIController uiBase = uiState.uiWindow;

            Log.Debug("[UISystem] OnOpen({0}) activeSelf before={1}", uiBase.gameObject.name, uiBase.gameObject.activeSelf);

            if (uiBase.gameObject.activeSelf)
            {
                // 窗口已激活：重复打开不应先广播"关闭"事件（语义矛盾，
                // 会导致 UIInputSystem.OnUIClosed 误触发 RestorePreviousFocus 弹出焦点）。
                // 直接走 OnOpen 刷新数据即可。
                uiBase.BeforeOpen(args, () =>
                {
                    LogElapsedTime(() =>
                    {
                        uiBase.OnOpen(args);
                    }, $"OnOpen UI {uiBase.gameObject.name}");

                    // 订阅者异常隔离：坏订阅者不应中断重复打开流程
                    try
                    {
                        OnOpenSubject.OnNext(uiBase);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("OnOpenSubject 订阅者异常（已隔离）: {0}", ex.Message);
                    }
                });
                return;
            }

            // 在 BeforeOpen 之前激活，确保 UI 可见（不论子类 BeforeOpen 行为如何）
            uiBase.gameObject.SetActive(true);
            Log.Debug("[UISystem] OnOpen({0}) SetActive(true) → activeSelf={1}", uiBase.gameObject.name, uiBase.gameObject.activeSelf);

            uiBase.BeforeOpen(args, () =>
            {
                LogElapsedTime(() =>
                {
                    uiBase.OnOpen(args);
                }, $"OnOpen UI {uiBase.gameObject.name}");

                // 订阅者异常隔离：坏订阅者不应中断开窗流程
                try
                {
                    OnOpenSubject.OnNext(uiBase);
                }
                catch (Exception ex)
                {
                    Log.Error("OnOpenSubject 订阅者异常（已隔离）: {0}", ex.Message);
                }
            });
        }

        /// <summary>
        /// 打开窗口（非复制）
        /// <summary>
        /// 打开窗口（字符串键）—— 保持现有 API 兼容
        /// </summary>
        public UILoadState OpenWindow(string uiName, params object[] args)
        {
            //TODO: 需要先创建脚本对象，再根据脚本中的值进行加载资源
            UILoadState uiState;

            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                uiState = LoadWindow(uiName, true, args);
                Log.Debug("[UISystem] OpenWindow({0}) LoadWindow → {1}", uiName, (uiState != null ? "OK" : "NULL"));
                return uiState;
            }

            if (!uiState.isOnInit)
            {
                uiState.isOnInit = true;
                if (uiState.uiWindow != null)
                {
                    uiState.uiWindow.OnInit();
                }
            }

            OnOpen(uiState, args);
            return uiState;
        }

        /// <summary>
        /// [推荐] 异步打开窗口 —— 走 LoadWindowAsync（Addressables 双轨加载，失败自动降级 Resources）。
        /// </summary>
        public async UniTask<UILoadState> OpenWindowAsync(string uiName, params object[] args)
        {
            UILoadState uiState;

            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                uiState = await LoadWindowAsync(uiName, true, args);
                Log.Debug("[UISystem] OpenWindowAsync({0}) LoadWindowAsync → {1}", uiName, (uiState != null ? "OK" : "NULL"));
                return uiState;
            }

            if (!uiState.isOnInit)
            {
                uiState.isOnInit = true;
                if (uiState.uiWindow != null)
                {
                    uiState.uiWindow.OnInit();
                }
            }

            OnOpen(uiState, args);
            return uiState;
        }

        /// <summary>
        /// [推荐] 类型安全的异步打开窗口 —— 通过 UIController 类型名自动推断 UI 名称。
        /// 使用方式：UISystem.Instance.OpenWindowAsync&lt;PopupUIPlane&gt;(args);
        /// </summary>
        public async UniTask<UILoadState> OpenWindowAsync<T>(params object[] args) where T : UIController
        {
            return await OpenWindowAsync(typeof(T).Name, args);
        }

        /// <summary>
        /// 设置窗口（非复制）
        /// </summary>
        /// <param name="uiName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public UILoadState SetWindow(string uiName, params object[] args)
        {
            UILoadState uiState;
            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                uiState = LoadWindow(uiName, true, args);
                return uiState;
            }

            if (!uiState.isOnInit)
            {
                uiState.isOnInit = true;
                if (uiState.uiWindow != null) uiState.uiWindow.OnInit();
            }
            OnSet(uiState, args);
            return uiState;
        }

        /// <summary>
        /// [推荐] 类型安全的打开窗口重载 —— 通过 UIController 类型名自动推断 UI 名称。
        /// 使用方式：UISystem.Instance.OpenWindow&lt;PopupUIPlane&gt;(args);
        /// 优点：编译时检查、重构安全、无字符串拼写错误。
        /// </summary>
        /// <typeparam name="T">UI 控制器类型（类名需与 prefab 名一致）</typeparam>
        /// <param name="args">打开参数</param>
        public UILoadState OpenWindow<T>(params object[] args) where T : UIController
        {
            // 约定：类型名 = prefab 名（如 PopupUIPlane 对应 PopupUIPlane.prefab）
            string uiName = typeof(T).Name;
            return OpenWindow(uiName, args);
        }

        /// <summary>
        /// 设置窗口
        /// </summary>
        /// <param name="uiState"></param>
        /// <param name="args"></param>
        private void OnSet(UILoadState uiState, params object[] args)
        {
            // LoadWindow 为同步加载，不存在“加载中”状态，无需 isLoading 分支
            UIController uiBase = uiState.uiWindow;

            if (uiBase != null && uiBase.gameObject.activeSelf)
            {
                // 在 BeforeOpen 之前激活，确保 UI 可见
                uiBase.gameObject.SetActive(true);

                uiBase.BeforeOpen(args, () =>
                {
                    float setStartTime = Time.realtimeSinceStartup;
                    uiBase.OnSet(args);
                    float setElapsed = Time.realtimeSinceStartup - setStartTime;

                    Log.Debug(string.Format("OnSet UI {0}, cost {1}", uiBase.gameObject.name, setElapsed));

                    // 订阅者异常隔离：坏订阅者不应中断 SetWindow 流程
                    try
                    {
                        OnSetSubject.OnNext(uiBase);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("OnSetSubject 订阅者异常（已隔离）: {0}", ex.Message);
                    }
                });
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="t"></param>
        public void CloseWindow(Type t)
        {
            CloseWindow(t.Name);
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void CloseWindow<T>()
        {
            CloseWindow(typeof(T));
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="name"></param>
        public void CloseWindow(string name)
        {
            UILoadState uiState;

            // 未开始Load
            if (!uiStateCache.TryGetValue(name, out uiState))
            {
                Log.Error("[CloseWindow]没有加载的UIWindow: {0}", name);
                return;
            }

            // LoadWindow 为同步加载，不存在“加载中”状态，无需 isLoading 分支
            uiState.uiWindow.gameObject.SetActive(false);

            uiState.uiWindow.OnClose();

            // 订阅者异常隔离：坏订阅者不应中断 CloseWindow 后续的窗口销毁
            try
            {
                OnCloseSubject.OnNext(uiState.uiWindow);
            }
            catch (Exception ex)
            {
                Log.Error("OnCloseSubject 订阅者异常（已隔离）: {0}", ex.Message);
            }

            if (!uiState.isStaticUI)
            {
                DestroyWindow(name);
            }
        }

        /// <summary>
        /// 销毁所有具有LoadState的窗口。请小心使用。
        /// </summary>
        public void DestroyAllWindows()
        {
            CloseAllWindows();

            List<string> LoadList = new List<string>(uiStateCache.Keys);
            foreach (string item in LoadList)
            {
                DestroyWindow(item);
            }
        }

        /// <summary>
        /// 关闭全部窗口
        /// </summary>
        public void CloseAllWindows()
        {
            List<string> toCloses = new List<string>();

            foreach (KeyValuePair<string, UILoadState> uiWindow in uiStateCache)
            {
                if (IsOpen(uiWindow.Key))
                {
                    toCloses.Add(uiWindow.Key);
                }
            }

            for (int i = toCloses.Count - 1; i >= 0; i--)
            {
                CloseWindow(toCloses[i]);
            }
        }

        /// <summary>
        /// 销毁窗口
        /// </summary>
        /// <param name="uiName"></param>
        /// <param name="destroyImmediate"></param>
        public void DestroyWindow(string uiName)
        {
            UILoadState uiState;
            uiStateCache.TryGetValue(uiName, out uiState);
            if (uiState == null || uiState.uiWindow == null)
            {
                Log.Warning("{0} 已被销毁", uiName);
                return;
            }

            // 使用 DestroyImmediate 立即销毁：Destroy 延迟到帧末，若同帧内重新打开同名窗口，
            // LoadWindow 会实例化新对象而旧对象尚未销毁 → 短暂双实例。
            // UI 窗口关闭时本应停止自身协程/动画，立即销毁是安全且符合预期的。
            UnityEngine.Object.DestroyImmediate(uiState.uiWindow.gameObject);

            uiState.uiWindow = null;
            uiStateCache.Remove(uiName);
        }
        #endregion

        #region 公共方法 判断
        /// <summary>
        /// 是否被加载了
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsLoad(string name)
        {
            if (uiStateCache.ContainsKey(name))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 是否已打开
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsOpen(string name)
        {
            UIController uiBase = GetUIBase(name);
            return uiBase == null ? false : uiBase.gameObject.activeSelf;
        }

        /// <summary>
        /// 是否存在任意已打开的窗口（零分配，供高频输入路径使用）
        /// </summary>
        public bool HasAnyOpenWindow()
        {
            foreach (var kv in uiStateCache)
            {
                var uiBase = kv.Value?.uiWindow;
                if (uiBase != null && uiBase.gameObject.activeSelf)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断指定类型窗口是否已打开
        /// </summary>
        public bool IsOpen<T>() where T : UIController
        {
            string uiName = typeof(T).Name;
            return IsOpen(uiName);
        }

        /// <summary>
        /// 判断窗口是否存在且可见
        /// </summary>
        /// <param name="uiName"></param>
        /// <returns></returns>
        public bool IsWindowVisible(string uiName)
        {
            var uiBase = GetUIBase(uiName);
            return uiBase != null && uiBase.IsVisiable;
        }
        #endregion

        #region 公共方法 Get
        /// <summary>
        /// 获取所有已打开窗口的名称
        /// </summary>
        public List<string> GetAllOpenWindowNames()
        {
            List<string> openNames = new List<string>();
            GetAllOpenWindowNames(openNames);
            return openNames;
        }

        /// <summary>
        /// 获取所有已打开窗口的名称（使用外部 List 避免分配）
        /// </summary>
        public void GetAllOpenWindowNames(List<string> result)
        {
            result.Clear();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key))
                    result.Add(kv.Key);
            }
        }

        /// <summary>
        /// 获取所有已打开窗口的UIController实例
        /// </summary>
        public List<UIController> GetAllOpenWindows()
        {
            List<UIController> openWindows = new List<UIController>();
            GetAllOpenWindows(openWindows);
            return openWindows;
        }

        /// <summary>
        /// 获取所有已打开窗口的UIController实例（使用外部 List 避免分配）
        /// </summary>
        public void GetAllOpenWindows(List<UIController> result)
        {
            result.Clear();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key) && kv.Value.uiWindow != null)
                    result.Add(kv.Value.uiWindow);
            }
        }

        /// <summary>
        /// 根据名称模糊查找窗口
        /// </summary>
        public List<string> FindWindowsByName(string partialName)
        {
            var result = new List<string>();
            FindWindowsByName(partialName, result);
            return result;
        }

        /// <summary>
        /// 根据名称模糊查找窗口（使用外部 List 避免分配）
        /// </summary>
        public void FindWindowsByName(string partialName, List<string> result)
        {
            result.Clear();
            foreach (var key in uiStateCache.Keys)
            {
                if (key.Contains(partialName))
                    result.Add(key);
            }
        }

        /// <summary>
        /// 获取指定类型的所有窗口名称
        /// </summary>
        public List<string> GetWindowNamesByPanelType(PanelType panelType)
        {
            var result = new List<string>();
            GetWindowNamesByPanelType(panelType, result);
            return result;
        }

        /// <summary>
        /// 获取指定类型的所有窗口名称（使用外部 List 避免分配）
        /// </summary>
        public void GetWindowNamesByPanelType(PanelType panelType, List<string> result)
        {
            result.Clear();
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.panelType == panelType)
                    result.Add(kv.Key);
            }
        }

        /// <summary>
        /// 获取UI控制器
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>

        private UIController GetUIBase(string name)
        {
            return uiStateCache.TryGetValue(name, out var uiState) ? uiState.uiWindow : null;
        }
        #endregion

        #region 公共方法 Set
        /// <summary>
        /// 切换 - 打开的隐藏，隐藏的打开
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="args"></param>
        public void ToggleWindow<T>(params object[] args)
        {
            string uiName = typeof(T).Name;
            ToggleWindow(uiName, args);
        }

        /// <summary>
        /// 切换 - 打开的隐藏，隐藏的打开
        /// </summary>
        /// <param name="name"></param>
        /// <param name="args"></param>
        public void ToggleWindow(string name, params object[] args)
        {
            if (IsOpen(name))
            {
                CloseWindow(name);
            }
            else
            {
                OpenWindow(name, args);
            }
        }

        /// <summary>
        /// 根据UI名称设置窗口优先级，并重新排序
        /// </summary>
        public void SetWindowPriority(string uiName, int priority)
        {
            var ui = GetUIBase(uiName);
            if (ui != null && ui.transform.parent != null)
            {
                ui.priority = priority;
                // 获取同级所有 UIController 并按优先级排序（避免 LINQ 分配）
                var parent = ui.transform.parent;
                int childCount = parent.childCount;
                var controllers = new UIController[childCount];
                int count = 0;
                for (int i = 0; i < childCount; i++)
                {
                    var ctrl = parent.GetChild(i).GetComponent<UIController>();
                    if (ctrl != null)
                    {
                        controllers[count++] = ctrl;
                    }
                }
                // 冒泡排序（子节点数通常很小）
                for (int i = 0; i < count - 1; i++)
                {
                    for (int j = 0; j < count - 1 - i; j++)
                    {
                        if (controllers[j].priority > controllers[j + 1].priority)
                        {
                            var temp = controllers[j];
                            controllers[j] = controllers[j + 1];
                            controllers[j + 1] = temp;
                        }
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    controllers[i].transform.SetSiblingIndex(i);
                }
            }
        }

        #endregion

        #region 公共方法 工具

        /// <summary>
        /// 给打开的UI添加脚本（脚本从程序集查找）
        /// </summary>
        /// <param name="uiObj"></param>
        /// <param name="uiTemplateName"></param>
        /// <returns></returns>

        // 类型缓存：避免每次打开 UI 都执行字符串拼接 + 反射
        private static readonly Dictionary<string, Type> uiControllerTypeCache = new Dictionary<string, Type>();

        public virtual UIController CreateUIController(GameObject uiObj, string uiTemplateName)
        {
            // 优先使用源码生成器生成的注册表（编译期扫描，零运行时反射）
            if (UIControllerRegistry.TryGet(uiTemplateName, out Type type))
            {
                return uiObj.AddComponent(type) as UIController;
            }

            // 后备：反射查找（覆盖生成器未覆盖的动态/外部类型）
            if (!uiControllerTypeCache.TryGetValue(uiTemplateName, out type))
            {
                // 在所有已加载程序集中查找类型（兼容多程序集项目）
                string fullName = "ReunionMovement.Core.UI." + uiTemplateName;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(fullName);
                    if (type != null) break;
                }
                uiControllerTypeCache[uiTemplateName] = type;
            }
            if (type == null)
            {
                Log.Error("CreateUIController: 未能找到UI脚本组件 ReunionMovement.Core.UI.{0}！", uiTemplateName);
                return null;
            }
            UIController uiBase = uiObj.AddComponent(type) as UIController;
            return uiBase;
        }

        /// <summary>
        /// 记录操作的耗时
        /// </summary>
        private void LogElapsedTime(Action action, string message)
        {
            float startTime = Time.realtimeSinceStartup;
            action();
            float elapsed = Time.realtimeSinceStartup - startTime;

            Log.Debug("{0}, cost {1}", message, elapsed);
        }

        /// <summary>
        /// 关闭指定类型的所有窗口
        /// </summary>
        /// <param name="panelType"></param>
        public void CloseAllWindowsByPanelType(PanelType panelType)
        {
            var toClose = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.panelType == panelType && IsOpen(kv.Key))
                    toClose.Add(kv.Key);
            }
            foreach (var name in toClose)
            {
                CloseWindow(name);
            }
        }

        /// <summary>
        /// 关闭除指定窗口外的所有窗口
        /// </summary>
        public void CloseAllExcept(params string[] exceptNames)
        {
            HashSet<string> exceptSet = new HashSet<string>(exceptNames);
            // 防御性拷贝：CloseWindow 会修改 uiStateCache，先收集键再遍历
            var keysToClose = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key) && !exceptSet.Contains(kv.Key))
                {
                    keysToClose.Add(kv.Key);
                }
            }
            foreach (var key in keysToClose)
            {
                CloseWindow(key);
            }
        }

        /// <summary>
        /// 将指定窗口置于同层级最前
        /// </summary>
        /// <param name="uiName"></param>
        public void BringToFront(string uiName)
        {
            var uiBase = GetUIBase(uiName);
            if (uiBase != null)
            {
                uiBase.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 隐藏所有窗口（可选按类型）
        /// </summary>
        /// <param name="panelType"></param>
        public void HideAllWindows(PanelType? panelType = null)
        {
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && (panelType == null || kv.Value.uiWindow.WindowAsset.panelType == panelType))
                {
                    kv.Value.uiWindow.IsVisiable = false;
                }
            }
        }

        /// <summary>
        /// 显示所有窗口（可选按类型）
        /// </summary>
        /// <param name="panelType"></param>
        public void ShowAllWindows(PanelType? panelType = null)
        {
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && (panelType == null || kv.Value.uiWindow.WindowAsset.panelType == panelType))
                {
                    kv.Value.uiWindow.IsVisiable = true;
                }
            }
        }

        /// <summary>
        /// 关闭指定组的所有窗口
        /// </summary>
        /// <param name="groupName"></param>
        public void CloseGroup(string groupName)
        {
            var keysToClose = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.groupName == groupName)
                    keysToClose.Add(kv.Key);
            }
            foreach (var key in keysToClose)
            {
                CloseWindow(key);
            }
        }

        #endregion
    }

    /// <summary>
    /// UILoadState是UI加载状态的类，负责管理UI界面的加载状态和回调
    /// </summary>
    public class UILoadState
    {
        // ui名称
        public string uiName;
        // ui窗口
        public UIController uiWindow;
        // ui类型
        public Type uiType;
        // 非复制出来的, 静态UI
        public bool isStaticUI;
        // 是否初始化
        public bool isOnInit = false;
        // 完成后是否打开
        public bool openWhenFinish;
        // 打开时的参数
        public object[] openArgs;

        /// <summary>
        /// UILoadState构造函数
        /// </summary>
        /// <param name="uiName"></param>
        /// <param name="uiControllerType"></param>
        public UILoadState(string uiName, Type uiControllerType = default(Type))
        {
            if (uiControllerType == default(Type)) uiControllerType = typeof(UIController);

            this.uiName = uiName;
            uiWindow = null;
            uiType = uiControllerType;

            openWhenFinish = false;
            openArgs = null;
        }

        /// <summary>
        /// 执行回调。UI 加载为同步（LoadWindow 完成后窗口已就绪），回调立即执行。
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void DoCallback(Action<UIController, object[]> callback, object[] args = null)
        {
            if (args == null)
            {
                args = new object[0];
            }

            callback(uiWindow, args);
        }
    }
}