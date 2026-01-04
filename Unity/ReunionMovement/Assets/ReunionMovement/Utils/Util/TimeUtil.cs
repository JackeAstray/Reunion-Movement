using System;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 时间工具类
    /// </summary>
    public static class TimeUtil
    {
        #region 字符串、时间、时间戳转换
        /// <summary>
        /// 获取当前本地时间
        /// </summary>
        public static DateTime GetCurrentTime()
        {
            return DateTime.Now;
        }

        /// <summary>
        /// 获取当前UTC时间
        /// </summary>
        public static DateTime GetCurrentUtcTime()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// 获取当前本地时间字符串（格式：yyyy-MM-dd HH:mm:ss）
        /// </summary>
        public static string GetCurrentTimeString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 获取当前UTC时间字符串（格式：yyyy-MM-dd HH:mm:ss）
        /// </summary>
        public static string GetCurrentUtcTimeString()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 获取当前UTC时间的Unix时间戳（秒）
        /// </summary>
        public static long GetCurrentUnixTimestampSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// 获取当前UTC时间的Unix时间戳（毫秒）
        /// </summary>
        public static long GetCurrentUnixTimestampMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 根据指定 DateTime 获取对应的 Unix 时间戳（秒）
        /// </summary>
        /// <param name="dateTime">要转换的时间</param>
        /// <param name="asUtc">是否按 UTC 处理，默认 true</param>
        /// <returns>Unix 时间戳（秒）</returns>
        public static long GetUnixTimestampSeconds(this DateTime dateTime, bool asUtc = true)
        {
            // 确保对 DateTime 进行实际的时区转换，而不是仅仅修改其 Kind
            var dt = asUtc ? dateTime.ToUniversalTime() : dateTime.ToLocalTime();
            return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }

        /// <summary>
        /// 根据指定 DateTime 获取对应的 Unix 时间戳（毫秒）
        /// </summary>
        /// <param name="dateTime">要转换的时间</param>
        /// <param name="asUtc">是否按 UTC 处理，默认 true</param>
        /// <returns>Unix 时间戳（毫秒）</returns>
        public static long GetUnixTimestampMilliseconds(this DateTime dateTime, bool asUtc = true)
        {
            // 确保对 DateTime 进行实际的时区转换，而不是仅仅修改其 Kind
            var dt = asUtc ? dateTime.ToUniversalTime() : dateTime.ToLocalTime();
            return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 尝试将字符串（格式：yyyy-MM-dd HH:mm:ss）转换为 DateTime。
        /// </summary>
        /// <param name="timeString">时间字符串</param>
        /// <param name="result">转换后的 DateTime</param>
        /// <returns>格式正确返回 true，否则返回 false</returns>
        public static bool TryParseTimeString(this string timeString, out DateTime result)
        {
            // 使用 TryParseExact 强制要求使用文档中声明的格式
            const string format = "yyyy-MM-dd HH:mm:ss";
            return DateTime.TryParseExact(
                timeString,
                format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out result
            );
        }

        /// <summary>
        /// Unix时间戳（秒）转DateTime（UTC）
        /// </summary>
        public static DateTime UnixTimestampSecondsToDateTime(this long seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        /// <summary>
        /// Unix时间戳（毫秒）转DateTime（UTC）
        /// </summary>
        public static DateTime UnixTimestampMillisecondsToDateTime(this long milliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
        }

        /// <summary>
        /// Unix时间戳（秒）转DateTime（本地时间）
        /// </summary>
        public static DateTime UnixTimestampSecondsToLocalDateTime(this long seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime;
        }

        /// <summary>
        /// Unix时间戳（毫秒）转DateTime（本地时间）
        /// </summary>
        public static DateTime UnixTimestampMillisecondsToLocalDateTime(this long milliseconds)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().DateTime;
        }
        #endregion

        #region 计算时间差
        /// <summary>
        /// 计算目标时间与当前本地时间的时间差（目标时间 - 当前时间）
        /// </summary>
        /// <param name="target">目标时间</param>
        /// <returns>时间差（TimeSpan）</returns>
        public static TimeSpan GetTimeDifference(this DateTime target)
        {
            return target - DateTime.Now;
        }

        /// <summary>
        /// 计算目标时间与当前UTC时间的时间差（目标时间 - 当前UTC时间）
        /// </summary>
        /// <param name="targetUtc">目标UTC时间</param>
        /// <returns>时间差（TimeSpan）</returns>
        public static TimeSpan GetUtcTimeDifference(this DateTime targetUtc)
        {
            return targetUtc - DateTime.UtcNow;
        }
        #endregion

        #region 增加时间
        /// <summary>
        /// 获取当前本地时间增加指定 TimeSpan 后的时间
        /// </summary>
        /// <param name="span">要增加的时间间隔</param>
        /// <returns>增加后的时间</returns>
        public static DateTime GetTimeAfter(this TimeSpan span)
        {
            return DateTime.Now.Add(span);
        }

        /// <summary>
        /// 获取当前UTC时间增加指定 TimeSpan 后的时间
        /// </summary>
        /// <param name="span">要增加的时间间隔</param>
        /// <returns>增加后的 UTC 时间</returns>
        public static DateTime GetUtcTimeAfter(this TimeSpan span)
        {
            return DateTime.UtcNow.Add(span);
        }

        /// <summary>
        /// 获取指定时间增加指定 TimeSpan 后的时间
        /// </summary>
        /// <param name="baseTime">基础时间</param>
        /// <param name="span">要增加的时间间隔</param>
        /// <returns>增加后的时间</returns>
        public static DateTime GetTimeAfter(this DateTime baseTime, TimeSpan span)
        {
            return baseTime.Add(span);
        }

        /// <summary>
        /// 获取当前本地时间增加指定秒数后的时间
        /// </summary>
        /// <param name="seconds">要增加的秒数</param>
        /// <returns>增加后的时间</returns>
        public static DateTime GetTimeAfterSeconds(this long seconds)
        {
            return DateTime.Now.AddSeconds(seconds);
        }

        /// <summary>
        /// 获取当前本地时间增加指定毫秒数后的时间
        /// </summary>
        /// <param name="milliseconds">要增加的毫秒数</param>
        /// <returns>增加后的时间</returns>
        public static DateTime GetTimeAfterMilliseconds(this long milliseconds)
        {
            return DateTime.Now.AddMilliseconds(milliseconds);
        }

        /// <summary>
        /// 获取当前UTC时间增加指定秒数后的时间
        /// </summary>
        /// <param name="seconds">要增加的秒数</param>
        /// <returns>增加后的 UTC 时间</returns>
        public static DateTime GetUtcTimeAfterSeconds(this long seconds)
        {
            return DateTime.UtcNow.AddSeconds(seconds);
        }

        /// <summary>
        /// 获取当前UTC时间增加指定毫秒数后的时间
        /// </summary>
        /// <param name="milliseconds">要增加的毫秒数</param>
        /// <returns>增加后的 UTC 时间</returns>
        public static DateTime GetUtcTimeAfterMilliseconds(this long milliseconds)
        {
            return DateTime.UtcNow.AddMilliseconds(milliseconds);
        }

        /// <summary>
        /// 获取指定时间增加指定秒数后的时间
        /// </summary>
        public static DateTime GetTimeAfterSeconds(this DateTime baseTime, long seconds)
        {
            return baseTime.AddSeconds(seconds);
        }

        /// <summary>
        /// 获取指定时间增加指定毫秒数后的时间
        /// </summary>
        public static DateTime GetTimeAfterMilliseconds(this DateTime baseTime, long milliseconds)
        {
            return baseTime.AddMilliseconds(milliseconds);
        }
        #endregion

        #region 额外方法
        /// <summary>
        /// 将 DateTime 转为指定格式的字符串（默认：yyyy-MM-dd HH:mm:ss）
        /// </summary>
        public static string ToTimeString(this DateTime dateTime, string format = "yyyy-MM-dd HH:mm:ss")
        {
            return dateTime.ToString(format);
        }

        /// <summary>
        /// 尝试按指定格式解析时间字符串
        /// </summary>
        public static bool TryParseTimeString(this string timeString, string format, out DateTime result)
        {
            return DateTime.TryParseExact(
                timeString,
                format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out result
            );
        }

        /// <summary>
        /// 判断两个时间是否为同一天（按本地日期比较）
        /// </summary>
        public static bool IsSameDay(this DateTime a, DateTime b)
        {
            return a.Date == b.Date;
        }

        /// <summary>
        /// 获取指定时间当天的起始时间（00:00:00）
        /// </summary>
        public static DateTime StartOfDay(this DateTime dateTime)
        {
            return dateTime.Date;
        }

        /// <summary>
        /// 获取指定时间当天的结束时间（23:59:59.9999999）
        /// </summary>
        public static DateTime EndOfDay(this DateTime dateTime)
        {
            return dateTime.Date.AddDays(1).AddTicks(-1);
        }

        /// <summary>
        /// 将秒数转换为可读字符串（天 时:分:秒），例如 "1天 02:03:04"
        /// </summary>
        public static string SecondsToReadable(this long seconds)
        {
            if (seconds < 0) seconds = 0;
            var ts = TimeSpan.FromSeconds(seconds);
            var parts = string.Empty;
            if (ts.Days > 0) parts = $"{ts.Days}天 ";
            parts += $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return parts;
        }

        /// <summary>
        /// 将时间转换到目标时区
        /// </summary>
        public static DateTime ConvertToTimeZone(this DateTime dateTime, TimeZoneInfo targetZone)
        {
            return TimeZoneInfo.ConvertTime(dateTime, targetZone);
        }

        /// <summary>
        /// 获取指定日期所在周的起始日期（可指定周起始日，默认周一）
        /// 返回的时间为当天 00:00:00
        /// </summary>
        public static DateTime GetWeekStart(this DateTime dateTime, DayOfWeek startOfWeek = DayOfWeek.Monday)
        {
            int diff = (7 + (dateTime.DayOfWeek - startOfWeek)) % 7;
            return dateTime.Date.AddDays(-diff);
        }
        #endregion
    }
}