using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Resources;
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
    public class StartGame : GameEntry
    {
        protected override IList<ICustommSystem> CreateModules()
        {
            var modules = base.CreateModules();

            modules.Add(ResourcesSystem.Instance);

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
        }

        /// <summary>
        /// 游戏启动
        /// </summary>
        /// <returns></returns>
        public override async Task OnGameStartAsync()
        {
            Log.Debug("游戏启动");
        }
    }
}
