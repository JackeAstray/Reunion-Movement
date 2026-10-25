using System;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// string[] 扩展（仅保留有调用点的成员；Then/Dequeue/执行器数组合并等无调用点成员已删除）
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// 添加单个元素
        /// </summary>
        public static string[] Add(this string[] arr1, string arr2)
        {
            return arr1.Add(new[] { arr2 });
        }

        /// <summary>
        /// 合并两个string数组
        /// </summary>
        public static string[] Add(this string[] arr1, string[] arr2)
        {
            arr1 ??= Array.Empty<string>();
            arr2 ??= Array.Empty<string>();
            var result = new string[arr1.Length + arr2.Length];
            Array.Copy(arr1, 0, result, 0, arr1.Length);
            Array.Copy(arr2, 0, result, arr1.Length, arr2.Length);
            return result;
        }
    }
}