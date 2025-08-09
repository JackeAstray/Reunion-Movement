using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Core.Sound
{
    public class SoundSystem : ICustommSystem
    {
        #region 单例与初始化
        private static readonly Lazy<SoundSystem> instance = new(() => new SoundSystem());
        public static SoundSystem Instance => instance.Value;
        public bool IsInited { get; private set; }
        private double initProgress = 0;
        public double InitProgress { get { return initProgress; } }
        #endregion

        public async Task Init()
        {
            initProgress = 0;

            //await OnInit();

            initProgress = 100;
            IsInited = true;
            Log.Debug("SoundSystem 初始化完成");
        }

        public void Update(float logicTime, float realTime)
        {

        }

        public void Clear()
        {
            Log.Debug("SoundSystem 清除数据");
        }

    }
}