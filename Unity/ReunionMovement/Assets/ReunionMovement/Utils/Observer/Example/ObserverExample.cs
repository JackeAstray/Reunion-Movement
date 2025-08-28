using ReunionMovement.Common;
using ReunionMovement.Common.Util;
using UnityEngine;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 观察者例子
    /// </summary>
    public class ObserverExample : ObserverBase
    {
        public int exampleValue;

        public void Init(SubjectBase subject)
        {
            this.subject = subject;
            this.subject.Attach(this);
        }

        public override void UpdateData(params object[] args)
        {
            if (args != null && args.Length > 0 && args[0] is int value)
            {
                Log.Debug($"收到数值变化通知: {value}");
                exampleValue = value;
            }
        }
    }
}