using System;
using NUnit.Framework;
using ReunionMovement.Common.Util;

namespace ReunionMovement.Tests
{
    /// <summary>
    /// 加密握手纯逻辑测试（EditMode，无场景/网络依赖）。
    /// 覆盖：会话密钥两端一致性 / 随机数敏感性 / 参数校验 / 加密 codec 往返与密钥错误拒绝。
    /// </summary>
    public class NetworkHandshakeTests
    {
        static byte[] Key32()
        {
            var k = new byte[32];
            for (int i = 0; i < k.Length; i++) k[i] = (byte)(i * 3 + 1);
            return k;
        }

        [Test]
        public void DeriveSessionKey_BothSidesMatch()
        {
            var master = Key32();
            var serverNonce = NetworkHandshake.GenerateNonce();
            var clientNonce = NetworkHandshake.GenerateNonce();

            var serverKey = NetworkHandshake.DeriveSessionKey(master, serverNonce, clientNonce);
            var clientKey = NetworkHandshake.DeriveSessionKey(master, serverNonce, clientNonce);

            CollectionAssert.AreEqual(serverKey, clientKey, "两端以相同输入派生应得到相同会话密钥");
            Assert.AreEqual(32, serverKey.Length);
        }

        [Test]
        public void DeriveSessionKey_DifferentClientNonce_DifferentKey()
        {
            var master = Key32();
            var serverNonce = NetworkHandshake.GenerateNonce();

            var k1 = NetworkHandshake.DeriveSessionKey(master, serverNonce, NetworkHandshake.GenerateNonce());
            var k2 = NetworkHandshake.DeriveSessionKey(master, serverNonce, NetworkHandshake.GenerateNonce());

            CollectionAssert.AreNotEqual(k1, k2, "客户端随机数不同应派生不同会话密钥（防重放）");
        }

        [Test]
        public void DeriveSessionKey_InvalidInputs_Throws()
        {
            var master = Key32();
            Assert.Throws<ArgumentException>(() =>
                NetworkHandshake.DeriveSessionKey(master, new byte[15], new byte[16]), "服务端随机数必须为 16 字节");
            Assert.Throws<ArgumentException>(() =>
                NetworkHandshake.DeriveSessionKey(master, new byte[16], new byte[17]), "客户端随机数必须为 16 字节");
            Assert.Throws<ArgumentException>(() =>
                NetworkHandshake.DeriveSessionKey(Array.Empty<byte>(), new byte[16], new byte[16]), "主密钥不能为空");
        }

        [Test]
        public void EncryptedCodec_RoundTrip_WithKey()
        {
            var codec = EncryptedCodec.Wrap(MessageIdCodec.Instance, Key32());
            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var frame = codec.Encode(9, payload);

            Assert.IsTrue(codec.TryDecode(frame, 0, frame.Length, out var id, out var seg));
            Assert.AreEqual(9, id);
            CollectionAssert.AreEqual(payload, seg.ToArray());
        }

        [Test]
        public void EncryptedCodec_WrongKey_FailsDecode()
        {
            var codecA = EncryptedCodec.Wrap(MessageIdCodec.Instance, Key32());
            var codecB = EncryptedCodec.Wrap(MessageIdCodec.Instance, new byte[32]); // 全零密钥

            var frame = codecA.Encode(1, new byte[] { 42 });

            Assert.IsFalse(codecB.TryDecode(frame, 0, frame.Length, out _, out _), "错误密钥应无法解密/通过 MAC 校验");
        }

        [Test]
        public void EncryptedCodec_DirectionBound_RejectsReflectedFrame()
        {
            var key = Key32();
            var client = EncryptedCodec.WrapClient(MessageIdCodec.Instance, key);
            var server = EncryptedCodec.WrapServer(MessageIdCodec.Instance, key);

            // 正常双向通信仍可用（两端方向密钥互为镜像）
            var c2s = client.Encode(21, new byte[] { 1, 2, 3 });
            Assert.IsTrue(server.TryDecode(c2s, 0, c2s.Length, out var id1, out var p1), "client→server 应可解密");
            Assert.AreEqual(21, id1);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, p1.ToArray());

            var s2c = server.Encode(22, new byte[] { 4, 5, 6 });
            Assert.IsTrue(client.TryDecode(s2c, 0, s2c.Length, out var id2, out var p2), "server→client 应可解密");
            Assert.AreEqual(22, id2);

            // 反射：把本端自己发出的帧原样送回本端 —— 必须被拒绝（否则可伪造 ACK / 把出站帧当输入执行）
            Assert.IsFalse(client.TryDecode(c2s, 0, c2s.Length, out _, out _), "客户端不得接受自己发出的帧");
            Assert.IsFalse(server.TryDecode(s2c, 0, s2c.Length, out _, out _), "服务端不得接受自己发出的帧");
        }

        [Test]
        public void EncryptedCodec_TamperedMessageId_IsRejected()
        {
            // 内层编解码器写入的 messageId 位于加密负载之外：必须纳入 MAC，
            // 否则中间人可在 MAC 仍有效的前提下翻转消息 ID（业务帧被当作系统帧重派发）
            var codec = EncryptedCodec.Wrap(MessageIdCodec.Instance, Key32());
            var frame = codec.Encode(0x1234, new byte[] { 9 });

            var tampered = (byte[])frame.Clone();
            tampered[0] ^= 0x01; // 翻转 messageId 最低位

            Assert.IsFalse(codec.TryDecode(tampered, 0, tampered.Length, out _, out _),
                "篡改 messageId 后必须因 MAC 不符被拒绝");
        }

        [Test]
        public void ReservedHandshakeIds_AreReserved()
        {
            Assert.IsTrue(NetworkConstants.IsReservedMessageId(NetworkConstants.ReservedHandshakeServerHello));
            Assert.IsTrue(NetworkConstants.IsReservedMessageId(NetworkConstants.ReservedHandshakeClientHello));
            Assert.IsFalse(NetworkConstants.IsReservedMessageId(NetworkConstants.DefaultMessageId));
        }

        [Test]
        public void ClientProof_OnlyVerifiesWithSameKeyAndNonces()
        {
            var master = Key32();
            var serverNonce = NetworkHandshake.GenerateNonce();
            var clientNonce = NetworkHandshake.GenerateNonce();

            var proof = NetworkHandshake.ComputeClientProof(master, serverNonce, clientNonce);
            Assert.AreEqual(NetworkHandshake.ProofLength, proof.Length);

            Assert.IsTrue(NetworkHandshake.ProofEquals(
                NetworkHandshake.ComputeClientProof(master, serverNonce, clientNonce), proof));

            Assert.IsFalse(NetworkHandshake.ProofEquals(
                NetworkHandshake.ComputeClientProof(master, serverNonce, NetworkHandshake.GenerateNonce()), proof),
                "换客户端随机数后证明必须不同（防重放）");
            Assert.IsFalse(NetworkHandshake.ProofEquals(
                NetworkHandshake.ComputeClientProof(new byte[32], serverNonce, clientNonce), proof),
                "换主密钥后证明必须不同");
            Assert.IsFalse(NetworkHandshake.ProofEquals(
                NetworkHandshake.ComputeServerProof(master, serverNonce), proof),
                "客户端/服务端证明必须域隔离，不可互相替换");
        }

        [Test]
        public void HelloPayload_RoundTrip_AndRejectsLegacyNonceOnlyLength()
        {
            var master = Key32();
            var nonce = NetworkHandshake.GenerateNonce();
            var proof = NetworkHandshake.ComputeServerProof(master, nonce);
            var payload = NetworkHandshake.BuildHello(nonce, proof);

            Assert.AreEqual(NetworkHandshake.HelloPayloadLength, payload.Length);

            Assert.IsTrue(NetworkHandshake.TryParseHello(new ArraySegment<byte>(payload), out var parsedNonce, out var parsedProof));
            CollectionAssert.AreEqual(nonce, parsedNonce);
            CollectionAssert.AreEqual(proof, parsedProof);

            // 旧版"只有 16 字节随机数"的 ClientHello/ServerHello 必须被拒绝：
            // 那种格式没有持有证明，等于任何 16 字节都能触发握手完成
            Assert.IsFalse(NetworkHandshake.TryParseHello(
                new ArraySegment<byte>(new byte[NetworkHandshake.NonceLength]), out _, out _));
        }
    }
}
