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
    }
}
