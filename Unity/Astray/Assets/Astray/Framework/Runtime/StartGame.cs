using Astray.Common;
using Astray.Framework.Modules;
using Astray.Utilities;
using Astray.Framework.Modules.Http;
using Astray.Framework.Modules.Timer;
using Astray.Framework.Runtime.Base;
using Astray.Framework.Runtime.EventMessage;
using Astray.Framework.Runtime.Languages;
using Astray.Framework.Runtime.Pause;
using Astray.Framework.Runtime.Resources;
using Astray.Framework.Runtime.Scene;
using Astray.Framework.Runtime.Sound;
using Astray.Framework.Runtime.Terminal;
using Astray.Framework.Runtime.UI;
using Astray.Framework.Runtime.UIInput;
using Astray.Framework.Runtime.UIToolkit;
using AOT;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Astray.Framework.Runtime
{
    /// <summary>
    /// 游戏入口 —— 注册所有模块并定义启动流程。
    /// 不再依赖 MonoBehaviour，由 Bootstrap 实例化。
    /// </summary>
    public class StartGame : GameEntry
    {
        /// <summary>进入游戏后加载的首个场景名（业务配置，不是框架约定）。</summary>
        public const string BootSceneName = "Temp";

        /// <summary>启动界面窗口名（= <see cref="StartGameUIPlane"/> 的窗口 Id）。</summary>
        public const string StartupWindowName = "StartGameUIPlane";
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
        /// 注册所有游戏模块。
        /// <para>
        /// 初始化顺序由各模块类上的 <c>[ModuleOrder(order, DependsOn...)]</c> 决定，
        /// 这里是"注册哪些模块"的清单，<b>不再是</b>"谁先谁后"的清单。
        /// 启动时 <see cref="ModuleCatalog"/> 会做拓扑排序并校验（缺依赖 / 重复注册 / 成环），
        /// 结果打印在启动日志的 "模块顺序: ..." 一行。
        /// </para>
        /// </summary>
        public override IList<ICustomSystem> CreateModules()
        {
            // 清单本身放在 ModulePresets 里，示例工程与接入方共用同一份定义（改这里请改 ModulePresets.Full）。
            // 实际初始化顺序由各模块的 [ModuleOrder(order, DependsOn...)] 拓扑排序决定，与下面的书写顺序无关。
            return ModulePresets.Full();
        }

        /// <summary>
        /// 在初始化模块之前执行（加载配置等）
        /// </summary>
        public override UniTask OnBeforeInitAsync()
        {
            Log.Debug("[StartGame] 初始化前执行");

            // 预加载配置（后续日志等模块访问 Config 属性时无需再走 Resources.Load）
            Astray.Config.EnsureLoaded();

            // 所有平台都加载一次。WebGL 的 PlayerPrefs 同样可用（播放器启动时以 runDependency
            // 阻塞等 IndexedDB 载入，见 GameOption.LoadOptions），故不再按平台跳过。
            GameOption.LoadOptions();

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
            // （引擎已启动且模块已注册，按契约用 Modules.Get；取不到会抛异常并列出已注册模块）
            Modules.Get<LanguagesSystem>().SetMultilingual(GameOption.CurrentOption.language);

            // 测试/调试场景：跳过自动场景跳转，保留当前场景用于调试
            if (Bootstrap.IsTestScene)
            {
                Log.Debug("[StartGame] 测试场景模式，跳过自动场景加载，保留当前场景");
                return;
            }

            // 游戏启动时，排除启动界面不随场景切换而隐藏（当前未启用该行为）
            // Modules.Get<SceneSystem>().ExcludeWindowFromSceneHide(StartupWindowName);

            // 加载初始场景
            await Modules.Get<SceneSystem>().LoadScene(BootSceneName, true, null, null);

            // 打开启动界面（Addressables 双轨加载，失败自动降级 Resources）
            await Modules.Get<UISystem>().OpenWindowAsync(StartupWindowName);
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
        /// 按钮点击等交互回调上，不能代码强制；WebGL 上全屏由浏览器控制，GameOption 仅维护该字段
        /// 的状态（写入存档，但 ApplyDisplayOptions 里的 SetResolution 按平台被跳过）。
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
        /// 同步 GameOption 的全屏状态并持久化（PlayerPrefs 在 WebGL 可用）；
        /// 分辨率应用由 ApplyDisplayOptions 按平台跳过，不会因此误切分辨率。
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
