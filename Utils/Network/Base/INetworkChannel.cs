using System;
namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络通道；
    /// </summary>
    public interface INetworkChannel
    {
        /// <summary>
        /// 网络地址；
        /// </summary>
        string Host { get; }
        /// <summary>
        /// 端口；
        /// </summary>
        int Port { get; }
        /// <summary>
        /// 通道名；
        /// </summary>
        string ChannelName { get; set; }
        /// <summary>
        /// 通道是否处于打开状态（客户端=已连接 / 服务端=监听中）；
        /// </summary>
        bool IsOpen { get; }
        /// <summary>
        /// 终结通道；
        /// </summary>
        void Close();
        /// <summary>
        /// 刷新通道；
        /// </summary>
        void TickRefresh();
    }
}