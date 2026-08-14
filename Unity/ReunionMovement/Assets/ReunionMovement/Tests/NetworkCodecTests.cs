using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ReunionMovement.Common.Util;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// 网络编解码层纯逻辑测试（EditMode，无场景/网络依赖）。
    /// 覆盖：编解码器往返 / 流式组装器分片重组 / RPC 帧 / 类型协议 / JSON 序列化。
    /// </summary>
    public class NetworkCodecTests
    {
        static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        [Test]
        public void MessageIdCodec_RoundTrip()
        {
            var codec = MessageIdCodec.Instance;
            var payload = Bytes("hello");
            var frame = codec.Encode(7, payload);

            Assert.IsTrue(codec.TryDecode(frame, 0, frame.Length, out var id, out var seg));
            Assert.AreEqual(7, id);
            CollectionAssert.AreEqual(payload, seg.ToArray());
        }

        [Test]
        public void MessageIdCodec_TooShort_Fails()
        {
            var codec = MessageIdCodec.Instance;
            Assert.IsFalse(codec.TryDecode(new byte[] { 0x01 }, 0, 1, out _, out _));
        }

        [Test]
        public void LengthPrefixedCodec_RoundTrip_WithId()
        {
            var codec = new LengthPrefixedCodec(includeMessageId: true);
            var payload = Bytes("payload");
            var frame = codec.Encode(42, payload);

            Assert.IsTrue(codec.TryDecode(frame, 0, frame.Length, out var id, out var seg));
            Assert.AreEqual(42, id);
            CollectionAssert.AreEqual(payload, seg.ToArray());
        }

        [Test]
        public void LengthPrefixedCodec_RoundTrip_WithoutId()
        {
            var codec = new LengthPrefixedCodec(includeMessageId: false);
            var payload = Bytes("payload");
            var frame = codec.Encode(99, payload);

            Assert.IsTrue(codec.TryDecode(frame, 0, frame.Length, out var id, out var seg));
            Assert.AreEqual(0, id, "无 ID 变体的消息 ID 恒为 0");
            CollectionAssert.AreEqual(payload, seg.ToArray());
        }

        [Test]
        public void Assembler_SplitFrame_AcrossChunks()
        {
            var codec = new LengthPrefixedCodec(includeMessageId: true);
            var assembler = new NetworkStreamAssembler(codec);
            var payload = Bytes("split payload");
            var frame = codec.Encode(5, payload);

            var received = new List<(ushort id, byte[] payload)>();
            Action<ushort, ArraySegment<byte>, ArraySegment<byte>> onFrame =
                (id, f, p) => received.Add((id, p.ToArray()));

            // 逐字节喂入：模拟 TCP 流任意分片
            for (int i = 0; i < frame.Length; i++)
            {
                assembler.Feed(new byte[] { frame[i] }, onFrame);
            }

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(5, received[0].id);
            CollectionAssert.AreEqual(payload, received[0].payload);
            Assert.AreEqual(0, assembler.BufferedBytes, "完整帧解析后缓冲应清空");
        }

        [Test]
        public void Assembler_MultipleFrames_InOneChunk()
        {
            var codec = new LengthPrefixedCodec(includeMessageId: true);
            var assembler = new NetworkStreamAssembler(codec);
            var f1 = codec.Encode(1, Bytes("one"));
            var f2 = codec.Encode(2, Bytes("two"));

            var merged = new byte[f1.Length + f2.Length];
            Buffer.BlockCopy(f1, 0, merged, 0, f1.Length);
            Buffer.BlockCopy(f2, 0, merged, f1.Length, f2.Length);

            var received = new List<(ushort id, byte[] payload)>();
            assembler.Feed(merged, (id, f, p) => received.Add((id, p.ToArray())));

            Assert.AreEqual(2, received.Count);
            Assert.AreEqual(1, received[0].id);
            Assert.AreEqual(2, received[1].id);
            CollectionAssert.AreEqual(Bytes("one"), received[0].payload);
            CollectionAssert.AreEqual(Bytes("two"), received[1].payload);
        }

        [Test]
        public void Assembler_DatagramMode_EachChunkIsFrame()
        {
            var codec = PassthroughCodec.Instance; // SupportsStreamFraming = false
            var assembler = new NetworkStreamAssembler(codec);
            var received = new List<byte[]>();

            assembler.Feed(Bytes("chunk-a"), (id, f, p) => received.Add(p.ToArray()));
            assembler.Feed(Bytes("chunk-b"), (id, f, p) => received.Add(p.ToArray()));

            Assert.AreEqual(2, received.Count);
            CollectionAssert.AreEqual(Bytes("chunk-a"), received[0]);
            CollectionAssert.AreEqual(Bytes("chunk-b"), received[1]);
        }

        [Test]
        public void RpcFrames_RequestRoundTrip()
        {
            var payload = Bytes("request");
            var frame = NetworkRpcFrames.EncodeRequest(12345, 77, payload);

            Assert.IsTrue(NetworkRpcFrames.TryDecodeRequest(new ArraySegment<byte>(frame), out var corr, out var target, out var seg));
            Assert.AreEqual(12345, corr);
            Assert.AreEqual(77, target);
            CollectionAssert.AreEqual(payload, seg.ToArray());
        }

        [Test]
        public void RpcFrames_ResponseRoundTrip()
        {
            var payload = Bytes("response");
            var frame = NetworkRpcFrames.EncodeResponse(54321, payload);

            Assert.IsTrue(NetworkRpcFrames.TryDecodeResponse(new ArraySegment<byte>(frame), out var corr, out var seg));
            Assert.AreEqual(54321, corr);
            CollectionAssert.AreEqual(payload, seg.ToArray());
        }

        [Test]
        public void TypedProtocol_RegisterAndLookup()
        {
            var protocol = new NetworkTypedProtocol();
            Assert.IsTrue(protocol.Register<string>(10));
            Assert.IsFalse(protocol.Register<int>(10), "同一 ID 不能绑定两个类型");

            Assert.IsTrue(protocol.TryGetId(typeof(string), out var id));
            Assert.AreEqual(10, id);
            Assert.IsTrue(protocol.TryGetType(10, out var type));
            Assert.AreEqual(typeof(string), type);
        }

        [Serializable]
        class TestMessage
        {
            public int number;
            public string text;
        }

        [Test]
        public void JsonSerializer_RoundTrip()
        {
            var serializer = JsonNetSerializer.Instance;
            var data = serializer.Serialize(new TestMessage { number = 3, text = "abc" });
            var obj = serializer.Deserialize<TestMessage>(data);
            Assert.AreEqual(3, obj.number);
            Assert.AreEqual("abc", obj.text);
        }
    }
}
