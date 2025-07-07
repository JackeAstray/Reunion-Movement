using ReunionMovement.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 算法工具类
    /// </summary>
    public static class AlgorithmUtil
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
            return !Convert.ToBoolean(value & 0x1);
        }

        /// <summary>
        /// 是否是偶数
        /// </summary>
        /// <param name="value">检测的值</param>
        /// <returns>是否是偶数</returns>
        public static bool IsEven(long value)
        {
            return Convert.ToBoolean(value & 0x1);
        }

        /// <summary>
        /// 判断两个数组是否相等
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns></returns>
        public static bool ArraysEqual<T>(T[] arg1, T[] arg2)
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
            int result = 0;
            int length = array.Length;
            for (int i = 0; i < length; i++)
            {
                result += Convert.ToInt32((Math.Abs(array[i]) * Math.Pow(10, length - 1 - i)));
            }

            return result;
        }
        #endregion

        #region 计算
        /// <summary>
        /// 线性插值
        /// 1. 动画与过渡效果：在游戏开发、UI动画等领域，用于平滑地过渡数值（如位置、颜色、透明度等），实现平滑动画。
        /// 2. 数据平滑与插值：在数据可视化、信号处理等场景，用于在已知数据点之间估算中间值，实现数据平滑或补全。
        /// 3. 物理模拟：在物理引擎中，用于计算物体在两个状态之间的中间状态（如速度、位置等）。
        /// 4. 图像处理：图像缩放、旋转等操作时，像素值的插值计算。
        /// 5. 音频处理：音频采样率转换、音量渐变等场景。
        /// 6. 数值分析与科学计算：用于一维表格数据的插值，快速估算未知点的值。
        /// 只要需要在两个数值之间平滑过渡或估算中间值的场景，都可以用到线性插值。
        /// </summary>
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// 双线性插值
        /// 1. 图像处理：在图像缩放、旋转或变形时，双线性插值可以用来计算新像素的颜色值，以获得更平滑的图像效果。
        /// 2. 纹理映射：在计算机图形学中，双线性插值用于在纹理映射过程中计算纹理坐标之间的颜色值。
        /// 3. 地理信息系统（GIS）：在地理数据的可视化和分析中，双线性插值用于在网格数据之间进行平滑过渡。
        /// 4. 物理模拟：在模拟流体、热传导等物理现象时，双线性插值用于计算网格点之间的值。
        /// 双线性插值适用于“二维网格数据之间的平滑过渡”，只要有二维数据、需要在格点之间插值的场景，都可以用到它。
        /// </summary>
        /// <param name="a">top-left</param>
        /// <param name="b">top-right</param>
        /// <param name="c">bottom-left</param>
        /// <param name="d">bottom-right</param>
        /// <param name="u">水平插值参数（介于0和1之间）</param>
        /// <param name="v">垂直插值参数（介于0和1之间）</param>
        /// <returns></returns>
        public static float Bilerp(float a, float b, float c, float d, float u, float v)
        {
            float s1 = Lerp(a, b, u);
            float s2 = Lerp(c, d, u);
            return Lerp(s1, s2, v);
        }

        /// <summary>
        /// 三线性插值
        /// 1.	体绘制（Volume Rendering）：在医学成像、科学计算和计算机图形学中，三线性插值用于在三维体数据中进行插值，以获得更平滑的可视化效果。
        /// 2.	3D 纹理映射：在计算机图形学中，三线性插值用于在三维纹理映射过程中计算纹理坐标之间的颜色值。
        /// 3.	物理模拟：在模拟流体、热传导等三维物理现象时，三线性插值用于计算网格点之间的值。
        /// 4.	地理信息系统（GIS）：在三维地理数据的可视化和分析中，三线性插值用于在网格数据之间进行平滑过渡。
        /// </summary>
        /// <param name="c000"></param>
        /// <param name="c100"></param>
        /// <param name="c010"></param>
        /// <param name="c110"></param>
        /// <param name="c001"></param>
        /// <param name="c101"></param>
        /// <param name="c011"></param>
        /// <param name="c111"></param>
        /// <param name="u">沿x轴的插值参数（在0和1之间）</param>
        /// <param name="v">沿y轴的插值参数（介于0和1之间）</param>
        /// <param name="w">沿z轴的插值参数（在0和1之间）</param>
        /// <returns></returns>
        public static float Trilerp(float c000, float c100, float c010, float c110,
                                    float c001, float c101, float c011, float c111,
                                    float u, float v, float w)
        {
            // 在c000和c100之间根据u进行线性插值
            float c00 = Lerp(c000, c100, u);
            // 在c010和c110之间根据u进行线性插值
            float c10 = Lerp(c010, c110, u);
            // 在c001和c101之间根据u进行线性插值
            float c01 = Lerp(c001, c101, u);
            // 在c011和c111之间根据u进行线性插值
            float c11 = Lerp(c011, c111, u);

            // 在c00和c10之间根据v进行线性插值
            float c0 = Lerp(c00, c10, v);
            // 在c01和c11之间根据v进行线性插值
            float c1 = Lerp(c01, c11, v);
            // 在c0和c1之间根据w进行线性插值
            return Lerp(c0, c1, w);
        }

        /// <summary>
        /// 返回大于等于指定整数 num 的最小2的幂（即最近的2的整数次方）
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static int GetNearestPower2(int num)
        {
            return (int)(Math.Pow(2, Math.Ceiling(Math.Log(num) / Math.Log(2))));
        }

        /// <summary>
        /// 计算最大公约数 （够同时整除两个或多个整数的最大的正整数）
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int CalculateMaximumCommonDivisor(int a, int b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            if (a == 0 && b == 0)
            {
                return 0;
            }
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }
            return a;
        }

        /// <summary>
        /// 计算最小公倍数（能被两个或多个整数整除的最小正整数）
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int CalculateMinimumCommonMultiple(int a, int b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }
            return Math.Abs(a * b) / CalculateMaximumCommonDivisor(a, b);
        }

        /// <summary>
        /// 数组去重
        /// </summary>
        /// <typeparam name="T">可比数据类型</typeparam>
        /// <param name="array">源数据</param>
        /// <returns>去重后的数据</returns>
        public static T[] Distinct<T>(IList<T> array)
        {
            if (array == null)
            {
                Log.Error("Distinct: 输入数组不能为空");
            }
            var set = new HashSet<T>();
            var result = new List<T>(array.Count);
            foreach (var item in array)
            {
                if (set.Add(item))
                {
                    result.Add(item);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 随机打乱数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="array">数组</param>
        public static void Disrupt<T>(IList<T> array)
        {
            Disrupt(array, 0, array.Count);
        }

        /// <summary>
        /// 随机打乱数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="startIndex">起始序号</param>
        /// <param name="count">数量</param>
        public static void Disrupt<T>(IList<T> array, int startIndex, int count)
        {
            if (array == null)
            {
                Log.Error("Distinct: 输入数组不能为空");
            }

            if (startIndex < 0 || count < 0 || startIndex + count > array.Count)
            {
                Log.Error($"Disrupt: 输入参数错误，startIndex: {startIndex}, count: {count}, array.Count: {array.Count}");
            }

            var endIndex = startIndex + count;
            for (int i = endIndex - 1; i > startIndex; i--)
            {
                int j = RandomUtil.RandomRange(startIndex, i + 1);
                if (i != j)
                {
                    (array[i], array[j]) = (array[j], array[i]);
                }
            }
        }
        #endregion

        #region 查找
        /// <summary>
        /// 泛型二分查找 支持升序、降序查找
        /// </summary>
        /// <typeparam name="T">键的类型</typeparam>
        /// <typeparam name="K">值的类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="target">目标</param>
        /// <param name="keySelector">键选择器</param>
        /// <returns>返回对象在数组中的序号，若不存在，则返回-1</returns>
        public static int BinarySearch_TryFind<T, K>(IList<T> array, K target, Func<T, K> keySelector, bool descending = false) where K : IComparable<K>
        {
            if (array == null)
            {
                Log.Error("BinarySearch_TryFind: 输入数组不能为空");
            }

            if (keySelector == null)
            {
                Log.Error("BinarySearch_TryFind: 键选择器不能为空");
            }

            int first = 0;
            int last = array.Count - 1;
            while (first <= last)
            {
                int mid = first + ((last - first) >> 1);
                K midKey = keySelector(array[mid]);
                int cmp = midKey.CompareTo(target);

                // 反转比较结果
                if (descending)
                {
                    cmp = -cmp;
                }

                if (cmp > 0)
                {
                    last = mid - 1;
                }
                else if (cmp < 0)
                {
                    first = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return -1;
        }

        /// <summary>
        /// 把集合转成字典，便于后续高效查找。
        /// </summary>
        /// <typeparam name="TKey">键的类型</typeparam>
        /// <typeparam name="TValue">值的类型</typeparam>
        /// <param name="source">源数据集合</param>
        /// <param name="keySelector">键选择器</param>
        /// <returns>构建的字典</returns>
        public static Dictionary<TKey, TValue>? BuildDictionary<TKey, TValue>(IEnumerable<TValue>? source, Func<TValue, TKey>? keySelector) where TKey : notnull
        {
            if (source == null || keySelector == null)
            {
                return null;
            }

            var dictionary = new Dictionary<TKey, TValue>();
            foreach (var item in source)
            {
                var key = keySelector(item);
                if (!dictionary.ContainsKey(key))
                {
                    dictionary[key] = item;
                }
            }
            return dictionary;
        }

        /// <summary>
        /// 安全地在字典中查找键对应的值
        /// </summary>
        /// <typeparam name="TKey">键的类型</typeparam>
        /// <typeparam name="TValue">值的类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="key">要查找的键</param>
        /// <param name="value">查找到的值</param>
        /// <returns>是否找到</returns>
        public static bool TryFindInDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, out TValue? value) where TKey : notnull
        {
            if (dictionary == null)
            {
                Log.Error("TryFindInDictionary: 输入字典不能为空");
                value = default;
                return false;
            }

            return dictionary.TryGetValue(key, out value);
        }
        #endregion

        #region 排序
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
            K a = handler(array[start]);
            K b = handler(array[mid]);
            K c = handler(array[end]);

            if (a.CompareTo(b) > 0)
            {
                Swap(array, start, mid);
            }
            if (a.CompareTo(c) > 0)
            {
                Swap(array, start, end);
            }
            if (b.CompareTo(c) > 0)
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
            if (array == null)
            {
                Log.Error("Sort: 输入数组不能为空");
            }
            if (keySelector == null)
            {
                Log.Error("Sort: 键选择器不能为空");
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
        ///  获取最小
        /// </summary>
        public static T? Min<T, K>(IList<T> array, Func<T, K> handler) where K : IComparable<K>
        {
            T? temp = default(T);
            temp = array[0];
            foreach (var arr in array)
            {
                if (handler(temp).CompareTo(handler(arr)) > 0)
                {
                    temp = arr;
                }
            }

            return temp;
        }

        /// <summary>
        /// 获取最大值
        /// </summary>
        public static T? Max<T, K>(IList<T> array, Func<T, K> handler) where K : IComparable<K>
        {
            T? temp = default(T);
            temp = array[0];
            foreach (var arr in array)
            {
                if (handler(temp).CompareTo(handler(arr)) < 0)
                {
                    temp = arr;
                }
            }

            return temp;
        }

        /// <summary>
        /// 获取最小
        /// </summary>
        public static T? Min<T, K>(IList<T> array, Comparison<T> comparison)
        {
            T? temp = default(T);
            temp = array[0];
            foreach (var arr in array)
            {
                if (comparison(temp, arr) > 0)
                {
                    temp = arr;
                }
            }

            return temp;
        }

        /// <summary>
        /// 获取最大值
        /// </summary>
        public static T? Max<T, K>(IList<T> array, Comparison<T> comparison)
        {
            T? temp = default(T);
            temp = array[0];
            foreach (var arr in array)
            {
                if (comparison(temp, arr) < 0)
                {
                    temp = arr;
                }
            }

            return temp;
        }

        /// <summary>
        /// 获得传入元素某个符合条件的所有对象
        /// </summary>
        public static T? Find<T>(IList<T> array, Predicate<T> handler)
        {
            T? temp = default(T);
            for (int i = 0; i < array.Count; i++)
            {
                if (handler(array[i]))
                {
                    return array[i];
                }
            }

            return temp;
        }

        /// <summary>
        /// 获得传入元素某个符合条件的所有对象
        /// </summary>
        public static T[] FindAll<T>(IList<T> array, Predicate<T> handler)
        {
            var dstArray = new T[array.Count];
            int idx = 0;
            for (int i = 0; i < array.Count; i++)
            {
                if (handler(array[i]))
                {
                    dstArray[idx] = array[i];
                    idx++;
                }
            }

            Array.Resize(ref dstArray, idx);
            return dstArray;
        }
        #endregion

        #region Vector
        /// <summary>
        /// 计算两个向量之间的夹角
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static float Angle(Vector2 value1, Vector2 value2)
        {
            return Vector2.Angle(value1, value2);
        }

        /// <summary>
        /// 计算两个向量之间的夹角
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static float Angle(Vector3 value1, Vector3 value2)
        {
            return Vector3.Angle(value1, value2);
        }

        /// <summary>
        /// 将角度转换为二维向量
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static Vector2 AngleToVector2D(float angle)
        {
            float radian = Mathf.Deg2Rad * angle; // 角度转弧度
            return new Vector2(Mathf.Cos(radian), Mathf.Sin(radian)).normalized; // 得到单位向量
        }

        /// <summary>
        /// 获取两个点之间的中间点（百分比0-1）
        /// </summary>
        /// <param name="start">起始点</param>
        /// <param name="end">结束点</param>
        /// <param name="percent">百分比</param>
        /// <returns></returns>
        public static Vector3 GetBetweenPointPercent(Vector3 start, Vector3 end, float percent)
        {
            return Vector3.Lerp(start, end, percent);
        }

        /// <summary>
        /// 获取两个点之间的中间点（距离）
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static Vector3 GetBetweenPointDistance(Vector3 start, Vector3 end, float distance)
        {
            return start + (end - start).normalized * distance;
        }

        /// <summary>
        /// 获取圆、椭圆上某个角度的相对位置
        /// angle = 0，返回椭圆最右侧点 (longHalfAxis, 0)
        /// angle = 90，返回椭圆最上侧点(0, shortHalfAxis)
        /// angle = 180，返回椭圆最左侧点(-longHalfAxis, 0)
        /// angle = 270，返回椭圆最下侧点(0, -shortHalfAxis)
        /// </summary>
        /// <param name="longHalfAxis">长半轴</param>
        /// <param name="shortHalfAxis">短半轴</param>
        /// <param name="angle">角度</param>
        /// <returns></returns>
        public static Vector2 GetRelativePositionOfEllipse(float longHalfAxis, float shortHalfAxis, float angle)
        {
            var rad = angle * Mathf.Deg2Rad; // 弧度
            var newPos = Vector2.right * longHalfAxis * Mathf.Cos(rad) + Vector2.up * shortHalfAxis * Mathf.Sin(rad);
            return newPos;
        }

        /// <summary>
        /// 获得固定位数小数的向量
        /// </summary>
        public static Vector3 Round(this Vector3 value, int decimals)
        {
            value.x = (float)Math.Round(value.x, decimals);
            value.y = (float)Math.Round(value.y, decimals);
            value.z = (float)Math.Round(value.z, decimals);
            return value;
        }

        /// <summary>
        /// 获得固定位数小数的向量
        /// </summary>
        public static Vector2 Round(this Vector2 value, int decimals)
        {
            value.x = (float)Math.Round(value.x, decimals);
            value.y = (float)Math.Round(value.y, decimals);
            return value;
        }

        /// <summary>
        /// 限制一个三维向量在最大值与最小值之间
        /// </summary>
        public static Vector3 Clamp(this Vector3 value, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            value.x = Mathf.Clamp(value.x, minX, maxX);
            value.y = Mathf.Clamp(value.y, minY, maxY);
            value.z = Mathf.Clamp(value.z, minZ, maxZ);
            return value;
        }

        /// <summary>
        /// 限制一个二维向量在最大值与最小值之间
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public static Vector2 Clamp(this Vector2 value, float minX, float minY, float maxX, float maxY)
        {
            value.x = Mathf.Clamp(value.x, minX, maxX);
            value.y = Mathf.Clamp(value.y, minY, maxY);
            return value;
        }

        /// <summary>
        /// 计算中心点
        /// </summary>
        /// <param name="Points"></param>
        /// <returns></returns>
        public static Vector3 CalculateCenterPoint(List<Transform> Points)
        {
            return Points.Aggregate(Vector3.zero, (acc, p) => acc + p.position) / Points.Count;
        }

        /// <summary>
        /// 获取BoxCollider内的随机位置
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public static Vector3 GetRandomPositionInBoxCollider(BoxCollider collider, int method = 1)
        {
            return new Vector3(UnityEngine.Random.Range(collider.bounds.min.x, collider.bounds.max.x),
                               UnityEngine.Random.Range(collider.bounds.min.y, collider.bounds.max.y),
                               UnityEngine.Random.Range(collider.bounds.min.z, collider.bounds.max.z));
        }

        /// <summary>
        /// 获取一个球内随机点（整体）
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="radius">半径</param>
        /// <returns>球内随机点</returns>
        public static Vector3 GetRandomPointInSphere(Vector3 center, float radius)
        {
            if (radius < 0)
                radius = 0;
            var rndPtr = UnityEngine.Random.insideUnitSphere * radius;
            var rndPos = rndPtr + center;
            return rndPos;
        }

        /// <summary>
        /// 获取一个球内随机点（环带）
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="miniRadius">最小半径</param>
        /// <param name="maxRadius">最大半径</param>
        /// <returns>球内随机点</returns>
        public static Vector3 GetRandomPointInSphere(Vector3 center, float miniRadius, float maxRadius)
        {
            if (miniRadius < 0)
            {
                miniRadius = 0;
            }
            if (maxRadius < miniRadius)
            {
                maxRadius = miniRadius;
            }
            var randomRadius = UnityEngine.Random.Range(miniRadius, maxRadius);
            var rndPtr = UnityEngine.Random.insideUnitSphere * randomRadius;
            var rndPos = rndPtr + center;
            return rndPos;
        }

        /// <summary>
        /// 生成一组以startPos为中心、在startDirection垂直方向上等间距排列的点。常用于队列、阵型、道路布点等需要横向分布的场景。
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="startDirection"></param>
        /// <param name="nNum"></param>
        /// <param name="meterInterval"></param>
        /// <returns></returns>
        public static Vector3[] GetParallelPoints(Vector3 startPos, Vector3 startDirection, int nNum, float meterInterval)
        {
            Vector3[] targetPos = new Vector3[nNum];
            Vector3 perpendicularDirection = Quaternion.AngleAxis(90, Vector3.forward) * startDirection.normalized; // 计算垂直方向
            int halfNum = nNum / 2;
            bool isEven = nNum % 2 == 0;

            for (int i = 0; i < nNum; i++)
            {
                int indexOffset = i - halfNum + (isEven ? 1 : 0);
                float distance = indexOffset * meterInterval + (isEven ? meterInterval / 2 : 0);
                targetPos[i] = startPos + perpendicularDirection * distance;
            }

            return targetPos;
        }

        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        /// <param name="ps1"></param>
        /// <param name="pe1"></param>
        /// <param name="ps2"></param>
        /// <param name="pe2"></param>
        /// <returns></returns>
        public static (bool, Vector3) LineIntersectionPoint(
            Vector3 ps1,
            Vector3 pe1,
            Vector3 ps2,
            Vector3 pe2
        )
        {
            // 快速排斥实验，先排除无交点的情况
            if (Mathf.Min(ps1.x, pe1.x) > Mathf.Max(ps2.x, pe2.x) || Mathf.Max(ps1.x, pe1.x) < Mathf.Min(ps2.x, pe2.x) ||
                Mathf.Min(ps1.y, pe1.y) > Mathf.Max(ps2.y, pe2.y) || Mathf.Max(ps1.y, pe1.y) < Mathf.Min(ps2.y, pe2.y) ||
                Mathf.Min(ps1.z, pe1.z) > Mathf.Max(ps2.z, pe2.z) || Mathf.Max(ps1.z, pe1.z) < Mathf.Min(ps2.z, pe2.z))
            {
                return (false, Vector3.zero);
            }

            Vector3 ab = pe1 - ps1;
            Vector3 cd = pe2 - ps2;
            Vector3 ca = ps1 - ps2;

            // 判断共面
            Vector3 v1 = Vector3.Cross(ca, cd);
            float coplanar = Mathf.Abs(Vector3.Dot(v1, ab));
            if (coplanar > Mathf.Epsilon)
            {
                return (false, Vector3.zero);
            }

            // 判断平行
            Vector3 ab_cd = Vector3.Cross(ab, cd);
            if (ab_cd.sqrMagnitude <= Mathf.Epsilon)
            {
                return (false, Vector3.zero);
            }

            // 跨立试验
            Vector3 ad = pe2 - ps1;
            Vector3 cb = pe1 - ps2;
            float s1 = Vector3.Dot(Vector3.Cross(-ca, ab), Vector3.Cross(ab, ad));
            float s2 = Vector3.Dot(Vector3.Cross(ca, cd), Vector3.Cross(cd, cb));
            if (s1 > 0 && s2 > 0)
            {
                Vector3 v2 = Vector3.Cross(cd, ab);
                float ratio = Vector3.Dot(v1, v2) / v2.sqrMagnitude;
                Vector3 intersectPos = ps1 + ab * ratio;
                return (true, intersectPos);
            }

            return (false, Vector3.zero);
        }

        /// <summary>
        /// 计算两条线段的交点
        /// </summary>
        /// <param name="ps1"></param>
        /// <param name="pe1"></param>
        /// <param name="ps2"></param>
        /// <param name="pe2"></param>
        /// <returns></returns>
        public static (bool, Vector2) LineIntersectionPoint(Vector2 ps1, Vector2 pe1, Vector2 ps2, Vector2 pe2)
        {
            float A1 = pe1.y - ps1.y;
            float B1 = ps1.x - pe1.x;
            float C1 = A1 * ps1.x + B1 * ps1.y;

            float A2 = pe2.y - ps2.y;
            float B2 = ps2.x - pe2.x;
            float C2 = A2 * ps2.x + B2 * ps2.y;

            float delta = A1 * B2 - A2 * B1;
            if (Mathf.Abs(delta) < Mathf.Epsilon)
            {
                // 平行或重合
                return (false, Vector2.zero);
            }

            Vector2 intersectPoint = new Vector2(
                (B2 * C1 - B1 * C2) / delta,
                (A1 * C2 - A2 * C1) / delta
            );

            // 判断交点是否在线段ps1-pe1上
            bool onSeg1 =
                intersectPoint.x >= Mathf.Min(ps1.x, pe1.x) - Mathf.Epsilon &&
                intersectPoint.x <= Mathf.Max(ps1.x, pe1.x) + Mathf.Epsilon &&
                intersectPoint.y >= Mathf.Min(ps1.y, pe1.y) - Mathf.Epsilon &&
                intersectPoint.y <= Mathf.Max(ps1.y, pe1.y) + Mathf.Epsilon;

            // 判断交点是否在线段ps2-pe2上
            bool onSeg2 =
                intersectPoint.x >= Mathf.Min(ps2.x, pe2.x) - Mathf.Epsilon &&
                intersectPoint.x <= Mathf.Max(ps2.x, pe2.x) + Mathf.Epsilon &&
                intersectPoint.y >= Mathf.Min(ps2.y, pe2.y) - Mathf.Epsilon &&
                intersectPoint.y <= Mathf.Max(ps2.y, pe2.y) + Mathf.Epsilon;

            if (onSeg1 && onSeg2)
            {
                return (true, intersectPoint);
            }

            return (false, Vector2.zero);
        }

        /// <summary>
        /// 在某个中心点周围，生成一组等角度分布、距离相同的点，常用于NPC环绕、队形等需求
        /// </summary>
        /// <param name="startDirection">起始方向</param>
        /// <param name="nNum">需要的数量</param>
        /// <param name="pAnchorPos"><锚点/param>
        /// <param name="fAngle">角度</param>
        /// <param name="nRadius">半径</param>
        /// <returns></returns>
        public static Vector3[] GetSmartNpcPoints(Vector3 startDirection, int nNum, Vector3 pAnchorPos, float fAngle, float nRadius)
        {
            Vector3[] points = new Vector3[nNum];
            // 每个点之间的角度增量
            float angleIncrement = fAngle / nNum;
            // 用于旋转的四元数
            Quaternion rotation = Quaternion.Euler(0, angleIncrement, 0);
            // 初始方向向量，确保其被规范化并乘以半径
            Vector3 direction = startDirection.normalized * nRadius;

            for (int i = 0; i < nNum; i++)
            {
                // 计算每个点的位置
                points[i] = pAnchorPos + direction;
                // 更新方向向量以指向下一个点
                direction = rotation * direction;
            }

            return points;
        }

        /// <summary>
        /// 将屏幕坐标转换为目标分辨率下的坐标
        /// </summary>
        /// <param name="originalX">原始X</param>
        /// <param name="originalY">原始Y</param>
        /// <param name="originalWidth">原始W</param>
        /// <param name="originalHight">原始H</param>
        /// <param name="targetWidth">目标W</param>
        /// <param name="targetHight">目标H</param>
        /// <returns></returns>
        public static Vector2 ConvertScreenPoint(float originalX, float originalY, float originalWidth, float originalHight, float targetWidth, float targetHight)
        {
            // 计算宽度和高度的缩放比例
            float scaleX = targetWidth / originalWidth;
            float scaleY = targetHight / originalHight;

            // 应用缩放比例到原始点位
            float newX = originalX * scaleX;
            float newY = originalY * scaleY;

            return new Vector2(newX, newY);
        }

        /// <summary>
        /// 获取两个Transform之间的旋转方向
        /// </summary>
        /// <param name="forward1">前方 半挂车 车头</param>
        /// <param name="forward2">后方 半挂车 半挂</param>
        /// <returns></returns>
        public static RotationDirection GetRotationDirection(Vector2 forward1, Vector2 forward2)
        {
            Vector2 v1 = forward1;
            Vector2 v2 = forward2;

            float rightFloat = v1.x * v2.y - v2.x * v1.y;

            if (rightFloat < 0)
            {
                return RotationDirection.Right;
            }
            else if (rightFloat > 0)
            {
                return RotationDirection.Left;
            }
            else
            {
                return RotationDirection.None;
            }
        }

        /// <summary>
        /// 在指定的容器中找到距离最近的位置
        /// </summary>
        /// <param name="position">自己的位置</param>
        /// <param name="otherPositions">其他对象的位置</param>
        /// <returns>最近的位置</returns>
        public static Vector3 GetClosest(this Vector3 position, IEnumerable<Vector3> otherPositions)
        {
            Vector3 closest = Vector3.zero;
            float shortestDistance = Mathf.Infinity;
            Vector3 difference;

            foreach (var otherPosition in otherPositions)
            {
                difference = position - otherPosition;
                float distance = difference.sqrMagnitude;

                if (distance < shortestDistance)
                {
                    closest = otherPosition;
                    shortestDistance = distance;
                }
            }

            return closest;
        }

        /// <summary>
        /// 将向量旋转指定角度
        /// </summary>
        /// <param name="vector">要旋转的向量</param>
        /// <param name="angleInDeg">角度（度）</param>
        /// <returns>旋转向量</returns>
        public static Vector2 Rotate(this Vector2 vector, float angleInDeg)
        {
            float angleInRad = Mathf.Deg2Rad * angleInDeg;
            float cosAngle = Mathf.Cos(angleInRad);
            float sinAngle = Mathf.Sin(angleInRad);

            float x = vector.x * cosAngle - vector.y * sinAngle;
            float y = vector.x * sinAngle + vector.y * cosAngle;

            return new Vector2(x, y);
        }

        /// <summary>
        /// 将向量围绕目标点旋转指定角度
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="angleInDeg">角度</param>
        /// <param name="axisPosition">目标点</param>
        /// <returns></returns>
        public static Vector2 RotateAround(this Vector2 vector, float angleInDeg, Vector2 axisPosition)
        {
            return (vector - axisPosition).Rotate(angleInDeg) + axisPosition;
        }


        /// <summary>
        /// 将向量旋转90度
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 Rotate90(this Vector2 vector)
        {
            return new Vector2(-vector.y, vector.x);
        }

        /// <summary>
        /// 将向量旋转180度
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 Rotate180(this Vector2 vector)
        {
            return new Vector2(-vector.x, -vector.y);
        }

        /// <summary>
        /// 将向量旋转270度
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 Rotate270(this Vector2 vector)
        {
            return new Vector2(vector.y, -vector.x);
        }

        /// <summary>
        /// 计算一个点在指定轴上的最近点
        /// </summary>
        /// <param name="axisDirection">轴的方向</param>
        /// <param name="point">要计算的点</param>
        /// <returns>点在轴上的最近点</returns>
        public static Vector3 NearestPointOnAxis(this Vector3 axisDirection, Vector3 point)
        {
            // 确保轴的方向是单位向量
            axisDirection.Normalize();

            // 计算点和轴方向的点积，得到点在轴上的投影长度
            var d = Vector3.Dot(point, axisDirection);

            // 将点积乘以轴的方向，得到点在轴上的最近点
            return axisDirection * d;
        }

        /// <summary>
        /// 计算一个点在给定直线上的最近点
        /// </summary>
        /// <param name="lineDirection">直线的方向向量</param>
        /// <param name="point">要计算的空间点</param>
        /// <param name="pointOnLine">用于唯一确定直线的位置，是直线上的一个已知点</param>
        /// <returns>点在直线上的最近点</returns>
        public static Vector3 NearestPointOnLine(this Vector3 lineDirection, Vector3 point, Vector3 pointOnLine)
        {
            // 确保直线的方向是单位向量
            lineDirection.Normalize();

            // 计算点和直线上的点的差，然后和直线方向的点积，得到点在直线上的投影长度
            var d = Vector3.Dot(point - pointOnLine, lineDirection);

            // 将点积乘以直线的方向，然后加上直线上的点，得到点在直线上的最近点
            return pointOnLine + lineDirection * d;
        }

        /// <summary>
        /// 计算一个点在给定平面上的最近点
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool IsFinite(this Vector2 v)
        {
            return v.x.IsFinite() && v.y.IsFinite();
        }

        /// <summary>
        /// 计算一个点在给定平面上的最近点
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
