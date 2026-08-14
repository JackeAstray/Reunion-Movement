using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using ReunionMovement.Core.Pause;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 游戏引擎 —— 纯 C# 类，驱动整个游戏生命周期。
    /// 不再继承 MonoBehaviour，由 GameEngineDriver 提供 Unity 生命周期桥接。
    /// </summary>
    public sealed class GameEngine
    {
        #region 静态访问
        /// <summary>当前引擎实例（Bootstrap 创建后设置，Dispose 时置 null）</summary>
        public static GameEngine Current { get; private set; }
        #endregion

        #region 状态与事件
        /// <summary>当前生命周期状态</summary>
        public EngineState State { get; private set; } = EngineState.Uninitialized;

        // ============================================================
        //  R3 响应式事件（推荐使用）—— 自动管理订阅生命周期，无内存泄漏
        // ============================================================

        /// <summary>初始化完成（R3 Subject）</summary>
        public static Subject<Unit> OnInitializedSubject { get; private set; } = new Subject<Unit>();

        /// <summary>初始化失败（R3 Subject，参数为错误消息）</summary>
        public static Subject<string> OnInitFailedSubject { get; private set; } = new Subject<string>();
        #endregion

        #region 模块
        /// <summary>已注册的游戏模块（只读）</summary>
        public IList<ICustomSystem> Modules => modules;
        private IList<ICustomSystem> modules;

        /// <summary>需要每帧 Update 的模块（预过滤，避免空调用）</summary>
        private readonly List<ISystemUpdatable> updatableModules = new List<ISystemUpdatable>();

        /// <summary>需要固定步长 FixedUpdate 的模块（预过滤，避免空调用）</summary>
        private readonly List<ISystemFixedUpdatable> fixedUpdateModules = new List<ISystemFixedUpdatable>();

        /// <summary>清理路径的可复用快照列表（避免每次 ClearModuleData 分配）</summary>
        private readonly List<ICustomSystem> clearSnapshot = new List<ICustomSystem>();

        /// <summary>启动取消链（外部 ct + 超时 + Dispose 取消），LaunchAsync 执行期间非 null</summary>
        private CancellationTokenSource launchCts;

        /// <summary>
        /// 运行时注册模块（主线程调用）。引擎已运行时先执行 Init 再纳入更新列表；
        /// 引擎未启动时仅加入列表（LaunchAsync 时统一 Init）。
        /// 注意：列表即 LaunchAsync 传入的列表，若外部重建列表则注册失效。
        /// </summary>
        /// <returns>是否注册成功（模块为 null/已存在/Init 失败返回 false）</returns>
        public async UniTask<bool> AddModuleAsync(ICustomSystem module)
        {
            if (module == null)
            {
                Log.Warning("[GameEngine] AddModuleAsync: module 为 null");
                return false;
            }
            if (modules == null)
            {
                Log.Warning("[GameEngine] AddModuleAsync: 模块列表未初始化（LaunchAsync 尚未调用）");
                return false;
            }
            if (modules.Contains(module))
            {
                Log.Debug("[GameEngine] 模块已注册，跳过: {0}", module.GetType().Name);
                return true;
            }

            // 引擎运行/暂停中：模块需先完成自身初始化再进入更新列表
            if (State == EngineState.Running || State == EngineState.Paused)
            {
                try
                {
                    await module.Init();
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] 运行时模块 {0} Init 异常（未注册）: {1}", module.GetType().Name, ex.Message);
                    return false;
                }
            }

            modules.Add(module);
            if (module is ISystemUpdatable updatable) updatableModules.Add(updatable);
            if (module is ISystemFixedUpdatable fixedUpdatable) fixedUpdateModules.Add(fixedUpdatable);
            Log.Debug("[GameEngine] 模块已注册: {0}", module.GetType().Name);
            return true;
        }

        /// <summary>
        /// 运行时移除模块（主线程调用）：从列表与更新列表摘除，并清理其数据（ISystemDisposable.Clear）。
        /// 模块可在自身 Update 内调用（更新循环的越界防护已覆盖列表收缩场景）。
        /// </summary>
        /// <returns>是否移除成功（模块为 null/未注册返回 false）</returns>
        public bool RemoveModule(ICustomSystem module)
        {
            if (module == null || modules == null) return false;
            if (!modules.Remove(module))
            {
                Log.Warning("[GameEngine] RemoveModule: 模块未注册: {0}", module.GetType().Name);
                return false;
            }

            if (module is ISystemUpdatable updatable) updatableModules.Remove(updatable);
            if (module is ISystemFixedUpdatable fixedUpdatable) fixedUpdateModules.Remove(fixedUpdatable);

            if (module is ISystemDisposable disposable)
            {
                try
                {
                    disposable.Clear();
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] 模块 {0} Clear 异常（已隔离）: {1}", module.GetType().Name, ex.Message);
                }
            }
            Log.Debug("[GameEngine] 模块已移除: {0}", module.GetType().Name);
            return true;
        }
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
        /// 在引擎重建时重置 static Subject（上轮 Dispose 后可能为 null）
        /// </summary>
        private static void ResetStaticSubjects()
        {
            if (UpdateSubject          == null) UpdateSubject          = new Subject<Unit>();
            if (UpdatePer300msSubject  == null) UpdatePer300msSubject  = new Subject<Unit>();
            if (UpdatePer1sSubject     == null) UpdatePer1sSubject     = new Subject<Unit>();
            if (OnInitializedSubject   == null) OnInitializedSubject   = new Subject<Unit>();
            if (OnInitFailedSubject    == null) OnInitFailedSubject    = new Subject<string>();
            if (OnAppPauseSubject      == null) OnAppPauseSubject      = new Subject<bool>();
            if (OnAppFocusSubject      == null) OnAppFocusSubject      = new Subject<bool>();
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

        /// <summary>
        /// 启动引擎 —— 完整的异步初始化流程。
        /// 返回 InitResult 表示成功或失败，可被 await。
        /// </summary>
        /// <param name="entry">游戏入口实例</param>
        /// <param name="moduleList">模块列表</param>
        /// <param name="ct">外部取消令牌（取消后初始化中止，引擎回退为可重试状态）</param>
        /// <param name="timeoutSeconds">总超时（秒，默认 60；任一模块 Init 或 LoadScene 挂起时强制失败）</param>
        /// <returns>初始化结果</returns>
        public async UniTask<InitResult> LaunchAsync(IGameEntry entry, IList<ICustomSystem> moduleList, CancellationToken ct = default, float timeoutSeconds = 60f)
        {
            if (entry == null) return InitResult.Failure("GameEntry 不能为 null");
            if (moduleList == null) return InitResult.Failure("模块列表不能为 null");

            // 防止重复启动
            if (State == EngineState.Running)
            {
                Log.Warning("[GameEngine] LaunchAsync 被调用但引擎已在运行中，忽略");
                return InitResult.Success();
            }
            if (State == EngineState.BeforeInit || State == EngineState.Initializing || State == EngineState.Starting)
            {
                return InitResult.Failure("引擎正在初始化中，请勿重复调用 LaunchAsync");
            }

            GameEntry = entry;
            modules = moduleList;

            // 失败重试时清空上次已注册的更新模块，避免重复添加导致模块每帧被 Update 多次
            updatableModules.Clear();
            fixedUpdateModules.Clear();

            // 取消链：外部 ct + 总超时 + Dispose（Dispose 中 Cancel launchCts 中断在途初始化）
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Mathf.Max(1f, timeoutSeconds)));
            launchCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                var t0 = Time.realtimeSinceStartup;

                // 阶段 1：初始化前
                State = EngineState.BeforeInit;
                Log.Debug("[GameEngine] → BeforeInit");
                await entry.OnBeforeInitAsync().AttachExternalCancellation(launchCts.Token);
                // await 恢复后复查：初始化期间引擎可能已被 Dispose，继续执行会把已销毁引擎"复活"成 Running
                if (State == EngineState.Disposed) return InitResult.Failure("引擎已销毁，初始化中止");

                // 阶段 2：初始化模块
                State = EngineState.Initializing;
                Log.Debug("[GameEngine] → Initializing modules");
                await InitModulesAsync(modules, launchCts.Token);
                if (State == EngineState.Disposed) return InitResult.Failure("引擎已销毁，初始化中止");

                // 阶段 3：启动游戏
                State = EngineState.Starting;
                Log.Debug("[GameEngine] → Starting game");
                await entry.OnGameStartAsync().AttachExternalCancellation(launchCts.Token);
                if (State == EngineState.Disposed) return InitResult.Failure("引擎已销毁，初始化中止");

                // 完成
                State = EngineState.Running;
                IsAppPlaying = true;
                ModuleRuntime.IsEngineRunning = true;

                var elapsed = Time.realtimeSinceStartup - t0;
                Log.Debug("[GameEngine] 初始化完成，总耗时: {0:F3}s", elapsed);

                // 隔离订阅者异常：初始化已成功完成，订阅者抛错不应翻转引擎生命周期
                try
                {
                    OnInitializedSubject?.OnNext(Unit.Default);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] OnInitializedSubject 订阅者异常（不影响引擎状态）: {0}", ex.Message);
                }
                return InitResult.Success();
            }
            catch (OperationCanceledException)
            {
                // 取消/超时/Dispose 分支：与普通失败同样清理，但给出区分原因
                string reason;
                if (State == EngineState.Disposed)
                {
                    reason = "引擎已销毁，初始化中止";
                }
                else if (ct.IsCancellationRequested)
                {
                    reason = "初始化被外部取消";
                }
                else if (timeoutCts.IsCancellationRequested)
                {
                    reason = string.Format("初始化超时（>{0:F0}s）", timeoutSeconds);
                }
                else
                {
                    reason = "初始化被取消";
                }
                Log.Error("[GameEngine] {0}", reason);
                return FailInit(reason, null);
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException?.Message ?? ex.Message;
                Log.Error("[GameEngine] 初始化失败: {0}\n{1}", errorMsg, ex.StackTrace);
                return FailInit(errorMsg, ex);
            }
            finally
            {
                // 释放取消链（无论成功失败）；Dispose 中引用的 launchCts 已失效置 null
                launchCts?.Dispose();
                launchCts = null;
                timeoutCts.Dispose();
            }
        }

        /// <summary>
        /// 统一的初始化失败处理：标记 Failed（Disposed 除外）、清理已初始化模块、广播失败事件。
        /// </summary>
        private InitResult FailInit(string errorMsg, Exception ex)
        {
            // 引擎已被 Dispose 时不覆盖其状态（否则会抹掉 Disposed 标记）
            if (State != EngineState.Disposed)
            {
                State = EngineState.Failed;
            }
            ModuleRuntime.IsEngineRunning = false;
            // 清理已成功初始化的模块（资源/UI/音效等），避免初始化失败后模块资源全部泄漏
            ClearModuleData();
            // 隔离订阅者异常：失败通知的订阅者抛错不应掩盖真实初始化错误
            try
            {
                OnInitFailedSubject?.OnNext(errorMsg);
            }
            catch (Exception ex2)
            {
                Log.Error("[GameEngine] OnInitFailedSubject 订阅者异常（不影响错误处理）: {0}", ex2.Message);
            }
            return InitResult.Failure(errorMsg, ex);
        }

        /// <summary>
        /// 初始化所有模块（顺序执行，尊重列表中的依赖顺序）
        /// </summary>
        private async UniTask InitModulesAsync(IList<ICustomSystem> moduleList, CancellationToken ct)
        {
            for (int i = 0; i < moduleList.Count; i++)
            {
                var module = moduleList[i];
                if (module == null) continue;

#if UNITY_EDITOR
                var startMem = GC.GetTotalMemory(false);
#endif
                var startTime = Time.realtimeSinceStartup;
                await module.Init().AttachExternalCancellation(ct);
                var endTime = Time.realtimeSinceStartup;

                // 预过滤：记录需要 Update 的模块（ISystemUpdatable 取代 IRequiresUpdate）
                if (module is ISystemUpdatable updatable)
                {
                    updatableModules.Add(updatable);
                }

                // 预过滤：记录需要固定步长更新的模块（ISystemFixedUpdatable）
                if (module is ISystemFixedUpdatable fixedUpdatable)
                {
                    fixedUpdateModules.Add(fixedUpdatable);
                }

#if UNITY_EDITOR
                var nowMem = GC.GetTotalMemory(false);
                Log.Debug("  Module [{0}] Init: {1:F3}s, 内存: {2} bytes", module.GetType().Name, endTime - startTime, nowMem - startMem);
#else
                Log.Debug("  Module [{0}] Init: {1:F3}s", module.GetType().Name, endTime - startTime);
#endif
            }
        }

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

            accumTime1s += deltaTime;
            accumTime300ms += deltaTime;

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
            // 倒序遍历以便安全移除已失效模块；逐模块隔离异常：
            // 一个模块 Update 抛异常不应中断本帧后续模块（否则异常每帧刷屏且其余模块停摆）。
            int count = updatableModules.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                ISystemUpdatable module = null;
                try
                {
                    // 模块 Update 期间可能触发引擎 Dispose/重启清空列表，索引访问必须放在 try 内防越界
                    if (i >= updatableModules.Count) break;
                    module = updatableModules[i];

                    // fake-null 防护：MonoBehaviour 模块被意外销毁后引用非 null 但已失效，
                    // `?.` 拦截不住，直接调用会抛 MissingReferenceException
                    if (module is UnityEngine.Object unityObj && unityObj == null)
                    {
                        Log.Warning("[GameEngine] 移除已销毁的 Update 模块: {0}", module.GetType().Name);
                        updatableModules.RemoveAt(i);
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

            // 倒序遍历以便安全移除已失效模块；逐模块隔离异常（与 OnUpdate 策略一致）
            int count = fixedUpdateModules.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                ISystemFixedUpdatable module = null;
                try
                {
                    // 模块 FixedUpdate 期间可能触发引擎 Dispose/重启清空列表，索引访问必须放在 try 内防越界
                    if (i >= fixedUpdateModules.Count) break;
                    module = fixedUpdateModules[i];

                    // fake-null 防护：MonoBehaviour 模块被意外销毁后引用非 null 但已失效
                    if (module is UnityEngine.Object unityObj && unityObj == null)
                    {
                        Log.Warning("[GameEngine] 移除已销毁的 FixedUpdate 模块: {0}", module.GetType().Name);
                        fixedUpdateModules.RemoveAt(i);
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
                    AudioListener.pause = pause;
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

            // 委托 PauseSystem 执行 timeScale/音频暂停（未注册为模块时降级为仅状态切换）
            try
            {
                PauseSystem.Instance.SetPaused(paused);
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] PauseSystem 暂停执行异常: {0}", ex.Message);
            }
        }

        /// <summary>暂停游戏（与 SetPaused(true) 等价）</summary>
        public void Pause() => SetPaused(true);

        /// <summary>恢复游戏（与 SetPaused(false) 等价）</summary>
        public void Resume() => SetPaused(false);
        #endregion

        #region 清理
        /// <summary>清除所有模块数据（如切换账号/低内存时调用）</summary>
        public void ClearModuleData()
        {
            if (modules == null) return;
            // 快照遍历：模块 Clear 期间可能修改列表（如移除自身）
            clearSnapshot.Clear();
            clearSnapshot.AddRange(modules);
            foreach (var module in clearSnapshot)
            {
                try
                {
                    // 仅清理实现了 ISystemDisposable 的模块（避免空调用）
                    if (module is ISystemDisposable disposable)
                        disposable.Clear();
                }
                catch (Exception ex)
                {
                    // 逐模块隔离：单个模块 Clear 抛异常不应中断其余模块与引擎销毁流程
                    // （否则 Current 悬空、Subject 不释放、重建被拒）
                    Log.Error("[GameEngine] 模块 Clear 异常（已隔离）: {0}, {1}", module?.GetType().Name ?? "Unknown", ex.Message);
                }
            }
            clearSnapshot.Clear();
        }

        /// <summary>销毁引擎，释放所有资源</summary>
        public void Dispose()
        {
            if (State == EngineState.Disposed) return;

            // 中断在途初始化（LaunchAsync 的 await 恢复后将检测到 State==Disposed 提前退出）
            launchCts?.Cancel();

            ClearModuleData();

            State = EngineState.Disposed;
            ModuleRuntime.IsEngineRunning = false;
            updatableModules.Clear();
            fixedUpdateModules.Clear();
            modules = null;
            GameEntry = null;
            Current = null;

            // 完成并释放 R3 Subject：
            // 1) OnCompleted 通知所有订阅者流已结束（允许 Dispose 清理订阅）
            // 2) Dispose 释放 Subject 自身资源
            // 3) 置 null 以便 Create() 中 ResetStaticSubjects() 重新初始化
            UpdateSubject?.OnCompleted();
            UpdatePer300msSubject?.OnCompleted();
            UpdatePer1sSubject?.OnCompleted();
            OnInitializedSubject?.OnCompleted();
            OnInitFailedSubject?.OnCompleted();
            OnAppPauseSubject?.OnCompleted();
            OnAppFocusSubject?.OnCompleted();

            UpdateSubject?.Dispose();
            UpdatePer300msSubject?.Dispose();
            UpdatePer1sSubject?.Dispose();
            OnInitializedSubject?.Dispose();
            OnInitFailedSubject?.Dispose();
            OnAppPauseSubject?.Dispose();
            OnAppFocusSubject?.Dispose();

            UpdateSubject          = null;
            UpdatePer300msSubject  = null;
            UpdatePer1sSubject     = null;
            OnInitializedSubject   = null;
            OnInitFailedSubject    = null;
            OnAppPauseSubject      = null;
            OnAppFocusSubject      = null;
        }
        #endregion
    }
}