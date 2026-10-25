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

        [Header("服务端防护")]
        [Tooltip("空闲超时（秒，0=禁用）：超过时长未收到任何数据的连接被断开（需客户端启用心跳配合）")]
        public float idleTimeoutSeconds = 0f;

        [Tooltip("单连接消息速率上限（条/秒，0=不限）：超限消息被丢弃（防刷消息 DoS）。默认 200 覆盖绝大多数游戏流量，高频率业务请调大")]
        public int maxMessagesPerSecond = 200;

        [Header("RPC 鉴权")]
        [Tooltip("RPC 鉴权开关：开启后，未通过 MarkConnectionAuthenticated 标记的连接发起的 RPC 请求将被拒绝（默认关闭保持兼容，生产环境建议开启）")]
        public bool requireRpcAuthentication = false;

        [Header("加密握手")]
        [Tooltip("启用加密握手：连接建立后交换随机数派生每连接会话密钥，业务帧 AES-256-CBC+HMAC 加密（两端需同时开启且主密钥一致）")]
        public bool enableEncryptedHandshake = false;

        [Tooltip("握手主密钥（32 字节）。生产环境建议运行时用 SetHandshakeMasterKey 从 HTTPS 登录接口下发，勿硬编码")]
        public byte[] handshakeMasterKey = null;

        public NetworkServerConfig Clone()
        {
            return (NetworkServerConfig)MemberwiseClone();
        }
    }
}
