using System;
using System.IO;
using System.Net.Sockets;

namespace Telepathy
{
    public static class NetworkStreamExtensions
    {
        // .Read 在远程关闭连接时返回 '0'，但在我们自愿关闭自己的连接时会抛出 IOException。
        //
        // 因此我们添加一个 ReadSafely 方法，在两种情况下都返回 '0'，这样我们就不必担心异常，因为断开连接就是断开连接...
        public static int ReadSafely(this NetworkStream stream, byte[] buffer, int offset, int size)
        {
            try
            {
                return stream.Read(buffer, offset, size);
            }
            // IOException 在我们自愿关闭自己的连接时发生。
            catch (IOException)
            {
                return 0;
            }
            // ObjectDisposedException 可能在 Client.Disconnect() 释放流的同时我们仍尝试读取时抛出。
            // 捕获它可以修复 https://github.com/vis2k/Telepathy/pull/104
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        // 帮助函数，用于精确读取 'n' 字节
        // -> 默认的 .Read 最多读取 'n' 字节。此函数会读取恰好 'n' 字节
        // -> 该调用将阻塞直到收到 'n' 字节
        // -> 在断开连接的情况下将立即返回 false
        public static bool ReadExactly(this NetworkStream stream, byte[] buffer, int amount)
        {
            // 可能没有足够的字节可供 .Read 一次性读取，因此我们需要继续尝试直到读取到所有字节（阻塞）
            //
            // 注意：这只是下面的更快实现的版本：
            //     for (int i = 0; i < amount; ++i)
            //         if (stream.Read(buffer, i, 1) == 0)
            //             return false;
            //     return true;
            int bytesRead = 0;
            while (bytesRead < amount)
            {
                // 使用 '安全' 读取扩展来读取最多 'remaining' 字节
                int remaining = amount - bytesRead;
                int result = stream.ReadSafely(buffer, bytesRead, remaining);

                // .Read 在断开连接时返回 0
                if (result == 0)
                    return false;

                // 否则累加已读字节数
                bytesRead += result;
            }
            return true;
        }
    }
}