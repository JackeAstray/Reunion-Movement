using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Gear.Framework.Modules;

namespace Gear.Tests.PlayMode
{
    /// <summary>
    /// 真实 TCP 回环：覆盖 RPC 成功往返与"未注册处理器 → NetworkRpcException"。
    /// 不启动 GameEngine，直接驱动 NetworkServer/NetworkClient 的 Tick。
    /// </summary>
    public class NetworkLoopbackPlayModeTests
    {
        private NetworkServer server;
        private NetworkClient client;
        private int port;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            server = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                port = 17890 + attempt;
                var candidate = new NetworkServer(new NetworkServerConfig
                {
                    transport = NetworkTransportType.Tcp,
                    port = port,
                    codec = NetworkCodecType.MessageId,
                });
                if (candidate.Start())
                {
                    server = candidate;
                    break;
                }
                candidate.Stop();
            }
            Assert.IsNotNull(server, "无法在 17890-17894 启动 TCP 服务端");

            client = new NetworkClient(new NetworkClientConfig
            {
                transport = NetworkTransportType.Tcp,
                host = "127.0.0.1",
                port = port,
                codec = NetworkCodecType.MessageId,
                autoReconnect = false,
                connectTimeout = 5f,
            });
            client.Connect();

            float deadline = UnityEngine.Time.realtimeSinceStartup + 5f;
            while (!client.IsConnected)
            {
                if (UnityEngine.Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("TCP 回环连接超时");
                }
                server.Tick();
                client.Tick();
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            client?.Close();
            server?.Stop();
            client = null;
            server = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rpc_RoundTrip_OverTcpLoopback()
        {
            server.RegisterRequestHandler(10, (connectionId, payload) =>
                new byte[] { payload[0], 2 });

            var task = client.RequestAsync(10, new byte[] { 1 }, TimeSpan.FromSeconds(3));
            while (!task.GetAwaiter().IsCompleted)
            {
                server.Tick();
                client.Tick();
                yield return null;
            }

            byte[] response = task.GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new byte[] { 1, 2 }, response);
        }

        [UnityTest]
        public IEnumerator Rpc_UnregisteredHandler_ThrowsNetworkRpcException()
        {
            var task = client.RequestAsync(99, Array.Empty<byte>(), TimeSpan.FromSeconds(3));
            while (!task.GetAwaiter().IsCompleted)
            {
                server.Tick();
                client.Tick();
                yield return null;
            }

            bool threw = false;
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (NetworkRpcException ex)
            {
                threw = true;
                Assert.AreEqual(NetworkRpcFrames.ErrorCodeHandlerMissing, ex.Code);
            }

            Assert.IsTrue(threw, "未注册处理器应抛 NetworkRpcException，而不是空成功/超时");
        }
    }
}
