using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Core
{
    public abstract class GameEntry : SingletonMgr<GameEntry>, IGameEntry
    {
        /// <summary>
        /// 游戏模块列表
        /// </summary>
        private IList<ICustommSystem> modules;

        /// <summary>
        /// 创建一个模块，里面有一些新类
        /// </summary>
        /// <returns></returns>
        protected virtual IList<ICustommSystem> CreateModules()
        {
            return new List<ICustommSystem> { };
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
        public abstract Task OnBeforeInitAsync();

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        public abstract Task OnGameStartAsync();
    }
}
