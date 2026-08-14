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
    /// StringUtil partial part: MD5 (same class name, no caller changes)
    /// </summary>
    public static partial class StringUtil
    {
        #region MD5
        /// <summary>
        /// 获得32位的MD5加密
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetMD5_32(string input)
        {
            using (var md5 = MD5.Create())
            {
                byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                {
                    sb.AppendFormat("{0:X2}", data[i]);
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 获得16位的MD5加密
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetMD5_16(string input)
        {
            return GetMD5_32(input).Substring(8, 16);
        }

        /// <summary>
        /// 获得8位的MD5加密
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetMD5_8(string input)
        {
            return GetMD5_32(input).Substring(8, 8);
        }

        /// <summary>
        /// 获得4位的MD5加密
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetMD5_4(string input)
        {
            return GetMD5_32(input).Substring(8, 4);
        }

        /// <summary>
        /// 计算字符串的MD5值
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string CreateMD5(string source)
        {
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                byte[] data = Encoding.UTF8.GetBytes(source);
                byte[] md5Data = md5.ComputeHash(data, 0, data.Length);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < md5Data.Length; i++)
                {
                    sb.Append(md5Data[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 计算文件的MD5值
        /// </summary>
        /// <param name="MD5File">MD5签名文件字符数组</param>
        /// <param name="index">计算起始位置</param>
        /// <param name="count">计算终止位置</param>
        /// <returns>计算结果</returns>
        public static string MD5Buffer(byte[] MD5File, int index, int count)
        {
            using (var get_md5 = new MD5CryptoServiceProvider())
            {
                byte[] hash_byte = get_md5.ComputeHash(MD5File, index, count);
                string result = BitConverter.ToString(hash_byte);
                result = result.Replace("-", "");
                return result;
            }
        }
        #endregion
    }
}
