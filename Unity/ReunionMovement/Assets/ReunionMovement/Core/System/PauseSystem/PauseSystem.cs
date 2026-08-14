using Cysharp.Threading.Tasks;
using R3;
using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using System;
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
        /// 设置暂停状态（幂等：同状态重复调用无副作用）。
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (!isInited)
            {
                Log.Warning("[PauseSystem] 系统未初始化，SetPaused 被忽略");
                return;
            }
            if (IsPaused.Value == paused) return;

            try
            {
                IsPaused.Value = paused;
            }
            catch (Exception ex)
            {
                Log.Error("[PauseSystem] 订阅者异常（不影响暂停状态）: {0}", ex.Message);
            }

            Time.timeScale = paused ? 0f : 1f;

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
        public void TogglePause() => SetPaused(!IsPaused.Value);

        public void Clear()
        {
            Log.Debug("PauseSystem 清除数据");

            // 恢复时间缩放与音频，防止引擎销毁/重建后残留 timeScale=0 卡死下一局
            if (IsPaused != null && IsPaused.Value)
            {
                IsPaused.Value = false;
                Time.timeScale = 1f;
            }
            if (audioPausedByUs)
            {
                audioPausedByUs = false;
                AudioListener.pause = false;
            }

            isInited = false;
            initProgress = 0;
        }
    }
}
