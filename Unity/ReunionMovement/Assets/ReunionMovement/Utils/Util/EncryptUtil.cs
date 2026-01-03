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
        /// 偏移加密的头部字节值
        /// </summary>
        private const byte encryptOffsetHead = 64;

        /// <summary>
        /// 异或加密Key
        /// </summary>
        private const byte encryptXOrKey = 64;

        /// <summary>
        /// 缓存的字节数组队列，用于重用字节数组以减少内存分配
        /// </summary>
        private static Queue<byte[]> cachedBytesQueue = new Queue<byte[]>();

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

            byte[] bytes = cachedBytesQueue.Peek();

            if (bytes.Length < length)
            {
                return new byte[length];
            }

            return cachedBytesQueue.Dequeue();
        }
        /// <summary>
        /// 释放缓存的字节数组
        /// </summary>
        /// <param name="bytes"></param>
        private static void ReleaseCachedBytes(byte[] bytes)
        {
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
            for (int i = 0; i < encryptBytesLength; i++)
            {
                cachedBytes[i] = encryptOffsetHead;
            }

            //写入原始数据
            Array.Copy(bytes, 0, cachedBytes, encryptBytesLength, bytes.Length);
            using (FileStream fs = File.OpenWrite(filePath))
            {
                fs.Position = 0;
                fs.Write(cachedBytes, 0, newLength);
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

            using (FileStream fs = File.Open(filePath, FileMode.Open))
            {
                int _ = fs.Read(cachedBytes, 0, encryptBytesLength);
                EncryptXOr(cachedBytes);
                fs.Position = 0;
                fs.Write(cachedBytes, 0, encryptXOrKey);
            }

            Array.Clear(cachedBytes, 0, cachedBytes.Length);
            ReleaseCachedBytes(cachedBytes);
        }

        /// <summary>
        /// 使用二进制数据进行异或加密/解密
        /// </summary>
        public static void EncryptXOr(byte[] bytes, long length = encryptBytesLength)
        {
            for (long i = 0; i < length; i++)
            {
                bytes[i] ^= encryptXOrKey;
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
        /// Base64是一種使用64基的位置計數法。它使用2的最大次方來代表僅可列印的ASCII 字元。
        /// 這使它可用來作為電子郵件的傳輸編碼。在Base64中的變數使用字元A-Z、a-z和0-9 ，
        /// 這樣共有62個字元，用來作為開始的64個數字，最後兩個用來作為數字的符號在不同的
        /// 系統中而不同。
        /// Base64加密
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string Base64Encrypt(string str)
        {
            byte[] encbuff = System.Text.Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(encbuff);
        }

        /// <summary>
        /// Base64解密
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string Base64Decrypt(string str)
        {
            byte[] decbuff = Convert.FromBase64String(str);
            return System.Text.Encoding.UTF8.GetString(decbuff);
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
