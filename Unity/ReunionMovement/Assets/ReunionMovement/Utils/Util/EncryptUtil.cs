using System;
using System.IO.Compression;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 加密工具类
    /// </summary>
    public static class EncryptUtil
    {
        /// <summary>
        /// 加密字节长度
        /// </summary>
        public const int encryptBytesLength = 64;

        /// <summary>
        /// 用于派生加解密密钥的种子值（可在运行时通过 SetKeySeed 更改）。
        /// 默认使用 AppHash 产生与安装实例相关的密钥，避免硬编码常量被反编译直接利用。
        /// </summary>
        private static uint keySeed;
        private static bool keySeedInitialized;

        private static readonly object keyLock = new object();

        /// <summary>
        /// 设置加密种子。应在游戏启动早期调用（如登录后从服务端下发）。
        /// 传入 0 将使用默认种子。
        /// </summary>
        public static void SetKeySeed(uint seed)
        {
            lock (keyLock)
            {
                keySeed = seed != 0 ? seed : GetDefaultSeed();
                keySeedInitialized = true;
            }
        }

        private static uint GetDefaultSeed()
        {
            // 混合多个来源产生与安装实例相关的默认种子，避免纯常量
            uint seed = 0x5A7B3C9D;
            try
            {
                string id = UnityEngine.Application.identifier ?? "ReunionMovement";
                foreach (char c in id)
                    seed = ((seed << 7) | (seed >> 25)) ^ c;
                seed ^= (uint)UnityEngine.Application.version.GetHashCode();
            }
            catch { /* 降级：使用编译时常量 */ }
            return seed != 0 ? seed : 0x6D8E2F1A;
        }

        private static byte GetOffsetHead()
        {
            EnsureKeySeed();
            return (byte)(((keySeed >> 24) ^ (keySeed >> 16) ^ (keySeed >> 8) ^ keySeed) & 0xFF);
        }

        private static byte GetXOrKey()
        {
            EnsureKeySeed();
            return (byte)(((keySeed >> 16) ^ (keySeed >> 8) ^ 0x5A) & 0xFF);
        }

        private static void EnsureKeySeed()
        {
            if (!keySeedInitialized)
            {
                lock (keyLock)
                {
                    if (!keySeedInitialized)
                    {
                        keySeed = GetDefaultSeed();
                        keySeedInitialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// 缓存的字节数组队列，用于重用字节数组以减少内存分配
        /// </summary>
        private static Queue<byte[]> cachedBytesQueue = new Queue<byte[]>();
        private const int MaxCachedBytesQueueSize = 16;

        /// <summary>
        /// 获取缓存的字节数组，如果没有缓存则创建新的字节数组
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private static byte[] GetCachedBytes(int length)
        {
            if (cachedBytesQueue.Count == 0)
            {
                return new byte[length];
            }

            // 取出队列首项并检查其大小。如果太小，则分配新的缓冲区。
            byte[] bytes = cachedBytesQueue.Dequeue();

            if (bytes.Length < length)
            {
                // 将小缓冲区归还队列（如果队列未满），然后分配合适大小的新缓冲区
                if (cachedBytesQueue.Count < MaxCachedBytesQueueSize)
                {
                    cachedBytesQueue.Enqueue(bytes);
                }
                return new byte[length];
            }

            return bytes;
        }
        /// <summary>
        /// 释放缓存的字节数组（队列上限 16，防止无限增长）
        /// </summary>
        /// <param name="bytes"></param>
        private static void ReleaseCachedBytes(byte[] bytes)
        {
            if (bytes == null) return;
            if (cachedBytesQueue.Count >= MaxCachedBytesQueueSize) return;
            cachedBytesQueue.Enqueue(bytes);
        }

        /// <summary>
        /// 偏移加密
        /// </summary>
        public static void EncryptOffset(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            int newLength = bytes.Length + encryptBytesLength;

            byte[] cachedBytes = GetCachedBytes(newLength);

            //写入额外的头部数据
            byte offsetHead = GetOffsetHead();
            for (int i = 0; i < encryptBytesLength; i++)
            {
                cachedBytes[i] = offsetHead;
            }

            //写入原始数据
            Array.Copy(bytes, 0, cachedBytes, encryptBytesLength, bytes.Length);
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Write))
            {
                fs.Position = 0;
                fs.Write(cachedBytes, 0, newLength);
                fs.SetLength(newLength);
            }

            Array.Clear(cachedBytes, 0, newLength);
            ReleaseCachedBytes(cachedBytes);
        }

        /// <summary>
        /// 使用Stream进行异或加密
        /// </summary>
        public static void EncryptXOr(string filePath)
        {
            byte[] cachedBytes = GetCachedBytes(encryptBytesLength);

            // Open file for read/write so we can read the first bytes and write them back
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                int bytesRead = fs.Read(cachedBytes, 0, Math.Min(cachedBytes.Length, encryptBytesLength));
                if (bytesRead > 0)
                {
                    EncryptXOr(cachedBytes, bytesRead);
                    fs.Position = 0;
                    fs.Write(cachedBytes, 0, bytesRead);
                }
            }

            Array.Clear(cachedBytes, 0, cachedBytes.Length);
            ReleaseCachedBytes(cachedBytes);
        }

        /// <summary>
        /// 使用二进制数据进行异或加密/解密
        /// </summary>
        public static void EncryptXOr(byte[] bytes, int length = encryptBytesLength)
        {
            int actualLength = Math.Min(length, bytes?.Length ?? 0);
            byte xorKey = GetXOrKey();
            for (int i = 0; i < actualLength; i++)
            {
                bytes[i] ^= xorKey;
            }
        }

        #region Base64与字符串转换
        /// <summary>
        /// Base64编码字符串
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string Base64Encode(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(data);
        }
        /// <summary>
        /// Base64解码字符串
        /// </summary>
        /// <param name="base64Text"></param>
        /// <returns></returns>
        public static string Base64Decode(string base64Text)
        {
            byte[] data = Convert.FromBase64String(base64Text);
            return Encoding.UTF8.GetString(data);
        }
        /// <summary>
        /// 判断是否是Base64
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool IsBase64String(string input)
        {
            try
            {
                // 尝试将字符串解码为字节数组
                byte[] bytes = Convert.FromBase64String(input);
                return true; // 如果成功解码，说明是有效的Base64编码
            }
            catch (FormatException)
            {
                return false; // 解码失败，不是有效的Base64编码
            }
        }

        /// <summary>
        /// Base64加密（已废弃，请使用 Base64Encode）
        /// </summary>
        [System.Obsolete("与 Base64Encode 功能完全相同，请使用 Base64Encode")]
        public static string Base64Encrypt(string str)
        {
            return Base64Encode(str);
        }

        /// <summary>
        /// Base64解密（已废弃，请使用 Base64Decode）
        /// </summary>
        [System.Obsolete("与 Base64Decode 功能完全相同，请使用 Base64Decode")]
        public static string Base64Decrypt(string str)
        {
            return Base64Decode(str);
        }
        #endregion

        #region 压缩解压字符串
        /// <summary>
        /// 压缩字符串
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string CompressString(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            using (var compressedStream = new MemoryStream())
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();
                return Convert.ToBase64String(compressedStream.ToArray());
            }
        }
        /// <summary>
        /// 解压缩字符串
        /// </summary>
        /// <param name="compressedText"></param>
        /// <returns></returns>
        public static string DecompressString(string compressedText)
        {
            byte[] data = Convert.FromBase64String(compressedText);
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return Encoding.UTF8.GetString(resultStream.ToArray());
            }
        }
        #endregion
    }
}
