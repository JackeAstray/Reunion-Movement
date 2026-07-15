using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 被观察者示例 —— 使用 R3 ReactiveProperty 替代 SubjectBase
    /// </summary>
    public class SubjectExample
    {
        /// <summary>R3 响应式属性 —— 值变化时自动通知所有订阅者</summary>
        public ReactiveProperty<int> Value { get; } = new ReactiveProperty<int>(0);

        public int exampleValue => Value.Value;

        public void ChangeValue(int value)
        {
            Value.Value = value; // 自动通知所有订阅者
        }

        public int GetValue() => exampleValue;
    }
}
