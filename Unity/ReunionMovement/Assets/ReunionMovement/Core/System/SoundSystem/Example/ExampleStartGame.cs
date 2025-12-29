using ReunionMovement.Common;
using ReunionMovement.Core;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.EventMessage;
using ReunionMovement.Core.Languages;
using ReunionMovement.Core.Resources;
using ReunionMovement.Core.Scene;
using ReunionMovement.Core.Sound;
using ReunionMovement.Core.Terminal;
using ReunionMovement.Core.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Example
{
    public class ExampleStartGame : GameEntry
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
            GameOption.LoadOptions();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        public override Task OnGameStartAsync()
        {
            Log.Debug("游戏启动");
            return Task.CompletedTask;
        }
    }
}
