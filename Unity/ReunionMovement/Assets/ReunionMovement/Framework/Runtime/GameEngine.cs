using ReunionMovement.Common;
using ReunionMovement.Framework.Runtime.Base;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ReunionMovement.Framework.Runtime
{
    /// <summary>
    /// 游戏引擎 —— 纯 C# 类，驱动整个游戏生命周期。
    /// 不再继承 MonoBehaviour，由 GameEngineDriver 提供 Unity 生命周期桥接。
    /// </summary>
    public sealed partial class GameEngine
    {
        #region 静态访问
        /// <summary>当前引擎实例（Bootstrap 创建后设置，Dispose 时置 null）</summary>
        public static GameEngine Current { get; private set; }
        #endregion

        #region 状态与事件
        /// <summary>当前生命周期状态</summary>
        private EngineState state = EngineState.Uninitialized;
        public EngineState State
        {
            get => state;
            private set
            {
                if (state == value) return;
                state = value;
                // 状态变化广播：UI/逻辑可订阅引擎进入 Failed/Disposed/Paused 等状态做响应
                try
                {
                    OnStateChangedSubject?.OnNext(value);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] OnStateChangedSubject 订阅者异常（已隔离）: {0}", ex.Message);
                }
            }
        }

        // ============================================================
        //  R3 响应式事件（推荐使用）—— 自动管理订阅生命周期，无内存泄漏
        // ============================================================

        /// <summary>初始化完成（R3 Subject）</summary>
        public static Subject<Unit> OnInitializedSubject { get; private set; } = new Subject<Unit>();

        /// <summary>初始化失败（R3 Subject，参数为错误消息）</summary>
        public static Subject<string> OnInitFailedSubject { get; private set; } = new Subject<string>();

        /// <summary>模块初始化聚合进度（0~100，R3 Subject；加载界面可直接订阅显示进度条）</summary>
        public static Subject<double> OnInitProgressSubject { get; private set; } = new Subject<double>();

        /// <summary>引擎状态变化（R3 Subject，参数为新状态）：UI/逻辑可据此响应 Failed/Disposed/Paused 等</summary>
        public static Subject<EngineState> OnStateChangedSubject { get; private set; } = new Subject<EngineState>();
        #endregion


        #region 游戏入口
        /// <summary>游戏入口实例</summary>
        public IGameEntry GameEntry { get; private set; }
        #endregion

        #region 帧计时
        private float accumTime1s;
        private float accumTime300ms;
        #endregion

        #region 全局事件
        // ============================================================
        //  R3 响应式定时事件（推荐使用）
        // ============================================================

        /// <summary>每帧更新（R3 Subject）</summary>
        public static Subject<Unit> UpdateSubject { get; private set; } = new Subject<Unit>();

        /// <summary>每 300ms 更新（R3 Subject）</summary>
        public static Subject<Unit> UpdatePer300msSubject { get; private set; } = new Subject<Unit>();

        /// <summary>每 1s 更新（R3 Subject）</summary>
        public static Subject<Unit> UpdatePer1sSubject { get; private set; } = new Subject<Unit>();

        /// <summary>应用暂停/恢复事件（参数：是否暂停，由 GameEngineDriver.OnApplicationPause 广播）</summary>
        public static Subject<bool> OnAppPauseSubject { get; private set; } = new Subject<bool>();

        /// <summary>应用焦点事件（参数：是否获得焦点）</summary>
        public static Subject<bool> OnAppFocusSubject { get; private set; } = new Subject<bool>();
        #endregion

        #region 应用状态（保持向后兼容）
        /// <summary>应用是否已退出</summary>
        public static bool IsApplicationQuit { get; private set; }
        /// <summary>应用是否拥有焦点</summary>
        public static bool IsApplicationFocus { get; private set; } = true;
        /// <summary>应用是否已暂停（切后台/锁屏等，由 GameEngineDriver.OnApplicationPause 驱动）</summary>
        public static bool IsApplicationPaused { get; private set; }
        /// <summary>应用是否正在运行</summary>
        public static bool IsAppPlaying { get; private set; }
        #endregion

        // 私有构造 —— 仅由 Bootstrap 创建
        private GameEngine() { }

        /// <summary>
        /// 创建引擎实例（由 Bootstrap 调用）
        /// </summary>
        internal static GameEngine Create()
        {
            // 如果已有引擎实例且状态正常，直接返回
            if (Current != null)
            {
                var state = Current.State;
                if (state != EngineState.Disposed && state != EngineState.Failed)
                {
                    Log.Warning("[GameEngine] 引擎实例已存在且未销毁，跳过重复创建");
                    return Current;
                }
                // 如果之前的引擎已销毁或失败，允许重新创建
                Current = null;
            }

            // 复位跨会话静态应用状态（关闭 Domain Reload 时静态字段不会自动重置）
            ResetStaticState();

            // 重新初始化 static Subject（上一轮 Dispose 可能已将其置 null）
            ResetStaticSubjects();

            var engine = new GameEngine();
            Current = engine;
            return engine;
        }

        /// <summary>
        /// 在引擎重建时重置 static Subject（上轮 Dispose 后可能为 null，或异常路径残留已 Dispose 的实例）
        /// </summary>
        private static void ResetStaticSubjects()
        {
            if (UpdateSubject          == null || UpdateSubject.IsDisposed)          UpdateSubject          = new Subject<Unit>();
            if (UpdatePer300msSubject  == null || UpdatePer300msSubject.IsDisposed)  UpdatePer300msSubject  = new Subject<Unit>();
            if (UpdatePer1sSubject     == null || UpdatePer1sSubject.IsDisposed)     UpdatePer1sSubject     = new Subject<Unit>();
            if (OnInitializedSubject   == null || OnInitializedSubject.IsDisposed)   OnInitializedSubject   = new Subject<Unit>();
            if (OnInitFailedSubject    == null || OnInitFailedSubject.IsDisposed)    OnInitFailedSubject    = new Subject<string>();
            if (OnInitProgressSubject  == null || OnInitProgressSubject.IsDisposed)  OnInitProgressSubject  = new Subject<double>();
            if (OnStateChangedSubject  == null || OnStateChangedSubject.IsDisposed)  OnStateChangedSubject  = new Subject<EngineState>();
            if (OnAppPauseSubject      == null || OnAppPauseSubject.IsDisposed)      OnAppPauseSubject      = new Subject<bool>();
            if (OnAppFocusSubject      == null || OnAppFocusSubject.IsDisposed)      OnAppFocusSubject      = new Subject<bool>();
        }

        /// <summary>
        /// 复位跨会话静态应用状态（关闭 Domain Reload 时由 Bootstrap 的 SubsystemRegistration 调用）。
        /// </summary>
        internal static void ResetStaticState()
        {
            IsApplicationPaused = false;
            IsApplicationQuit = false;
            IsApplicationFocus = true;
            IsAppPlaying = false;
            ModuleRuntime.IsEngineRunning = false;
        }

    }
}
