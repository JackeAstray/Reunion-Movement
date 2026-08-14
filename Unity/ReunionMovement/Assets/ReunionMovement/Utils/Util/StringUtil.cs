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
    /// 字符串工具类
    /// </summary>
    public static partial class StringUtil
    {
        /// <summary>
        /// 字符串转byte
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static byte ToByte(this string val)
        {
            if (byte.TryParse(val, out byte result))
            {
                return result;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 字符串转int64
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static long ToInt64(this string val)
        {
            if (long.TryParse(val, out long result))
            {
                return result;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 字符串转float
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public static float ToFloat(this string val)
        {
            if (float.TryParse(val, out float result))
            {
                return result;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 字符串转int32
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static public Int32 ToInt32(this string str)
        {
            if (Int32.TryParse(str, out Int32 result))
            {
                return result;
            }
            else
            {
                return 0;
            }
        }

        public static string Uid(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return uid ?? string.Empty;
            int position = uid.LastIndexOf('_');
            return uid.Remove(0, position + 1);
        }

        /// <summary>
        /// 得到字符串长度，一个汉字长度为2（使用 UTF8 编码判断，避免 ASCII 误判 '?'）
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        public static int StringLength(this string inputString)
        {
            int tempLen = 0;
            byte[] s = Encoding.UTF8.GetBytes(inputString);
            int i = 0;
            while (i < s.Length)
            {
                // 跳过 UTF-8 后续字节 (10xxxxxx)，只计首字节
                if ((s[i] & 0xC0) == 0x80) { i++; continue; }
                tempLen += (s[i] >= 0x80) ? 2 : 1;
                i++;
            }
            return tempLen;
        }

        /// <summary>
        /// 获取内容在UTF8编码下的字节长度；
        /// </summary>
        /// <param name="context">需要检测的内容</param>
        /// <returns>字节长度</returns>
        public static int GetUTF8Length(this string context)
        {
            return Encoding.UTF8.GetBytes(context).Length;
        }

        /// <summary>
        /// 判断字符串是否是数字
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNumber(this string str)
        {
            return double.TryParse(str, out _);
        }

        /// <summary>
        /// 分割字符串
        /// </summary>
        /// <param name="fullString">完整字段</param>
        /// <param name="separator">new string[]{"."}</param>
        /// <param name="removeEmptyEntries">是否返回分割后数组中的空元素</param>
        /// <param name="subStringIndex">分割后数组的序号</param>
        /// <returns>分割后的字段</returns>
        public static string StringSplit(this string fullString, string[] separator, bool removeEmptyEntries, int subStringIndex)
        {
            if (string.IsNullOrEmpty(fullString))
            {
                return string.Empty;
            }

            string[] stringArray = null;
            if (removeEmptyEntries)
            {
                stringArray = fullString.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                stringArray = fullString.Split(separator, StringSplitOptions.None);
            }
            if (subStringIndex < 0 || subStringIndex >= stringArray.Length)
            {
                Log.Error($"StringSplit: 索引 {subStringIndex} 超出范围 (数组长度: {stringArray.Length})");
                return string.Empty;
            }
            string subString = stringArray[subStringIndex];
            return subString;
        }

        /// <summary>
        /// 分割字符串
        /// </summary>
        /// <param name="fullString">完整字段</param>
        /// <param name="separator">new string[]{"."}</param>
        /// <param name="count">要返回的子字符串的最大数量</param>
        /// <param name="removeEmptyEntries">是否移除空实体</param>
        /// <returns>分割后的字段</returns>
        public static string[] StringSplit(this string fullString, string[] separator, int count, bool removeEmptyEntries)
        {
            if (string.IsNullOrEmpty(fullString))
            {
                return Array.Empty<string>();
            }

            string[] stringArray = null;
            if (removeEmptyEntries)
            {
                stringArray = fullString.Split(separator, count, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                stringArray = fullString.Split(separator, count, StringSplitOptions.None);
            }
            return stringArray;
        }

        /// <summary>
        /// 分割字符串
        /// </summary>
        /// <param name="fullString">分割字符串</param>
        /// <param name="separator">new string[]{"."}</param>
        /// <returns>分割后的字段数组</returns>
        public static string[] StringSplit(this string fullString, string[] separator)
        {
            if (string.IsNullOrEmpty(fullString))
            {
                return Array.Empty<string>();
            }

            string[] stringArray = fullString.Split(separator, StringSplitOptions.None);
            return stringArray;
        }

        /// <summary>
        /// 截断字符串变成数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static List<T> StringSplit<T>(this string str, params char[] args)
        {
            if (args.Length == 0)
            {
                args = new[] { '|' }; // 默认
            }

            if (string.IsNullOrEmpty(str))
            {
                return new List<T>();
            }

            return str.Split(args)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => (T)Convert.ChangeType(s.Trim(), typeof(T)))
                    .ToList();
        }

        /// <summary>
        /// 多字符替换；
        /// </summary>
        /// <param name="context">需要修改的内容</param>
        /// <param name="oldContext">需要修改的内容</param>
        /// <param name="newContext">修改的新内容</param>
        /// <returns>修改后的内容</returns>
        public static string Replace(string context, string[] oldContext, string newContext)
        {
            if (string.IsNullOrEmpty(context))
            {
                throw new ArgumentNullException("上下文无效");
            }
            if (oldContext == null)
            {
                throw new ArgumentNullException("旧上下文无效");
            }
            // 允许空串替换（“删除文本”是合法用途），仅校验 null
            if (newContext == null)
            {
                throw new ArgumentNullException("新上下文无效");
            }
            var length = oldContext.Length;
            for (int i = 0; i < length; i++)
            {
                context = context.Replace(oldContext[i], newContext);
            }

            return context;
        }

        /// <summary>
        /// 判断字符串有效
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static bool IsStringValid(this string context)
        {
            if (string.IsNullOrEmpty(context))
            {
                return false;
            }
            return true;
        }

        // 预编译正则，避免每次调用解析模式
        private static readonly Regex ClassNameRegex = new Regex(@"^[A-Z][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex FieldNameRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>
        /// 检查类名
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool CheckClassName(string str)
        {
            return ClassNameRegex.IsMatch(str);
        }

        /// <summary>
        /// 检查字段名
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool CheckFieldName(string name)
        {
            return FieldNameRegex.IsMatch(name);
        }

        /// <summary>
        /// 字符串首字母大写，不改变其他字符
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string CapitalFirstChar(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            return char.ToUpper(str[0]) + str.Substring(1);
        }

        /// <summary>
        /// 首字母大写，其他小写
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public static string ToTitleCase(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return word;
            }

            return char.ToUpper(word[0]) + (word.Length > 1 ? word.Substring(1).ToLower() : "");
        }

        /// <summary>
        /// 驼峰命名转下划线命名
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToSentenceCase(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            // 连续大写字母后跟小写字母：XMLParser → xml_parser
            str = Regex.Replace(str, "([A-Z]+)([A-Z][a-z])", m =>
                m.Groups[1].Value.ToLower() + "_" + m.Groups[2].Value.ToLower());
            // 小写/数字后跟大写：myVar → my_var
            str = Regex.Replace(str, "([a-z\\d])([A-Z])", m =>
                m.Groups[1].Value + "_" + m.Groups[2].Value.ToLower());
            return char.ToLower(str[0]) + str.Substring(1);
        }

        /// <summary>
        /// 人性化数字显示，百万，千万，亿
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static string HumanizeNumber(int number)
        {
            if (number > 100000000)
            {
                return $"{number / 100000000}亿";
            }
            else if (number > 10000000)
            {
                return $"{number / 10000000}千万";
            }
            else if (number > 1000000)
            {
                return $"{number / 1000000}百万";
            }
            else if (number > 10000)
            {
                return $"{number / 10000}万";
            }

            return number.ToString();
        }

        /// <summary>
        /// 文件大小格式化显示成KB，MB,GB
        /// </summary>
        /// <param name="size">字节</param>
        public static String FormatFileSize(long size)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = size;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024d;
            }
            return String.Format("{0:0.##} {1}", len, sizes[order]);
        }

        /// <summary>
        /// Base64转图片
        /// </summary>
        /// <param name="imageData"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static byte[] Base64ToByte(this string imageData, int offset = 0)
        {
            if (imageData == null) throw new ArgumentNullException(nameof(imageData));
            if (offset < 0 || offset > imageData.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            imageData = imageData.Substring(offset);
            byte[] data = Convert.FromBase64String(imageData);
            return data;
        }

        /// <summary>
        /// 图片转Base64
        /// </summary>
        /// <param name="bytesArr"></param>
        /// <returns></returns>
        public static string ByteToBase64(this byte[] bytesArr)
        {
            return Convert.ToBase64String(bytesArr);
        }

        /// <summary>
        /// 加入
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="sp"></param>
        /// <returns></returns>
        public static string Join<T>(this IEnumerable<T> source, string sp)
        {
            return string.Join(sp, source);
        }

        /// <summary>
        /// 字典转到字符串A:1|B:2|C:3这类
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="K"></typeparam>
        /// <param name="dict"></param>
        /// <param name="delimeter1"></param>
        /// <param name="delimeter2"></param>
        /// <returns></returns>
        public static string DictToSplitStr<T, K>(Dictionary<T, K> dict, char delimeter1 = '|', char delimeter2 = ':')
        {
            if (dict == null || dict.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var kvp in dict)
            {
                sb.Append(kvp.Key);
                sb.Append(delimeter2);
                sb.Append(kvp.Value);
                sb.Append(delimeter1);
            }
            sb.Remove(sb.Length - 1, 1); // 移除最后一个分隔符
            return sb.ToString();
        }

        /// <summary>
        /// A:1|B:2|C:3这类字符串转成字典
        /// </summary>
        /// <typeparam name="T">string</typeparam>
        /// <typeparam name="K">string</typeparam>
        /// <param name="str">原始字符串</param>
        /// <param name="delimeter1">分隔符1</param>
        /// <param name="delimeter2">分隔符2</param>
        /// <returns></returns>
        public static Dictionary<T, K> SplitToDict<T, K>(string str, char delimeter1 = '|', char delimeter2 = ':')
        {
            var dict = new Dictionary<T, K>();
            if (string.IsNullOrEmpty(str)) return dict;

            var pairs = str.Split(delimeter1);
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split(delimeter2);

                // 跳过无效或不完整的键值对
                if (keyValue.Length != 2)
                {
                    continue;
                }
                T key = (T)Convert.ChangeType(keyValue[0], typeof(T));
                K value = (K)Convert.ChangeType(keyValue[1], typeof(K));
                // 使用索引器添加或更新字典项
                dict[key] = value;
            }

            return dict;
        }

        /// <summary>
        /// 字符串转枚举
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e"></param>
        /// <returns></returns>
        public static T ToEnum<T>(this string e)
        {
            return (T)Enum.Parse(typeof(T), e);
        }

    }
}
