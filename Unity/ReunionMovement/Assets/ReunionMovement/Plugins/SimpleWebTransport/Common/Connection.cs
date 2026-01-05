using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Mirror.SimpleWeb
{
    internal sealed class Connection : IDisposable
    {
        readonly object disposedLock = new object();

        public const int IdNotSet = -1;
        public TcpClient client;
        public int connId = IdNotSet;

        /// <summary>
        /// 连接请求，从客户端发送以开始握手
        /// <para>仅在服务器上有效</para>
        /// </summary>
        public Request request;
        /// <summary>
        /// 远端地址或请求头中的地址
        /// <para>仅在服务器上有效</para>
        /// </summary>
        public string remoteAddress;

        public Stream stream;
        public Thread receiveThread;
        public Thread sendThread;

        public ManualResetEventSlim sendPending = new ManualResetEventSlim(false);
        public ConcurrentQueue<ArrayBuffer> sendQueue = new ConcurrentQueue<ArrayBuffer>();

        public Action<Connection> onDispose;
        volatile bool hasDisposed;

        public Connection(TcpClient client, Action<Connection> onDispose)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.onDispose = onDispose;
        }

        /// <summary>
        /// 释放客户端并停止线程
        /// </summary>
        public void Dispose()
        {
            Log.Verbose("[SWT-Connection]: 释放连接 {0}", ToString());

            // 先检查 hasDisposed，以避免在锁内触发 ThreadInterruptedException
            if (hasDisposed) return;

            Log.Verbose("[SWT-Connection]: 连接关闭: {0}", ToString());

            lock (disposedLock)
            {
                // 再次在锁内检查 hasDisposed，确保没有其他对象调用过该方法
                if (hasDisposed) return;

                hasDisposed = true;

                // 先停止线程，避免它们尝试使用已释放的对象
                receiveThread.Interrupt();
                sendThread?.Interrupt();

                try
                {
                    // 释放流
                    stream?.Dispose();
                    stream = null;
                    client.Dispose();
                    client = null;
                }
                catch (Exception e)
                {
                    Log.Exception(e);
                }

                sendPending.Dispose();

                // 释放发送队列中的所有缓冲区
                while (sendQueue.TryDequeue(out ArrayBuffer buffer))
                    buffer.Release();

                onDispose.Invoke(this);
            }
        }

        public override string ToString()
        {
            // remoteAddress 在握手完成后才会被设置
            if (hasDisposed)
                return $"[Conn:{connId}, 已释放]";
            else if (!string.IsNullOrWhiteSpace(remoteAddress))
                return $"[Conn:{connId}, 端点:{remoteAddress}]";
            else
                try
                {
                    EndPoint endpoint = client?.Client?.RemoteEndPoint;
                    return $"[Conn:{connId}, 端点:{endpoint}]";
                }
                catch (SocketException)
                {
                    return $"[Conn:{connId}, 端点:不可用]";
                }
        }

        /// <summary>
        /// 根据 <see cref="request"/> 和 RemoteEndPoint 获取地址
        /// <para>在接受 ServerHandShake 后调用</para>
        /// </summary>
        internal string CalculateAddress()
        {
            if (request.Headers.TryGetValue("X-Forwarded-For", out string forwardFor))
            {
                string actualClientIP = forwardFor.ToString().Split(',').First();
                // 从地址中移除端口号
                return actualClientIP.Split(':').First();
            }
            else
            {
                IPEndPoint ipEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                IPAddress ipAddress = ipEndPoint.Address;
                if (ipAddress.IsIPv4MappedToIPv6)
                    ipAddress = ipAddress.MapToIPv4();

                return ipAddress.ToString();
            }
        }
    }
}
