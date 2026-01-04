using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 随机工具类
    /// </summary>
    public static class RandomUtil
    {
        public static readonly Random random = new Random();

        /// <summary>
        /// 根据给定的概率（百分比）判断某个事件是否“发生”  float （0-100）
        /// </summary>
        /// <param name="chancePercent"></param>
        /// <returns></returns>
        public static bool Probability(float chancePercent)
        {
            // 限制概率在0~100之间（手动实现以兼容较旧的框架）
            if (chancePercent < 0f) chancePercent = 0f;
            if (chancePercent > 100f) chancePercent = 100f;
            return random.NextDouble() * 100.0 <= chancePercent;
        }

        /// <summary>
        /// 根据给定的概率（百分比）判断某个事件是否“发生” byte （0-255）
        /// </summary>
        /// <param name="chancePercent"></param>
        /// <returns></returns>
        public static bool Probability(byte chancePercent)
        {
            // 当传入255(或更大)视为必然发生
            if (chancePercent >= 255)
            {
                return true;
            }
            return random.Next(0, 256) < chancePercent;
        }

        /// <summary>
        /// 生成一个随机小数 [0.0, 1.0)
        /// </summary>
        /// <returns> </returns>
        public static double RandomDouble()
        {
            return random.NextDouble();
        }

        /// <summary>
        /// 1或-1
        /// </summary>
        /// <returns></returns>
        public static int OneOrMinusOne()
        {
            return random.Next(0, 2) * 2 - 1;
        }

        /// <summary>
        /// 随机在范围内生成一个int （不包括最大值）
        /// </summary>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        public static int RandomRange(int maxValue)
        {
            if (maxValue <= 0)
            {
                Log.Error($"RandomRange(int): 非法的 maxValue={maxValue}，返回0");
                return 0;
            }
            return random.Next(maxValue);
        }

        /// <summary>
        /// 随机在范围内生成一个double （不包括最大值）
        /// </summary>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        public static double RandomRange(double maxValue)
        {
            if (maxValue <= 0.0)
            {
                Log.Error($"RandomRange(double): 非法的 maxValue={maxValue}，返回0");
                return 0.0;
            }
            return random.NextDouble() * maxValue;
        }

        /// <summary>
        /// 随机在范围内生成一个int
        /// </summary>
        /// <param name="minValue">随机取值最小区间</param>
        /// <param name="maxValue">随机取值最大区间</param>
        /// <returns>生成的int整数</returns>
        public static int RandomRange(int minValue, int maxValue)
        {
            if (minValue >= maxValue)
            {
                Log.Error("RandomRange : minValue 大于或等于 maxValue，返回 minValue");
                return minValue;
            }
            return random.Next(minValue, maxValue);
        }

        /// <summary>
        /// 随机在范围内生成一个double
        /// </summary>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static double RandomRange(double minValue, double maxValue)
        {
            if (minValue >= maxValue)
            {
                Log.Error("RandomRange : minValue 大于或等于 maxValue，返回 minValue");
                return minValue;
            }

            return (random.NextDouble() * (maxValue - minValue) + minValue);
        }

        /// <summary>
        /// 在指定值的基础上随机偏移一个范围内的值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public static float RandomOffset(float value, float range)
        {
            if (range < 0f)
            {
                Log.Error($"RandomOffset: 传入的 range 为负值 ({range})，使用其绝对值");
                range = Math.Abs(range);
            }
            var offset = random.NextDouble() * range - range / 2.0;
            return (float)(value + offset);
        }

        #region 正态分布
        /// <summary>
        /// Box-Muller 正态分布生成一个随机数
        /// </summary>
        /// <param name="miu">均值</param>
        /// <param name="sigma">标准差（必须大于0）</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns></returns>
        public static double RandomNormalDistribution(double miu, double sigma, double min, double max)
        {
            if (min > max)
            {
                Log.Error("RandomNormalDistribution: min 大于 max，交换两者");
                var tmp = min; min = max; max = tmp;
            }

            if (sigma <= 0)
            {
                Log.Error("RandomNormalDistribution: sigma 必须大于 0，使用绝对值");
                sigma = Math.Abs(sigma);
                if (sigma <= 0)
                {
                    sigma = 1e-6;
                }
            }

            double value;
            int safety = 0;
            do
            {
                // Box-Muller 变换，确保 u1 不为 0
                double u1;
                do
                {
                    u1 = random.NextDouble();
                } while (u1 <= double.Epsilon);

                double u2 = random.NextDouble();
                double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                value = miu + sigma * z0;

                safety++;
                if (safety > 100000)
                {
                    // 防止极端参数导致死循环，返回边界值
                    Log.Error("RandomNormalDistribution: 采样超过安全限制，返回最近的边界值");
                    return Math.Max(min, Math.Min(max, value));
                }
            } while (value < min || value > max);

            return value;
        }
        #endregion
    }
}
