using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 状态机示例的状态枚举
    /// </summary>
    public enum StateMachineExampleState
    {
        Idle,
        Running,
        Jumping,
        Attacking
    }

    /// <summary>
    /// 震动类型枚举
    /// </summary>
    public enum HapticTypes
    {
        Selection,
        Success,
        Warning,
        Failure,
        LightImpact,
        MediumImpact,
        HeavyImpact
    }

    /// <summary>
    /// 多语言支持枚举
    /// </summary>
    public enum Multilingual
    {
        // 中文
        ZH,
        // 英文
        EN,
        // 俄语
        RU,
        // 日语
        JP,
    }
}
