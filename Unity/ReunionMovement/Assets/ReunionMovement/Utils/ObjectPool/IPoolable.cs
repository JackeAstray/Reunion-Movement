using UnityEngine;

namespace ReunionMovement.Common.Util.Pool
{
    /// <summary>
    /// 池化对象钩子：预制体上任意组件实现本接口即可接收取出/归还回调，
    /// 替代低效的 SendMessage("OnSpawned") 反射方案。
    /// </summary>
    public interface IPoolable
    {
        /// <summary>对象从池中取出时调用（可在此重置状态，避免复用残留）</summary>
        void OnSpawned();

        /// <summary>对象归还池时调用（可在此清理引用、停止协程/特效）</summary>
        void OnDespawned();
    }
}
