using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.EventMessage;
using ReunionMovement.Core.Languages;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using ReunionMovement.Core.Sound;
using ReunionMovement.Core.Terminal;
using ReunionMovement.Core.UI;
using ReunionMovement.Core.UIInput;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏入口类
    /// </summary>
    public class StartGame : GameEntry
    {
        // 全局游戏初始化状态标记与事件
        public static bool IsGameInitFinished { get; private set; }
        public static event Action OnGameInitComplete;

        protected override IList<ICustomSystem> CreateModules()
        {
            var modules = base.CreateModules();

            modules.Add(ResourcesSystem.Instance);

            modules.Add(SceneSystem.Instance);

            modules.Add(EventMessageSystem.Instance);

            modules.Add(LanguagesSystem.Instance);

            modules.Add(SoundSystem.Instance);

            modules.Add(UISystem.Instance);

            modules.Add(UIInputSystem.Instance);

            modules.Add(TerminalSystem.Instance);

            return modules;
        }

        /// <summary>
        /// 在初始化模块之前，协同程序
        /// </summary>
        /// <returns></returns>
        public override Task OnBeforeInitAsync()
        {
            Log.Debug("初始化前执行");

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                GameOption.LoadOptions();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        public override async Task OnGameStartAsync()
        {
            Log.Debug("游戏启动");

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                GameOption.ResetOptions();
            }

            // 临时设置游戏选项
            //GameOption.currentOption.sfxMuted = false;
            //GameOption.currentOption.musicMuted = false;
            //GameOption.SaveOptions();

            // 播放声音
            //SoundSystem.Instance.PlayMusic(100001);
            //SoundSystem.Instance.PlaySfx(300001);

            // 先打开 UI，再注册为场景切换时不隐藏
            UISystem.Instance.OpenWindow("StartGameUIPlane");
            SceneSystem.Instance.ExcludeWindowFromSceneHide("StartGameUIPlane");

            // 加载场景
            await SceneSystem.Instance.LoadScene("Temp", true);

            // 对于在场景大量散落的组件，使用静态标志位和事件通知，避免遗漏
            IsGameInitFinished = true;
            OnGameInitComplete?.Invoke();
        }
    }
}
