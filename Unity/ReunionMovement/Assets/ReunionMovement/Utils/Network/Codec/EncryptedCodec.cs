using System;
using System.IO;
using System.Security.Cryptography;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 加密编解码器包装器 —— 包装底层编解码器，对负载进行 AES-256-CBC 加密 + HMAC-SHA256 完整性保护
    /// （Encrypt-then-MAC）。
    /// 帧格式：[1B 版本][16B 随机 IV][AES-CBC 密文（PKCS7）][32B HMAC-SHA256(版本+IV+密文)]。
    /// 与纯 CBC 相比：篡改/填充 oracle 探测在解密前即被 MAC 校验拒绝；每包随机 IV 避免同明文同密文。
    /// 解密失败（密钥不符/数据损坏/被篡改）时 TryDecode 返回 false，不影响主循环。
    ///
    /// 用法（两端密钥必须一致）：
    ///   var codec = EncryptedCodec.Wrap(NetworkCodecFactory.Create(NetworkCodecType.MessageId), keyBytes32);
    /// 注意：加解密有 CPU 成本（每包创建 Aes/HMAC 实例，收发线程独立互不共享），
    /// 建议仅对敏感通道整链路启用，而非高频状态同步。
    /// </summary>
    public sealed class EncryptedCodec : INetworkMessageCodec
    {
        const byte FormatVersion = 1;
        const int IvLength = 16;
        const int MacLength = 32;

        readonly INetworkMessageCodec inner;
        readonly byte[] encKey;
        readonly byte[] macKey;

        public bool SupportsStreamFraming => inner.SupportsStreamFraming;

        /// <summary>
        /// 包装底层编解码器。key 长度必须为 16/24/32 字节（AES-128/192/256）。
        /// </summary>
        public static EncryptedCodec Wrap(INetworkMessageCodec inner, byte[] key)
        {
            return new EncryptedCodec(inner, key);
        }

        public EncryptedCodec(INetworkMessageCodec inner, byte[] key)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (key == null || (key.Length != 16 && key.Length != 24 && key.Length != 32))
            {
                throw new ArgumentException("AES 密钥长度必须为 16/24/32 字节", nameof(key));
            }
            // 由主密钥派生加密密钥与 MAC 密钥（HMAC-SHA256 标签分离），避免同密钥双重用途
            using (var sha = SHA256.Create())
            {
                encKey = sha.ComputeHash(Combine(key, new byte[] { 0x01 }));
                macKey = sha.ComputeHash(Combine(key, new byte[] { 0x02 }));
            }
        }

        static byte[] Combine(byte[] a, byte[] b)
        {
            var r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        public byte[] Encode(ushort messageId, byte[] payload)
        {
            if (payload == null) payload = Array.Empty<byte>();
            var encrypted = Encrypt(payload);
            return inner.Encode(messageId, encrypted);
        }

        public bool TryGetFrameLength(byte[] buffer, int offset, int count, out int frameLength)
        {
            return inner.TryGetFrameLength(buffer, offset, count, out frameLength);
        }

        public bool TryDecode(byte[] frame, int offset, int length, out ushort messageId, out ArraySegment<byte> payload)
        {
            if (!inner.TryDecode(frame, offset, length, out messageId, out var encrypted))
            {
                payload = default;
                return false;
            }
            try
            {
                payload = new ArraySegment<byte>(Decrypt(encrypted));
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[EncryptedCodec] 负载解密/完整性校验失败（密钥不符或数据被篡改）: {0}", ex.Message);
                payload = default;
                return false;
            }
        }

        byte[] Encrypt(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = encKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV(); // 每包随机 IV：同明文产生不同密文

            byte[] cipher;
            using (var encryptor = aes.CreateEncryptor())
            {
                cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
            }

            var result = new byte[1 + IvLength + cipher.Length + MacLength];
            result[0] = FormatVersion;
            Buffer.BlockCopy(aes.IV, 0, result, 1, IvLength);
            Buffer.BlockCopy(cipher, 0, result, 1 + IvLength, cipher.Length);
            using (var hmac = new HMACSHA256(macKey))
            {
                var mac = hmac.ComputeHash(result, 0, 1 + IvLength + cipher.Length);
                Buffer.BlockCopy(mac, 0, result, 1 + IvLength + cipher.Length, MacLength);
            }
            return result;
        }

        byte[] Decrypt(ArraySegment<byte> data)
        {
            if (data.Count < 1 + IvLength + 16 + MacLength)
            {
                throw new InvalidDataException("加密帧长度不足");
            }
            var arr = data.Array;
            int off = data.Offset;
            int cipherLen = data.Count - 1 - IvLength - MacLength;

            // Encrypt-then-MAC：先校验完整性（常量时间比较），任何篡改在解密前即被拒绝，
            // 堵住填充 oracle 探测路径
            using (var hmac = new HMACSHA256(macKey))
            {
                var expected = hmac.ComputeHash(arr, off, 1 + IvLength + cipherLen);
                int macOff = off + 1 + IvLength + cipherLen;
                if (!FixedTimeEquals(expected, arr, macOff, MacLength))
                {
                    throw new InvalidDataException("MAC 校验失败（数据被篡改）");
                }
            }

            using var aes = Aes.Create();
            aes.Key = encKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var iv = new byte[IvLength];
            Buffer.BlockCopy(arr, off + 1, iv, 0, IvLength);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(arr, off + 1 + IvLength, cipherLen);
        }

        /// <summary>常量时间字节比较（避免 MAC 校验的计时侧信道）</summary>
        static bool FixedTimeEquals(byte[] expected, byte[] actual, int actualOffset, int count)
        {
            int diff = expected.Length ^ count;
            for (int i = 0; i < count; i++)
            {
                diff |= expected[i] ^ actual[actualOffset + i];
            }
            return diff == 0;
        }
    }
}
