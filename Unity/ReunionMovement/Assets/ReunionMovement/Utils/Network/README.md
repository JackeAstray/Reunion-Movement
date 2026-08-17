# Network 系统使用指南

传输无关的统一网络框架 —— 对接**任何服务器**，功能分层可插拔。

## 目录结构

```text
Utils/Network/
├── Base/           INetworkChannel / INetworkClientChannel / INetworkServerChannel / INetworkMessageCodec
├── Codec/          帧编解码器（MessageId / LengthPrefixed / Passthrough）+ 字节流组装器
│                   + CompressedCodec（Deflate 压缩）/ EncryptedCodec（Encrypt-then-MAC 加密）
├── Serialization/  对象序列化器（默认 JsonUtility）
├── Core/           NetworkClient / NetworkServer / 配置 / 通道工厂 / RPC 帧 / 类型协议
├── TCPChannel/     Telepathy TCP 通道
├── KCPChannel/     kcp2k 可靠 UDP 通道
├── WebSocketChannel/ SimpleWebTransport 通道（ws:// wss://）
├── RawTcpChannel/  原生 TCP 字节流通道（对接任意服务器）
├── Message/        NetworkMessageCodec（旧静态 API）/ NetworkMessageDispatcher
└── UniversalNetworkBehaviour(.Client/.Server).cs   Inspector 可视化组件
```

## 快速开始（Inspector 方式）

1. 空物体挂 `UniversalNetworkBehaviour`；
2. 选择 `mode`（Client/Server）、`transport`（TCP/KCP/WebSocket/RawTCP）、`codec`（协议帧格式）；
3. 播放后点击 Inspector 的「启动」，或代码调用 `StartAsConfigured()`；
4. 发送：`SendClientString` / `SendToAllClientsString`；事件：`ClientConnected`、`ServerDataReceived` 等（Inspector UnityEvent 与 C# 事件均可订阅）。

## 对接任意服务器（核心能力）

「对接任意服务器」= 传输 + 帧格式 两组可插拔参数：

| 服务器类型 | transport | codec | 说明 |
| --- | --- | --- | --- |
| 本框架两端 | TCP / KCP / WebSocket | MessageId | 默认配置 |
| 通用长度前缀 TCP | RawTcp | LengthPrefixed / LengthPrefixedWithId | 最常见通用帧格式 `[4B长度][负载]` |
| 自定义私有协议 | RawTcp | Passthrough | 原始字节流，自行解析 |
| 浏览器/网关 | WebSocket | MessageId / Passthrough | ws:// wss://（可带路径） |

自研协议：实现 `INetworkMessageCodec`（流式需实现 `TryGetFrameLength`），
`config.codec` 改为 `NetworkCodecType` 之外时可自行注入 codec 实例。

## 高级 API（代码方式）

### 客户端 `NetworkClient`

```csharp
var cfg = new NetworkClientConfig
{
    transport = NetworkTransportType.RawTcp,   // 对接任意 TCP 服务器
    host = "example.com", port = 9000,
    codec = NetworkCodecType.LengthPrefixed,   // 帧格式
    autoReconnect = true,
    reconnectBaseDelay = 1f, reconnectBackoffFactor = 2f, reconnectMaxDelay = 30f, // 指数退避+抖动
    enableHeartbeat = true, heartbeatInterval = 5f, heartbeatTimeout = 15f,        // 心跳+死链检测
};
var client = new NetworkClient(cfg);
client.OnStateChanged += (prev, next) => { };
client.OnConnected += () => { };
client.OnMessage += (messageId, payload) => { };       // 解码后的消息
client.Dispatcher.RegisterHandler(1, payload => { });  // 按 ID 分发
client.Connect();                                      // 之后每帧 client.Tick()
// 或自动驱动：client.DriveAsync(destroyCancellationToken).Forget();
```

请求/响应（带超时，服务端注册处理器）：

```csharp
client.RegisterObjectMessage<LoginRequest>(10);
var resp = await client.RequestAsync<LoginRequest, LoginResponse>(
    new LoginRequest { user = "a" }, TimeSpan.FromSeconds(5), ct);
```

可靠发送 / 背压 / 统计：

```csharp
// 可靠消息：服务端回 ACK，超时自动重发；断线时可选保留待连接后补发（persistOnDisconnect）
bool acked = await client.SendReliableAsync(1, payload,
    TimeSpan.FromSeconds(5), maxRetries: 5, persistOnDisconnect: true);

// 背压：SendDetailed 区分成功 / 未连接 / 被拒，避免静默丢包
SendResult result = client.SendDetailed(2, payload);   // Ok / NotConnected / Rejected

// 流量与延迟统计（排障）
long sent = client.BytesSent, received = client.BytesReceived;  // 累计收发字节
float rtt  = client.LastRttMs;                                  // 最近一次心跳往返毫秒
```

### 服务端 `NetworkServer`

```csharp
var server = new NetworkServer(new NetworkServerConfig
{
    transport = NetworkTransportType.Tcp, port = 9000,
    codec = NetworkCodecType.MessageId,
});
server.OnClientConnected += (id, address) => { };
server.OnMessage += (id, messageId, payload) => { };
server.GetDispatcher(id).RegisterHandler(1, payload => { }); // 每连接独立分发器
server.RegisterRequestHandler(10, (connectionId, requestBytes) => responseBytes); // RPC
server.Broadcast(bytes); server.BroadcastExcept(exceptId, bytes); server.Send(id, bytes);
server.Start(); // 之后每帧 server.Tick()
```

强类型对象消息：`RegisterObjectMessage<T>(id)` + `RegisterObjectHandler<T>` /
`SendObject<T>` / `BroadcastObject<T>`（默认 JSON，可替换 `INetworkSerializer`）。

### 状态机

`Disconnected → Connecting → Connected`；断开后 `→ Reconnecting → Connecting → ...`；
重连次数耗尽或 `Disconnect()` 后回到 `Disconnected`；`Close()` 后 `Closed`（不可复用）。

## 压缩 / 加密（可选增强）

**压缩**：内置 `NetworkCodecType` 枚举已支持，直接配置即可：

```csharp
var cfg = new NetworkClientConfig
{
    transport = NetworkTransportType.Tcp,
    codec = NetworkCodecType.CompressedMessageId,           // [2B ID][Deflate 压缩负载]
    // codec = NetworkCodecType.CompressedLengthPrefixedWithId, // [4B 长度][2B ID][压缩负载]
};
```

**加密**：`EncryptedCodec` 包装底层编解码器，对负载做 AES-256-CBC 加密 + HMAC-SHA256 完整性校验
（Encrypt-then-MAC，防篡改 / 防填充 oracle）：

```csharp
// 两端密钥必须一致（16/24/32 字节）
var codec = EncryptedCodec.Wrap(NetworkCodecFactory.Create(NetworkCodecType.MessageId), keyBytes32);
```

> 说明：`NetworkClient` / `NetworkServer` 当前通过 `NetworkCodecType` 枚举构建编解码栈（内置压缩类型可直接用）；
> `EncryptedCodec` 为独立可用的编解码器（帧格式 `[1B 版本][16B IV][密文][32B MAC]`），
> 用于底层 `INetworkChannel` 直连场景，或自行扩展 `NetworkCodecFactory` 接入客户端/服务端。
> 加解密有 CPU 成本，建议仅对敏感通道整链路启用。

## 兼容性说明

- `NetworkMessageCodec` 静态类与旧 `INetworkClientChannel` 用法保持不变；
- `UniversalNetworkBehaviour` 公共字段与事件契约不变，新增 `codec` 字段与 `RawTCP` 传输；
- 默认 `codec = MessageId`，线上帧格式与旧版一致（`[2B ID][负载]`，ID=0 即旧版裸数据）；
- RPC 占用保留消息 ID `0xFFFE`（请求）/ `0xFFFF`（响应），业务消息请避开；
- `RawTcp` 基于原生 Socket，仅支持桌面/移动等原生平台（WebGL 请使用 WebSocket 传输）。
