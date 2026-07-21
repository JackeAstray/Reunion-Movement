using ReunionMovement.Common;
using ReunionMovement.Common.Util.Timer;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.EventMessage;
using ReunionMovement.Core.Languages;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using ReunionMovement.Core.Sound;
using ReunionMovement.Core.Terminal;
using ReunionMovement.Core.UI;
using ReunionMovement.Core.UIInput;
using ReunionMovement.Core.UIToolkit;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏入口 —— 注册所有模块并定义启动流程。
    /// 不再依赖 MonoBehaviour，由 Bootstrap 实例化。
    /// </summary>
    public class StartGame : GameEntry
    {
        /// <summary>
        /// 注册所有游戏模块。列表顺序决定初始化顺序（先注册的先初始化）。
        /// ResourcesSystem 必须在最前面（其他模块依赖它加载资源）。
        /// </summary>
        /// <summary>
        /// 注册所有游戏模块。列表顺序决定初始化顺序（先注册的先初始化）。
        /// ResourcesSystem 必须在最前面（其他模块依赖它加载资源）。
        /// </summary>
        public override IList<ICustomSystem> CreateModules()
        {
            var modules = new List<ICustomSystem>(10);

            modules.Add(ResourcesSystem.Instance);    // 0: 资源加载（最高依赖）
            modules.Add(SceneSystem.Instance);        // 1: 场景管理
            modules.Add(EventMessageSystem.Instance); // 2: 事件总线
            modules.Add(LanguagesSystem.Instance);    // 3: 多语言
            modules.Add(SoundSystem.Instance);        // 4: 音频（需要 Update 驱动淡入淡出）
            modules.Add(TimerMgr.Instance);           // 5: 计时器（需要 Update 驱动）
            modules.Add(UISystem.Instance);           // 6: UI 管理
            modules.Add(UIInputSystem.Instance);      // 7: UI 输入（需要 Update 驱动导航）
            modules.Add(UIToolkitSystem.Instance);    // 8: UI Toolkit
            modules.Add(TerminalSystem.Instance);     // 9: 终端（需要 Update 检测按键）

            return modules;
        }

        /// <summary>
        /// 在初始化模块之前执行（加载配置等）
        /// </summary>
        public override UniTask OnBeforeInitAsync()
        {
            Log.Debug("[StartGame] 初始化前执行");

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

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                GameOption.ResetOptions();
            }

            // 打开启动 UI 并注册为场景切换时不隐藏
            UISystem.Instance.OpenWindow(UINames.StartGame);
            SceneSystem.Instance.ExcludeWindowFromSceneHide(UINames.StartGame);

            // 打开启动 UI Toolkit 面板
            // UIToolkitSystem.Instance.OpenPanel<StartGameUIPanel>("StartGame");

            // 加载初始场景
            await SceneSystem.Instance.LoadScene("Temp", true);
        }
    }
}
