using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReunionMovement.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 算法工具类
    /// </summary>
    /// <summary>
    /// AlgorithmUtil 拆分部分（partial class，与其余 *.cs 同属一个静态类，调用方式不变）
    /// </summary>
    public static partial class AlgorithmUtil
    {
        #region 判断
        /// <summary>
        /// 判断一个整数是否为正数
        /// </summary>
        /// <param name="p"></param>
        /// <returns> 正数 1，负数 -1，零 0 </returns>
        public static int Sign(int p)
        {
            return Math.Sign(p);
        }

        /// <summary>
        /// 判断两个 float 是否近似相等
        /// </summary>
        public static bool Equal(float a, float b, float epsilon = 1e-6f)
        {
            return Math.Abs(a - b) < epsilon;
        }

        /// <summary>
        /// 判断两个 double 是否近似相等
        /// </summary>
        public static bool Equal(double a, double b, double epsilon = 1e-12)
        {
            return Math.Abs(a - b) < epsilon;
        }

        /// <summary>
        /// 判断两个 decimal 是否近似相等
        /// </summary>
        public static bool Equal(decimal a, decimal b, decimal epsilon = 1e-18m)
        {
            return Math.Abs(a - b) < epsilon;
        }

        /// <summary>
        /// 判断一个值是否在0-1范围内
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool InRange01(float value)
        {
            return InRange(value, 0, 1);
        }

        /// <summary>
        /// 判断一个值是否在一个范围内
        /// </summary>
        /// <param name="value"></param>
        /// <param name="closedLeft"></param>
        /// <param name="openRight"></param>
        /// <returns></returns>
        public static bool InRange(float value, float closedLeft, float openRight)
        {
            return value >= closedLeft && value < openRight;
        }

        /// <summary>
        /// 是否是奇数
        /// </summary>
        /// <param name="value">检测的值</param>
        /// <returns>是否是奇数</returns>
        public static bool IsOdd(long value)
        {
            return (value & 1) != 0;
        }

        /// <summary>
        /// 是否是偶数
        /// </summary>
        /// <param name="value">检测的值</param>
        /// <returns>是否是偶数</returns>
        public static bool IsEven(long value)
        {
            return !((value & 1) != 0);
        }

        /// <summary>
        /// 判断两个数组是否相等
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns></returns>
        public static bool ArraysEqual<T>(IEnumerable<T> arg1, IEnumerable<T> arg2)
        {
            return Enumerable.SequenceEqual(arg1, arg2);
        }

        /// <summary>
        /// 判断一个数是否2的次方
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static bool CheckPow2(int num)
        {
            return num > 0 && (num & (num - 1)) == 0;
        }

        /// <summary>
        /// 将整数数组转换为整数
        /// </summary>
        /// <param name="array">[1,2,3,4]</param>
        /// <returns>1234</returns>
        public static int ConvertIntArrayToInt(int[] array)
        {
            if (array == null || array.Length == 0)
            {
                return 0;
            }

            // 使用整数运算避免 Math.Pow 带来的精度问题
            long result = 0;
            int length = array.Length;

            // 使用首位的符号决定结果符号（若需要保留负号）
            int sign = array[0] < 0 ? -1 : 1;

            for (int i = 0; i < length; i++)
            {
                // 与 CalculateMaximumCommonDivisor 同理，用 long 取绝对值：
                // Math.Abs(int.MinValue) 会抛 OverflowException（int 无法表示其相反数）
                long digit = Math.Abs((long)array[i]);
                // 保证为一位数字（若传入多位，取低位）
                digit %= 10;
                result = result * 10 + digit;

                if (result > int.MaxValue)
                {
                    Log.Error("ConvertIntArrayToInt: 结果溢出 int 范围，已截断");
                    return sign > 0 ? int.MaxValue : int.MinValue;
                }
            }

            return (int)(result * sign);
        }

        /// <summary>
        /// 判断是否为空
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static bool IsEmpty<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        /// <summary>
        /// 判断活动状态
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool IsActive(this Transform t)
        {
            return t?.gameObject.activeInHierarchy ?? false;
        }
        /// <summary>
        /// 判断刚体是否存在
        /// </summary>
        /// <param name="gobj"></param>
        /// <returns></returns>
        public static bool HasRigidbody(this GameObject gobj)
        {
            return gobj.GetComponent<Rigidbody>() != null;
        }
        /// <summary>
        /// 判断动画是否存在
        /// </summary>
        /// <param name="gobj"></param>
        /// <returns></returns>
        public static bool HasAnimation(this GameObject gobj)
        {
            return gobj.GetComponent<Animation>() != null;
        }

        /// <summary>
        /// 判断向量是否为有限数
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool IsFinite(this Vector2 v)
        {
            return v.x.IsFinite() && v.y.IsFinite();
        }

        /// <summary>
        /// 判断向量是否为有限数
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        private static bool IsFinite(this float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }
        #endregion

    }
}
