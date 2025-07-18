using ReunionMovement.Common.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Example
{
    public class SubjectExample : SubjectBase
    {
        public int exampleValue;

        public void ChangeValue(int value)
        {
            exampleValue = value;
            SetState(exampleValue); // 通知所有观察者
        }

        public int GetValue() => exampleValue;
    }
}
