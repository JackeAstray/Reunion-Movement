// 一个简单的日志类，默认使用 Console.WriteLine 输出。
// 也可以在 Unity 环境中设置为 e.g. Log.Info = Debug.Log 来使用 Unity 的日志系统。
// （这样就不用强制依赖 UnityEngine）
using System;

namespace kcp2k
{
    public static class Log
    {
        public static Action<string> Info    = Console.WriteLine;
        public static Action<string> Warning = Console.WriteLine;
        public static Action<string> Error   = Console.Error.WriteLine;
    }
}
