using System;
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
        // 使用 RandomNumberGenerator 替代已过时的 RNGCryptoServiceProvider

        /// <summary>生成 [0, 1) 范围的加密随机浮点数（GetInt32 避免每次 new byte[4] 分配）</summary>
        private static float CryptoRandomValue()
        {
            return RandomNumberGenerator.GetInt32(0, int.MaxValue) / (float)int.MaxValue;
        }

        /// <summary>生成 [min, max) 范围的加密随机整数（GetInt32 内部无模偏差）</summary>
        private static int CryptoRandomRange(int min, int max)
        {
            if (min >= max) return min;
            return RandomNumberGenerator.GetInt32(min, max);
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

        // ===== 公开查询 API（UI 展示保底进度/结果用，此前只能依赖 Log 输出） =====
        /// <summary>当前五星保底计数（距离硬保底 90 抽）</summary>
        public int Pity5Star => pity5Star;
        /// <summary>当前四星保底计数（距离硬保底 10 抽）</summary>
        public int Pity4Star => pity4Star;
        /// <summary>最近一次抽出五星时的抽数（0 = 尚未出过五星）</summary>
        public int Last5StarPullCount => last5StarPullCount;
        /// <summary>最近一次抽卡是否为 UP</summary>
        public bool IsLastPullUp => isLastPullUp;
        /// <summary>是否处于五星大保底（下一次五星必为 UP）</summary>
        public bool IsGuaranteedUp5Star => isGuaranteedUp5Star;

        // ===== 概率参数 =====
        private const float BASE_5STAR_RATE = 0.006f;    // 0.6%
        private const float BASE_4STAR_RATE = 0.051f;    // 5.1%
        private const int HARD_PITY_5STAR = 90;          // 硬保底
        private const int SOFT_PITY_START = 73;          // 概率递增起点

        // ===== 核心抽卡逻辑 =====
        /// <summary>
        /// 执行一次抽卡（优先判定五星，其次四星，否则三星）。
        /// 每次抽卡同时推进五星和四星保底计数，并同步保底持久化。
        /// </summary>
        /// <returns></returns>
        public GachaItem PerformPull()
        {
            // 卡池全空时不应推进保底：否则空配置下保底被无意义消耗，且结果恒为 null
            if ((up5StarPool == null || up5StarPool.Count == 0)
                && (standard5StarPool == null || standard5StarPool.Count == 0)
                && (up4StarPool == null || up4StarPool.Count == 0)
                && (standard4StarPool == null || standard4StarPool.Count == 0)
                && (standard3StarPool == null || standard3StarPool.Count == 0))
            {
                Log.Error("所有卡池为空！请在 Inspector 中配置卡池列表");
                return null;
            }

            pity5Star++;
            pity4Star++;

            GachaItem result;
            // 五星保底判断
            if (Check5StarPull())
            {
                result = Get5StarItem();
            }
            // 四星保底判断
            else if (Check4StarPull())
            {
                result = Get4StarItem();
            }
            else
            {
                result = Get3StarItem();
            }

            // 保底持久化：每次抽卡更新存档（PlayerPrefs.SetString 为内存操作，
            // Unity 在正常退出时统一落盘，避免"重启游戏保底清零"）
            SavePityState();
            return result;
        }

        /// <summary>
        /// 应用服务端下发的抽卡结果（客户端仅表现层，权威结果应由服务端下发以保证公平性）：
        /// 按结果星级推进/重置保底计数，并同步保底/UP 状态与最近结果，返回结果供 UI 展示。
        /// </summary>
        /// <param name="serverResult">服务端下发的抽卡结果项</param>
        /// <param name="isUp">服务端下发的该结果是否为 UP（驱动大保底/UP 提示状态）</param>
        public GachaItem ApplyServerResult(GachaItem serverResult, bool isUp = false)
        {
            if (serverResult == null) return null;

            pity5Star++;
            pity4Star++;

            switch (serverResult.starRating)
            {
                case 5:
                    last5StarPullCount = pity5Star;
                    // 五星重置五星保底（四星保底独立，不重置）
                    ResetCounters();
                    // 同步保底/UP 状态：否则 IsGuaranteedUp5Star/IsLastPullUp 永远停留在
                    // 本地模拟遗留值，大保底提示 UI 显示错误
                    isGuaranteedUp5Star = !isUp;
                    isLastPullUp = isUp;
                    break;
                case 4:
                    pity4Star = 0;
                    isGuaranteedUp4Star = !isUp;
                    isLastPullUp = isUp;
                    break;
                default:
                    // 3 星：仅推进计数，最近一次结果非 UP
                    isLastPullUp = false;
                    break;
            }
            // 服务端权威结果同样需要持久化（客户端展示的保底进度跨重启保持一致）
            SavePityState();
            return serverResult;
        }

        #region 保底状态持久化
        private const string PitySaveKey = "gacha_pity_save_v1";

        [Serializable]
        private class PitySaveData
        {
            public int pity5Star;
            public int pity4Star;
            public bool isGuaranteedUp5Star;
            public bool isGuaranteedUp4Star;
            public int last5StarPullCount;
            public bool isLastPullUp;
        }

        /// <summary>
        /// 保存保底状态到 PlayerPrefs（SetString 为内存操作，Unity 正常退出时统一落盘；
        /// flush=true 立即强制落盘，供关键节点（账号登出/付费点）使用）。
        /// </summary>
        public void SavePityState(bool flush = false)
        {
            var data = new PitySaveData
            {
                pity5Star = pity5Star,
                pity4Star = pity4Star,
                isGuaranteedUp5Star = isGuaranteedUp5Star,
                isGuaranteedUp4Star = isGuaranteedUp4Star,
                last5StarPullCount = last5StarPullCount,
                isLastPullUp = isLastPullUp,
            };
            PlayerPrefs.SetString(PitySaveKey, JsonUtility.ToJson(data));
            if (flush) PlayerPrefs.Save();
        }

        /// <summary>从 PlayerPrefs 恢复保底状态（玩家重启后保底不清零，避免"重开"绕过保底）</summary>
        public void LoadPityState()
        {
            if (!PlayerPrefs.HasKey(PitySaveKey)) return;
            try
            {
                var data = JsonUtility.FromJson<PitySaveData>(PlayerPrefs.GetString(PitySaveKey));
                if (data == null) return;
                pity5Star = data.pity5Star;
                pity4Star = data.pity4Star;
                isGuaranteedUp5Star = data.isGuaranteedUp5Star;
                isGuaranteedUp4Star = data.isGuaranteedUp4Star;
                last5StarPullCount = data.last5StarPullCount;
                isLastPullUp = data.isLastPullUp;
            }
            catch (Exception ex)
            {
                Log.Warning("GachaSystem 保底状态恢复失败: {0}", ex.Message);
            }
        }

        /// <summary>清空持久化的保底状态（测试/重置账号）</summary>
        public void ResetPityState()
        {
            PlayerPrefs.DeleteKey(PitySaveKey);
        }
        #endregion

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
        }

        /// <summary>
        /// 五星物品获取
        /// </summary>
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
            // 硬保底触发（第 10 抽必出四星）
            if (pity4Star >= 10) return true;

            // 动态概率：第 9 抽起从 66% 递增，第 10 抽达到 100%。
            // 注意偏移量必须是 (pity4Star - 9)：若用 -8，第 9 抽即达 1.0，
            // 第 10 抽的硬保底分支将永远不可达。
            float currentRate = BASE_4STAR_RATE;
            if (pity4Star >= 9)
            {
                currentRate = Mathf.Min(0.66f + (0.34f * (pity4Star - 9)), 1.0f);
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
                // 卡池为空时 PerformPull 可能返回 null（SelectRandomItem 已告警），跳过统计避免 NRE
                if (item == null) continue;
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

        /// <summary>
        /// 测试用：填充演示卡池（会覆盖 Inspector 中配置的卡池数据）。
        /// 此前为 public 可被业务误调覆盖配置；降为私有，仅调试代码内部可用。
        /// </summary>
        private void Test()
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
                if (item == null) continue; // 卡池为空时跳过统计，避免 NRE
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
                    if (item == null) continue; // 卡池为空时跳过统计，避免 NRE
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