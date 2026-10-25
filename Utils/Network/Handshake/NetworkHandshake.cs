using System;
using System.Security.Cryptography;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 加密握手（Encrypted Handshake）工具 —— 客户端/服务端在明文阶段交换随机数
    /// 与"持有主密钥"证明，以"主密钥 + 双随机数"派生出每连接唯一的会话密钥，
    /// 随后切换为 AES-256-CBC+HMAC 加密。
    ///
    /// 安全说明：若主密钥仅硬编码在客户端二进制（可被反编译提取），本机制提供的是
    /// 每连接唯一密钥 + 防重放/防离线分析加固（明文抓包无法还原会话内容）。
    /// 生产环境应通过 HTTPS 登录接口向客户端下发主密钥（NetworkServer/NetworkClient.SetHandshakeMasterKey），
    /// 方可构成真正的加密信任边界。
    /// </summary>
    public static class NetworkHandshake
    {
        /// <summary>随机数长度（字节）：客户端/服务端各 16B，参与会话密钥派生</summary>
        public const int NonceLength = 16;

        /// <summary>会话密钥长度（字节）：AES-256 主密钥派生输入</summary>
        public const int SessionKeyLength = 32;

        /// <summary>生成加密安全的随机数（RandomNumberGenerator，不可预测）。</summary>
        public static byte[] GenerateNonce()
        {
            byte[] nonce = new byte[NonceLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce); // 实例方法（.NET Standard 2.1 无静态 GetBytes(int) 重载）
            }
            return nonce;
        }

        /// <summary>
        /// 由主密钥 + 双随机数派生每连接会话密钥（HMAC-SHA256，固定派生顺序保证两端结果一致）。
        /// 派生顺序固定为 [label][serverNonce][clientNonce]，服务端与客户端均按此顺序计算。
        /// </summary>
        public static byte[] DeriveSessionKey(byte[] masterKey, byte[] serverNonce, byte[] clientNonce)
        {
            if (masterKey == null || masterKey.Length == 0)
            {
                throw new ArgumentException("主密钥不能为空", nameof(masterKey));
            }
            if (serverNonce == null || serverNonce.Length != NonceLength)
            {
                throw new ArgumentException($"服务端随机数必须为 {NonceLength} 字节", nameof(serverNonce));
            }
            if (clientNonce == null || clientNonce.Length != NonceLength)
            {
                throw new ArgumentException($"客户端随机数必须为 {NonceLength} 字节", nameof(clientNonce));
            }

            byte[] input = new byte[1 + NonceLength * 2];
            input[0] = 0x53; // label 'S'：会话密钥派生域（与 MAC/加密域隔离）
            Buffer.BlockCopy(serverNonce, 0, input, 1, NonceLength);
            Buffer.BlockCopy(clientNonce, 0, input, 1 + NonceLength, NonceLength);
            using (var hmac = new HMACSHA256(masterKey))
            {
                return hmac.ComputeHash(input); // 32 字节
            }
        }

        // ===== 持有证明（Proof of Key Possession）=====
        //
        // 背景：仅交换随机数并不能证明对端真的持有主密钥 —— 任意 16 字节的 ClientHello
        // 都足以让服务端把连接标记为 Secured 并触发 OnClientSecured（业务层把它当作"已认证"信号）。
        // 因此在随机数之后附带一段 HMAC(masterKey, ...) 作为持有证明。
        //
        // 局限（务必了解）：本机制使用对称预共享主密钥，客户端与服务端持有同一把密钥，
        // 因此它只能证明"对端知道主密钥"，**不能**在密钥已泄露给客户端的情况下防止
        // 中间人转发/冒充。要构成真正的服务端认证，需要非对称签名或每客户端独立凭据（或 TLS）。

        /// <summary>持有证明长度（HMAC-SHA256 输出）</summary>
        public const int ProofLength = 32;

        /// <summary>握手负载长度：[随机数 16B][持有证明 32B]</summary>
        public const int HelloPayloadLength = NonceLength + ProofLength;

        // 证明域标签：与会话密钥派生的 0x53 明确区分，且客户端/服务端各用一个标签，
        // 保证两种证明互不可替换（也保证证明值不会等于会话密钥本身）
        const byte ClientProofLabel = 0x82;
        const byte ServerProofLabel = 0x81;

        /// <summary>
        /// 客户端持有证明：HMAC-SHA256(masterKey, [0x82][serverNonce][clientNonce])。
        /// 绑定服务端随机数，使抓取到的 ClientHello 无法重放到新会话。
        /// </summary>
        public static byte[] ComputeClientProof(byte[] masterKey, byte[] serverNonce, byte[] clientNonce)
        {
            return ComputeProof(masterKey, ClientProofLabel, serverNonce, clientNonce);
        }

        /// <summary>
        /// 服务端持有证明：HMAC-SHA256(masterKey, [0x81][serverNonce])。
        /// 服务端下发 ServerHello 时客户端随机数尚不存在，故只绑定服务端随机数。
        /// </summary>
        public static byte[] ComputeServerProof(byte[] masterKey, byte[] serverNonce)
        {
            return ComputeProof(masterKey, ServerProofLabel, serverNonce);
        }

        static byte[] ComputeProof(byte[] masterKey, byte label, params byte[][] nonces)
        {
            if (masterKey == null || masterKey.Length == 0)
            {
                throw new ArgumentException("主密钥不能为空", nameof(masterKey));
            }
            int total = 1;
            if (nonces != null)
            {
                foreach (var n in nonces)
                {
                    if (n == null || n.Length != NonceLength)
                    {
                        throw new ArgumentException($"随机数必须为 {NonceLength} 字节", nameof(nonces));
                    }
                    total += n.Length;
                }
            }

            var input = new byte[total];
            input[0] = label;
            int offset = 1;
            if (nonces != null)
            {
                foreach (var n in nonces)
                {
                    Buffer.BlockCopy(n, 0, input, offset, n.Length);
                    offset += n.Length;
                }
            }
            using (var hmac = new HMACSHA256(masterKey))
            {
                return hmac.ComputeHash(input);
            }
        }

        /// <summary>构造握手负载：[随机数][持有证明]</summary>
        public static byte[] BuildHello(byte[] nonce, byte[] proof)
        {
            if (nonce == null || nonce.Length != NonceLength)
            {
                throw new ArgumentException($"随机数必须为 {NonceLength} 字节", nameof(nonce));
            }
            if (proof == null || proof.Length != ProofLength)
            {
                throw new ArgumentException($"持有证明必须为 {ProofLength} 字节", nameof(proof));
            }
            var payload = new byte[HelloPayloadLength];
            Buffer.BlockCopy(nonce, 0, payload, 0, NonceLength);
            Buffer.BlockCopy(proof, 0, payload, NonceLength, ProofLength);
            return payload;
        }

        /// <summary>
        /// 解析握手负载。长度不符（例如旧版仅 16 字节随机数的 ClientHello/ServerHello）返回 false，
        /// 调用方应断开连接 —— 旧格式没有持有证明，等同于任何人都能触发握手完成。
        /// </summary>
        public static bool TryParseHello(ArraySegment<byte> payload, out byte[] nonce, out byte[] proof)
        {
            nonce = null;
            proof = null;
            if (payload.Array == null || payload.Count != HelloPayloadLength)
            {
                return false;
            }
            nonce = new byte[NonceLength];
            proof = new byte[ProofLength];
            Buffer.BlockCopy(payload.Array, payload.Offset, nonce, 0, NonceLength);
            Buffer.BlockCopy(payload.Array, payload.Offset + NonceLength, proof, 0, ProofLength);
            return true;
        }

        /// <summary>恒定时间比较持有证明（防时序侧信道）；长度不一致直接返回 false</summary>
        public static bool ProofEquals(byte[] expected, byte[] actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length)
            {
                return false;
            }
            int diff = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                diff |= expected[i] ^ actual[i];
            }
            return diff == 0;
        }
    }
}
