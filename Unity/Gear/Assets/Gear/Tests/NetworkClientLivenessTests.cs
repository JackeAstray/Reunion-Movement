using System;
using NUnit.Framework;
using Gear.Framework.Modules;

namespace Gear.Tests
{
    /// <summary>
    /// 通道存活判定回归：同名通道被 NetworkMgr 替换后，底层通道 IsConnect=false，
    /// NetworkClient 必须把它判定为"已丢失"并触发重连，而不是停在 Connected。
    /// </summary>
    public class NetworkClientLivenessTests
    {
        private sealed class FakeChannel : INetworkClientChannel
        {
            public string Host => "127.0.0.1";
            public int Port => 0;
            public string ChannelName { get; set; } = "test";
            public bool IsOpen => IsConnect;
            public bool IsConnect { get; set; }

            public event Action OnConnected { add { } remove { } }
            public event Action<byte[]> OnDataReceived { add { } remove { } }
            public event Action OnDisconnected { add { } remove { } }
            public event Action<string> OnError { add { } remove { } }

            public void Close() => IsConnect = false;
            public void TickRefresh() { }
            public void Connect(string host, int port) => IsConnect = true;
            public bool SendMessage(byte[] data) => true;
        }

        [Test]
        public void ConnectedWithOpenChannel_IsNotLost()
        {
            var channel = new FakeChannel { IsConnect = true };
            Assert.IsFalse(NetworkClient.IsChannelLost(NetworkClient.ClientState.Connected, channel));
        }

        [Test]
        public void ConnectedWithClosedChannel_IsLost()
        {
            var channel = new FakeChannel { IsConnect = false };
            Assert.IsTrue(NetworkClient.IsChannelLost(NetworkClient.ClientState.Connected, channel));
        }

        [Test]
        public void ConnectedWithNullChannel_IsLost()
        {
            Assert.IsTrue(NetworkClient.IsChannelLost(NetworkClient.ClientState.Connected, null));
        }

        [Test]
        public void NonConnectedStates_AreNotTreatedAsLost()
        {
            var channel = new FakeChannel { IsConnect = false };
            Assert.IsFalse(NetworkClient.IsChannelLost(NetworkClient.ClientState.Disconnected, channel));
            Assert.IsFalse(NetworkClient.IsChannelLost(NetworkClient.ClientState.Connecting, channel));
            Assert.IsFalse(NetworkClient.IsChannelLost(NetworkClient.ClientState.Reconnecting, channel));
            Assert.IsFalse(NetworkClient.IsChannelLost(NetworkClient.ClientState.Closed, channel));
        }
    }
}
