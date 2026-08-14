using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 服务端连接元数据：客户端 ID、地址、连接时间、流量统计与用户自定义标记。
    /// 通过 NetworkServer.GetConnectionInfo(connectionId) 获取。
    /// </summary>
    public sealed class NetworkConnectionInfo
    {
        /// <summary>连接 ID（服务端分配，断开后可能复用？—— 本项目不复用，单调递增）</summary>
        public int ConnectionId { get; }

        /// <summary>对端地址（IP:端口）</summary>
        public string Address { get; }

        /// <summary>连接建立时间（UTC）</summary>
        public DateTime ConnectedAt { get; }

        /// <summary>已接收字节数</summary>
        public long BytesReceived { get; internal set; }

        /// <summary>已发送字节数</summary>
        public long BytesSent { get; internal set; }

        /// <summary>最近一次收到数据的时刻（Time.realtimeSinceStartup，用于死链判定）</summary>
        public float LastReceiveTime { get; internal set; }

        /// <summary>用户自定义数据（如玩家对象、鉴权状态等）</summary>
        public object Tag { get; set; }

        /// <summary>已连接时长</summary>
        public TimeSpan Uptime => DateTime.UtcNow - ConnectedAt;

        internal NetworkConnectionInfo(int connectionId, string address)
        {
            ConnectionId = connectionId;
            Address = address ?? string.Empty;
            ConnectedAt = DateTime.UtcNow;
        }
    }
}
