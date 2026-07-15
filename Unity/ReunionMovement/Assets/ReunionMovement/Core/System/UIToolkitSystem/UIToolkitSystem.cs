using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReunionMovement.Core.UIToolkit
{
    /// <summary>
    /// UI Toolkit 系统 —— 基于 UIDocument + UXML/USS 的声明式 UI 模块。
    /// 与现有 UISystem (uGUI/Canvas) 并存，可按界面粒度混用两种 UI 方案。
    /// </summary>
    public class UIToolkitSystem : ICustomSystem
    {
        #region 单例与初始化

        private static readonly Lazy<UIToolkitSystem> instance = new(() => new UIToolkitSystem());
        public static UIToolkitSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        #region 核心组件

        /// <summary>全局 UIDocument（挂载在 DontDestroyOnLoad 的 GameObject 上）</summary>
        public UIDocument GlobalDocument { get; private set; }

        /// <summary>全局根 VisualElement</summary>
        public VisualElement RootVisualElement => GlobalDocument != null
            ? GlobalDocument.rootVisualElement
            : null;

        /// <summary>PanelSettings 配置（决定缩放、分辨率等）</summary>
        private PanelSettings panelSettings;

        /// <summary>全局样式表（所有面板共享）</summary>
        private StyleSheet globalStyleSheet;
        #endregion

        #region 面板管理

        /// <summary>所有已打开面板的实例（面板名 → 面板实例）</summary>
        private readonly Dictionary<string, UIToolkitPanel> panelInstances
            = new Dictionary<string, UIToolkitPanel>(32);

        /// <summary>面板栈（用于层级管理、返回栈等）</summary>
        private readonly Stack<UIToolkitPanel> panelStack = new Stack<UIToolkitPanel>();

        /// <summary>面板 UXML 资源缓存（面板名 → VisualTreeAsset）</summary>
        private readonly Dictionary<string, VisualTreeAsset> panelAssetCache
            = new Dictionary<string, VisualTreeAsset>(32);

        /// <summary>面板 USS 样式缓存（面板名 → StyleSheet）</summary>
        private readonly Dictionary<string, StyleSheet> panelStyleCache
            = new Dictionary<string, StyleSheet>(32);
        #endregion

        #region R3 响应式事件

        /// <summary>面板打开事件</summary>
        public readonly Subject<UIToolkitPanel> OnPanelOpenSubject = new Subject<UIToolkitPanel>();

        /// <summary>面板关闭事件</summary>
        public readonly Subject<UIToolkitPanel> OnPanelCloseSubject = new Subject<UIToolkitPanel>();
        #endregion

        #region 资源路径常量

        /// <summary>UXML 资源路径前缀（相对于 Resources 文件夹）</summary>
        private const string UXML_PATH_PREFIX = "UI/UIToolkit/";

        /// <summary>USS 资源路径前缀（相对于 Resources 文件夹）</summary>
        private const string USS_PATH_PREFIX = "UI/UIToolkit/Styles/";

        /// <summary>全局 PanelSettings 资源路径</summary>
        private const string PANEL_SETTINGS_PATH = "UI/UIToolkit/GlobalPanelSettings";

        /// <summary>全局样式表路径</summary>
        private const string GLOBAL_STYLE_PATH = "UI/UIToolkit/Styles/Global";

        /// <summary>全局主题样式表路径（PanelSettings.themeStyleSheet 必需项）</summary>
        private const string GLOBAL_THEME_PATH = "UI/UIToolkit/GlobalTheme";
        #endregion

        #region 初始化 / 清理

        public async UniTask Init()
        {
            initProgress = 0;

            // 1. 创建全局 UIDocument
            CreateGlobalDocument();
            initProgress = 30;

            // 2. 加载 PanelSettings
            await LoadPanelSettingsAsync();
            initProgress = 60;

            // 3. 预加载全局样式表
            LoadGlobalStyleSheet();
            initProgress = 80;

            initProgress = 100;
            isInited = true;
            Log.Debug("[UIToolkitSystem] 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {
            // UI Toolkit 的更新由 Unity 内部处理
            // 此处可添加自定义逻辑（如 UI 动画 Tick、过渡效果等）
        }

        public void Clear()
        {
            Log.Debug("[UIToolkitSystem] 清除数据");

            // 关闭所有面板
            foreach (var kvp in panelInstances)
            {
                kvp.Value?.OnClose();
            }
            panelInstances.Clear();
            panelStack.Clear();
            panelAssetCache.Clear();
            panelStyleCache.Clear();

            // 释放 R3 Subject
            OnPanelOpenSubject?.Dispose();
            OnPanelCloseSubject?.Dispose();

            // 销毁全局 UIDocument
            if (GlobalDocument != null)
            {
                UnityEngine.Object.Destroy(GlobalDocument.gameObject);
                GlobalDocument = null;
            }

            isInited = false;
        }
        #endregion

        #region 初始化细节

        private void CreateGlobalDocument()
        {
            var go = new GameObject("[UIToolkitRoot]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            GlobalDocument = go.AddComponent<UIDocument>();

            // 设置 PanelSettings（如果已加载）
            if (panelSettings != null)
            {
                GlobalDocument.panelSettings = panelSettings;
            }
        }

        private async UniTask LoadPanelSettingsAsync()
        {
            panelSettings = await ResourcesSystem.Instance
                .LoadAsync<PanelSettings>(PANEL_SETTINGS_PATH, isCache: true);

            if (panelSettings == null)
            {
                // 没有 PanelSettings 资源 → 尝试用 Unity 内置默认值
                Log.Warning("[UIToolkitSystem] 未找到 PanelSettings，将使用运行时默认设置。" +
                    $"请在 Resources/{PANEL_SETTINGS_PATH} 下创建 PanelSettings 资源以获得更好的效果。");
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            }

            // 确保 PanelSettings 有 Theme Style Sheet（Unity 6 强制要求，否则 UI 不渲染）
            EnsureThemeStyleSheet();

            if (GlobalDocument != null)
            {
                GlobalDocument.panelSettings = panelSettings;
            }
        }

        /// <summary>
        /// 确保 PanelSettings 已设置 Theme Style Sheet。
        /// Unity 6+ 中 PanelSettings 必须绑定一个主题 TSS 文件，否则 UI 无法正常渲染。
        /// </summary>
        private void EnsureThemeStyleSheet()
        {
            if (panelSettings == null) return;

            // 已有主题则跳过
            if (panelSettings.themeStyleSheet != null) return;

            // 尝试从 Resources 加载自定义主题（ThemeStyleSheet 是 .tss 资源）
            var theme = ResourcesSystem.Instance
                .Load<ThemeStyleSheet>(GLOBAL_THEME_PATH, isCache: true);

            if (theme != null)
            {
                panelSettings.themeStyleSheet = theme;
                Log.Debug("[UIToolkitSystem] 已加载自定义主题样式表");
                return;
            }

            // 无可用主题 → 用户需在 Editor 中手动设置
            Log.Warning(
                "[UIToolkitSystem] ⚠ PanelSettings 缺少 Theme Style Sheet，UI 可能渲染异常！\n" +
                "  → 解决方法：在 Unity Editor 中选中 GlobalPanelSettings 资源，\n" +
                "  → 将 Theme Style Sheet 字段设置为 Unity 默认主题文件。\n" +
                "  → 默认主题通常位于：Packages/com.unity.ui/... 或创建一个 .tss 文件放入 Resources/UI/UIToolkit/GlobalTheme.tss");
        }

        private void LoadGlobalStyleSheet()
        {
            globalStyleSheet = ResourcesSystem.Instance
                .Load<StyleSheet>(GLOBAL_STYLE_PATH, isCache: true);
            if (globalStyleSheet != null && RootVisualElement != null)
            {
                RootVisualElement.styleSheets.Add(globalStyleSheet);
            }
        }
        #endregion

        #region 面板操作 —— 同步

        /// <summary>
        /// 同步打开面板（资源须已预加载或位于 Resources 目录）
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="panelName">面板名称（同时也是 UXML 文件名，不含扩展名）</param>
        /// <param name="data">传递给面板的数据</param>
        /// <returns>面板实例，失败返回 null</returns>
        public T OpenPanel<T>(string panelName, object data = null) where T : UIToolkitPanel, new()
        {
            if (RootVisualElement == null)
            {
                Log.Error("[UIToolkitSystem] OpenPanel 失败：RootVisualElement 为空");
                return null;
            }

            // 已打开则直接返回
            if (panelInstances.TryGetValue(panelName, out var existing))
            {
                existing.OnOpen(data);
                OnPanelOpenSubject.OnNext(existing);
                return existing as T;
            }

            // 加载 UXML
            if (!panelAssetCache.TryGetValue(panelName, out var asset))
            {
                asset = ResourcesSystem.Instance
                    .Load<VisualTreeAsset>($"{UXML_PATH_PREFIX}{panelName}", isCache: true);
                if (asset == null)
                {
                    Log.Error($"[UIToolkitSystem] 无法加载 UXML: Resources/{UXML_PATH_PREFIX}{panelName}");
                    return null;
                }
                panelAssetCache[panelName] = asset;
            }

            // 实例化面板
            var panel = new T();
            panel.Initialize(panelName, asset, RootVisualElement, this);

            // 加载面板专属样式
            LoadPanelStyle(panelName, panel.Root);

            panelInstances[panelName] = panel;
            panelStack.Push(panel);

            panel.OnOpen(data);
            OnPanelOpenSubject.OnNext(panel);

            Log.Debug($"[UIToolkitSystem] 打开面板: {panelName}");
            return panel;
        }

        /// <summary>
        /// 同步打开面板（泛型参数自动推断面板名称 = typeof(T).Name）
        /// </summary>
        public T OpenPanel<T>(object data = null) where T : UIToolkitPanel, new()
        {
            return OpenPanel<T>(typeof(T).Name, data);
        }
        #endregion

        #region 面板操作 —— 异步

        /// <summary>
        /// 异步打开面板（支持异步加载 UXML 资源）
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="panelName">面板名称</param>
        /// <param name="data">传递给面板的数据</param>
        /// <returns>面板实例，失败返回 null</returns>
        public async UniTask<T> OpenPanelAsync<T>(string panelName, object data = null)
            where T : UIToolkitPanel, new()
        {
            if (RootVisualElement == null)
            {
                Log.Error("[UIToolkitSystem] OpenPanelAsync 失败：RootVisualElement 为空");
                return null;
            }

            // 已打开则直接返回
            if (panelInstances.TryGetValue(panelName, out var existing))
            {
                existing.OnOpen(data);
                OnPanelOpenSubject.OnNext(existing);
                return existing as T;
            }

            // 异步加载 UXML
            if (!panelAssetCache.TryGetValue(panelName, out var asset))
            {
                asset = await ResourcesSystem.Instance
                    .LoadAsync<VisualTreeAsset>($"{UXML_PATH_PREFIX}{panelName}", isCache: true);
                if (asset == null)
                {
                    Log.Error($"[UIToolkitSystem] 无法加载 UXML: Resources/{UXML_PATH_PREFIX}{panelName}");
                    return null;
                }
                panelAssetCache[panelName] = asset;
            }

            // 实例化面板
            var panel = new T();
            panel.Initialize(panelName, asset, RootVisualElement, this);

            // 异步加载面板样式
            await LoadPanelStyleAsync(panelName, panel.Root);

            panelInstances[panelName] = panel;
            panelStack.Push(panel);

            panel.OnOpen(data);
            OnPanelOpenSubject.OnNext(panel);

            Log.Debug($"[UIToolkitSystem] 打开面板: {panelName}");
            return panel;
        }

        /// <summary>
        /// 异步打开面板（泛型参数自动推断面板名称）
        /// </summary>
        public UniTask<T> OpenPanelAsync<T>(object data = null) where T : UIToolkitPanel, new()
        {
            return OpenPanelAsync<T>(typeof(T).Name, data);
        }
        #endregion

        #region 面板操作 —— 关闭

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void ClosePanel(string panelName)
        {
            if (!panelInstances.TryGetValue(panelName, out var panel)) return;

            panel.OnClose();
            panelInstances.Remove(panelName);

            // 从栈中移除（先从栈中重建）
            RebuildStackWithout(panel);

            OnPanelCloseSubject.OnNext(panel);
            Log.Debug($"[UIToolkitSystem] 关闭面板: {panelName}");
        }

        /// <summary>
        /// 关闭顶层面板
        /// </summary>
        public void CloseTopPanel()
        {
            if (panelStack.Count > 0)
            {
                var top = panelStack.Peek();
                ClosePanel(top.PanelName);
            }
        }

        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public void CloseAllPanels()
        {
            var names = new List<string>(panelInstances.Keys);
            foreach (var name in names)
            {
                ClosePanel(name);
            }
        }

        private void RebuildStackWithout(UIToolkitPanel target)
        {
            var temp = new Stack<UIToolkitPanel>();
            while (panelStack.Count > 0)
            {
                var p = panelStack.Pop();
                if (p != target) temp.Push(p);
            }
            while (temp.Count > 0)
            {
                panelStack.Push(temp.Pop());
            }
        }
        #endregion

        #region 面板查询

        /// <summary>
        /// 获取已打开的面板实例
        /// </summary>
        public T GetPanel<T>(string panelName) where T : UIToolkitPanel
        {
            panelInstances.TryGetValue(panelName, out var panel);
            return panel as T;
        }

        /// <summary>
        /// 获取已打开的面板实例（泛型名称）
        /// </summary>
        public T GetPanel<T>() where T : UIToolkitPanel
        {
            return GetPanel<T>(typeof(T).Name);
        }

        /// <summary>
        /// 判断面板是否已打开
        /// </summary>
        public bool IsPanelOpen(string panelName)
        {
            return panelInstances.ContainsKey(panelName);
        }
        #endregion

        #region 样式加载

        /// <summary>
        /// 同步加载面板专属 USS 样式表（可选，不存在时不报错）
        /// </summary>
        private void LoadPanelStyle(string panelName, VisualElement root)
        {
            // 使用 Resources.Load 直接加载（不存在时返回 null，不打 Error）
            var style = UnityEngine.Resources.Load<StyleSheet>($"{USS_PATH_PREFIX}{panelName}");
            if (style != null)
            {
                root.styleSheets.Add(style);
                panelStyleCache[panelName] = style;
            }
        }

        /// <summary>
        /// 异步加载面板专属 USS 样式表（可选，不存在时不报错）
        /// </summary>
        private async UniTask LoadPanelStyleAsync(string panelName, VisualElement root)
        {
            // 异步加载，不缓存到 ResourcesSystem（避免缺失时打 Error）
            var request = UnityEngine.Resources.LoadAsync<StyleSheet>($"{USS_PATH_PREFIX}{panelName}");
            await UniTask.WaitUntil(() => request.isDone);
            var style = request.asset as StyleSheet;
            if (style != null)
            {
                root.styleSheets.Add(style);
                panelStyleCache[panelName] = style;
            }
        }
        #endregion
    }
}
