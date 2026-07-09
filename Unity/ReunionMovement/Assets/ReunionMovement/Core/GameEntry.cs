using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace ReunionMovement.Core
{
    public abstract class GameEntry : SingletonMgr<GameEntry>, IGameEntry
    {
        /// <summary>
        /// 游戏模块列表
        /// </summary>
        private IList<ICustomSystem> modules;

        /// <summary>
        /// 创建一个模块，里面有一些新类
        /// </summary>
        /// <returns></returns>
        protected virtual IList<ICustomSystem> CreateModules()
        {
            return new List<ICustomSystem> { };
        }

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(this.gameObject);
            modules = CreateModules();
            GameEngine.StartEngine(gameObject, this, modules);
        }

        /// <summary>
        /// 在初始化之前
        /// </summary>
        /// <returns></returns>
        public abstract UniTask OnBeforeInitAsync();

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        public abstract UniTask OnGameStartAsync();
    }
}
