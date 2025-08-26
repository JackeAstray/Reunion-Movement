using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.EventMessage;
using ReunionMovement.Core.Languages;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using ReunionMovement.Core.Sound;
using ReunionMovement.Core.UI;
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
        protected override IList<ICustommSystem> CreateModules()
        {
            var modules = base.CreateModules();

            modules.Add(ResourcesSystem.Instance);

            modules.Add(SceneSystem.Instance);

            modules.Add(EventMessageSystem.Instance);

            modules.Add(LanguagesSystem.Instance);

            modules.Add(SoundSystem.Instance);

            modules.Add(UISystem.Instance);

            return modules;
        }

        /// <summary>
        /// 在初始化模块之前，协同程序
        /// </summary>
        /// <returns></returns>
        public override async Task OnBeforeInitAsync()
        {
            Log.Debug("初始化前执行");
            GameOption.LoadOptions();
        }

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        public override async Task OnGameStartAsync()
        {
            Log.Debug("游戏启动");

            // 临时设置游戏选项
            //GameOption.currentOption.sfxMuted = false;
            //GameOption.currentOption.musicMuted = false;
            //GameOption.SaveOptions();

            UISystem.Instance.OpenWindow("StartGameUIPlane");

            // 播放声音
            //SoundSystem.Instance.PlayMusic(100001);
            //SoundSystem.Instance.PlaySfx(300001);

            // 加载场景
            //await SceneSystem.Instance.LoadScene("Temp", true);
        }
    }
}
