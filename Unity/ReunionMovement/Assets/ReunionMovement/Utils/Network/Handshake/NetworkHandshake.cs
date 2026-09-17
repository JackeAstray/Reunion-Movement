using System;
using System.Security.Cryptography;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 加密握手（Encrypted Handshake）工具 —— 客户端/服务端在明文阶段交换随机数，
    /// 以"主密钥 + 双随机数"派生出每连接唯一的会话密钥，随后切换为 AES-256-CBC+HMAC 加密。
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
    }
}
