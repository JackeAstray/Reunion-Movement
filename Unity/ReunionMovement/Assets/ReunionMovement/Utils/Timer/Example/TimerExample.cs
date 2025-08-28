using ReunionMovement.Common;
using ReunionMovement.Common.Util.Timer;
using UnityEngine;

namespace ReunionMovement.Example
{
    public class TimerExample : MonoBehaviour
    {
        public void Start()
        {
            // 创建一个计时器，5秒后完成
            Timer timer = TimerMgr.Instance.CreateTimer(5f, true);
            timer.OnCompleted += () => Log.Debug("Timer 1 completed!");
            timer.OnTick += (elapsed) => Log.Debug($"1 Elapsed time: {elapsed} seconds");
            // 启动计时器
            timer.Start();

            // 创建一个计时器，5秒后完成
            Timer timer2 = TimerMgr.Instance.CreateTimer(5f, false);
            timer2.OnCompleted += () => Log.Debug("Timer 2 completed!");
            timer2.OnTick += (elapsed) => Log.Debug($"2 Elapsed time: {elapsed} seconds");
            // 启动计时器
            timer2.Start();
        }
    }
}