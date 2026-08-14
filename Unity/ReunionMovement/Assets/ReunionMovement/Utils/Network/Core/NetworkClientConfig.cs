using System;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 网络客户端配置（可序列化，供 Inspector / ScriptableObject / 代码使用）。
    /// 对接任意服务器只需调整 transport + host/port + codec 三组参数。
    /// </summary>
    [Serializable]
    public class NetworkClientConfig
    {
        [Header("连接")]
        [Tooltip("通道名（NetworkMgr 注册索引用，同组件内需唯一）")]
        public string channelName = "NET_CLIENT";

        [Tooltip("传输类型：Tcp(Telepathy) / Kcp / WebSocket / RawTcp(原生字节流，对接任意服务器)")]
        public NetworkTransportType transport = NetworkTransportType.Tcp;

        [Tooltip("服务器地址：IP 或域名；WebSocket 支持 ws:// wss:// 前缀（可带路径）")]
        public string host = "127.0.0.1";

        public int port = 7778;

        [Header("协议")]
        [Tooltip("消息帧格式：MessageId(默认) / LengthPrefixed / LengthPrefixedWithId / Passthrough；RawTcp 建议 LengthPrefixed")]
        public NetworkCodecType codec = NetworkCodecType.MessageId;

        [Tooltip("流式组装缓冲上限（字节），防恶意超大帧")]
        public int maxAssembledFrameSize = 1 << 20;

        [Header("自动重连")]
        public bool autoReconnect = true;

        [Tooltip("-1 表示无限重连")]
        public int maxReconnectAttempts = -1;

        [Tooltip("首次重连基础延迟（秒）")]
        public float reconnectBaseDelay = 3f;

        [Tooltip("指数退避因子：第 n 次重连延迟 = base * factor^(n-1)，封顶 maxDelay")]
        public float reconnectBackoffFactor = 2f;

        [Tooltip("重连延迟上限（秒）")]
        public float reconnectMaxDelay = 30f;

        [Tooltip("随机抖动比例（0~1），避免多客户端同时重连造成惊群")]
        public float reconnectJitter = 0.1f;

        [Tooltip("连接建立超时（秒），超时视为失败并进入重连流程")]
        public float connectTimeout = 10f;

        [Header("心跳")]
        public bool enableHeartbeat = false;

        public float heartbeatInterval = 5f;

        public string heartbeatText = "PING";

        [Tooltip("死链判定：超过此时长未收到任何数据则断开并重连；0 = 不启用")]
        public float heartbeatTimeout = 0f;

        public NetworkClientConfig Clone()
        {
            return (NetworkClientConfig)MemberwiseClone();
        }
    }
}
