using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gear.Framework.Runtime;
using UnityEngine;
using UnityEngine.UI;

using Gear.Common;
namespace Gear.Utilities
{
    public static partial class AlgorithmUtil
    {
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
                // 溢出保护：num > 2^30 时 int 范围内不存在满足条件的 2 的幂。
                // result 到达 2^30 后再 <<= 1 会回绕为 int.MinValue（仍 < num）→ 再移位变 0，
                // 而 0 < num 恒真 → 死循环（主线程卡死，与 UWRExecutor 的分段下载循环同类）
                if (result >= (1 << 30))
                {
                    Log.Error("GetNearestPower2: 输入 {0} 超出 int 可表示的最大 2 的幂，返回 0", num);
                    return 0;
                }
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

    }
}
