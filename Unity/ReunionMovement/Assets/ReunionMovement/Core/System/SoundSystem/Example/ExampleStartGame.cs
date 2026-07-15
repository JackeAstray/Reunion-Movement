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
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 示例游戏入口 —— 演示如何自定义模块注册和启动流程。
    /// 在 Bootstrap 中将 new StartGame() 替换为 new ExampleStartGame() 即可切换。
    /// </summary>
    public class ExampleStartGame : GameEntry
    {
        public override IList<ICustomSystem> CreateModules()
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

        public override UniTask OnBeforeInitAsync()
        {
            Log.Debug("[ExampleStartGame] 初始化前执行");
            GameOption.LoadOptions();
            return UniTask.CompletedTask;
        }

        public override UniTask OnGameStartAsync()
        {
            Log.Debug("[ExampleStartGame] 游戏启动");
            return UniTask.CompletedTask;
        }
    }
}
