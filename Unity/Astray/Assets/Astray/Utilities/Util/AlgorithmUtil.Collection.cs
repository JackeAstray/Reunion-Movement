using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Astray.Framework.Runtime;
using UnityEngine;
using UnityEngine.UI;

using Astray.Common;
namespace Astray.Utilities
{
    public static partial class AlgorithmUtil
    {
        #region Collection
        public delegate bool FilterAction<T, K>(T t, K k);

        /// <summary>
        /// 交换两个值
        /// </summary>
        /// <typeparam name="T">传入的对象类型</typeparam>
        /// <param name="lhs">第一个需要交换的值</param>
        /// <param name="rhs">第二个需要交换的值</param>
        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            (lhs, rhs) = (rhs, lhs);
        }

        /// <summary>
        /// 交换数组中的两个元素
        /// </summary>
        /// <typeparam name="T">传入的对象类型</typeparam>
        /// <param name="array">传入的数组</param>
        /// <param name="i">序号i</param>
        /// <param name="j">序号j</param>
        private static void Swap<T>(IList<T> array, int i, int j)
        {
            if (array == null)
            {
                Log.Error("Swap: 输入数组不能为空");
                return;
            }

            T temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }

        /// <summary>
        /// 快速排序
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <typeparam name="K">比较类型</typeparam>
        /// <param name="array">需要排序的数组对象</param>
        /// <param name="handler">排序条件</param>
        /// <param name="start">起始位</param>
        /// <param name="end">结束位</param>
        /// <param name="ascending">是否升序（默认升序）</param>
        public static void QuickSort<T, K>(IList<T> array, Func<T, K> handler, int start, int end, bool ascending = true) where K : IComparable<K>
        {
            if (array == null || handler == null || start < 0 || end < 0 || start >= end)
            {
                return;
            }

            // 切换到插入排序
            if (end - start <= 10)
            {
                InsertionSort(array, handler, start, end, ascending);
                return;
            }

            // 三数取中法选择基准点
            int mid = start + (end - start) / 2;
            int pivot = MedianOfThree(array, handler, start, mid, end);

            // 分区
            T pivotValue = array[pivot];
            Swap(array, pivot, end);
            int storeIndex = start;

            for (int i = start; i < end; i++)
            {
                int comparison = handler(array[i]).CompareTo(handler(pivotValue));
                if ((ascending && comparison < 0) || (!ascending && comparison > 0))
                {
                    Swap(array, i, storeIndex);
                    storeIndex++;
                }
            }

            Swap(array, storeIndex, end);

            // 递归排序
            QuickSort(array, handler, start, storeIndex - 1, ascending);
            QuickSort(array, handler, storeIndex + 1, end, ascending);
        }

        /// <summary>
        /// 插入排序
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="K"></typeparam>
        /// <param name="array"></param>
        /// <param name="handler"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="ascending"></param>
        private static void InsertionSort<T, K>(IList<T> array, Func<T, K> handler, int start, int end, bool ascending) where K : IComparable<K>
        {
            if (array == null || handler == null || start < 0 || end < 0 || start >= end)
            {
                return;
            }

            for (int i = start + 1; i <= end; i++)
            {
                T temp = array[i];
                int j = i - 1;
                while (j >= start && ((ascending && handler(array[j]).CompareTo(handler(temp)) > 0) || (!ascending && handler(array[j]).CompareTo(handler(temp)) < 0)))
                {
                    array[j + 1] = array[j];
                    j--;
                }
                array[j + 1] = temp;
            }
        }

        /// <summary>
        /// 三数取中法选择基准点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="K"></typeparam>
        /// <param name="array"></param>
        /// <param name="handler"></param>
        /// <param name="start"></param>
        /// <param name="mid"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        private static int MedianOfThree<T, K>(IList<T> array, Func<T, K> handler, int start, int mid, int end) where K : IComparable<K>
        {
            if (array == null || handler == null)
            {
                Log.Error("MedianOfThree: 输入数组或处理器不能为空");
                return start;
            }

            // 必须在每次 Swap 之后重新读取位置上的键，不能用调用前捕获的 a/b/c：
            // 交换会改变位置上的值，用旧值判断时 (start=最大, mid=最小, end=中间) 这一种排列
            // 会返回最大值而非中位数，破坏"三数取中"契约（排序结果仍正确，但基准退化为极值，
            // 最坏情况向 O(n²) 退化）。重读版本在全部 6 种排列下都返回中位数。
            if (handler(array[start]).CompareTo(handler(array[mid])) > 0)
            {
                Swap(array, start, mid);
            }
            if (handler(array[start]).CompareTo(handler(array[end])) > 0)
            {
                Swap(array, start, end);
            }
            if (handler(array[mid]).CompareTo(handler(array[end])) > 0)
            {
                Swap(array, mid, end);
            }

            return mid; // 返回中间值作为基准点
        }

        /// <summary>
        /// LINQ排序
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <typeparam name="K">比较类型</typeparam>
        /// <param name="array">需要排序的数组对象</param>
        /// <param name="keySelector">排序条件</param>
        /// <param name="ascending">是否升序（默认升序）</param>
        public static void Sort<T, K>(IList<T> array, Func<T, K> keySelector, bool ascending = true) where K : IComparable<K>
        {
            if (array == null || keySelector == null)
            {
                Log.Error("Sort: 输入数组或键选择器不能为空");
                return;
            }

            var sorted = ascending
                ? array.OrderBy(keySelector).ToList()
                : array.OrderByDescending(keySelector).ToList();

            for (int i = 0; i < array.Count; i++)
            {
                array[i] = sorted[i];
            }
        }

        /// <summary>
        /// 获取最小或最大值
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="array">待查找的集合</param>
        /// <param name="comparison">比较器，返回小于0表示第一个参数更小，大于0表示第二个参数更小</param>
        /// <param name="findMax">true查找最大值，false查找最小值</param>
        /// <returns>最小或最大值</returns>
        public static T MinMax<T>(IList<T> array, Comparison<T> comparison, bool findMax = false)
        {
            if (array == null)
            {
                Log.Error("MinMax: 输入数组不能为空");
                return default;
            }

            if (array.Count == 0)
            {
                Log.Error("MinMax: 输入数组不能为空");
                return default;
            }

            if (comparison == null)
            {
                Log.Error("MinMax: 比较器不能为空");
                return default;
            }

            T temp = array[0];

            foreach (var arr in array)
            {
                int cmp = comparison(temp, arr);
                if ((findMax && cmp < 0) || (!findMax && cmp > 0))
                {
                    temp = arr;
                }
            }
            return temp;
        }

        /// <summary>
        /// 获取最小值
        /// </summary>
        public static T Min<T, K>(IList<T> array, Func<T, K> keySelector) where K : IComparable<K>
        {
            if (array == null || array.Count == 0)
            {
                Log.Error("Min: 输入数组不能为空");
                return default;
            }

            if (keySelector == null)
            {
                Log.Error("Min: 键选择器不能为空");
                return default;
            }

            return MinMax(array, (a, b) => keySelector(a).CompareTo(keySelector(b)), false);
        }

        /// <summary>
        /// 获取最大值
        /// </summary>
        public static T Max<T, K>(IList<T> array, Func<T, K> keySelector) where K : IComparable<K>
        {
            if (array == null)
            {
                Log.Error("Max: 输入数组不能为空");
                return default;
            }

            if (keySelector == null)
            {
                Log.Error("Max: 键选择器不能为空");
                return default;
            }

            return MinMax(array, (a, b) => keySelector(a).CompareTo(keySelector(b)), true);
        }

        /// <summary>
        /// 获取最小值（自定义比较器）
        /// </summary>
        public static T Min<T>(IList<T> array, Comparison<T> comparison)
        {
            if (array == null)
            {
                Log.Error("Min: 输入数组不能为空");
                return default;
            }

            if (comparison == null)
            {
                Log.Error("Min: 比较器不能为空");
                return default;
            }

            return MinMax(array, comparison, false);
        }

        /// <summary>
        /// 获取最大值（自定义比较器）
        /// </summary>
        public static T Max<T>(IList<T> array, Comparison<T> comparison)
        {
            if (array == null)
            {
                Log.Error("Max: 输入数组不能为空");
                return default;
            }

            if (comparison == null)
            {
                Log.Error("Max: 比较器不能为空");
                return default;
            }

            return MinMax(array, comparison, true);
        }

        /// <summary>
        /// 从序列中获取第N个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public static List<T> First<T>(this IEnumerable<T> source, int num)
        {
            if (source == null)
            {
                return new List<T>();
            }

            return source.Take(num).ToList();
        }

        /// <summary>
        /// 从序列中获取最后N个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public static List<T> Last<T>(this IEnumerable<T> source, int num)
        {
            if (source == null)
            {
                return new List<T>();
            }

            var list = source as IList<T> ?? source.ToList();
            int startIndex = Math.Max(0, list.Count - num);
            return list.Skip(startIndex).Take(num).ToList();
        }

        /// <summary>
        /// 从集合中随机获取一个元素，支持 IList、数组、IEnumerable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static T GetRandomItemFromList<T>(IEnumerable<T> source)
        {
            if (source == null)
            {
                return default;
            }

            // 优先处理 IList<T>，如 List、数组，效率高
            if (source is IList<T> list)
            {
                if (list.Count == 0)
                {
                    return default;
                }
                return list[RandomUtil.Next(list.Count)];
            }

            // 其它 IEnumerable，使用蓄水池抽样算法
            int count = 0;
            T selected = default;
            foreach (var item in source)
            {
                count++;
                if (RandomUtil.Next(count) == 0)
                    selected = item;
            }
            return count == 0 ? default : selected;
        }

        #endregion
    }
}
