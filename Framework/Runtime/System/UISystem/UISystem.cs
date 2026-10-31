using Gear.Common;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Framework.Runtime.UI
{
    /// <summary>
    /// UI系统 —— UI 生命周期事件使用 R3 Subject 替代 static event
    /// </summary>
    [ModuleOrder(90, typeof(Gear.Framework.Runtime.Resources.ResourcesSystem), typeof(Gear.Framework.Runtime.Resources.AddressableSystem), typeof(Gear.Framework.Runtime.Scene.SceneSystem))]
    public partial class UISystem : ICustomSystem, ISystemDisposable, ICustomSystemCancellableInit
    {
        #region 单例与初始化
        private static readonly Lazy<UISystem> instance = new(() => new UISystem());
        public static UISystem Instance => instance.Value;

        public bool isInited { get; private set; }

        /// <summary>初始化代次：Clear/重新 Init 会递增，用于让在途 Init 的跨帧续体失效</summary>
        private int initGeneration;

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        // UI加载状态缓存（用于跟踪每个UI窗口的加载状态）
        private Dictionary<string, UILoadState> uiStateCache = new Dictionary<string, UILoadState>(32);

        // 同名窗口并发加载去重：SingleFlightLoader 统一管理单飞/取消/摘除语义，
        // 避免双实例化 + uiStateCache.Add 键冲突抛异常导致孤儿窗口
        private readonly SingleFlightLoader<UILoadState> windowLoadGate = new SingleFlightLoader<UILoadState>();

        // 加载期间收到的"关闭"意图：窗口尚未登记进 uiStateCache 时 CloseWindow 找不到它，
        // 先记在这里，加载完成后立即关闭并按失败返回，避免"点了取消反而弹出窗口"。
        private readonly HashSet<string> pendingCloseRequests = new HashSet<string>();

        // CloseAllWindows 批量关闭的复用缓冲（避免每次批量操作 new List 分配）
        private readonly List<string> closeBuffer = new List<string>();

        // SetWindowPriority 排序复用缓冲（按需扩容，避免每次调用 new 数组）
        private UIController[] prioritySortBuffer;

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

        #region 窗口对象池（高频弹窗/提示复用，消除反复 Instantiate/Destroy 的 GC 与 Canvas 重建）
        /// <summary>是否启用窗口对象池（关闭时放池、打开时优先取池）</summary>
        public bool enableWindowPool = true;
        /// <summary>可池化窗口白名单（仅提示/飘字/确认框类轻量窗口；重窗口保持销毁语义）</summary>
        /// <summary>每个窗口名最多缓存的空闲对象数（防无限累积）</summary>
        public int maxPooledWindowsPerName = 3;
        private readonly Dictionary<string, Stack<GameObject>> windowPool = new Dictionary<string, Stack<GameObject>>();
        #endregion
        public UniTask Init() => InitCore(default);

        async UniTask ICustomSystemCancellableInit.Init(CancellationToken ct) => await InitCore(ct);

        private async UniTask InitCore(CancellationToken ct)
        {
            initProgress = 0;
            isInited = false;
            // 初始化代次：CreateRoot 含两次跨帧 await，期间引擎可能超时/Dispose 并调用 Clear()。
            // 续体必须据此放弃，否则会在 Clear 之后重新创建 UIRoot/EventSystem 并置 isInited=true。
            int myGeneration = ++initGeneration;

            // 重建可能已被 Clear() 释放的 R3 Subject（支持模块重初始化）
            OnInitSubject ??= new Subject<UIController>();
            OnOpenSubject ??= new Subject<UIController>();
            OnSetSubject ??= new Subject<UIController>();
            OnCloseSubject ??= new Subject<UIController>();

            if (!await CreateRoot(myGeneration, ct)) return;
            if (myGeneration != initGeneration) return;

            // 预热窗口目录：扫一次程序集 + 特性（约几毫秒），之后按 Id/类型查询零反射，
            // 避免"第一次打开某个窗口"时才扫描造成的毛刺。
            UIWindowCatalog.Warmup();

            initProgress = 100;
            isInited = true;
            Log.Debug("UISystem 初始化完成");
        }

        public void Clear()
        {
            Log.Debug("UISystem 清除数据");
            isInited = false;
            // 使在途 Init 的续体失效
            initGeneration++;
            uiStateCache.Clear();
            uiControllerTypeCache.Clear();
            // 取消在途的窗口加载（其完成回调会登记到已清空的缓存中）
            windowLoadGate.CancelAll();
            pendingCloseRequests.Clear();

            // 先让路由层收尾：取消所有等待中的结果（返回 None）、清空导航栈/模态、断开订阅。
            // 若放在 Subject Dispose 之后，调用方的 await 将永远不完成，且旧栈会残留到下次会话。
            UIRouter.NotifySystemClearing();
            // 释放 R3 Subject（自动断开所有订阅）并置 null 以支持重初始化
            OnInitSubject?.Dispose();
            OnInitSubject = null;
            OnOpenSubject?.Dispose();
            OnOpenSubject = null;
            OnSetSubject?.Dispose();
            OnSetSubject = null;
            OnCloseSubject?.Dispose();
            OnCloseSubject = null;

            // 窗口池内对象挂 uiRoot 下，随 DestroyRoot 一起销毁；这里仅清空字典防止 fake-null 残留
            windowPool.Clear();

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
        /// 创建根节点
        /// </summary>
        /// <returns>false 表示初始化期间系统已被 Clear/重启，调用方不应再置 isInited</returns>
        private async UniTask<bool> CreateRoot(int generation, CancellationToken ct)
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
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            if (generation != initGeneration) return false;

            // 销毁已有的 EventSystem，从预制体加载干净的实例
            var existingES = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existingES != null)
            {
                UnityEngine.Object.DestroyImmediate(existingES.gameObject);
            }

            var esPrefab = Modules.Get<ResourcesSystem>().Load<GameObject>("Prefabs/EventSystem/EventSystem");
            if (esPrefab != null)
            {
                GameObject esGo;
                try
                {
                    esGo = UnityEngine.Object.Instantiate(esPrefab);
                }
                finally
                {
                    Modules.Get<ResourcesSystem>().DeleteAssetCache("Prefabs/EventSystem/EventSystem");
                }
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
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    if (generation != initGeneration)
                    {
                        // 期间已被 Clear：销毁刚实例化的 EventSystem，避免孤儿根节点
                        if (esGo != null) UnityEngine.Object.Destroy(esGo);
                        EventSystem = null;
                        return false;
                    }

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
            return true;
        }

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
