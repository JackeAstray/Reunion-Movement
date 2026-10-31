using Gear.Common;
using Gear.Framework.Runtime;
using Gear.Framework.Runtime.Base;
using Gear.Framework.Runtime.EventMessage;
using Gear.Framework.Runtime.Languages;
using Gear.Framework.Runtime.Resources;
using Gear.Framework.Runtime.Scene;
using Gear.Framework.Runtime.Sound;
using Gear.Framework.Runtime.Terminal;
using Gear.Framework.Runtime.UI;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace Gear.Example
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

            modules.Add(ResourcesSystem.GetOrInstance());

            modules.Add(SceneSystem.GetOrInstance());

            modules.Add(EventMessageSystem.GetOrInstance());

            modules.Add(LanguagesSystem.GetOrInstance());

            modules.Add(SoundSystem.GetOrInstance());

            modules.Add(UISystem.GetOrInstance());

            modules.Add(TerminalSystem.GetOrInstance());

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
