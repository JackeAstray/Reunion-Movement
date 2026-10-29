using System;

namespace Gear.Framework.Runtime.Base
{
    /// <summary>性能指标只读视图 —— 由 Modules 的 PerformanceMonitor 实现。</summary>
    public interface IPerformanceStats
    {
        float CurrentFps { get; }
        float MinFpsRecord { get; }
        long CurrentMemoryMB { get; }
    }

    /// <summary>
    /// 性能指标的运行时桥。PerformanceMonitor 在 Awake 注册、OnDestroy 注销；
    /// Runtime 的诊断命令通过它读取指标，不直接引用 Modules。
    /// </summary>
    public static class PerformanceStatsBridge
    {
        /// <summary>当前实现者；PerformanceMonitor 未创建时为 null。</summary>
        public static IPerformanceStats Current { get; private set; }

        /// <summary>按需创建实现者（保持旧行为：诊断命令首次访问时创建 PerformanceMonitor）。</summary>
        public static Func<IPerformanceStats> Resolve { get; set; }

        /// <summary>取当前实现者；没有时按注册的工厂创建。</summary>
        public static IPerformanceStats GetOrCreate()
        {
            if (Current != null) return Current;
            return Resolve?.Invoke();
        }

        public static void Register(IPerformanceStats stats)
        {
            if (stats != null) Current = stats;
        }

        public static void Unregister(IPerformanceStats stats)
        {
            if (ReferenceEquals(Current, stats)) Current = null;
        }

        /// <summary>清空当前实例（跨 Play 会话复位用；保留工厂，避免复位顺序影响按需创建）。</summary>
        public static void ClearCurrent()
        {
            Current = null;
        }

        /// <summary>清空当前实例与工厂（测试用）。</summary>
        public static void Reset()
        {
            Current = null;
            Resolve = null;
        }
    }
}
