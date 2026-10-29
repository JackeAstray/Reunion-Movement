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
    /// StringUtil partial part: Compression (same class name, no caller changes)
    /// </summary>
    public static partial class StringUtil
    {
        #region 压缩/解压缩
        /// <summary>
        /// 字符串压缩
        /// </summary>
        /// <param name="strSource"></param>
        /// <returns></returns>
        public static byte[] Compress(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zip.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 字符串解压缩
        /// </summary>
        /// <param name="strSource"></param>
        /// <returns></returns>
        public static byte[] Decompress(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var zip = new GZipStream(ms, CompressionMode.Decompress, true))
            using (var msreader = new MemoryStream())
            {
                byte[] buffer = new byte[0x1000];
                int reader;
                while ((reader = zip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    msreader.Write(buffer, 0, reader);
                }
                return msreader.ToArray();
            }
        }

        /// <summary>
        /// 压缩字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string CompressString(string str)
        {
            byte[] compressBeforeByte = Encoding.UTF8.GetBytes(str);
            byte[] compressAfterByte = Compress(compressBeforeByte);
            string compressString = Convert.ToBase64String(compressAfterByte);
            return compressString;
        }

        /// <summary>
        /// 解压字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string DecompressString(string str)
        {
            byte[] compressBeforeByte = Convert.FromBase64String(str);
            byte[] compressAfterByte = Decompress(compressBeforeByte);
            string compressString = Encoding.UTF8.GetString(compressAfterByte);
            return compressString;
        }
        #endregion
    }
}
