using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// UI系统
    /// </summary>
    public class UISystem : ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<UISystem> instance = new(() => new UISystem());
        public static UISystem Instance => instance.Value;
        public bool IsInited { get; private set; }
        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        // UI窗口对象池（用于复用UI窗口，减少资源消耗）
        private UIWindowPool windowPool = new UIWindowPool();
        // UI加载状态缓存（用于跟踪每个UI窗口的加载状态）
        private Dictionary<string, UILoadState> uiStateCache = new Dictionary<string, UILoadState>();

        public static event Action<UIController> onInitEvent;
        public static event Action<UIController> onOpenEvent;
        public static event Action<UIController> onSetEvent;
        public static event Action<UIController> onCloseEvent;

        public EventSystem EventSystem;
        public GameObject uiRoot { get; private set; }
        public GameObject mainUIRoot { get; private set; }
        public GameObject normalUIRoot { get; private set; }
        public GameObject headInfoUIRoot { get; private set; }
        public GameObject tipsUIRoot { get; private set; }

        public async Task Init()
        {
            initProgress = 0;

            await CreateRoot();

            initProgress = 100;
            IsInited = true;
            Log.Debug("UISystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("UISystem 清除数据");
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
        private Task CreateRoot()
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

            EventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            EventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            GameObject.DontDestroyOnLoad(EventSystem);

            initProgress = 50;

            return Task.CompletedTask;
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
                Log.Error($"没有默认PanelType: {windowAsset.panelType}");
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

            GameObject uiObj = windowPool.Get(name) ?? ResourcesSystem.Instance.InstantiateAsset<GameObject>(Config.UIPath + name);
            if (uiObj == null)
            {
                return null;
            }

            InitUIAsset(uiObj);
            uiObj.SetActive(false);
            uiObj.name = name;
            uiObj.transform.localRotation = Quaternion.identity;
            uiObj.transform.localScale = Vector3.one;

            var uiController = uiObj.GetComponent<UIController>();
            var uiLoadState = new UILoadState(name)
            {
                uiWindow = uiController ?? CreateUIController(uiObj, name),
                isLoading = false,
                openWhenFinish = openWhenFinish,
                openArgs = args,
                isOnInit = true
            };
            uiLoadState.uiWindow.UIName = name;
            InitWindow(uiLoadState, uiLoadState.uiWindow, uiLoadState.openWhenFinish, uiLoadState.openArgs);

            uiStateCache.Add(name, uiLoadState);
            return uiLoadState;
        }

        /// <summary>
        /// 初始化UI
        /// </summary>
        /// <param name="uiState"></param>
        /// <param name="uiBase"></param>
        /// <param name="open"></param>
        /// <param name="args"></param>
        private void InitWindow(UILoadState uiState, UIController uiBase, bool open, params object[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            uiBase.OnInit();
            stopwatch.Stop();

            Log.Debug($"OnInit UI {uiBase.gameObject.name}, cost {stopwatch.ElapsedMilliseconds * 0.001f}");

            onInitEvent?.Invoke(uiBase);

            if (open)
            {
                OnOpen(uiState, args);
            }

            if (!open)
            {
                if (!uiState.isStaticUI)
                {
                    CloseWindow(uiBase.UIName); // Destroy
                    return;
                }
                else
                {
                    uiBase.gameObject.SetActive(false);
                }
            }

            uiState.OnUIWindowLoadedCallbacks(uiState);
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

            if (uiStateCache.TryGetValue(uiName, out uiState))
            {
                // 只加载，不打开
                uiState = LoadWindow(uiName, false);
                uiStateCache[uiName] = uiState;
            }

            uiState.DoCallback(callback, args);
        }

        /// <summary>
        /// 打开窗口
        /// </summary>
        /// <param name="uiState"></param>
        /// <param name="args"></param>
        private void OnOpen(UILoadState uiState, params object[] args)
        {
            if (uiState.isLoading)
            {
                uiState.openWhenFinish = true;
                uiState.openArgs = args;
                return;
            }

            UIController uiBase = uiState.uiWindow;

            if (uiBase.gameObject.activeSelf)
            {
                uiBase.OnClose();
                onCloseEvent?.Invoke(uiBase);
            }

            uiBase.BeforeOpen(args, () =>
            {
                uiBase.gameObject.SetActive(true);

                LogElapsedTime(() =>
                {
                    uiBase.OnOpen(args);
                }, $"OnOpen UI {uiBase.gameObject.name}");

                onOpenEvent?.Invoke(uiBase);
            });
        }

        /// <summary>
        /// 打开窗口（非复制）
        /// </summary>
        /// <param name="uiName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public UILoadState OpenWindow(string uiName, params object[] args)
        {
            //TOD需要先创建脚本对象，再根据脚本中的值进行加载资源
            UILoadState uiState;
            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                uiState = LoadWindow(uiName, true, args);
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
        /// 设置窗口
        /// </summary>
        /// <param name="uiState"></param>
        /// <param name="args"></param>
        private void OnSet(UILoadState uiState, params object[] args)
        {
            if (uiState.isLoading)
            {
                uiState.openWhenFinish = true;
                uiState.openArgs = args;
                return;
            }

            UIController uiBase = uiState.uiWindow;

            if (uiBase.gameObject.activeSelf)
            {
                uiBase.BeforeOpen(args, () =>
                {
                    uiBase.gameObject.SetActive(true);
                    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();
                    uiBase.OnSet(args);
                    stopwatch.Stop();

                    Log.Debug(string.Format("OnOpen UI {0}, cost {1}", uiBase.gameObject.name, stopwatch.Elapsed.TotalMilliseconds * 0.001f));

                    onSetEvent?.Invoke(uiBase);
                });
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="t"></param>
        public void CloseWindow(Type t)
        {
            CloseWindow(t.Name.Remove(0, 3)); // XUI remove
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
                Log.Error($"[CloseWindow]没有加载的UIWindow: {name}");
                return;
            }

            // Loading中
            if (uiState.isLoading)
            {
                Log.Error($"[CloseWindow]是加载中的{name}");
                uiState.openWhenFinish = false;
                return;
            }

            uiState.uiWindow.gameObject.SetActive(false);

            uiState.uiWindow.OnClose();

            onCloseEvent?.Invoke(uiState.uiWindow);

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
            List<string> LoadList = new List<string>();

            foreach (KeyValuePair<string, UILoadState> uiWindow in uiStateCache)
            {
                if (IsLoad(uiWindow.Key))
                {
                    LoadList.Add(uiWindow.Key);
                }
            }

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
                Log.Warning($"{uiName} 已被销毁");
                return;
            }

            // 回收到对象池而不是直接销毁
            windowPool.Return(uiName, uiState.uiWindow.gameObject);

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
        /// 是否已经打开
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsOpen(string name)
        {
            UIController uiBase = GetUIBase(name);
            return uiBase == null ? false : uiBase.gameObject.activeSelf;
        }

        /// <summary>
        /// 判断指定类型窗口是否已打开
        /// </summary>
        public bool IsOpen<T>() where T : UIController
        {
            string uiName = typeof(T).Name.Remove(0, 3);
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
        /// <returns></returns>
        public List<string> GetAllOpenWindowNames()
        {
            List<string> openNames = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key))
                    openNames.Add(kv.Key);
            }
            return openNames;
        }

        /// <summary>
        /// 获取所有已打开窗口的UIController实例
        /// </summary>
        /// <returns></returns>
        public List<UIController> GetAllOpenWindows()
        {
            List<UIController> openWindows = new List<UIController>();
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key) && kv.Value.uiWindow != null)
                    openWindows.Add(kv.Value.uiWindow);
            }
            return openWindows;
        }

        /// <summary>
        /// 根据名称模糊查找窗口
        /// </summary>
        /// <param name="partialName"></param>
        /// <returns></returns>
        public List<string> FindWindowsByName(string partialName)
        {
            var result = new List<string>();
            foreach (var key in uiStateCache.Keys)
            {
                if (key.Contains(partialName))
                    result.Add(key);
            }
            return result;
        }

        /// <summary>
        /// 获取指定类型的所有窗口名称
        /// </summary>
        /// <param name="panelType"></param>
        /// <returns></returns>
        public List<string> GetWindowNamesByPanelType(PanelType panelType)
        {
            var result = new List<string>();
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.panelType == panelType)
                    result.Add(kv.Key);
            }
            return result;
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
            string uiName = typeof(T).Name.Remove(0, 3); // 去掉"CUI"
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
        /// <param name="uiName"></param>
        /// <param name="priority"></param>
        public void SetWindowPriority(string uiName, int priority)
        {
            var ui = GetUIBase(uiName);
            if (ui != null)
            {
                ui.Priority = priority;
                // 按优先级排序
                var siblings = ui.transform.parent.Cast<Transform>().OrderBy(t => (t.GetComponent<UIController>()?.Priority) ?? 0).ToList();
                for (int i = 0; i < siblings.Count; i++)
                {
                    siblings[i].SetSiblingIndex(i);
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

        public virtual UIController CreateUIController(GameObject uiObj, string uiTemplateName)
        {
            UIController uiBase = uiObj.AddComponent(System.Type.GetType("ReunionMovement.Core.UI." + uiTemplateName + ", Assembly-CSharp")) as UIController;
            return uiBase;
        }

        /// <summary>
        /// 记录操作的耗时
        /// </summary>
        /// <param name="action"></param>
        /// <param name="message"></param>
        private void LogElapsedTime(Action action, string message)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            action();
            stopwatch.Stop();

            Log.Debug($"{message}, cost {stopwatch.ElapsedMilliseconds * 0.001f}");
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
            foreach (var kv in uiStateCache)
            {
                if (IsOpen(kv.Key) && !exceptSet.Contains(kv.Key))
                {
                    CloseWindow(kv.Key);
                }
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
            foreach (var kv in uiStateCache)
            {
                if (kv.Value.uiWindow != null && kv.Value.uiWindow.WindowAsset.GroupName == groupName)
                    CloseWindow(kv.Key);
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
        // 是否正在加载
        public bool isLoading;
        // 非复制出来的, 静态UI
        public bool isStaticUI;
        // 是否初始化
        public bool isOnInit = false;
        // 完成后是否打开
        public bool openWhenFinish;
        // 打开时的参数
        public object[] openArgs;
        // 回调
        internal Queue<Action<UIController, object[]>> callbacksWhenFinish;
        // 回调参数
        internal Queue<object[]> callbacksArgsWhenFinish;

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

            isLoading = true;
            openWhenFinish = false;
            openArgs = null;

            callbacksWhenFinish = new Queue<Action<UIController, object[]>>();
            callbacksArgsWhenFinish = new Queue<object[]>();
        }

        /// <summary>
        /// 确保加载完成后的回调
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="args"></param>
        public void DoCallback(Action<UIController, object[]> callback, object[] args = null)
        {
            if (args == null)
            {
                args = new object[0];
            }

            // Loading
            if (isLoading)
            {
                callbacksWhenFinish.Enqueue(callback);
                callbacksArgsWhenFinish.Enqueue(args);
                return;
            }

            // 立即执行即可
            callback(uiWindow, args);
        }

        /// <summary>
        /// 执行加载完成后的回调
        /// </summary>
        /// <param name="uiState"></param>
        internal void OnUIWindowLoadedCallbacks(UILoadState uiState)
        {
            while (uiState.callbacksWhenFinish.Count > 0)
            {
                Action<UIController, object[]> callback = uiState.callbacksWhenFinish.Dequeue();
                object[] args = uiState.callbacksArgsWhenFinish.Dequeue();
                DoCallback(callback, args);
            }
        }
    }

    /// <summary>
    /// UIWindowPool是一个简单的对象池，用于管理UI窗口的复用
    /// </summary>
    public class UIWindowPool
    {
        private readonly Dictionary<string, Stack<GameObject>> pool = new();

        /// <summary>
        /// 获取一个对象池中的对象，如果没有可用对象则返回null
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public GameObject Get(string name)
        {
            if (pool.TryGetValue(name, out var stack) && stack.Count > 0)
            {
                var obj = stack.Pop();
                obj.SetActive(true);
                return obj;
            }
            return null;
        }

        /// <summary>
        /// 将一个对象返回到对象池中，设置为非激活状态
        /// </summary>
        /// <param name="name"></param>
        /// <param name="obj"></param>
        public void Return(string name, GameObject obj)
        {
            if (!obj.activeSelf)
            {
                return; // 已经是非激活状态，避免重复回收
            }

            obj.SetActive(false);

            foreach (var comp in obj.GetComponents<MonoBehaviour>())
            {
                if (comp is UIController controller)
                {
                    controller.OnClose();
                }
            }

            if (!pool.ContainsKey(name))
            {
                pool[name] = new Stack<GameObject>();
            }
            pool[name].Push(obj);
        }

        /// <summary>
        /// 清空对象池中的所有对象
        /// </summary>
        public void Clear()
        {
            pool.Clear();
        }
    }
}