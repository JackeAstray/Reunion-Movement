using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using ReunionMovement.Common.Util.HttpService;
using ReunionMovement.Common.Util.Timer;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.EventMessage;
using ReunionMovement.Core.Languages;
using ReunionMovement.Core.Pause;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using ReunionMovement.Core.Sound;
using ReunionMovement.Core.Terminal;
using ReunionMovement.Core.UI;
using ReunionMovement.Core.UIInput;
using ReunionMovement.Core.UIToolkit;
using AOT;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏入口 —— 注册所有模块并定义启动流程。
    /// 不再依赖 MonoBehaviour，由 Bootstrap 实例化。
    /// </summary>
    public class StartGame : GameEntry
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SetFullscreenState(int value);

        /// <summary>当前是否全屏（查询浏览器实时状态，1/0）</summary>
        [DllImport("__Internal")]
        private static extern int GetFullscreenState();

        /// <summary>注册浏览器全屏状态变化回调（参数：1=全屏，0=非全屏）</summary>
        [DllImport("__Internal")]
        private static extern void RegisterFullscreenChanged(Action<int> callback);

        // 持有回调委托引用，防止被 GC 回收导致浏览器侧回调失效
        private static readonly Action<int> fullscreenChangedCallback = OnBrowserFullscreenChanged;
#endif

        /// <summary>
        /// 注册所有游戏模块。列表顺序决定初始化顺序（先注册的先初始化）。
        /// ResourcesSystem 必须在最前面（其他模块依赖它加载资源）。
        /// </summary>
        public override IList<ICustomSystem> CreateModules()
        {
            var modules = new List<ICustomSystem>(14);

            modules.Add(ResourcesSystem.Instance);    // 0: 资源加载（同步/兜底，最高依赖）
            modules.Add(AddressableSystem.Instance);  // 1: Addressables（受管异步/远程）【新增】
            modules.Add(SceneSystem.Instance);        // 2: 场景管理
            modules.Add(EventMessageSystem.Instance); // 3: 事件总线
            modules.Add(LanguagesSystem.Instance);    // 4: 多语言
            modules.Add(PauseSystem.Instance);        // 5: 暂停管理（Time.timeScale 统一切换）【新增】
            modules.Add(SoundSystem.Instance);        // 6: 音频（需要 Update 驱动淡入淡出）
            modules.Add(TimerMgr.Instance);           // 7: 计时器（需要 Update 驱动）
            modules.Add(UISystem.Instance);           // 8: UI 管理
            modules.Add(UIInputSystem.Instance);      // 9: UI 输入（需要 Update 驱动导航）
            modules.Add(UIToolkitSystem.Instance);    // 10: UI Toolkit
            modules.Add(TerminalSystem.Instance);     // 11: 终端（需要 Update 检测按键）
            modules.Add(NetworkMgr.Instance);         // 12: 网络通道管理（需要 Update 消费移除队列）
            modules.Add(HttpMgr.Instance);            // 13: HTTP 请求（需要 Update 轮询进度）

            return modules;
        }

        /// <summary>
        /// 在初始化模块之前执行（加载配置等）
        /// </summary>
        public override UniTask OnBeforeInitAsync()
        {
            Log.Debug("[StartGame] 初始化前执行");

            // 预加载配置（后续日志等模块访问 Config 属性时无需再走 Resources.Load）
            ReunionMovement.Config.EnsureLoaded();

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                GameOption.LoadOptions();
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 游戏启动 —— 所有模块初始化完成后执行。
        /// 打开主界面、加载场景等。
        /// </summary>
        public override async UniTask OnGameStartAsync()
        {
            Log.Debug("[StartGame] 游戏启动");

#if UNITY_WEBGL && !UNITY_EDITOR
            // 注册浏览器全屏状态变化回调（含用户按 Esc、页面按钮退出）：把真实状态同步回
            // GameOption，避免设置界面显示的开关状态与浏览器不一致
            RegisterFullscreenChanged(fullscreenChangedCallback);

            // 初始校准：浏览器起始必为窗口模式（无用户手势禁止自动进入全屏），而 GameOption
            // 默认 fullscreen=true；以浏览器实际状态为准同步一次
            bool actualFullscreen = GetFullscreenState() != 0;
            if (GameOption.CurrentOption.fullscreen != actualFullscreen)
            {
                GameOption.SetFullscreen(actualFullscreen);
            }
#endif

            // 注意：这里不再调用 GameOption.ResetOptions()。
            // OnBeforeInitAsync 已用 GameOption.LoadOptions() 读回玩家存档，
            // 此处若再 ResetOptions() 会把 PlayerPrefs 覆盖成默认值，导致设置无法持久化。
            // 重置逻辑仅保留给“恢复默认设置”的 UI 按钮。

            // 应用玩家保存的语言设置：此前全项目无 SetMultilingual 调用点，
            // GameOption.language 加载后从不生效，玩家所选语言重启即回 ZH_CN
            LanguagesSystem.Instance.SetMultilingual(GameOption.CurrentOption.language);

            // 测试/调试场景：跳过自动场景跳转，保留当前场景用于调试
            if (Bootstrap.IsTestScene)
            {
                Log.Debug("[StartGame] 测试场景模式，跳过自动场景加载，保留当前场景");
                return;
            }

            // 游戏启动时，排除启动界面不随场景切换而隐藏
            // SceneSystem.Instance.ExcludeWindowFromSceneHide(UINames.StartGame);

            // 加载初始场景
            await SceneSystem.Instance.LoadScene("Temp", true, null, null);

            // 打开启动界面（Addressables 双轨加载，失败自动降级 Resources）
            await UISystem.Instance.OpenWindowAsync(UINames.StartGame);
        }

        /// <summary>
        /// 全屏状态变化事件（参数：true = 进入全屏）。
        /// 覆盖主动切换（SetFullscreen）与浏览器侧变化（WebGL 用户按 Esc/页面按钮退出，
        /// 经 jslib 回调同步后触发）；设置界面订阅即可保持图标/开关与真实状态一致。
        /// </summary>
        public static event Action<bool> OnFullscreenChanged;

        /// <summary>
        /// 切换全屏模式（设置界面全屏开关的入口）。
        /// WebGL：以 GameOption 内存状态取反（浏览器状态变化回调已把外部变化同步回该字段），
        /// 经 jslib 调用浏览器 Fullscreen API —— 浏览器要求全屏由用户手势触发，因此必须挂在
        /// 按钮点击等交互回调上，不能代码强制；WebGL 上分辨率/全屏设置由浏览器控制，
        /// GameOption 仅维护内存状态（存档与 SetResolution 在 WebGL 分支内均被跳过）。
        /// 其他平台：更新 GameOption（持久化）并由 ApplyDisplayOptions 走 Screen.SetResolution。
        /// </summary>
        public static void SetFullscreen()
        {
            bool fullscreen = !GameOption.CurrentOption.fullscreen;
            GameOption.SetFullscreen(fullscreen);

#if UNITY_WEBGL && !UNITY_EDITOR
            Log.Debug("[StartGame] {0}全屏", fullscreen ? "进入" : "退出");
            SetFullscreenState(fullscreen ? 1 : 0);
#endif

            // 通知 UI 刷新图标/开关（WebGL 全屏异步生效，浏览器回调会按真实状态再校正）
            OnFullscreenChanged?.Invoke(fullscreen);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// 浏览器全屏状态变化回调（由 jslib 的 fullscreenchange 触发，含 Esc 与页面按钮退出）。
        /// 仅同步 GameOption 内存状态：WebGL 下存档写入与分辨率应用均被平台分支跳过。
        /// </summary>
        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnBrowserFullscreenChanged(int fullscreen)
        {
            bool active = fullscreen != 0;
            if (GameOption.CurrentOption.fullscreen != active)
            {
                Log.Debug("[StartGame] 浏览器全屏状态同步: {0}", active);
                GameOption.SetFullscreen(active);
                OnFullscreenChanged?.Invoke(active);
            }
        }
#endif
    }
}
