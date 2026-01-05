// 一个简单的日志类，默认使用 Console.WriteLine。
// 也可以将 Logger.LogMethod 设置为 Debug.Log 用于 Unity 等。
// （这样我们就不必依赖 UnityEngine.DLL，也不需要为每个 Unity 版本提供不同的实现）
using System;

namespace Telepathy
{
    public static class Log
    {
        public static Action<string> Info = Console.WriteLine;
        public static Action<string> Warning = Console.WriteLine;
        public static Action<string> Error = Console.Error.WriteLine;
    }
}
