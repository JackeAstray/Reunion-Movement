using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// StringUtil partial part: Byte/Hex helpers (same class name, no caller changes)
    /// </summary>
    public static partial class StringUtil
    {
        #region StringHelper
        /// <summary>
        /// 将字符串转换为字节集合，使用默认编码（通常是系统默认编码）
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static IEnumerable<byte> ToBytes(this string str)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(str);
            return byteArray;
        }

        /// <summary>
        /// 将字符串转换为字节数组，使用默认编码（通常是系统默认编码）
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] ToByteArray(this string str)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(str);
            return byteArray;
        }

        /// <summary>
        /// 将字符串转换为UTF8编码的字节数组
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] ToUtf8(this string str)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(str);
            return byteArray;
        }

        /// <summary>
        /// 将十六进制字符串转换为字节数组
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static byte[] HexToBytes(this string hexString)
        {
            if (string.IsNullOrEmpty(hexString)) throw new ArgumentNullException(nameof(hexString));
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "二进制密钥不能有奇数位数: {0}", hexString));
            }

            var hexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < hexAsBytes.Length; index++)
            {
                string byteValue = "";
                byteValue += hexString[index * 2];
                byteValue += hexString[index * 2 + 1];
                hexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return hexAsBytes;
        }

        /// <summary>
        /// 格式化字符串
        /// </summary>
        /// <param name="text"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string Fmt(this string text, params object[] args)
        {
            return string.Format(text, args);
        }

        /// <summary>
        /// 将List转换为字符串
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static string ListToString<T>(this List<T> list)
        {
            if (list == null || list.Count == 0) return string.Empty;
            return string.Join(",", list);
        }
        #endregion
        #region ByteHelper
        /// <summary>
        /// 将字节转换为十六进制字符串
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static string ToHex(this byte b)
        {
            return b.ToString("X2");
        }

        /// <summary>
        /// 将字节数组转换为十六进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ToHex(this byte[] bytes)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (byte b in bytes)
            {
                stringBuilder.Append(b.ToString("X2"));
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// 将字节数组转换为十六进制字符串，使用指定的格式
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public static string ToHex(this byte[] bytes, string format)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (byte b in bytes)
            {
                stringBuilder.Append(b.ToString(format));
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// 将字节数组转换为十六进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static string ToHex(this byte[] bytes, int offset, int count)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = offset; i < offset + count; ++i)
            {
                stringBuilder.Append(bytes[i].ToString("X2"));
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// 将字节数组转换为字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ToStr(this byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 将字节数组转换为字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static string ToStr(this byte[] bytes, int index, int count)
        {
            return Encoding.UTF8.GetString(bytes, index, count);
        }

        /// <summary>
        /// 将字节数组转换为UTF8字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string Utf8ToStr(this byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 将字节数组转换为UTF8字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static string Utf8ToStr(this byte[] bytes, int index, int count)
        {
            return Encoding.UTF8.GetString(bytes, index, count);
        }

        /// <summary>
        /// 将uint写入字节数组
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="num"></param>
        public static void WriteTo(this byte[] bytes, int offset, uint num)
        {
            bytes[offset] = (byte)(num & 0xff);
            bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
            bytes[offset + 2] = (byte)((num & 0xff0000) >> 16);
            bytes[offset + 3] = (byte)((num & 0xff000000) >> 24);
        }

        /// <summary>
        /// 将int写入字节数组
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="num"></param>
        public static void WriteTo(this byte[] bytes, int offset, int num)
        {
            bytes[offset] = (byte)(num & 0xff);
            bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
            bytes[offset + 2] = (byte)((num & 0xff0000) >> 16);
            bytes[offset + 3] = (byte)((num & 0xff000000) >> 24);
        }

        /// <summary>
        /// 将byte写入字节数组
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="num"></param>
        public static void WriteTo(this byte[] bytes, int offset, byte num)
        {
            bytes[offset] = num;
        }

        /// <summary>
        /// 将short写入字节数组
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="num"></param>
        public static void WriteTo(this byte[] bytes, int offset, short num)
        {
            bytes[offset] = (byte)(num & 0xff);
            bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
        }

        /// <summary>
        /// 将ushort写入字节数组
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="num"></param>
        public static void WriteTo(this byte[] bytes, int offset, ushort num)
        {
            bytes[offset] = (byte)(num & 0xff);
            bytes[offset + 1] = (byte)((num & 0xff00) >> 8);
        }
        #endregion
    }
}
