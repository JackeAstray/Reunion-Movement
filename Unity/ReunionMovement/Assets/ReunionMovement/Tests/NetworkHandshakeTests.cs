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
        public void ReservedHandshakeIds_AreReserved()
        {
            Assert.IsTrue(NetworkConstants.IsReservedMessageId(NetworkConstants.ReservedHandshakeServerHello));
            Assert.IsTrue(NetworkConstants.IsReservedMessageId(NetworkConstants.ReservedHandshakeClientHello));
            Assert.IsFalse(NetworkConstants.IsReservedMessageId(NetworkConstants.DefaultMessageId));
        }
    }
}
