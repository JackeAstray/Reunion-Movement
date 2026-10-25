using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏入口抽象基类 —— 纯 C# 类，不再依赖 MonoBehaviour。
    /// 由 Bootstrap 实例化并通过 GameEngine.LaunchAsync() 驱动生命周期。
    /// </summary>
    public abstract class GameEntry : IGameEntry
    {
        /// <summary>
        /// 创建游戏模块列表。子类重写以注册自定义模块。
        /// 列表顺序即初始化顺序（先注册的先初始化）。
        /// </summary>
        /// <returns>模块列表</returns>
        public virtual IList<ICustomSystem> CreateModules()
        {
            return new List<ICustomSystem>();
        }

        /// <summary>
        /// 模块初始化前执行（如加载配置）
        /// </summary>
        public abstract UniTask OnBeforeInitAsync();

        /// <summary>
        /// 所有模块初始化完成后，启动游戏逻辑
        /// </summary>
        public abstract UniTask OnGameStartAsync();
    }
}
