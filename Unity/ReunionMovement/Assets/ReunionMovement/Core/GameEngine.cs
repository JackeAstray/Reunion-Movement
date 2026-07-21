using ReunionMovement.Common;
using ReunionMovement.Core.Base;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
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

        // ---- 向后兼容的 C# 事件（已废弃，转发到 R3 Subject）----

        /// <summary>[已废弃] 初始化完成事件。请使用 OnInitializedSubject.Subscribe()</summary>
        [Obsolete("请使用 GameEngine.OnInitializedSubject.Subscribe()", false)]
        public static event Action OnInitialized
        {
            add { OnInitializedSubject.Subscribe(_ => value()); }
            remove { /* R3 Subject 不支持移除单个订阅者；请使用 IDisposable.Dispose() */ }
        }

        /// <summary>[已废弃] 初始化失败事件。请使用 GameEngine.OnInitFailedSubject.Subscribe()</summary>
        [Obsolete("请使用 GameEngine.OnInitFailedSubject.Subscribe()", false)]
        public static event Action<string> OnInitFailed
        {
            add { OnInitFailedSubject.Subscribe(value); }
            remove { /* R3 Subject 不支持移除单个订阅者；请使用 IDisposable.Dispose() */ }
        }
        #endregion

        #region 模块
        /// <summary>已注册的游戏模块（只读）</summary>
        public IList<ICustomSystem> Modules => modules;
        private IList<ICustomSystem> modules;

        /// <summary>需要每帧 Update 的模块（预过滤，避免空调用）</summary>
        private readonly List<ISystemUpdatable> updatableModules = new List<ISystemUpdatable>();
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

        // ---- 向后兼容的 C# 事件（已废弃，转发到 R3 Subject）----

        /// <summary>[已废弃] 每帧更新事件。请使用 UpdateSubject.Subscribe()</summary>
        [Obsolete("请使用 GameEngine.UpdateSubject.Subscribe()", false)]
        public static event Action UpdateEvent
        {
            add { UpdateSubject.Subscribe(_ => value()); }
            remove { /* R3 Subject 不支持移除单个订阅者；请使用 IDisposable.Dispose() */ }
        }

        /// <summary>[已废弃] 每 300ms 更新事件。请使用 UpdatePer300msSubject.Subscribe()</summary>
        [Obsolete("请使用 GameEngine.UpdatePer300msSubject.Subscribe()", false)]
        public static event Action UpdatePer300msEvent
        {
            add { UpdatePer300msSubject.Subscribe(_ => value()); }
            remove { /* R3 Subject 不支持移除单个订阅者；请使用 IDisposable.Dispose() */ }
        }

        /// <summary>[已废弃] 每 1s 更新事件。请使用 UpdatePer1sSubject.Subscribe()</summary>
        [Obsolete("请使用 GameEngine.UpdatePer1sSubject.Subscribe()", false)]
        public static event Action UpdatePer1sEvent
        {
            add { UpdatePer1sSubject.Subscribe(_ => value()); }
            remove { /* R3 Subject 不支持移除单个订阅者；请使用 IDisposable.Dispose() */ }
        }
        #endregion

        #region 应用状态（保持向后兼容）
        /// <summary>应用是否已退出</summary>
        public static bool IsApplicationQuit { get; private set; }
        /// <summary>应用是否拥有焦点</summary>
        public static bool IsApplicationFocus { get; private set; } = true;
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

            var engine = new GameEngine();
            Current = engine;
            return engine;
        }

        /// <summary>
        /// 启动引擎 —— 完整的异步初始化流程。
        /// 返回 InitResult 表示成功或失败，可被 await。
        /// </summary>
        /// <param name="entry">游戏入口实例</param>
        /// <param name="moduleList">模块列表</param>
        /// <returns>初始化结果</returns>
        public async UniTask<InitResult> LaunchAsync(IGameEntry entry, IList<ICustomSystem> moduleList)
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

            try
            {
                var t0 = Time.realtimeSinceStartup;

                // 阶段 1：初始化前
                State = EngineState.BeforeInit;
                Log.Debug("[GameEngine] → BeforeInit");
                await entry.OnBeforeInitAsync();

                // 阶段 2：初始化模块
                State = EngineState.Initializing;
                Log.Debug("[GameEngine] → Initializing modules");
                await InitModulesAsync(modules);

                // 阶段 3：启动游戏
                State = EngineState.Starting;
                Log.Debug("[GameEngine] → Starting game");
                await entry.OnGameStartAsync();

                // 完成
                State = EngineState.Running;
                IsAppPlaying = true;

                var elapsed = Time.realtimeSinceStartup - t0;
                Log.Debug("[GameEngine] 初始化完成，总耗时: {0:F3}s", elapsed);

                OnInitializedSubject.OnNext(Unit.Default);
                return InitResult.Success();
            }
            catch (Exception ex)
            {
                State = EngineState.Failed;
                string errorMsg = ex.InnerException?.Message ?? ex.Message;
                Log.Error("[GameEngine] 初始化失败: {0}\n{1}", errorMsg, ex.StackTrace);
                OnInitFailedSubject.OnNext(errorMsg);
                return InitResult.Failure(errorMsg, ex);
            }
        }

        /// <summary>
        /// 初始化所有模块（顺序执行，尊重列表中的依赖顺序）
        /// </summary>
        private async UniTask InitModulesAsync(IList<ICustomSystem> moduleList)
        {
            for (int i = 0; i < moduleList.Count; i++)
            {
                var module = moduleList[i];
                if (module == null) continue;

#if UNITY_EDITOR
                var startMem = GC.GetTotalMemory(false);
#endif
                var startTime = Time.realtimeSinceStartup;
                await module.Init();
                var endTime = Time.realtimeSinceStartup;

                // 预过滤：记录需要 Update 的模块（ISystemUpdatable 取代 IRequiresUpdate）
                if (module is ISystemUpdatable updatable)
                {
                    updatableModules.Add(updatable);
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
            if (State != EngineState.Running) return;

            UpdateSubject.OnNext(Unit.Default);

            accumTime1s += deltaTime;
            accumTime300ms += deltaTime;

            if (accumTime1s >= 1.0f)
            {
                accumTime1s = 0f;
                UpdatePer1sSubject.OnNext(Unit.Default);
            }
            if (accumTime300ms >= 0.3f)
            {
                accumTime300ms = 0f;
                UpdatePer300msSubject.OnNext(Unit.Default);
            }

            // 仅遍历需要 Update 的模块（通过 ISystemUpdatable 接口预过滤）
            for (int i = 0; i < updatableModules.Count; i++)
            {
                updatableModules[i]?.Update(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>应用退出（由 GameEngineDriver 调用）</summary>
        internal void OnAppQuit()
        {
            GameOption.SaveOptions();
            IsApplicationQuit = true;
            IsAppPlaying = false;
        }

        /// <summary>应用焦点变化（由 GameEngineDriver 调用）</summary>
        internal void OnAppFocus(bool focus)
        {
            IsApplicationFocus = focus;
        }
        #endregion

        #region 清理
        /// <summary>清除所有模块数据（如切换账号/低内存时调用）</summary>
        public void ClearModuleData()
        {
            if (modules == null) return;
            foreach (var module in modules)
            {
                module?.Clear();
            }
        }

        /// <summary>销毁引擎，释放所有资源</summary>
        public void Dispose()
        {
            if (State == EngineState.Disposed) return;

            ClearModuleData();

            State = EngineState.Disposed;
            updatableModules.Clear();
            modules = null;
            GameEntry = null;
            Current = null;

            // 释放 R3 Subject（自动断开所有订阅，避免引用残留阻止 GC）
            UpdateSubject?.Dispose();
            UpdatePer300msSubject?.Dispose();
            UpdatePer1sSubject?.Dispose();
            OnInitializedSubject?.Dispose();
            OnInitFailedSubject?.Dispose();
        }
        #endregion
    }
}