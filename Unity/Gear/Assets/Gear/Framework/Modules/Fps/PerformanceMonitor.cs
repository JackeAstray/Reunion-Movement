using System;
using UnityEngine;

using Gear.Common;
using Gear.Framework.Runtime.Base;
using Gear.Utilities;
namespace Gear.Framework.Modules
{
    /// <summary>
    /// 性能监控 —— 每秒采样一次 FPS 与托管内存，超阈值时告警（防抖：同一指标只告警一次，恢复后允许再次告警）。
    /// 用法：PerformanceMonitor.Instance 读取当前指标 / 订阅告警事件；阈值可在 Inspector 或代码配置。
    /// FPS 采样单一权威：FPSCounter 等显示组件直接从 Instance.CurrentFps 读取，不再各自计数。
    /// 注意：独立 1s 节流轮询，属于监控工具（与 ScreenLogger 同类），刻意不接入 GameEngine 模块调度。
    /// </summary>
    public class PerformanceMonitor : SingletonMgr<PerformanceMonitor>, IPerformanceStats
    {
        /// <summary>跨场景存活（采样统计不因切场景重置）</summary>
        protected override bool IsPersistentAcrossScenes => true;

        protected override void Awake()
        {
            base.Awake();
            if (ReferenceEquals(Instance, this))
            {
                // Runtime 的诊断命令通过 Base 的桥读指标，不直接引用 Modules。
                PerformanceStatsBridge.Register(this);
            }
        }

        protected override void OnDestroy()
        {
            PerformanceStatsBridge.Unregister(this);
            base.OnDestroy();
        }

        /// <summary>进入 Play 时注册按需创建工厂（不创建对象），交由 Runtime 的诊断命令使用。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterBridgeOnPlay()
        {
            PerformanceStatsBridge.Resolve = () => Instance;
        }

        [Header("告警阈值")]
        [Tooltip("低于此 FPS 触发低帧率告警")]
        public float lowFpsThreshold = 20f;
        [Tooltip("托管内存超过此值（MB）触发内存告警")]
        public long highMemoryMB = 1024L;

        [Header("采样间隔（秒）")]
        public float sampleInterval = 1f;

        /// <summary>当前 FPS（每个采样周期更新）</summary>
        public float CurrentFps { get; private set; }
        /// <summary>当前托管内存（MB，GC.GetTotalMemory）</summary>
        public long CurrentMemoryMB { get; private set; }
        /// <summary>历史最低 FPS 记录</summary>
        public float MinFpsRecord { get; private set; } = float.MaxValue;

        /// <summary>低帧率告警事件（参数：当前 FPS）</summary>
        public event Action<float> OnLowFpsWarning;
        /// <summary>内存告警事件（参数：当前 MB）</summary>
        public event Action<long> OnHighMemoryWarning;

        private float sampleTimer;
        private int frameCount;
        private bool lowFpsWarned;
        private bool highMemWarned;

        private void Update()
        {
            sampleTimer += Time.unscaledDeltaTime;
            frameCount++;
            // 钳制采样间隔：sampleInterval<=0 时原判断永不成立 → CurrentFps 恒 0，
            // 下游 FPSCounter 会一直显示红色 "FPS: 0"
            float effectiveInterval = Mathf.Max(0.05f, sampleInterval);
            if (sampleTimer < effectiveInterval) return;

            // 采样（unscaledDeltaTime：不受 timeScale 影响）
            CurrentFps = frameCount / Mathf.Max(sampleTimer, 0.0001f);
            CurrentMemoryMB = GC.GetTotalMemory(false) / (1024L * 1024L);
            MinFpsRecord = Mathf.Min(MinFpsRecord, CurrentFps);
            sampleTimer = 0f;
            frameCount = 0;

            // 低帧率告警（防抖：持续低帧只告警一次，恢复后重置）
            if (CurrentFps < lowFpsThreshold)
            {
                if (!lowFpsWarned)
                {
                    lowFpsWarned = true;
                    Log.Warning($"[PerformanceMonitor] 低帧率告警: {CurrentFps:F1} FPS（阈值 {lowFpsThreshold:F0}）");
                    OnLowFpsWarning?.Invoke(CurrentFps);
                }
            }
            else
            {
                lowFpsWarned = false;
            }

            // 内存告警（防抖）
            if (CurrentMemoryMB > highMemoryMB)
            {
                if (!highMemWarned)
                {
                    highMemWarned = true;
                    Log.Warning($"[PerformanceMonitor] 内存告警: {CurrentMemoryMB} MB（阈值 {highMemoryMB} MB）");
                    OnHighMemoryWarning?.Invoke(CurrentMemoryMB);
                }
            }
            else
            {
                highMemWarned = false;
            }
        }
    }
}
