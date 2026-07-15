using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ReunionMovement.Core.UI
{
    /// <summary>
    /// UI系统 —— UI 生命周期事件使用 R3 Subject 替代 static event
    /// </summary>
    public class UISystem : ICustomSystem
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

        #region R3 响应式事件（推荐新代码使用）

        /// <summary>UI 初始化完成事件</summary>
        public readonly Subject<UIController> OnInitSubject = new Subject<UIController>();

        /// <summary>UI 打开事件</summary>
        public readonly Subject<UIController> OnOpenSubject = new Subject<UIController>();

        /// <summary>UI 设置事件</summary>
        public readonly Subject<UIController> OnSetSubject = new Subject<UIController>();

        /// <summary>UI 关闭事件</summary>
        public readonly Subject<UIController> OnCloseSubject = new Subject<UIController>();

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

            await CreateRoot();

            initProgress = 100;
            isInited = true;
            Log.Debug("UISystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("UISystem 清除数据");
            isInited = false;
            uiStateCache.Clear();
            uiControllerTypeCache.Clear();
            // 释放 R3 Subject（自动断开所有订阅）
            OnInitSubject?.Dispose();
            OnOpenSubject?.Dispose();
            OnSetSubject?.Dispose();
            OnCloseSubject?.Dispose();
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
        private UniTask CreateRoot()
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
            var inputModule = EventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            // 通过 ResourcesSystem 加载 InputSystem_Actions 资产（需放在 Resources 文件夹下）
            var inputActions = ResourcesSystem.Instance.Load<UnityEngine.InputSystem.InputActionAsset>("InputSystem_Actions");
            if (inputActions != null)
            {
                inputModule.actionsAsset = inputActions;
            }

            GameObject.DontDestroyOnLoad(EventSystem);

            initProgress = 50;

            return UniTask.CompletedTask;
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
                Log.Error($"UI对象 {uiObj.name} 缺少 UIWindowAsset 组件！");
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

            GameObject uiObj = ResourcesSystem.Instance.InstantiateAsset<GameObject>(Config.UIPath + name);
            Log.Debug($"[UISystem] LoadWindow({name}) Instantiate → {(uiObj ? uiObj.name : "NULL")}, activeSelf={uiObj?.activeSelf}");
            if (uiObj == null)
            {
                return null;
            }

            uiObj.name = name;
            InitUIAsset(uiObj);
            uiObj.transform.localRotation = Quaternion.identity;
            uiObj.transform.localScale = Vector3.one;

            var uiController = uiObj.GetComponent<UIController>() ?? CreateUIController(uiObj, name);
            if (uiController == null)
            {
                Log.Error($"加载 UI {name} 失败，找不到或无法创建 UIController 脚本！");
                UnityEngine.Object.Destroy(uiObj);
                return null;
            }
            Log.Debug($"[UISystem] LoadWindow({name}) uiController={uiController.GetType().Name}");

            var uiLoadState = new UILoadState(name)
            {
                uiWindow = uiController,
                isLoading = false,
                openWhenFinish = openWhenFinish,
                openArgs = args,
                isOnInit = true
            };
            uiLoadState.uiWindow.uiName = name;
            uiStateCache.Add(name, uiLoadState);

            InitWindow(uiLoadState, uiLoadState.uiWindow, uiLoadState.openWhenFinish, uiLoadState.openArgs);
            Log.Debug($"[UISystem] LoadWindow({name}) 完成, activeSelf={uiController.gameObject.activeSelf}");

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

            Log.Debug($"OnInit UI {uiBase.gameObject.name}");

            OnInitSubject.OnNext(uiBase);

            if (open)
            {
                OnOpen(uiState, args);
                // 防御：OnOpen 可能因 BeforeOpen 被子类覆盖而延迟激活，此处兜底
                if (!uiBase.gameObject.activeSelf)
                {
                    Log.Warning($"UI {uiBase.gameObject.name} OnOpen 后仍未激活，强制激活");
                    uiBase.gameObject.SetActive(true);
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
            if (uiState.isLoading)
            {
                uiState.openWhenFinish = true;
                uiState.openArgs = args;
                return;
            }

            UIController uiBase = uiState.uiWindow;

            Log.Debug($"[UISystem] OnOpen({uiBase.gameObject.name}) activeSelf before={uiBase.gameObject.activeSelf}");

            if (uiBase.gameObject.activeSelf)
            {
                uiBase.OnClose();
                OnCloseSubject.OnNext(uiBase);
            }

            // 在 BeforeOpen 之前激活，确保 UI 可见（不论子类 BeforeOpen 行为如何）
            uiBase.gameObject.SetActive(true);
            Log.Debug($"[UISystem] OnOpen({uiBase.gameObject.name}) SetActive(true) → activeSelf={uiBase.gameObject.activeSelf}");

            uiBase.BeforeOpen(args, () =>
            {
                LogElapsedTime(() =>
                {
                    uiBase.OnOpen(args);
                }, $"OnOpen UI {uiBase.gameObject.name}");

                OnOpenSubject.OnNext(uiBase);
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
            //TODO: 需要先创建脚本对象，再根据脚本中的值进行加载资源
            UILoadState uiState;
            if (!uiStateCache.TryGetValue(uiName, out uiState))
            {
                uiState = LoadWindow(uiName, true, args);
                Log.Debug($"[UISystem] OpenWindow({uiName}) LoadWindow → {(uiState != null ? "OK" : "NULL")}");
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

                    OnSetSubject.OnNext(uiBase);
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

            OnCloseSubject.OnNext(uiState.uiWindow);

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
                Log.Warning($"{uiName} 已被销毁");
                return;
            }

            UnityEngine.Object.Destroy(uiState.uiWindow.gameObject);

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
            if (!uiControllerTypeCache.TryGetValue(uiTemplateName, out Type type))
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
                Log.Error($"CreateUIController: 未能找到UI脚本组件 ReunionMovement.Core.UI.{uiTemplateName}！");
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

            Log.Debug($"{message}, cost {elapsed}");
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

            isLoading = false;
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
}