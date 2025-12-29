using ReunionMovement.Common;
using ReunionMovement.Common.Util.Timer;
using TMPro;
using UnityEngine;

namespace ReunionMovement.Example
{
    public class TimerExample : MonoBehaviour
    {
        public TextMeshProUGUI timer1;
        public TextMeshProUGUI timer2;

        public void Start()
        {
            // 创建一个计时器，5秒后完成
            Timer tempTimer1 = TimerMgr.Instance.CreateTimer(5f, true);
            tempTimer1.OnCompleted += () => timer1.text = "Timer 1 completed!";
            tempTimer1.OnTick += (elapsed) => timer1.text = $"1 Elapsed time: {elapsed} seconds";
            // 启动计时器
            tempTimer1.Start();

            // 创建一个计时器，5秒后完成
            Timer tempTimer2 = TimerMgr.Instance.CreateTimer(5f, false);
            tempTimer2.OnCompleted += () => timer2.text = "Timer 2 completed!";
            tempTimer2.OnTick += (elapsed) => timer2.text = $"2 Elapsed time: {elapsed} seconds";
            // 启动计时器
            tempTimer2.Start();
        }
    }
}