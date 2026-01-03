using ReunionMovement.Common;
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 截止日期管理器
    /// </summary>
    public class DeadlineMgr : SingletonMgr<DeadlineMgr>
    {
        [Tooltip("起始日期，格式建议：yyyy-MM-dd（也兼容yyyy-M-d）")]
        public string startDate = "2025-9-20";
        [Tooltip("截止日期，格式建议：yyyy-MM-dd（也兼容yyyy-M-d）")]
        public string deadlineDate = "2025-9-25";
        [Tooltip("当前日期（运行时自动填充，格式：yyyy-MM-dd）")]
        public string currentDate;

        // 本地存储 key
        private const string prefKey_LastUtcTicks = "Deadline_LastUtcTicks_v1";
        private const string prefKey_Hash = "Deadline_LastUtcHash_v1";
        // 简单的内置 salt（最好构建时注入或由服务端提供）
        private const string internalSalt = "@REUNION_m0v3m3nt$alt";

        // 容忍的向后跳阈值（例如：1 分钟）。如果跳得比这个多则认为可疑。
        private static readonly TimeSpan RollbackTolerance = TimeSpan.FromMinutes(1);

        void Start()
        {
            EnforceDateRestriction();
        }

        /// <summary>
        /// 执行日期限制检查（包含离线时钟回拨检测）
        /// </summary>
        private void EnforceDateRestriction()
        {
            // 使用本地时间作日期比较（保持原行为）
            var nowLocal = DateTime.Now.Date;
            currentDate = nowLocal.ToString("yyyy-MM-dd");

            // 先做离线时钟回拨检测（基于 UTC）
            var nowUtc = DateTime.UtcNow;
            if (IsClockRolledBackOrTampered(nowUtc))
            {
                Debug.LogWarning("DeadlineMgr: 检测到系统时间被回拨或本地记录被篡改，触发限制处理。");
                PurgeActiveScene();
                return;
            }

            if (!TryParseDate(startDate, out var start) || !TryParseDate(deadlineDate, out var end))
            {
                Debug.LogError("DeadlineMgr: 日期格式错误，请使用 yyyy-MM-dd 或 yyyy-M-d。");
                return;
            }

            // 容错：若起始晚于截止，自动交换
            if (start > end)
            {
                Debug.LogWarning($"DeadlineMgr: StartDate({start:yyyy-MM-dd}) 晚于 DeadlineDate({end:yyyy-MM-dd})，已自动交换。");
                var tmp = start;
                start = end;
                end = tmp;
            }

            // 不在 [start, end]（含边界）范围内则清空场景
            if (nowLocal < start || nowLocal > end)
            {
                PurgeActiveScene();
            }
        }

        /// <summary>
        /// 检测时钟回拨或本地记录被篡改。
        /// 逻辑：
        ///  - 如果没有本地记录（ticks/hash 均不存在），创建记录（安全哈希随存）。
        ///  - 如果存在部分缺失（仅 ticks 或仅 hash） -> 认为被篡改。
        ///  - 如果存在记录但哈希不匹配 -> 认为被篡改。
        ///  - 如果存在记录且当前 UTC 时间小于记录 - 容忍阈值 -> 认为回拨。
        ///  - 否则更新记录为 max(记录, 当前时间) 并保存哈希。
        /// 返回 true 表示发现问题（篡改或回拨）。
        /// </summary>
        private bool IsClockRolledBackOrTampered(DateTime nowUtc)
        {
            try
            {
                var hasTicks = PlayerPrefs.HasKey(prefKey_LastUtcTicks);
                var hasHash = PlayerPrefs.HasKey(prefKey_Hash);

                // 首次运行或无任何记录：写入当前 UTC
                if (!hasTicks && !hasHash)
                {
                    SaveUtcTicks(nowUtc.Ticks);
                    return false;
                }

                // 如果存在部分缺失，视为被篡改
                if (!hasTicks || !hasHash)
                {
                    Debug.LogWarning("DeadlineMgr: 本地存储不完整（ticks/hash 缺失），已视为被篡改。");
                    return true;
                }

                var storedTicksStr = PlayerPrefs.GetString(prefKey_LastUtcTicks, string.Empty);
                var storedHash = PlayerPrefs.GetString(prefKey_Hash, string.Empty);

                if (!long.TryParse(storedTicksStr, out var storedTicks))
                {
                    // 数据异常，认为被篡改
                    Debug.LogWarning("DeadlineMgr: 本地存储的时间格式异常，已视为被篡改。");
                    return true;
                }

                // 验证哈希
                var expectedHash = ComputeHashForTicks(storedTicks);
                if (!string.Equals(expectedHash, storedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("DeadlineMgr: 本地存储的完整性校验失败，已视为被篡改。");
                    return true;
                }

                var storedUtc = new DateTime(storedTicks, DateTimeKind.Utc);

                // 如果当前 UTC 小于存储时间减去容忍阈值，则认为是回拨
                if (nowUtc < storedUtc - RollbackTolerance)
                {
                    Debug.LogWarning($"DeadlineMgr: 发现系统时间回拨（当前UTC={nowUtc:o}，记录UTC={storedUtc:o}）。");
                    return true;
                }

                // 更新存储为较大的时间（防止攻击者在未来设置一个更小的时间绕过检测）
                var newTicks = Math.Max(storedTicks, nowUtc.Ticks);
                SaveUtcTicks(newTicks);

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"DeadlineMgr: 检测时钟回拨时出现异常，按安全策略处理。异常: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// 保存给定的 UTC ticks 及其哈希
        /// </summary>
        /// <param name="ticks"></param>
        private void SaveUtcTicks(long ticks)
        {
            PlayerPrefs.SetString(prefKey_LastUtcTicks, ticks.ToString());
            PlayerPrefs.SetString(prefKey_Hash, ComputeHashForTicks(ticks));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 计算给定 ticks 的安全哈希
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        private string ComputeHashForTicks(long ticks)
        {
            // 使用简单的 SHA256(ticks + Application.identifier + salt) 作为完整性校验
            var payload = ticks.ToString() + "|" + Application.identifier + "|" + internalSalt;
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(payload);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// 尝试解析日期字符串
        /// </summary>
        /// <param name="s"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        private static bool TryParseDate(string s, out DateTime date)
        {
            date = default;
            if (string.IsNullOrEmpty(s)) return false;

            // 兼容 2025-9-7 / 2025-09-07 等
            var formats = new[] { "yyyy-MM-dd", "yyyy-M-d", "yyyy-M-dd", "yyyy-MM-d" };
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
                DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                date = parsed.Date;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清空当前活动场景的所有根对象
        /// </summary>
        private void PurgeActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            // 先销毁其它根对象
            for (int i = 0; i < roots.Length; i++)
            {
                var go = roots[i];
                if (go != this.gameObject)
                {
                    Destroy(go);
                }
            }

            // 最后销毁自身（避免循环中被销毁导致异常）
            Destroy(gameObject);

            Debug.LogWarning("DeadlineMgr: 当前日期不在允许范围内或检测到篡改，已删除当前场景的所有对象。");
        }
    }
}