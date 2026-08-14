using System;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络服务端配置（可序列化）。
    /// </summary>
    [Serializable]
    public class NetworkServerConfig
    {
        [Header("监听")]
        [Tooltip("通道名（NetworkMgr 注册索引用）")]
        public string channelName = "NET_SERVER";

        [Tooltip("传输类型：Tcp(Telepathy) / Kcp / WebSocket / RawTcp(原生字节流)")]
        public NetworkTransportType transport = NetworkTransportType.Tcp;

        public int port = 7778;

        [Header("协议")]
        [Tooltip("消息帧格式，需与客户端一致")]
        public NetworkCodecType codec = NetworkCodecType.MessageId;

        [Tooltip("流式组装缓冲上限（字节），防恶意超大帧")]
        public int maxAssembledFrameSize = 1 << 20;

        public NetworkServerConfig Clone()
        {
            return (NetworkServerConfig)MemberwiseClone();
        }
    }
}
