using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Security.Cryptography;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 抽卡系统 模拟
    /// 注意：客户端抽卡仅为表现层模拟，实际抽卡结果应由服务端下发以保证公平性。
    /// 客户端使用加密随机数生成器增加不可预测性。
    /// </summary>
    public class GachaSystem : MonoBehaviour
    {
        // ===== 加密随机数生成器（不可预测，替代 UnityEngine.Random） =====
        private static readonly RNGCryptoServiceProvider cryptoRng = new RNGCryptoServiceProvider();

        /// <summary>生成 [0, 1) 范围的加密随机浮点数</summary>
        private static float CryptoRandomValue()
        {
            var bytes = new byte[4];
            cryptoRng.GetBytes(bytes);
            // 将 4 字节转换为 [0, 1) 的浮点数
            uint randomUint = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
            return randomUint / (uint.MaxValue + 1.0);
        }

        /// <summary>生成 [min, max) 范围的加密随机整数</summary>
        private static int CryptoRandomRange(int min, int max)
        {
            if (min >= max) return min;
            uint range = (uint)(max - min);
            var bytes = new byte[4];
            cryptoRng.GetBytes(bytes);
            uint randomUint = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
            return min + (int)(randomUint % range);
        }
        // ===== 数据结构 =====
        [System.Serializable]
        public class GachaItem
        {
            public string itemName;
            public int starRating; // 3/4/5
            public Sprite icon;    // 物品图标
            public bool isWeapon;  // 是否为武器
        }

        // ===== 卡池配置 =====
        public List<GachaItem> up5StarPool;   // UP五星池
        public List<GachaItem> standard5StarPool; // 常驻五星池
        public List<GachaItem> up4StarPool;   // UP四星池
        public List<GachaItem> standard4StarPool; // 常驻四星池
        public List<GachaItem> standard3StarPool; // 三星池

        // ===== 系统状态 =====
        [SerializeField]
        private int pity5Star = 0;        // 五星保底计数
        [SerializeField]
        private int pity4Star = 0;        // 四星保底计数
        [SerializeField]
        private bool isGuaranteedUp5Star = false; // 大保底标记
        [SerializeField]
        private bool isGuaranteedUp4Star = false; // 四星保底标记
        private int last5StarPullCount = 0; // 记录第几抽抽到5星
        private bool isLastPullUp = false; // 记录最近一次抽卡是否为UP

        // ===== 概率参数 =====
        private const float BASE_5STAR_RATE = 0.006f;    // 0.6%
        private const float BASE_4STAR_RATE = 0.051f;    // 5.1%
        private const int HARD_PITY_5STAR = 90;          // 硬保底
        private const int SOFT_PITY_START = 73;          // 概率递增起点

        // ===== 核心抽卡逻辑 =====
        /// <summary>
        /// 执行一次抽卡（优先判定五星，其次四星，否则三星）。
        /// 每次抽卡同时推进五星和四星保底计数。
        /// </summary>
        /// <returns></returns>
        public GachaItem PerformPull()
        {
            pity5Star++;
            pity4Star++;

            // 五星保底判断
            if (Check5StarPull())
            {
                return Get5StarItem();
            }
            // 四星保底判断
            else if (Check4StarPull())
            {
                return Get4StarItem();
            }
            else
            {
                return Get3StarItem();
            }
        }

        /// <summary>
        /// 执行一次抽卡并强制返回四星（用于十连保底，正常推进五星保底计数）。
        /// </summary>
        private GachaItem PerformPullForce4Star()
        {
            pity5Star++; // 这次替换抽卡也计入五星保底
            return Get4StarItem();
        }

        /// <summary>
        /// 五星保底判断
        /// </summary>
        /// <returns></returns>
        private bool Check5StarPull()
        {
            // 硬保底触发
            if (pity5Star >= HARD_PITY_5STAR) return true;

            // 动态概率计算[1](@ref)
            float currentRate = pity5Star >= SOFT_PITY_START ?
                BASE_5STAR_RATE + 0.06f * (pity5Star - SOFT_PITY_START) :
                BASE_5STAR_RATE;
            // 上限保护，防止概率溢出
            currentRate = Mathf.Min(currentRate, 1.0f);

            return CryptoRandomValue() <= currentRate;
        /// </summary>
        /// <returns></returns>
        private GachaItem Get5StarItem()
        {
            // 记录当前抽数
            last5StarPullCount = pity5Star;

            // 判断是否为UP
            bool isUp = isGuaranteedUp5Star ? true : CryptoRandomValue() <= 0.5f;
            isGuaranteedUp5Star = !isUp; // 未出UP则触发大保底

            // 更新是否为UP的状态
            isLastPullUp = isUp;

            List<GachaItem> pool = isUp ? up5StarPool : standard5StarPool;
            ResetCounters();
            return SelectRandomItem(pool);
        }

        /// <summary>
        /// 四星保底判断
        /// </summary>
        /// <returns></returns>
        private bool Check4StarPull()
        {
            // 硬保底触发
            if (pity4Star >= 10) return true;

            // 动态概率计算
            float currentRate = BASE_4STAR_RATE;
            if (pity4Star >= 8) // 第9抽开始递增
            {
                currentRate = Mathf.Min(0.66f + (0.34f * (pity4Star - 8)), 1.0f);
            }
            return CryptoRandomValue() <= currentRate;
        }

        /// <summary>
        /// 获取四星物品
        /// </summary>
        /// <returns></returns>
        private GachaItem Get4StarItem()
        {
            // 判断是否触发UP保底
            bool isUp = isGuaranteedUp4Star ? true : CryptoRandomValue() <= 0.5f;
            isGuaranteedUp4Star = !isUp; // 更新保底状态

            // 更新是否为UP的状态
            isLastPullUp = isUp;

            //// 动态概率验证（调试用）
            //Debug.Log($"四星触发于第{pity4Star}抽 | UP状态:{isUp}");

            // 选择卡池
            List<GachaItem> pool = isUp ? up4StarPool : standard4StarPool;
            pity4Star = 0; // 重置四星计数器
            return SelectRandomItem(pool);
        }

        /// <summary>
        /// 获取三星物品
        /// </summary>
        /// <returns></returns>
        private GachaItem Get3StarItem()
        {
            // 从常驻三星池随机选取
            return SelectRandomItem(standard3StarPool);
        }

        // ===== 辅助方法 =====
        /// <summary>
        /// 重置计数器
        /// </summary>
        private void ResetCounters()
        {
            pity5Star = 0;
            // 获取五星不重置四星保底（独立重置）
        }

        /// <summary>
        /// 随机选择物品
        /// </summary>
        /// <param name="pool"></param>
        /// <returns></returns>
        private GachaItem SelectRandomItem(List<GachaItem> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                Log.Error("卡池为空！请在 Inspector 中配置卡池列表");
                return null;
            }
            return pool[CryptoRandomRange(0, pool.Count)];
        }

        // ===== 十连优化 =====
        /// <summary>
        /// 执行十连抽卡（保底至少一个四星或以上物品）。
        /// 优先保留原有的五星/四星结果；仅在没有任何 4★+ 时替换最后一个 3★ 为 4★。
        /// </summary>
        /// <returns></returns>
        public List<GachaItem> Perform10Pull()
        {
            List<GachaItem> results = new List<GachaItem>();
            bool hasFourStarOrAbove = false;
            int lastThreeStarIndex = -1;

            for (int i = 0; i < 10; i++)
            {
                GachaItem item = PerformPull();
                results.Add(item);
                if (item.starRating >= 4)
                    hasFourStarOrAbove = true;
                else
                    lastThreeStarIndex = i; // 记录最后一个三星的位置
            }

            // 确保至少有一个四星或以上物品。
            // 直接调用 Get4StarItem 替换最后一个三星结果，避免 PerformPullForce4Star 额外推进 pity5Star 导致保底计数偏移。
            if (!hasFourStarOrAbove && lastThreeStarIndex >= 0)
            {
                results[lastThreeStarIndex] = Get4StarItem();
            }

            return results;
        }



        // ===== 测试方法 =====
        void Start()
        {
#if UNITY_EDITOR
            // Test(); // 仅在需要调试时手动取消注释，避免覆盖 Inspector 中配置的卡池数据
#endif
        }

        public void Test()
        {
            // 配置卡池
            up5StarPool = new List<GachaItem>
            {
                new GachaItem { itemName = "UP五星1", starRating = 5 },
                new GachaItem { itemName = "UP五星2", starRating = 5 },
                new GachaItem { itemName = "UP五星3", starRating = 5 },
            };
            standard5StarPool = new List<GachaItem>
            {
                new GachaItem { itemName = "常驻五星1", starRating = 5 },
                new GachaItem { itemName = "常驻五星2", starRating = 5 },
                new GachaItem { itemName = "常驻五星3", starRating = 5 },
            };
            up4StarPool = new List<GachaItem>
            {
                new GachaItem { itemName = "UP四星1", starRating = 4 },
                new GachaItem { itemName = "UP四星2", starRating = 4 },
                new GachaItem { itemName = "UP四星3", starRating = 4 },
            };
            standard4StarPool = new List<GachaItem>
            {
                new GachaItem { itemName = "常驻四星1", starRating = 4 },
                new GachaItem { itemName = "常驻四星2", starRating = 4 },
                new GachaItem { itemName = "常驻四星3", starRating = 4 },
                new GachaItem { itemName = "常驻四星4", starRating = 4 },
                new GachaItem { itemName = "常驻四星5", starRating = 4 },
            };
            standard3StarPool = new List<GachaItem>
            {
                new GachaItem { itemName = "三星1", starRating = 3 },
                new GachaItem { itemName = "三星2", starRating = 3 },
                new GachaItem { itemName = "三星3", starRating = 3 },
                new GachaItem { itemName = "三星4", starRating = 3 },
                new GachaItem { itemName = "三星5", starRating = 3 },
                new GachaItem { itemName = "三星6", starRating = 3 },
                new GachaItem { itemName = "三星7", starRating = 3 },
                new GachaItem { itemName = "三星8", starRating = 3 },
            };
        }

        [ContextMenu("TestPull")]
        public void TestPull()
        {
            // 统计单抽星级数量
            Dictionary<int, int> singlePullStarCount = new Dictionary<int, int> { { 3, 0 }, { 4, 0 }, { 5, 0 } };
            for (int i = 0; i < 90; i++)
            {
                GachaItem item = PerformPull();
                singlePullStarCount[item.starRating]++;
                if (item.starRating == 5)
                {
                    Log.Debug("<color=#ffd32a>第 {0} 抽抽到了5星: {1}| 是否UP: {2}</color>", last5StarPullCount, item.itemName, isLastPullUp);
                }
                //else
                //{
                //    Debug.Log($"第 {i + 1} 抽: {item.itemName} | 星级: {item.starRating} | 是否UP: {isLastPullUp}");
                //}
            }
            Log.Debug("单抽统计: 三星: {0} | 四星: {1} | 五星: {2}", singlePullStarCount[3], singlePullStarCount[4], singlePullStarCount[5]);
            Log.Debug("-------------------------");

            // 统计十连抽星级数量
            Dictionary<int, int> tenPullStarCount = new Dictionary<int, int> { { 3, 0 }, { 4, 0 }, { 5, 0 } };
            for (int i = 0; i < 9; i++)
            {
                List<GachaItem> tenPullResults = Perform10Pull();
                foreach (var item in tenPullResults)
                {
                    tenPullStarCount[item.starRating]++;
                    if (item.starRating == 5)
                    {
                        Log.Debug("<color=#ffd32a>第 {0} 抽抽到了5星: {1}| 是否UP: {2}</color>", last5StarPullCount, item.itemName, isLastPullUp);
                    }
                    //else
                    //{
                    //    Debug.Log($"十连抽: {item.itemName} | 星级: {item.starRating} | 是否UP: {isLastPullUp}");
                    //}
                }
            }
            Log.Debug("十连抽统计: 三星: {0} | 四星: {1} | 五星: {2}", tenPullStarCount[3], tenPullStarCount[4], tenPullStarCount[5]);
        }

        [ContextMenu("TestPull1")]
        public void TestPull1()
        {
            // 统计单抽星级数量
            Dictionary<int, int> singlePullStarCount = new Dictionary<int, int> { { 3, 0 }, { 4, 0 }, { 5, 0 } };
            for (int i = 0; i < 90; i++)
            {
                GachaItem item = PerformPull();
                singlePullStarCount[item.starRating]++;
                if (item.starRating == 5)
                {
                    Log.Debug("<color=#ffd32a>第 {0} 抽抽到了5星: {1}| 是否UP: {2}</color>", last5StarPullCount, item.itemName, isLastPullUp);
                }
                //else
                //{
                //    Debug.Log($"第 {i + 1} 抽: {item.itemName} | 星级: {item.starRating} | 是否UP: {isLastPullUp}");
                //}
            }
            Log.Debug("单抽统计: 三星: {0} | 四星: {1} | 五星: {2}", singlePullStarCount[3], singlePullStarCount[4], singlePullStarCount[5]);
            Log.Debug("-------------------------");
        }

        [ContextMenu("TestPull10")]
        public void TestPull10()
        {
            // 统计十连抽星级数量
            Dictionary<int, int> tenPullStarCount = new Dictionary<int, int> { { 3, 0 }, { 4, 0 }, { 5, 0 } };
            for (int i = 0; i < 9; i++)
            {
                List<GachaItem> tenPullResults = Perform10Pull();
                foreach (var item in tenPullResults)
                {
                    tenPullStarCount[item.starRating]++;
                    if (item.starRating == 5)
                    {
                        Log.Debug("<color=#ffd32a>第 {0} 抽抽到了5星: {1}| 是否UP: {2}</color>", last5StarPullCount, item.itemName, isLastPullUp);
                    }
                    //else
                    //{
                    //    Debug.Log($"十连抽: {item.itemName} | 星级: {item.starRating} | 是否UP: {isLastPullUp}");
                    //}
                }
            }
            Log.Debug("十连抽统计: 三星: {0} | 四星: {1} | 五星: {2}", tenPullStarCount[3], tenPullStarCount[4], tenPullStarCount[5]);
        }
    }
}