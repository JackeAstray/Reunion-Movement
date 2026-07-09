using System.Threading;

namespace Mirror.SimpleWeb
{
    internal static class Utils
    {
        public static void CheckForInterupt()
        {
            // 睡眠以触发 ThreadInterruptedException 检查
            Thread.Sleep(1);
        }
    }
}
