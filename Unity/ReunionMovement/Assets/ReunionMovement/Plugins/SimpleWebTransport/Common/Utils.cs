using System.Threading;

namespace Mirror.SimpleWeb
{
    internal static class Utils
    {
        public static void CheckForInterupt()
        {
            // Ë¯ÃßÒÔ´¥·¢ ThreadInterruptedException ¼ì²é
            Thread.Sleep(1);
        }
    }
}
