using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReunionMovement.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ReunionMovement.Common.Util
{
    public static partial class AlgorithmUtil
    {
        #region Collection (continued)
        /// <summary>
        /// 筛选(列表)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="testAction"></param>
        /// <returns></returns>
        public static List<T> Filter<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
            {
                Log.Error("Filter: 源不能为空");
                return new List<T>();
            }

            if (predicate == null)
            {
                Log.Error("Filter: 断言不能为空");
                return new List<T>();
            }

            return source?.Where(predicate).ToList() ?? new List<T>();
        }

        /// <summary>
        /// 筛选(字典)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="K"></typeparam>
        /// <param name="source"></param>
        /// <param name="testAction"></param>
        /// <returns></returns>
        public static Dictionary<T, K> Filter<T, K>(this IEnumerable<KeyValuePair<T, K>> source, FilterAction<T, K> testAction)
        {
            if (source == null)
            {
                Log.Error("Filter: 源不能为空");
                return new Dictionary<T, K>();
            }

            if (testAction == null)
            {
                Log.Error("Filter: 断言不能为空");
                return new Dictionary<T, K>();
            }

            return source.Where(pair => testAction(pair.Key, pair.Value)).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        /// <summary>
        /// 给哈希集添加批量数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection">目标集合</param>
        /// <param name="other">待添加的项；与 collection 为同一实例时先取快照，避免枚举期修改集合</param>
        /// <returns></returns>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> other)
        {
            if (collection == null)
            {
                Log.Error("集合为空");
                return;
            }

            if (other == null)
            {
                // 此前未校验：other 为 null 时 foreach 直接抛 NullReferenceException
                Log.Error("AddRange: 待添加集合为空");
                return;
            }

            // 自追加（collection 与 other 为同一实例）时直接 foreach 会在枚举期间修改集合：
            // List 等有版本校验的实现抛 InvalidOperationException，无版本校验的实现则可能
            // 无限增长直至 OOM。先物化快照，使"把自身内容追加到自身"成为可预期的一次复制
            if (ReferenceEquals(collection, other))
            {
                other = other.ToList();
            }

            foreach (var obj in other)
            {
                collection.Add(obj);
            }
        }

        /// <summary>
        /// 用固定值填充列表
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="list">列表</param>
        /// <param name="value">固定值</param>
        public static void Fill<T>(this IList<T> list, T value)
        {
            if (list == null)
            {
                Log.Error("列表为空");
                return;
            }

            if (list.Count == 0)
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                list[i] = value;
            }
        }

        /// <summary>
        /// 用默认值填充列表
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="list">列表</param>
        public static void FillWithDefault<T>(this IList<T> list)
        {
            if (list == null)
            {
                Log.Error("列表为空");
                return;
            }

            Fill(list, default);
        }

        /// <summary>
        /// 通过二分查找在集合中查找元素。
        /// </summary>
        /// <typeparam name="TCollection"></typeparam>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="source"></param>
        /// <param name="value"></param>
        /// <param name="getSubElement"></param>
        /// <param name="index"></param>
        /// <param name="length"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static int BinarySearch<TCollection, TElement>(this IList<TCollection> source,
                                                              TElement value,
                                                              Func<TCollection, TElement> getSubElement,
                                                              int index,
                                                              int length,
                                                              IComparer<TElement> comparer)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (getSubElement == null)
            {
                throw new ArgumentNullException(nameof(getSubElement));
            }

            if (index < 0 || length < 0 || index + length > source.Count)
            {
                throw new ArgumentOutOfRangeException("索引或长度超出范围");
            }

            if (comparer == null)
            {
                // 如果没有传入比较器，使用默认比较器
                comparer = Comparer<TElement>.Default;
            }

            int min = index;
            int max = index + length - 1;

            while (min <= max)
            {
                int mid = min + ((max - min) >> 1);
                int cmp = comparer.Compare(getSubElement(source[mid]), value);

                if (cmp == 0)
                {
                    return mid;
                }

                if (cmp > 0)
                {
                    max = mid - 1;
                }
                else
                {
                    min = mid + 1;
                }
            }

            return ~min;
        }

        /// <summary>
        /// 比较器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comparer"></param>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool Less<T>(this IComparer<T> comparer, T a, T b) => comparer.Compare(a, b) < 0;

        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="v"></param>
        /// <param name="w"></param>
        /// <returns></returns>
        internal static bool Less<T>(T v, T w) where T : IComparable<T> => v.CompareTo(w) < 0;

        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        internal static bool LessAt<T>(T[] list, int i, int j) where T : IComparable<T> => Less(list[i], list[j]);

        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        internal static bool LessAt<T>(IList<T> list, int i, int j) where T : IComparable<T> => Less(list[i], list[j]);

        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        internal static bool LessAt<T>(this T[] list, int i, int j, IComparer<T> comparer) => comparer.Less(list[i], list[j]);

        /// <summary>
        /// 小于
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comparer"></param>
        /// <param name="list"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        internal static bool LessAt<T>(IComparer<T> comparer, IList<T> list, int i, int j) => comparer.Less(list[i], list[j]);

        /// <summary>
        /// 小于等于
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="v"></param>
        /// <param name="w"></param>
        /// <returns></returns>
        internal static bool LessOrEqual<T>(T v, T w) where T : IComparable<T> => v.CompareTo(w) <= 0;

        /// <summary>
        /// 小于等于
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        internal static bool LessOrEqualAt<T>(this IList<T> list, int i, int j) where T : IComparable<T> => LessOrEqual(list[i], list[j]);

        /// <summary>
        /// 移动到目标索引
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="sourceIndex"></param>
        /// <param name="destinationIndex"></param>
        internal static void MoveAt<T>(this IList<T> list, int sourceIndex, int destinationIndex)
        {
            if (list == null)
            {
                Log.Error("列表为空");
                return;
            }

            if (sourceIndex < 0 || sourceIndex >= list.Count)
            {
                Log.Error("sourceIndex 超出范围");
                return;
            }

            if (destinationIndex < 0 || destinationIndex >= list.Count)
            {
                Log.Error("destinationIndex 超出范围");
                return;
            }

            if (sourceIndex == destinationIndex)
            {
                return;
            }

            var item = list[sourceIndex];

            if (sourceIndex < destinationIndex)
            {
                // 向后移动: 手动向前移动区间内元素
                for (int i = sourceIndex; i < destinationIndex; i++)
                {
                    list[i] = list[i + 1];
                }
                list[destinationIndex] = item;
            }
            else // sourceIndex > destinationIndex
            {
                // 向前移动: 手动向后移动区间内元素
                for (int i = sourceIndex; i > destinationIndex; i--)
                {
                    list[i] = list[i - 1];
                }
                list[destinationIndex] = item;
            }
        }

        /// <summary>
        /// 移动到目标索引
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="sourceIndex"></param>
        /// <param name="destinationIndex"></param>
        internal static void MoveAt<T>(this T[] list, int sourceIndex, int destinationIndex)
        {
            if (list == null)
            {
                Log.Error("列表为空");
                return;
            }

            if (sourceIndex < 0 || sourceIndex >= list.Length)
            {
                Log.Error("sourceIndex 超出范围");
                return;
            }

            if (destinationIndex < 0 || destinationIndex >= list.Length)
            {
                Log.Error("destinationIndex 超出范围");
                return;
            }

            if (sourceIndex == destinationIndex)
            {
                return;
            }

            var item = list[sourceIndex];

            if (sourceIndex < destinationIndex)
            {
                // 向后移动: 手动向前移动区间内元素
                for (int i = sourceIndex; i < destinationIndex; i++)
                {
                    list[i] = list[i + 1];
                }
                list[destinationIndex] = item;
            }
            else // sourceIndex > destinationIndex
            {
                // 向前移动: 手动向后移动区间内元素
                for (int i = sourceIndex; i > destinationIndex; i--)
                {
                    list[i] = list[i - 1];
                }
                list[destinationIndex] = item;
            }
        }
        #endregion
    }
}
