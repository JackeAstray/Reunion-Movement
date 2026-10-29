using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ReunionMovement.Core
{
    public sealed partial class GameEngine
    {
        #region Unity 生命周期桥接（由 GameEngineDriver 调用）
        /// <summary>每帧更新（由 GameEngineDriver.Update 调用）</summary>
        internal void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            // Paused 状态仍驱动模块：timeScale=0 已使 logicTime 归零，模块可自行按 realTime 决定行为
            // （UI/网络等在暂停期间仍需工作，只有 Disposed/Failed/未初始化才停摆）
            if (State != EngineState.Running && State != EngineState.Paused) return;

            // 隔离订阅者异常：R3 Subject 会把订阅者异常传播到调用方，
            // 一个坏订阅者不应中断帧计时与其他模块更新
            try
            {
                UpdateSubject.OnNext(Unit.Default);
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] UpdateSubject 订阅者异常（已隔离）: {0}", ex.Message);
            }

            // 周期事件用 unscaledDeltaTime 累计：引擎 Paused 时仍驱动模块（UI/网络需工作），
            // timeScale=0 会使 scaled deltaTime 归零 → 心跳/自动保存类 1s/300ms 周期任务永久停摆；
            // 慢动作（timeScale<1）时周期任务也应按真实时间而非变慢。
            accumTime1s += unscaledDeltaTime;
            accumTime300ms += unscaledDeltaTime;

            // 保留余数（-= 而非 =0），避免低帧率下周期持续漂移
            if (accumTime1s >= 1.0f)
            {
                accumTime1s -= 1.0f;
                try
                {
                    UpdatePer1sSubject.OnNext(Unit.Default);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] UpdatePer1sSubject 订阅者异常（已隔离）: {0}", ex.Message);
                }
            }
            if (accumTime300ms >= 0.3f)
            {
                accumTime300ms -= 0.3f;
                try
                {
                    UpdatePer300msSubject.OnNext(Unit.Default);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] UpdatePer300msSubject 订阅者异常（已隔离）: {0}", ex.Message);
                }
            }

            // 仅遍历需要 Update 的模块（通过 ISystemUpdatable 接口预过滤）。
            // 快照遍历：模块 Update 内移除"其他模块"时列表收缩，直接倒序遍历会让
            // 同一模块同帧被驱动两次（RemoveModule(B) 后下标错位）；快照外本帧新注册的模块下一帧驱动。
            // 逐模块隔离异常：一个模块 Update 抛异常不中断本帧后续模块。
            updateSnapshot.Clear();
            updateSnapshot.AddRange(updatableModules);
            for (int i = updateSnapshot.Count - 1; i >= 0; i--)
            {
                ISystemUpdatable module = updateSnapshot[i];
                try
                {
                    // 快照内的模块可能已被本帧更早的模块 Update 移除：跳过，防同帧双驱动
                    if (!updatableModules.Contains(module)) continue;

                    // fake-null 防护：MonoBehaviour 模块被意外销毁后引用非 null 但已失效，
                    // `?.` 拦截不住，直接调用会抛 MissingReferenceException
                    if (module is UnityEngine.Object unityObj && unityObj == null)
                    {
                        Log.Warning("[GameEngine] 移除已销毁的 Update 模块: {0}", module.GetType().Name);
                        updatableModules.Remove(module);
                        continue;
                    }
                    module.Update(deltaTime, unscaledDeltaTime);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] 模块 Update 异常（已隔离）: {0}, {1}", module?.GetType().Name ?? "Unknown", ex.Message);
                }
            }
        }

        /// <summary>固定步长更新（由 GameEngineDriver.FixedUpdate 调用）</summary>
        internal void OnFixedUpdate(float fixedDeltaTime)
        {
            // 暂停时不驱动固定步长模块（timeScale=0 时 Unity 本就不调 FixedUpdate，此处双重保险）
            if (State != EngineState.Running) return;
            // 快照遍历（与 OnUpdate 一致）：模块 FixedUpdate 内移除其他模块时防同帧双驱动
            fixedUpdateSnapshot.Clear();
            fixedUpdateSnapshot.AddRange(fixedUpdateModules);
            for (int i = fixedUpdateSnapshot.Count - 1; i >= 0; i--)
            {
                ISystemFixedUpdatable module = fixedUpdateSnapshot[i];
                try
                {
                    if (!fixedUpdateModules.Contains(module)) continue;

                    // fake-null 防护：MonoBehaviour 模块被意外销毁后引用非 null 但已失效
                    if (module is UnityEngine.Object unityObj && unityObj == null)
                    {
                        Log.Warning("[GameEngine] 移除已销毁的 FixedUpdate 模块: {0}", module.GetType().Name);
                        fixedUpdateModules.Remove(module);
                        continue;
                    }
                    module.FixedUpdate(fixedDeltaTime);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] 模块 FixedUpdate 异常（已隔离）: {0}, {1}", module?.GetType().Name ?? "Unknown", ex.Message);
                }
            }
        }

        /// <summary>延迟更新（由 GameEngineDriver.LateUpdate 调用）：在全部模块 Update 之后驱动，
        /// 供依赖当帧状态的逻辑（跟随动画、相机、UI 布局收尾）使用。</summary>
        internal void OnLateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            // 与 OnUpdate 一致：Running/Paused 均驱动（timeScale=0 时 logicTime 归零，模块按 realTime 决策）
            if (State != EngineState.Running && State != EngineState.Paused) return;

            // 快照遍历 + 逐模块异常隔离（与 OnUpdate 策略一致）
            lateUpdateSnapshot.Clear();
            lateUpdateSnapshot.AddRange(lateUpdateModules);
            for (int i = lateUpdateSnapshot.Count - 1; i >= 0; i--)
            {
                ISystemLateUpdatable module = lateUpdateSnapshot[i];
                try
                {
                    if (!lateUpdateModules.Contains(module)) continue;

                    // fake-null 防护：MonoBehaviour 模块被意外销毁后引用非 null 但已失效
                    if (module is UnityEngine.Object unityObj && unityObj == null)
                    {
                        Log.Warning("[GameEngine] 移除已销毁的 LateUpdate 模块: {0}", module.GetType().Name);
                        lateUpdateModules.Remove(module);
                        continue;
                    }
                    module.LateUpdate(deltaTime, unscaledDeltaTime);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] 模块 LateUpdate 异常（已隔离）: {0}, {1}", module?.GetType().Name ?? "Unknown", ex.Message);
                }
            }
        }

        /// <summary>应用退出（由 GameEngineDriver 调用）</summary>
        internal void OnAppQuit()
        {
            // 刷新日志缓冲区，确保批量 flush 的日志在退出时写入磁盘
            GameLogger.FlushFileLog();
            GameOption.SaveOptions();
            IsApplicationQuit = true;
            IsAppPlaying = false;
            // 关闭文件日志：退订回调、关闭 StreamWriter（编辑器跨会话/长时间运行防句柄泄漏）
            LogHelper.Shutdown();
        }

        /// <summary>应用焦点变化（由 GameEngineDriver 调用）</summary>
        internal void OnAppFocus(bool focus)
        {
            IsApplicationFocus = focus;
            try
            {
                OnAppFocusSubject?.OnNext(focus);
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] OnAppFocusSubject 订阅者异常（已隔离）: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 应用暂停/恢复（由 GameEngineDriver.OnApplicationPause 调用）。
        /// autoPause 选项开启时：切后台自动暂停音频、回前台自动恢复。
        /// </summary>
        internal void OnAppPause(bool pause)
        {
            IsApplicationPaused = pause;
            try
            {
                OnAppPauseSubject?.OnNext(pause);
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] OnAppPauseSubject 订阅者异常（已隔离）: {0}", ex.Message);
            }
            try
            {
                if (GameOption.CurrentOption.autoPause)
                {
                    // 与游戏级暂停的音频状态协调：游戏级暂停（已静音）期间切后台再回前台，
                    // 不能无条件恢复音频 —— 最终状态 = 应用级暂停 || 游戏级暂停。
                    // （反之 PauseSystem.ApplyPausedState 恢复时已用 IsApplicationPaused && autoPause 还原本状态，
                    //   本处补上另一半对称逻辑，避免两套系统互相覆盖。）
                    // 引擎不直接引用 PauseSystem：只向已注册模块找 IGamePauseHandler 实现者。
                    bool pausedByGame = FindModule<IGamePauseHandler>()?.IsPaused == true;
                    AudioListener.pause = pause || pausedByGame;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] OnAppPause 应用音频暂停失败: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 游戏级暂停/恢复（显式状态机入口）：Running ↔ Paused。
        /// 推荐统一通过本入口暂停游戏（内部委托 PauseSystem 执行 timeScale/音频），
        /// 避免绕过状态机直接改 timeScale 导致引擎状态失配。
        /// </summary>
        public bool IsPaused => State == EngineState.Paused;

        public void SetPaused(bool paused)
        {
            if (State != EngineState.Running && State != EngineState.Paused)
            {
                Log.Warning("[GameEngine] SetPaused 在状态 {0} 下被忽略（仅 Running/Paused 可切换）", State);
                return;
            }
            if (paused == IsPaused) return;

            State = paused ? EngineState.Paused : EngineState.Running;
            Log.Debug("[GameEngine] 引擎{0}", paused ? "已暂停" : "已恢复");

            // 委托暂停执行者处理 timeScale/音频（未注册实现者时降级为仅状态切换）
            try
            {
                FindModule<IGamePauseHandler>()?.SetPaused(paused);
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] GamePauseHandler 暂停执行异常: {0}", ex.Message);
            }
        }

        /// <summary>暂停游戏（与 SetPaused(true) 等价）</summary>
        public void Pause() => SetPaused(true);

        /// <summary>恢复游戏（与 SetPaused(false) 等价）</summary>
        public void Resume() => SetPaused(false);
        #endregion

    }
}
