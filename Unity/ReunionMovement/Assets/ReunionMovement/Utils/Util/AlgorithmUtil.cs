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
                int digit = Math.Abs(array[i]);
                // 保证为一位数字（若传入多位，取低位）
                digit = digit % 10;
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
            if (num <= 0)
            {
                Log.Error("GetNearestPower2: 输入必须为正整数, 当前值: {0}", num);
                return 0;
            }

            // 位运算代替浮点对数，避免 double 精度误差（如 Math.Log(16)/Math.Log(2) 可能得 4.000000001）
            int result = 1;
            while (result < num)
            {
                result <<= 1;
            }
            return result;
        }

        /// <summary>
        /// 计算最大公约数 （够同时整除两个或多个整数的最大的正整数）
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int CalculateMaximumCommonDivisor(int a, int b)
        {
            // 用 long 求绝对值，避免 Math.Abs(int.MinValue) 抛 OverflowException
            long la = Math.Abs((long)a);
            long lb = Math.Abs((long)b);

            if (la == 0 && lb == 0)
            {
                return 0;
            }

            while (lb != 0)
            {
                (la, lb) = (lb, la % lb);
            }
            return (int)la;
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

            // 用 long 中间计算，避免 a*b 在 int 阶段溢出（如 50000*50000 > int.MaxValue）
            long la = Math.Abs((long)a);
            long lb = Math.Abs((long)b);
            long gcd = CalculateMaximumCommonDivisor(a, b);

            // la/gcd*lb 最大可达 ~2^62，虽不溢出 long，但 (int) 强转会静默回绕成负数/错误值；
            // 显式检测超出 int 范围，返回 int.MaxValue 并告警
            long result = la / gcd * lb;
            if (result > int.MaxValue)
            {
                Log.Warning("CalculateMinimumCommonMultiple({0}, {1}) 结果溢出 int，返回 int.MaxValue", a, b);
                return int.MaxValue;
            }
            return (int)result;
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
                return Array.Empty<T>();
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
        /// 把集合转成字典，便于后续高效查找。
        /// 当输入为 null 时返回空字典而不是 null，以统一错误返回风格。
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
                Log.Error("BuildDictionary: 源或键选择器为空，返回空字典");
                return new Dictionary<TKey, TValue>();
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
        /// 随机打乱数组
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <param name="array">数组</param>
        public static void Shuffle<T>(IList<T> array)
        {
            if (array == null)
            {
                Log.Error("Shuffle: 输入数组不能为空");
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
                Log.Error("Shuffle: 输入数组不能为空");
                return;
            }

            if (startIndex < 0 || count < 0 || startIndex + count > array.Count)
            {
                Log.Error("Shuffle: 输入参数错误，startIndex: {0}, count: {1}, array.Count: {2}", startIndex, count, array.Count);
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
        /// 打开一个URL链接（仅允许 http/https 协议，防止打开恶意链接或本地文件）
        /// </summary>
        /// <param name="url"></param>
        public static void OpenURL(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Log.Error("OpenURL: URL 不能为空");
                return;
            }

            // 验证 URL 协议，仅允许 http 和 https
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Log.Error("OpenURL: 无效的 URL 格式: {0}", url);
                return;
            }

            if (uri.Scheme != "https" && uri.Scheme != "http")
            {
                Log.Error("OpenURL: 不支持的协议 '{0}'，仅允许 http/https", uri.Scheme);
                return;
            }

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
        /// <param name="this"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> other)
        {
            if (collection == null)
            {
                Log.Error("集合为空");
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
