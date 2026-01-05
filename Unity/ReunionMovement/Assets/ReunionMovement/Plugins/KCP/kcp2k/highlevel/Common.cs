using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace kcp2k
{
    public static class Common
    {
        // 帮助函数：将主机名解析为 IPAddress
        public static bool ResolveHostname(string hostname, out IPAddress[] addresses)
        {
            try
            {
                // 注意：DNS 查询是阻塞的，可能会耗时一秒左右。
                addresses = Dns.GetHostAddresses(hostname);
                return addresses.Length >= 1;
            }
            catch (SocketException exception)
            {
                Log.Info($"[KCP] 无法解析主机: {hostname} 原因: {exception}");
                addresses = null;
                return false;
            }
        }

        // 如果在高负载下连接断开，先将套接字缓冲区调整到操作系统允许的极限。
        // 如果仍然不足，则需要调整操作系统的限制。
        public static void ConfigureSocketBuffers(Socket socket, int recvBufferSize, int sendBufferSize)
        {
            // 记录初始大小以供比较。
            int initialReceive = socket.ReceiveBufferSize;
            int initialSend = socket.SendBufferSize;

            // 设置为配置的大小
            try
            {
                socket.ReceiveBufferSize = recvBufferSize;
                socket.SendBufferSize = sendBufferSize;
            }
            catch (SocketException)
            {
                Log.Warning($"[KCP] 无法设置 Socket RecvBufSize = {recvBufferSize} SendBufSize = {sendBufferSize}");
            }

            Log.Info($"[KCP] 接收缓冲: {initialReceive}=>{socket.ReceiveBufferSize} ({(initialReceive == 0 ? 0 : socket.ReceiveBufferSize / initialReceive)}x) 发送缓冲: {initialSend}=>{socket.SendBufferSize} ({(initialSend == 0 ? 0 : socket.SendBufferSize / initialSend)}x)");
        }

        // 根据 IP+端口生成连接哈希。
        //
        // 注意：IPEndPoint.GetHashCode() 会导致分配。
        //  它会调用 m_Address.GetHashCode()。
        //  m_Address 是一个 IPAddress。
        //  对于 IPv6，GetHashCode() 会分配：
        //  https://github.com/mono/mono/blob/bdd772531d379b4e78593587d15113c37edd4a64/mcs/class/referencesource/System/net/System/Net/IPAddress.cs#L699
        // => 仅使用 newClientEP.Port 不可行，因为不同连接可能使用相同端口。
        public static int ConnectionHash(EndPoint endPoint) =>
            endPoint.GetHashCode();

        // Cookie 必须使用安全的随机生成器来生成，保证不可预测。
        // RNG 在静态字段中缓存以避免运行时分配。
        static readonly RNGCryptoServiceProvider cryptoRandom = new RNGCryptoServiceProvider();
        static readonly byte[] cryptoRandomBuffer = new byte[4];
        public static uint GenerateCookie()
        {
            cryptoRandom.GetBytes(cryptoRandomBuffer);
            return BitConverter.ToUInt32(cryptoRandomBuffer, 0);
        }
    }
}
