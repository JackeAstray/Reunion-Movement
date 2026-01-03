using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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
            return Convert.ToBoolean(value & 0x1);
        }

        /// <summary>
        /// 是否是偶数
        /// </summary>
        /// <param name="value">检测的值</param>
        /// <returns>是否是偶数</returns>
        public static bool IsEven(long value)
        {
            return !Convert.ToBoolean(value & 0x1);
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
            int result = 0;
            int length = array.Length;
            for (int i = 0; i < length; i++)
            {
                result += Convert.ToInt32((Math.Abs(array[i]) * Math.Pow(10, length - 1 - i)));
            }

            return result;
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
                return null;
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
        public static void Shuffle<T>(IList<T> array)
        {
            if (array == null)
            {
                Log.Error("Distinct: 输入数组不能为空");
                return;
            }
            Shuffle(array, 0, array.Count);
        }

        /// <summary>
        /// 随机打乱数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="startIndex">起始序号</param>
        /// <param name="count">数量</param>
        public static void Shuffle<T>(IList<T> array, int startIndex, int count)
        {
            if (array == null)
            {
                Log.Error("Distinct: 输入数组不能为空");
                return;
            }

            if (startIndex < 0 || count < 0 || startIndex + count > array.Count)
            {
                Log.Error($"Disrupt: 输入参数错误，startIndex: {startIndex}, count: {count}, array.Count: {array.Count}");
                return;
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
                return -1;
            }

            if (keySelector == null)
            {
                Log.Error("BinarySearch_TryFind: 键选择器不能为空");
                return -1;
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
        public static Dictionary<TKey, TValue> BuildDictionary<TKey, TValue>(IEnumerable<TValue> source, Func<TValue, TKey> keySelector) where TKey : notnull
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
        public static bool TryFindInDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, out TValue value) where TKey : notnull
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

        #region Engine
        /// <summary>
        /// 设置屏幕分辨率
        /// </summary>
        /// <param name="width">屏幕宽度</param>
        /// <param name="height">屏幕高度</param>
        /// <param name="fullScreen">是否全屏显示</param>
        public static void SetScreen(int width, int height, bool fullScreen)
        {
            Screen.SetResolution(width, height, fullScreen);
        }

        /// <summary>
        /// 打开一个URL链接
        /// </summary>
        /// <param name="url"></param>
        public static void OpenURL(string url)
        {
            Application.OpenURL(url);
        }

        /// <summary>
        /// 退出
        /// </summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// 网络可用
        /// </summary>
        public static bool NetAvailable
        {
            get
            {
                return Application.internetReachability != NetworkReachability.NotReachable;
            }
        }

        /// <summary>
        /// 是否是无线
        /// </summary>
        public static bool IsWifi
        {
            get
            {
                return Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
            }
        }
        #endregion

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
                return;
            }
            if (keySelector == null)
            {
                Log.Error("Sort: 键选择器不能为空");
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
            if (array == null || array.Count == 0)
            {
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
            return MinMax(array, (a, b) => keySelector(a).CompareTo(keySelector(b)), false);
        }

        /// <summary>
        /// 获取最大值
        /// </summary>
        public static T Max<T, K>(IList<T> array, Func<T, K> keySelector) where K : IComparable<K>
        {
            return MinMax(array, (a, b) => keySelector(a).CompareTo(keySelector(b)), true);
        }

        /// <summary>
        /// 获取最小值（自定义比较器）
        /// </summary>
        public static T Min<T>(IList<T> array, Comparison<T> comparison)
        {
            return MinMax(array, comparison, false);
        }

        /// <summary>
        /// 获取最大值（自定义比较器）
        /// </summary>
        public static T Max<T>(IList<T> array, Comparison<T> comparison)
        {
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
                    return default;
                return list[RandomUtil.random.Next(list.Count)];
            }

            // 其它 IEnumerable，使用蓄水池抽样算法
            int count = 0;
            T selected = default;
            foreach (var item in source)
            {
                count++;
                if (RandomUtil.random.Next(count) == 0)
                    selected = item;
            }
            return count == 0 ? default : selected;
        }

        /// <summary>
        /// 筛选(列表)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="testAction"></param>
        /// <returns></returns>
        public static List<T> Filter<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
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
            return source.Where(pair => testAction(pair.Key, pair.Value)).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        /// <summary>
        /// 给哈希集添加批量数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="this"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> other)
        {
            if (other == null)
            {
                return;
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
                Log.Error("list is null");
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
                Log.Error("list is null");
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
                Log.Error("source 为空");
                return -1;
            }

            if (index < 0)
            {
                Log.Error("index is less than the lower bound of array.");
                return -1;
            }

            if (length < 0)
            {
                Log.Error("Value has to be >= 0.");
                return -1;
            }

            if (index > source.Count - length)
            {
                Log.Error("index and length do not specify a valid range in array.");
                return -1;
            }

            comparer ??= Comparer<TElement>.Default;

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
                Log.Error("list is null");
                return;
            }
            if (sourceIndex < 0 || sourceIndex >= list.Count)
            {
                Log.Error("sourceIndex is out of range");
                return;
            }
            if (destinationIndex < 0 || destinationIndex >= list.Count)
            {
                Log.Error("destinationIndex is out of range");
                return;
            }

            var item = list[sourceIndex];
            list.RemoveAt(sourceIndex);
            list.Insert(destinationIndex, item);
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
                Log.Error("list is null");
                return;
            }
            if (sourceIndex < 0 || sourceIndex >= list.Length)
            {
                Log.Error("sourceIndex is out of range");
                return;
            }
            if (destinationIndex < 0 || destinationIndex >= list.Length)
            {
                Log.Error("destinationIndex is out of range");
                return;
            }
            var item = list[sourceIndex];
            Array.Copy(list, sourceIndex + 1, list, sourceIndex, destinationIndex - sourceIndex);
            list[destinationIndex] = item;
        }

        /// <summary>
        /// 转成数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T[] ToArray<T>(this IEnumerable<T> source)
        {
            return source.ToList().ToArray();
        }

        /// <summary>
        /// 转成列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static List<T> ToList<T>(this IEnumerable<T> source)
        {
            return new List<T>(source);
        }

        /// <summary>
        /// 列表合并
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static List<T> Union<T>(this List<T> first, List<T> second, IEqualityComparer<T> comparer)
        {
            return first.Concat(second).Distinct(comparer).ToList();
        }
        #endregion

        #region Component
        /// <summary>
        /// 将一个组件附加到给定组件的游戏对象
        /// </summary>
        /// <param name="component">Component.</param>
        /// <returns>Newly attached component.</returns>
        public static T AddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 获取附加到给定组件的游戏对象的组件
        /// 如果没有找到，则会附加一个新的并返回
        /// </summary>
        /// <param name="component">Component.</param>
        /// <returns>Previously or newly attached component.</returns>
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            var existingComponent = component.GetComponent<T>();
            return existingComponent != null ? existingComponent : component.AddComponent<T>();
        }

        /// <summary>
        /// 检查组件的游戏对象是否附加了类型为T的组件
        /// </summary>
        /// <param name="component">Component.</param>
        /// <returns>True when component is attached.</returns>
        public static bool HasComponent<T>(this Component component) where T : Component
        {
            return component.GetComponent<T>() != null;
        }

        /// <summary>
        /// 搜索子物体组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static T Get<T>(this Component go, string subnode) where T : Component
        {
            var transform = go.transform.Find(subnode);
            return transform != null ? transform.GetComponent<T>() : null;
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
        /// <param name="originalHeight">原始H</param>
        /// <param name="targetWidth">目标W</param>
        /// <param name="targetHeight">目标H</param>
        /// <returns></returns>
        public static Vector2 ConvertScreenPoint(float originalX, float originalY, float originalWidth, float originalHeight, float targetWidth, float targetHeight)
        {
            // 计算宽度和高度的缩放比例
            float scaleX = targetWidth / originalWidth;
            float scaleY = targetHeight / originalHeight;

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
        #endregion

        #region Transform
        /// <summary>
        /// 使指定的多个GameObject成为子节点
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="children"></param>
        public static void AddChildren(this Transform transform, GameObject[] children)
        {
            foreach (var child in children)
            {
                child.transform.parent = transform;
            }
        }

        /// <summary>
        /// 使指定的多个Component成为子节点
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="children"></param>
        public static void AddChildren(this Transform transform, Component[] children)
        {
            foreach (var child in children)
            {
                child.transform.parent = transform;
            }
        }

        /// <summary>
        /// 重置子节点位置
        /// </summary>
        /// <param name="transform">父对象</param>
        /// <param name="recursive">父对象一起重置</param>
        public static void ResetChildPositions(this Transform transform, bool recursive = false)
        {
            foreach (Transform child in transform)
            {
                child.localPosition = Vector3.zero;

                if (recursive)
                {
                    child.ResetChildPositions(true);
                }
            }
        }

        /// <summary>
        /// 设置子层级的layer
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="layerName"></param>
        /// <param name="recursive"></param>
        public static void SetChildLayers(this Transform transform, string layerName, bool recursive = false)
        {
            var layer = LayerMask.NameToLayer(layerName);

            foreach (Transform child in transform)
            {
                child.gameObject.layer = layer;

                if (recursive)
                {
                    child.SetChildLayers(layerName, true);
                }
            }
        }

        /// <summary>
        /// 设置XYZ位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public static void SetXYZ(this Transform transform, float x, float y, float z)
        {
            transform.position = new Vector3(x, y, z);
        }

        /// <summary>
        /// 设置XYZ位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public static void SetLocalXYZ(this Transform transform, float x, float y, float z)
        {
            transform.localPosition = new Vector3(x, y, z);
        }

        /// <summary>
        /// 设置X、Y和Y缩放
        /// </summary>
        public static void SetScaleXYZ(this Transform transform, float x, float y, float z)
        {
            transform.localScale = new Vector3(x, y, z);
        }

        /// <summary>
        /// 在X、Y和Z方向上缩放
        /// </summary>
        public static void ScaleByXYZ(this Transform transform, float x, float y, float z)
        {
            transform.localScale = new Vector3(x, y, z);
        }

        /// <summary>
        /// 在X、Y和Z方向上缩放
        /// </summary>
        public static void ScaleByXYZ(this Transform transform, float r)
        {
            transform.ScaleByXYZ(r, r, r);
        }

        /// <summary>
        /// 设置X轴旋转
        /// </summary>
        public static void SetRotationX(this Transform transform, float angle)
        {
            transform.eulerAngles = new Vector3(angle, 0, 0);
        }

        /// <summary>
        /// 设置Y轴旋转
        /// </summary>
        public static void SetRotationY(this Transform transform, float angle)
        {
            transform.eulerAngles = new Vector3(0, angle, 0);
        }

        /// <summary>
        /// 设置Z轴旋转
        /// </summary>
        public static void SetRotationZ(this Transform transform, float angle)
        {
            transform.eulerAngles = new Vector3(0, 0, angle);
        }

        /// <summary>
        /// 设置本地X轴旋转
        /// </summary>
        public static void SetLocalRotationX(this Transform transform, float angle)
        {
            transform.localRotation = Quaternion.Euler(new Vector3(angle, 0, 0));
        }

        /// <summary>
        /// 设置本地Y轴旋转
        /// </summary>
        public static void SetLocalRotationY(this Transform transform, float angle)
        {
            transform.localRotation = Quaternion.Euler(new Vector3(0, angle, 0));
        }

        /// <summary>
        /// 设置本地Z轴旋转
        /// </summary>
        public static void SetLocalRotationZ(this Transform transform, float angle)
        {
            transform.localRotation = Quaternion.Euler(new Vector3(0, 0, angle));
        }

        /// <summary>
        /// 重置位置
        /// </summary>
        public static void ResetPosition(this Transform transform)
        {
            transform.position = Vector3.zero;
        }

        /// <summary>
        /// 重置位置
        /// </summary>
        public static void ResetLocalPosition(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// 重置旋转
        /// </summary>
        /// <param name="transform"></param>
        public static void ResetRotation(this Transform transform)
        {
            transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// 重置旋转
        /// </summary>

        public static void ResetLocalRotation(this Transform transform)
        {
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 重置本地位置/旋转/缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void ResetLocal(this Transform transform)
        {
            transform.ResetLocalRotation();
            transform.ResetLocalPosition();
            transform.ResetScale();

        }

        /// <summary>
        /// 重置位置/旋转/缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void Reset(this Transform transform)
        {
            transform.ResetRotation();
            transform.ResetPosition();
            transform.ResetScale();
        }

        /// <summary>
        /// 重置缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void ResetScale(this Transform transform)
        {
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 计算该物体的位置。无论它位于顶部还是底部。分别为-1和1。
        /// </summary>
        /// <returns></returns>
        public static int CloserEdge(this Transform transform, Camera camera, int width, int height)
        {
            // 世界坐标转换为屏幕坐标
            var worldPointTop = camera.ScreenToWorldPoint(new Vector3(width / 2, height));
            var worldPointBot = camera.ScreenToWorldPoint(new Vector3(width / 2, 0));
            // 计算距离
            var deltaTop = Vector2.Distance(worldPointTop, transform.position);
            var deltaBottom = Vector2.Distance(worldPointBot, transform.position);

            return deltaBottom <= deltaTop ? 1 : -1;
        }

        /// <summary>
        /// 搜索子物体组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tf"></param>
        /// <param name="objName"></param>
        /// <returns></returns>
        public static T Get<T>(this Transform tf, string objName) where T : Component
        {
            var sub = tf?.Find(objName);
            return sub?.GetComponent<T>();
        }

        /// <summary>
        /// 清除所有子节点
        /// </summary>
        /// <param name="tf"></param>
        public static void ClearChild(this Transform tf)
        {
            if (tf == null) return;
            for (int i = tf.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(tf.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// 将位置旋转缩放清零
        /// </summary>
        /// <param name="tf"></param>
        public static void ResetLocalTransform(this Transform tf)
        {
            tf.localPosition = Vector3.zero;
            tf.localRotation = Quaternion.identity;
            tf.localScale = Vector3.one;
        }

        /// <summary>
        /// 查找子项
        /// </summary>
        /// <param name="findTrans"></param>
        /// <param name="objName"></param>
        /// <param name="check_visible">检查可见性</param>
        /// <param name="raise_error">抛出错误</param>
        /// <returns></returns>
        public static Transform Child(this Transform findTrans, string objName, bool check_visible = false, bool raise_error = true)
        {
            if (!findTrans)
            {
                if (raise_error)
                {
                    Log.Error("查找失败. findTrans是空的!");
                }
                return null;
            }

            if (string.IsNullOrEmpty(objName))
            {
                return null;
            }
            // 如果需要检查可见性，但是当前物体不可见
            if (check_visible && !findTrans.gameObject.activeInHierarchy)
            {
                return null;
            }
            if (objName == ".")
            {
                return findTrans;
            }

            var ids = objName.Split('/');

            foreach (var id1 in ids)
            {
                findTrans = ChildDirect(findTrans, id1, check_visible);
                if (findTrans == null)
                {
                    // 如果需要抛出错误
                    if (raise_error)
                    {
                        Log.Error($"查找子项失败, id:{objName} ,parent={findTrans.name}");
                    }
                    break;
                }
            }

            return findTrans;
        }

        /// <summary>
        /// 查找子项
        /// </summary>
        /// <param name="trans"></param>
        /// <param name="objName"></param>
        /// <param name="check_visible"></param>
        /// <returns></returns>
        private static Transform ChildDirect(this Transform trans, string objName, bool check_visible)
        {
            if (trans == null || string.IsNullOrEmpty(objName))
            {
                return null;
            }

            var child = trans.Find(objName);
            if (child != null && (!check_visible || child.gameObject.activeInHierarchy))
            {
                return child;
            }

            if (!check_visible)
            {
                // 如果不检查可见性且未找到匹配项，直接返回null
                return null;
            }

            foreach (Transform t in trans)
            {
                if (t.gameObject.activeInHierarchy)
                {
                    var found = ChildDirect(t, objName, true);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取从父节点到自己的完整路径
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static string GetRootPathName(this Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            StringBuilder path = new StringBuilder();
            BuildPath(transform, ref path);
            return path.ToString();
        }

        /// <summary>
        /// 递归构建Transform的完整路径名称
        /// </summary>
        /// <param name="current"></param>
        /// <param name="path"></param>
        private static void BuildPath(Transform current, ref StringBuilder path)
        {
            if (current.parent != null)
            {
                BuildPath(current.parent, ref path);
                path.Append("/");
            }
            path.Append(current.name);
        }

        /// <summary>
        /// 旋转物体，处理万向锁
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="v3"></param>
        /// <param name="order"></param>
        /// <param name="algorithmType"></param>
        public static void RotateXYZ(this Transform transform, Vector3 v3, XYZOrder order, XYZAlgorithmType algorithmType = XYZAlgorithmType.Quaternion)
        {
            if (algorithmType == XYZAlgorithmType.Quaternion)
            {
                transform.rotation = RotateXYZ_Quaternion(v3, order);
            }
            else
            {
                transform.rotation = RotateXYZ_Matrix4x4(v3, order);
            }
        }

        /// <summary>
        /// 旋转物体，处理万向锁 采用四元数计算
        /// </summary>
        /// <param name="v3"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public static Quaternion RotateXYZ_Quaternion(Vector3 v3, XYZOrder order)
        {
            Quaternion xRot = Quaternion.AngleAxis(v3.x, Vector3.right);
            Quaternion yRot = Quaternion.AngleAxis(v3.y, Vector3.up);
            Quaternion zRot = Quaternion.AngleAxis(v3.z, Vector3.forward);

            Quaternion combinedRotation;

            switch (order)
            {
                case XYZOrder.XYZ:
                    combinedRotation = xRot * yRot * zRot;
                    break;
                case XYZOrder.XZY:
                    combinedRotation = xRot * zRot * yRot;
                    break;
                case XYZOrder.YXZ:
                    combinedRotation = yRot * xRot * zRot;
                    break;
                case XYZOrder.YZX:
                    combinedRotation = yRot * zRot * xRot;
                    break;
                case XYZOrder.ZXY:
                    combinedRotation = zRot * xRot * yRot;
                    break;
                case XYZOrder.ZYX:
                    combinedRotation = zRot * yRot * xRot;
                    break;
                // 与unity inspector中的顺序一致
                default:
                    combinedRotation = yRot * xRot * zRot;
                    break;
            }

            return combinedRotation;
        }

        /// <summary>
        /// 旋转物体，处理万向锁 采用矩阵计算
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public static Quaternion RotateXYZ_Matrix4x4(Vector3 v3, XYZOrder order)
        {
            Matrix4x4 xRot = RotXMat(v3.x * Mathf.Deg2Rad);
            Matrix4x4 yRot = RotYMat(v3.y * Mathf.Deg2Rad);
            Matrix4x4 zRot = RotZMat(v3.z * Mathf.Deg2Rad);

            Matrix4x4 combinedRotation;

            switch (order)
            {
                case XYZOrder.XYZ:
                    combinedRotation = xRot * yRot * zRot;
                    break;
                case XYZOrder.XZY:
                    combinedRotation = xRot * zRot * yRot;
                    break;
                case XYZOrder.YXZ:
                    combinedRotation = yRot * xRot * zRot;
                    break;
                case XYZOrder.YZX:
                    combinedRotation = yRot * zRot * xRot;
                    break;
                case XYZOrder.ZXY:
                    combinedRotation = zRot * xRot * yRot;
                    break;
                case XYZOrder.ZYX:
                    combinedRotation = zRot * yRot * xRot;
                    break;
                // 与unity inspector中的顺序一致
                default:
                    combinedRotation = yRot * xRot * zRot;
                    break;
            }

            return combinedRotation.rotation;
        }

        static Matrix4x4 RotXMat(float angle)
        {
            Matrix4x4 rxmat = new Matrix4x4();
            rxmat.SetRow(0, new Vector4(1f, 0f, 0f, 0f));
            rxmat.SetRow(1, new Vector4(0f, Mathf.Cos(angle), -Mathf.Sin(angle), 0f));
            rxmat.SetRow(2, new Vector4(0f, Mathf.Sin(angle), Mathf.Cos(angle), 0f));
            rxmat.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            return rxmat;
        }

        static Matrix4x4 RotYMat(float angle)
        {
            Matrix4x4 rymat = new Matrix4x4();
            rymat.SetRow(0, new Vector4(Mathf.Cos(angle), 0f, Mathf.Sin(angle), 0f));
            rymat.SetRow(1, new Vector4(0f, 1f, 0f, 0f));
            rymat.SetRow(2, new Vector4(-Mathf.Sin(angle), 0f, Mathf.Cos(angle), 0f));
            rymat.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            return rymat;
        }

        static Matrix4x4 RotZMat(float angle)
        {
            Matrix4x4 rzmat = new Matrix4x4();
            rzmat.SetRow(0, new Vector4(Mathf.Cos(angle), -Mathf.Sin(angle), 0f, 0f));
            rzmat.SetRow(1, new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), 0f, 0f));
            rzmat.SetRow(2, new Vector4(0f, 0f, 1f, 0f));
            rzmat.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            return rzmat;
        }
        #endregion

        #region RectTransform
        /// <summary>
        /// 设置RectTransform的锚点位置X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetAnchoredPositionX(this RectTransform rectTransform, float x)
        {
            var pos = rectTransform.anchoredPosition;
            pos.x = x;
            rectTransform.anchoredPosition = pos;
        }
        /// <summary>
        /// 设置RectTransform的锚点位置Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetAnchoredPositionY(this RectTransform rectTransform, float y)
        {
            var pos = rectTransform.anchoredPosition;
            pos.y = y;
            rectTransform.anchoredPosition = pos;
        }
        /// <summary>
        /// 设置RectTransform的锚点位置Z
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="z"></param>
        public static void SetAnchoredPositionZ(this RectTransform rectTransform, float z)
        {
            var pos = rectTransform.anchoredPosition3D;
            pos.z = z;
            rectTransform.anchoredPosition3D = pos;
        }
        /// <summary>
        /// 设置RectTransform的大小X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetSizeDeltaX(this RectTransform rectTransform, float x)
        {
            var size = rectTransform.sizeDelta;
            size.x = x;
            rectTransform.sizeDelta = size;
        }
        /// <summary>
        /// 设置RectTransform的大小Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetSizeDeltaY(this RectTransform rectTransform, float y)
        {
            var size = rectTransform.sizeDelta;
            size.y = y;
            rectTransform.sizeDelta = size;
        }
        /// <summary>
        /// 设置RectTransform的锚点最小位置X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetAnchorMinX(this RectTransform rectTransform, float x)
        {
            var anchor = rectTransform.anchorMin;
            anchor.x = x;
            rectTransform.anchorMin = anchor;
        }
        /// <summary>
        /// 设置RectTransform的锚点最小位置Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetAnchorMinY(this RectTransform rectTransform, float y)
        {
            var anchor = rectTransform.anchorMin;
            anchor.y = y;
            rectTransform.anchorMin = anchor;
        }
        /// <summary>
        /// 设置RectTransform的锚点最大位置X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetAnchorMaxX(this RectTransform rectTransform, float x)
        {
            var anchor = rectTransform.anchorMax;
            anchor.x = x;
            rectTransform.anchorMax = anchor;
        }
        /// <summary>
        /// 设置RectTransform的锚点最大位置Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetAnchorMaxY(this RectTransform rectTransform, float y)
        {
            var anchor = rectTransform.anchorMax;
            anchor.y = y;
            rectTransform.anchorMax = anchor;
        }
        /// <summary>
        /// 设置RectTransform的Pivot X
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="x"></param>
        public static void SetPivotX(this RectTransform rectTransform, float x)
        {
            var pivot = rectTransform.pivot;
            pivot.x = x;
            rectTransform.pivot = pivot;
        }

        /// <summary>
        /// 设置RectTransform的Pivot Y
        /// </summary>
        /// <param name="rectTransform"></param>
        /// <param name="y"></param>
        public static void SetPivotY(this RectTransform rectTransform, float y)
        {
            var pivot = rectTransform.pivot;
            pivot.y = y;
            rectTransform.pivot = pivot;
        }

        /// <summary>
        /// 设置锚点
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="type"></param>
        public static void SetAnchor(this RectTransform rect, AnchorType type)
        {
            if (rect == null)
                return;
            var size = rect.sizeDelta;
            //left,right对应x,top,bottom对应Y
            switch (type)
            {
                case AnchorType.TopRight:
                    rect.pivot = new Vector2(1, 1);
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case AnchorType.TopLeft:
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.anchoredPosition = Vector2.zero;
                    break;
                case AnchorType.Stretch:
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Vector2.zero;
                    break;
                case AnchorType.StretchTop:
                    rect.pivot = new Vector2(0.5f, 1);
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
                    break;
                case AnchorType.StretchBottom:
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(0, rect.sizeDelta.y);
                    break;
                case AnchorType.StretchLeft:
                    rect.pivot = new Vector2(0, 0.5f);
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, 0);
                    break;
                case AnchorType.StretchRight:
                    rect.pivot = new Vector2(1, 0.5f);
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, 0);
                    break;
                default:
                    Log.Debug("未知的锚点类型");
                    break;
            }
        }
        #endregion

        #region GameObject
        /// <summary>
        /// 设置宽
        /// </summary>
        /// <param name="rectTrans"></param>
        /// <param name="width"></param>
        public static void SetWidth(this RectTransform rectTrans, float width)
        {
            rectTrans.sizeDelta = new Vector2(width, rectTrans.sizeDelta.y);
        }

        /// <summary>
        /// 设置高
        /// </summary>
        /// <param name="rectTrans"></param>
        /// <param name="height"></param>
        public static void SetHeight(this RectTransform rectTrans, float height)
        {
            rectTrans.sizeDelta = new Vector2(rectTrans.sizeDelta.x, height);
        }
        /// <summary>
        /// 获取位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newX"></param>
        public static void SetPositionX(this Transform t, float newX)
        {
            t.position = new Vector3(newX, t.position.y, t.position.z);
        }
        /// <summary>
        /// 获取位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newY"></param>
        public static void SetPositionY(this Transform t, float newY)
        {
            t.position = new Vector3(t.position.x, newY, t.position.z);
        }
        /// <summary>
        /// 获取位置的Z轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newZ"></param>
        public static void SetPositionZ(this Transform t, float newZ)
        {
            t.position = new Vector3(t.position.x, t.position.y, newZ);
        }
        /// <summary>
        /// 获取本地位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newX"></param>
        public static void SetLocalPositionX(this Transform t, float newX)
        {
            t.localPosition = new Vector3(newX, t.localPosition.y, t.localPosition.z);
        }
        /// <summary>
        /// 获取本地位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newY"></param>
        public static void SetLocalPositionY(this Transform t, float newY)
        {
            t.localPosition = new Vector3(t.localPosition.x, newY, t.localPosition.z);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newZ"></param>
        public static void SetLocalPositionZ(this Transform t, float newZ)
        {
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, newZ);
        }
        /// <summary>
        /// 设置缩放为0
        /// </summary>
        /// <param name="t"></param>
        /// <param name="newScale"></param>
        public static void SetLocalScale(this Transform t, Vector3 newScale)
        {
            t.localScale = newScale;
        }
        /// <summary>
        /// 设置本地缩放为0
        /// </summary>
        /// <param name="t"></param>
        public static void SetLocalScaleZero(this Transform t)
        {
            t.localScale = Vector3.zero;
        }
        /// <summary>
        /// 获取位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetPositionX(this Transform t)
        {
            return t.position.x;
        }
        /// <summary>
        /// 获取位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetPositionY(this Transform t)
        {
            return t.position.y;
        }
        /// <summary>
        /// 获取位置的Z轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetPositionZ(this Transform t)
        {
            return t.position.z;
        }
        /// <summary>
        /// 获取本地位置的X轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetLocalPositionX(this Transform t)
        {
            return t.localPosition.x;
        }
        /// <summary>
        /// 获取本地位置的Y轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetLocalPositionY(this Transform t)
        {
            return t.localPosition.y;
        }
        /// <summary>
        /// 获取本地位置的Z轴
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static float GetLocalPositionZ(this Transform t)
        {
            return t.localPosition.z;
        }

        /// <summary>
        /// 变换转矩阵变换
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static RectTransform RectTransform(this Transform t)
        {
            return t?.gameObject.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 设置动画速度
        /// </summary>
        /// <param name="anim"></param>
        /// <param name="newSpeed"></param>
        public static void SetSpeed(this Animation anim, float newSpeed)
        {
            anim[anim.clip.name].speed = newSpeed;
        }
        /// <summary>
        /// v3转v2
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static Vector2 ToVector2(this Vector3 vec)
        {
            return new Vector2(vec.x, vec.y);
        }
        /// <summary>
        /// 设置活动状态
        /// </summary>
        /// <param name="com"></param>
        /// <param name="visible"></param>
        public static void SetActive(this Component com, bool visible)
        {
            if (com && com.gameObject && com.gameObject.activeSelf != visible) com.gameObject.SetActive(visible);
        }
        /// <summary>
        /// 设置活动状态（反向）
        /// </summary>
        /// <param name="go"></param>
        /// <param name="visible"></param>
        public static void SetActiveReverse(this GameObject go, bool visible)
        {
            if (go && go.activeSelf != visible) go.SetActive(visible);
        }
        /// <summary>
        /// 设置名字
        /// </summary>
        /// <param name="go"></param>
        /// <param name="name"></param>
        public static void SetName(this GameObject go, string name)
        {
            if (go && go.name != name) go.name = name;
        }

        /// <summary>
        /// 获取附加到给定游戏对象的组件
        /// 如果找不到，则附加一个新的并返回
        /// </summary>
        /// <param name="gameObject">Game object.</param>
        /// <returns>Previously or newly attached component.</returns>
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 检查游戏对象是否附加了类型为T的组件
        /// </summary>
        /// <param name="gameObject">Game object.</param>
        /// <returns>True when component is attached.</returns>
        public static bool HasComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() != null;
        }

        /// <summary>
        /// 搜索子物体组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static T Get<T>(this GameObject go, string subnode) where T : Component
        {
            if (go != null)
            {
                Transform sub = go.transform.Find(subnode);
                if (sub != null) return sub.GetComponent<T>();
            }
            return null;
        }

        /// <summary>
        /// 递归设置游戏对象的层
        /// </summary>
        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayer(child.gameObject, layer);
            }
        }

        /// <summary> 
        /// 在指定物体上添加指定图片 
        /// </summary>
        public static Image AddImage(this GameObject target, Sprite sprite)
        {
            target.SetActive(false);
            Image image = target.GetComponent<Image>();
            if (!image)
            {
                image = target.AddComponent<Image>();
            }
            image.sprite = sprite;
            image.SetNativeSize();
            target.SetActive(true);
            return image;
        }

        /// <summary>
        /// 查找子对象
        /// </summary>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名称</param>
        /// <returns></returns>
        public static GameObject Child(this GameObject go, string objName)
        {
            return Child(go.transform, objName);
        }

        /// <summary>
        /// 查找子对象
        /// </summary>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名称</param>
        /// <returns></returns>
        public static GameObject Child(Transform go, string objName)
        {
            Transform tran = go.Find(objName);
            return tran?.gameObject;
        }

        /// <summary>
        /// 查找子对象
        /// </summary>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名</param>
        /// <param name="check_visible">检查可见</param>
        /// <param name="error">错误</param>
        /// <returns></returns>
        public static GameObject Child(this GameObject go, string objName, bool check_visible = false, bool error = true)
        {
            if (!go)
            {
                if (error)
                {
                    Log.Error("查找失败，GameObject是空的！");
                }
                return null;
            }

            var t = Child(go, objName, check_visible, error);
            return t?.gameObject;
        }

        /// <summary>
        /// 查找子对象组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go">自己</param>
        /// <param name="objName">对象名</param>
        /// <param name="check_visible">检查可见</param>
        /// <param name="error">错误</param>
        /// <returns></returns>
        public static T Child<T>(this GameObject go, string objName, bool check_visible = false, bool error = true) where T : Component
        {
            var t = Child(go, objName, check_visible, error);
            return t?.GetComponent<T>();
        }

        /// <summary>
        /// 查找子项组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="go"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T FindInChild<T>(this GameObject go, string name = "") where T : Component
        {
            if (!go)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(name) && !go.name.Contains(name))
            {
                return null;
            }

            var comp = go.GetComponent<T>();
            if (comp)
            {
                return comp;
            }

            return go.GetComponentsInChildren<T>().FirstOrDefault();
        }

        /// <summary>
        /// 取平级对象
        /// </summary>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static GameObject Peer(this GameObject go, string subnode)
        {
            return Peer(go.transform, subnode);
        }

        /// <summary>
        /// 取平级对象
        /// </summary>
        /// <param name="go"></param>
        /// <param name="subnode"></param>
        /// <returns></returns>
        public static GameObject Peer(Transform go, string subnode)
        {
            Transform tran = go.parent.Find(subnode);
            return tran?.gameObject;
        }

        /// <summary>
        /// 清除单个实例，默认延迟为0，仅在场景中删除对应对象
        /// </summary>
        public static void DestroyObject(this UnityEngine.Object obj, float delay = 0)
        {
            GameObject.Destroy(obj, delay);
        }

        /// <summary>
        /// 立刻清理实例对象，会在内存中清理实例，Editor适用
        /// </summary>
        public static void DestroyObjectImmediate(this UnityEngine.Object obj)
        {
            GameObject.DestroyImmediate(obj);
        }

        /// <summary>
        /// 清除一组实例
        /// </summary>
        /// <typeparam name="T">实例类型</typeparam>
        /// <param name="objs">对象实例集合</param>
        public static void DestroyObjects<T>(IEnumerable<T> objs) where T : UnityEngine.Object
        {
            foreach (var obj in objs)
            {
                GameObject.Destroy(obj);
            }
        }

        /// <summary>
        /// 清除所有子节点
        /// </summary>
        /// <param name="go"></param>
        public static void ClearChild(this GameObject go)
        {
            var tran = go.transform;

            while (tran.childCount > 0)
            {
                var child = tran.GetChild(0);

                if (Application.isEditor && !Application.isPlaying)
                {
                    GameObject.DestroyImmediate(child.gameObject);
                }
                else
                {
                    GameObject.Destroy(child.gameObject);
                }
                child.parent = null;
            }
        }
        #endregion

        #region Object
        /// <summary>
        /// 从一个 object[] 数组中，安全地获取并转换指定下标的元素为目标类型 T。
        /// object[] args = { 123, "hello", 3.14f };
        /// int a = args.Get<int>(0);// 123
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="openArgs"></param>
        /// <param name="index">下标</param>
        /// <param name="isLog">开启log</param>
        /// <returns></returns>
        public static T Get<T>(this object[] openArgs, int index, bool isLog = true)
        {
            if (openArgs == null)
            {
                if (isLog)
                {
                    Log.Error("[获取错误<object[]>], openArgs为null");
                }
                return default;
            }

            if (index < 0 || index >= openArgs.Length)
            {
                if (isLog)
                {
                    Log.Error($"[获取错误<object[]>], 越界: {index}  {openArgs.Length}");
                }
                return default;
            }

            var arrElement = openArgs[index];
            if (arrElement == null || arrElement is DBNull)
            {
                return default;
            }

            try
            {
                // 直接类型匹配
                if (arrElement is T t)
                {
                    return t;
                }

                // 可空类型支持
                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                // 针对常用类型做特殊处理
                if (underlyingType == typeof(int))
                {
                    return (T)(object)arrElement.ObjToInt32();
                }
                if (underlyingType == typeof(long))
                {
                    return (T)(object)arrElement.ObjToInt64();
                }
                if (underlyingType == typeof(float))
                {
                    return (T)(object)arrElement.ObjToFloat();
                }
                if (underlyingType == typeof(string))
                {
                    return (T)(object)arrElement.ObjToString();
                }

                // 其它类型尝试通用转换
                return (T)Convert.ChangeType(arrElement, underlyingType);
            }
            catch (Exception ex)
            {
                if (isLog)
                    Log.Error($"[获取错误<object[]>], '{arrElement}' 无法转换为类型<{typeof(T)}>: {ex}");
                return default;
            }
        }
        /// <summary>
        /// object转int32
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static int ObjToInt32(this object obj)
        {
            if (obj is int i)
            {
                return i;
            }

            try
            {
                return Convert.ToInt32(obj);
            }
            catch (Exception ex)
            {
                Log.Error("ToInt32 : " + ex);
                return 0;
            }
        }

        /// <summary>
        /// object转int64 | long
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static long ObjToInt64(this object obj)
        {
            if (obj is long l)
            {
                return l;
            }

            try
            {
                return Convert.ToInt64(obj);
            }
            catch (Exception ex)
            {
                Log.Error("ToInt64 : " + ex);
                return 0;
            }
        }

        /// <summary>
        /// object转float
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static float ObjToFloat(this object obj)
        {
            if (obj is float f)
            {
                return f;
            }

            try
            {
                return (float)Math.Round(Convert.ToSingle(obj), 2);
            }
            catch (Exception ex)
            {
                Log.Error("object转float失败 : " + ex);
                return 0;
            }
        }

        /// <summary>
        /// object转string
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ObjToString(this object obj)
        {
            if (obj is string s)
            {
                return s;
            }

            try
            {
                return Convert.ToString(obj);
            }
            catch (Exception ex)
            {
                Log.Error("object转string失败 : " + ex);
                return null;
            }
        }
        #endregion

        #region Texture
        /// <summary>
        /// texture 转换成 texture2d
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Texture2D TextureToTexture2D(Texture texture)
        {
            Texture2D texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 32, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, renderTexture);

            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();

            RenderTexture.active = currentRT;
            RenderTexture.ReleaseTemporary(renderTexture);

            return texture2D;
        }

        /// <summary>
        /// 解除texture锁
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Texture2D DuplicateTexture(this Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            return readableText;
        }

        /// <summary>
        /// 裁剪正方形
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Texture2D CutForSquare(this Texture2D texture)
        {
            Texture2D tex;
            int TextureWidth = texture.width;//图片的宽
            int TextureHeight = texture.height;//图片的高

            int TextureSide = Mathf.Min(TextureWidth, TextureHeight);
            tex = new Texture2D(TextureSide, TextureSide);
            UnityEngine.Color[] col = texture.GetPixels((TextureWidth - TextureSide) / 2, (TextureHeight - TextureSide) / 2, TextureSide, TextureSide);
            tex.SetPixels(0, 0, TextureSide, TextureSide, col);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 正方型裁剪
        /// 以图片中心为轴心，截取正方型，然后等比缩放
        /// 用于头像处理
        /// </summary>
        /// <param name="texture">要处理的图片</param>
        /// <param name="side_x">指定的边长</param>
        /// <param name="side_y">指定的边宽</param>
        /// <returns></returns>
        public static Texture2D CutForSquare(this Texture2D texture, int side_x, int side_y)
        {
            Texture2D tex;
            int TextureWidth = texture.width;//图片的宽
            int TextureHeight = texture.height;//图片的高

            //如果图片的高和宽都比side大
            if (TextureWidth > side_x && TextureHeight > side_y)
            {
                tex = new Texture2D(side_x, side_y);
                UnityEngine.Color[] col = texture.GetPixels((TextureWidth - side_x) / 2, (TextureHeight - side_y) / 2, side_x, side_y);
                tex.SetPixels(0, 0, side_x, side_y, col);
                tex.Apply();
                return tex;
            }
            //如果图片的宽或高小于side
            if (TextureWidth < side_x || TextureHeight < side_y)
            {
                int TextureSide = Mathf.Min(TextureWidth, TextureHeight);
                tex = new Texture2D(TextureSide, TextureSide);
                UnityEngine.Color[] col = texture.GetPixels((TextureWidth - TextureSide) / 2, (TextureHeight - TextureSide) / 2, TextureSide, TextureSide);
                tex.SetPixels(0, 0, TextureSide, TextureSide, col);
                tex.Apply();
                return tex;
            }
            return null;
        }

        /// <summary>
        /// byte[]转换为Texture2D
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Texture2D BytesToTexture2D(this byte[] bytes, int width, int height)
        {
            Texture2D texture2D = new Texture2D(width, height);
            texture2D.LoadImage(bytes);
            return texture2D;
        }

        /// <summary>
        /// 双线性插值法缩放图片，等比缩放 
        /// </summary>
        public static Texture2D ScaleTextureBilinear(this Texture2D originalTexture, float scaleFactor)
        {
            Texture2D newTexture = new Texture2D(Mathf.CeilToInt(originalTexture.width * scaleFactor),
                Mathf.CeilToInt(originalTexture.height * scaleFactor));
            float scale = 1.0f / scaleFactor;
            int maxX = originalTexture.width - 1;
            int maxY = originalTexture.height - 1;
            for (int y = 0; y < newTexture.height; y++)
            {
                for (int x = 0; x < newTexture.width; x++)
                {
                    float targetX = x * scale;
                    float targetY = y * scale;
                    int x1 = Mathf.Min(maxX, Mathf.FloorToInt(targetX));
                    int y1 = Mathf.Min(maxY, Mathf.FloorToInt(targetY));
                    int x2 = Mathf.Min(maxX, x1 + 1);
                    int y2 = Mathf.Min(maxY, y1 + 1);

                    float u = targetX - x1;
                    float v = targetY - y1;
                    float w1 = (1 - u) * (1 - v);
                    float w2 = u * (1 - v);
                    float w3 = (1 - u) * v;
                    float w4 = u * v;
                    Color color1 = originalTexture.GetPixel(x1, y1);
                    Color color2 = originalTexture.GetPixel(x2, y1);
                    Color color3 = originalTexture.GetPixel(x1, y2);
                    Color color4 = originalTexture.GetPixel(x2, y2);
                    Color color = new Color(
                        Mathf.Clamp01(color1.r * w1 + color2.r * w2 + color3.r * w3 + color4.r * w4),
                        Mathf.Clamp01(color1.g * w1 + color2.g * w2 + color3.g * w3 + color4.g * w4),
                        Mathf.Clamp01(color1.b * w1 + color2.b * w2 + color3.b * w3 + color4.b * w4),
                        Mathf.Clamp01(color1.a * w1 + color2.a * w2 + color3.a * w3 + color4.a * w4)
                    );
                    newTexture.SetPixel(x, y, color);
                }
            }

            newTexture.Apply();
            return newTexture;
        }

        /// <summary> 
        /// 双线性插值法缩放图片为指定尺寸 
        /// </summary>
        public static Texture2D SizeTextureBilinear(this Texture2D originalTexture, Vector2 size)
        {
            Texture2D newTexture = new Texture2D(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.y));
            float scaleX = originalTexture.width / size.x;
            float scaleY = originalTexture.height / size.y;
            int maxX = originalTexture.width - 1;
            int maxY = originalTexture.height - 1;
            for (int y = 0; y < newTexture.height; y++)
            {
                for (int x = 0; x < newTexture.width; x++)
                {
                    float targetX = x * scaleX;
                    float targetY = y * scaleY;
                    int x1 = Mathf.Min(maxX, Mathf.FloorToInt(targetX));
                    int y1 = Mathf.Min(maxY, Mathf.FloorToInt(targetY));
                    int x2 = Mathf.Min(maxX, x1 + 1);
                    int y2 = Mathf.Min(maxY, y1 + 1);

                    float u = targetX - x1;
                    float v = targetY - y1;
                    float w1 = (1 - u) * (1 - v);
                    float w2 = u * (1 - v);
                    float w3 = (1 - u) * v;
                    float w4 = u * v;
                    Color color1 = originalTexture.GetPixel(x1, y1);
                    Color color2 = originalTexture.GetPixel(x2, y1);
                    Color color3 = originalTexture.GetPixel(x1, y2);
                    Color color4 = originalTexture.GetPixel(x2, y2);
                    Color color = new Color(
                        Mathf.Clamp01(color1.r * w1 + color2.r * w2 + color3.r * w3 + color4.r * w4),
                        Mathf.Clamp01(color1.g * w1 + color2.g * w2 + color3.g * w3 + color4.g * w4),
                        Mathf.Clamp01(color1.b * w1 + color2.b * w2 + color3.b * w3 + color4.b * w4),
                        Mathf.Clamp01(color1.a * w1 + color2.a * w2 + color3.a * w3 + color4.a * w4)
                    );
                    newTexture.SetPixel(x, y, color);
                }
            }

            newTexture.Apply();
            return newTexture;
        }

        /// <summary> 
        /// Texture旋转
        /// </summary>
        public static Texture2D RotateTexture(this Texture2D texture, float eulerAngles)
        {
            int x;
            int y;
            int i;
            int j;
            float phi = eulerAngles / (180 / Mathf.PI);
            float sn = Mathf.Sin(phi);
            float cs = Mathf.Cos(phi);
            Color32[] arr = texture.GetPixels32();
            Color32[] arr2 = new Color32[arr.Length];
            int W = texture.width;
            int H = texture.height;
            int xc = W / 2;
            int yc = H / 2;

            for (j = 0; j < H; j++)
            {
                for (i = 0; i < W; i++)
                {
                    arr2[j * W + i] = new Color32(0, 0, 0, 0);

                    x = (int)(cs * (i - xc) + sn * (j - yc) + xc);
                    y = (int)(-sn * (i - xc) + cs * (j - yc) + yc);

                    if ((x > -1) && (x < W) && (y > -1) && (y < H))
                    {
                        arr2[j * W + i] = arr[y * W + x];
                    }
                }
            }

            Texture2D newImg = new Texture2D(W, H);
            newImg.SetPixels32(arr2);
            newImg.Apply();

            return newImg;
        }
        #endregion
    }
}