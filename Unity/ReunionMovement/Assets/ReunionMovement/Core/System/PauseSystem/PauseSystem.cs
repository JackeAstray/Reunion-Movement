using Cysharp.Threading.Tasks;
using R3;
using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Core.Pause
{
    /// <summary>
    /// 暂停系统 —— 统一管理游戏暂停/恢复（此前项目 Time.timeScale 赋值 0 处，缺统一暂停能力）。
    /// 能力：
    /// - Time.timeScale 统一切换（0=暂停 / 1=恢复）
    /// - R3 ReactiveProperty&lt;bool&gt; IsPaused 供 UI/逻辑订阅
    /// - 可选暂停全局音频（与 GameEngine.OnAppPause 的 autoPause 逻辑协调，不互相覆盖）
    /// 注意：暂停期间 Unity 的 FixedUpdate 停摆（ISystemFixedUpdatable 模块不被驱动），
    /// Update 路径模块正常驱动（logicTime 归零），需要"暂停时仍运行"的逻辑请使用 realTime。
    /// </summary>
    public class PauseSystem : ICustomSystem, ISystemDisposable
    {
        #region 单例与初始化
        private static readonly Lazy<PauseSystem> instance = new(() => new PauseSystem());
        public static PauseSystem Instance => instance.Value;

        public bool isInited { get; private set; }

        private double initProgress = 0;
        public double InitProgress => initProgress;
        #endregion

        /// <summary>当前是否暂停（R3 可观测属性，UI 直接绑定）</summary>
        public ReactiveProperty<bool> IsPaused { get; private set; } = new ReactiveProperty<bool>(false);

        /// <summary>暂停时是否同步暂停全局音频（AudioListener）；默认开启</summary>
        public bool pauseAudioOnPause = true;

        /// <summary>全局音频暂停是否由本系统设置（恢复时据此判断是否还原，避免覆盖 autoPause 的状态）</summary>
        private bool audioPausedByUs = false;

        /// <summary>进入暂停前的外部时间缩放（慢动作等特效），恢复时还原而非无条件覆盖为 1</summary>
        private float timeScaleBeforePause = 1f;

        /// <summary>全局暂停原因键（SetPaused/TogglePause 兼容旧语义时使用）</summary>
        private static readonly object GlobalPauseReason = new object();

        /// <summary>
        /// 暂停原因集合：多系统可同时请求暂停（各自携带原因对象），
        /// 全部原因释放后才真正恢复——一方误释放不会打断其他系统的暂停。
        /// </summary>
        private readonly HashSet<object> pauseReasons = new HashSet<object>();

        public UniTask Init()
        {
            initProgress = 0;

            // Clear() 可能已 Dispose 并置 null，重初始化时重建
            IsPaused ??= new ReactiveProperty<bool>(false);

            initProgress = 100;
            isInited = true;
            Log.Debug("PauseSystem 初始化完成");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 设置暂停状态（兼容旧 API，幂等：同状态重复调用无副作用）。
        /// 等价于 RequestPause(全局键)/ReleasePause(全局键)。
        /// 多个系统同时暂停时推荐使用 <see cref="RequestPause"/>/<see cref="ReleasePause"/> 携带各自原因对象。
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (paused) RequestPause(GlobalPauseReason);
            else ReleasePause(GlobalPauseReason);
        }

        /// <summary>
        /// 请求暂停：注册一个暂停原因（同一 reason 重复注册幂等）。
        /// 只有所有原因都被 ReleasePause 释放后才真正恢复游戏。
        /// </summary>
        /// <param name="reason">原因标识对象（如请求暂停的系统实例；null 使用全局键）</param>
        public void RequestPause(object reason)
        {
            if (!isInited)
            {
                Log.Warning("[PauseSystem] 系统未初始化，RequestPause 被忽略");
                return;
            }
            if (reason == null) reason = GlobalPauseReason;

            if (pauseReasons.Count == 0)
            {
                ApplyPausedState(true);
            }
            pauseReasons.Add(reason);
        }

        /// <summary>释放一个暂停原因：全部原因释放后恢复游戏。</summary>
        public void ReleasePause(object reason)
        {
            if (reason == null) reason = GlobalPauseReason;
            if (pauseReasons.Remove(reason) && pauseReasons.Count == 0)
            {
                ApplyPausedState(false);
            }
        }

        /// <summary>实际应用暂停/恢复状态（IsPaused 值变化时执行 timeScale/音频切换）</summary>
        private void ApplyPausedState(bool paused)
        {
            if (IsPaused == null || IsPaused.Value == paused) return;

            try
            {
                IsPaused.Value = paused;
            }
            catch (Exception ex)
            {
                Log.Error("[PauseSystem] 订阅者异常（不影响暂停状态）: {0}", ex.Message);
            }

            // 进入暂停前记住外部时间缩放，恢复时还原（避免覆盖慢动作等特效设置的时间缩放）
            if (paused)
            {
                timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
            }

            if (pauseAudioOnPause)
            {
                if (paused && !AudioListener.pause)
                {
                    audioPausedByUs = true;
                    AudioListener.pause = true;
                }
                else if (!paused && audioPausedByUs)
                {
                    audioPausedByUs = false;
                    // 还原为应用级暂停状态（autoPause 开启且 App 在后台时仍应静音）
                    AudioListener.pause = GameEngine.IsApplicationPaused && GameOption.CurrentOption.autoPause;
                }
            }

            Log.Debug("[PauseSystem] 游戏{0}", paused ? "已暂停" : "已恢复");
        }

        /// <summary>切换暂停状态</summary>
        public void TogglePause()
        {
            // Clear 后 IsPaused 为 null（引擎已销毁）：视为未暂停，切换为暂停。
            // 直接访问 IsPaused.Value 会 NRE（残留 UI 回调在引擎重建间隙触发）。
            SetPaused(!(IsPaused != null && IsPaused.Value));
        }

        public void Clear()
        {
            Log.Debug("PauseSystem 清除数据");

            // 恢复时间缩放与音频，防止引擎销毁/重建后残留 timeScale=0 卡死下一局
            if (IsPaused != null && IsPaused.Value)
            {
                IsPaused.Value = false;
                Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
            }
            if (audioPausedByUs)
            {
                audioPausedByUs = false;
                AudioListener.pause = false;
            }

            timeScaleBeforePause = 1f;

            // 清空全部暂停原因（引擎重建后重新计票）
            pauseReasons.Clear();

            // 释放 R3 对象并置 null：否则已销毁组件的订阅仍被 ReactiveProperty 持有（订阅者泄漏），
            // 且与项目其他系统（UIInputSystem/LanguagesSystem）的 Clear 模式保持一致；Init 中 ??= 会重建
            IsPaused?.Dispose();
            IsPaused = null;

            isInited = false;
            initProgress = 0;
        }
    }
}
