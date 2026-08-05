using System;
using System.Net;
using kcp2k;

namespace ReunionMovement.Common.Util
{
    public class KcpServerEndPoint : KcpServer
    {
        public KcpServerEndPoint(Action<int, IPEndPoint> OnConnected, Action<int, ArraySegment<byte>, KcpChannel> OnData, Action<int> OnDisconnected, Action<int, ErrorCode, string> OnError, KcpConfig config)
            : base(OnConnected, OnData, OnDisconnected, OnError, config)
        {
        }
        public string IPAddress
        {
            get
            {
                // Start 之前 socket 为 null，访问 LocalEndPoint 会 NRE
                try
                {
                    return socket?.LocalEndPoint?.ToString() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}