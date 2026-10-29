using Gear.Common;
using Gear.Framework.Runtime.Base;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Gear.Framework.Runtime
{
    public sealed partial class GameEngine
    {
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

            // 依赖补全：接入方删掉某个模块时，被它依赖的提供者会按 [ModuleOrder(..., DependsOn)] 自动补回，
            // 避免"缺依赖 → 告警 → 退回注册顺序"这种看不出原因的退化。可用 Bootstrap.Options 关掉。
            var declaredModules = moduleList;
            if (Bootstrap.Options.AutoExpandModuleDependencies)
            {
                declaredModules = ModuleCatalog.ExpandDependencies(moduleList,
                    out var addedModules, out var unresolvedModules);

                if (addedModules.Count > 0)
                {
                    Log.Debug("[GameEngine] 按 [ModuleOrder] 自动补入依赖模块: {0}", string.Join("、", addedModules));
                }
                if (unresolvedModules.Count > 0)
                {
                    Log.Warning("[GameEngine] 以下依赖无法自动补入（未注册且无法创建）: {0}", string.Join("；", unresolvedModules));
                }
            }

            // 模块顺序：优先按 [ModuleOrder] 声明的依赖与序号排序；
            // 校验失败（缺依赖 / 重复注册 / 成环）沿用注册顺序，保证不比改造前更差，同时把问题写进启动日志。
            if (ModuleCatalog.TryBuild(declaredModules, out var orderedModules, out var catalogErrors))
            {
                modules = orderedModules;
                Log.Debug("[GameEngine] 模块顺序: {0}", ModuleCatalog.Describe(modules));
            }
            else
            {
                Log.Warning("[GameEngine] 模块顺序校验未通过，沿用注册顺序: {0}", catalogErrors);
                modules = declaredModules;
            }

            // 失败重试时清空上次已注册的更新模块，避免重复添加导致模块每帧被 Update/LateUpdate 多次
            updatableModules.Clear();
            fixedUpdateModules.Clear();
            lateUpdateModules.Clear();

            // 取消链：外部 ct + 总超时 + Dispose（Dispose 中 Cancel launchCts 中断在途初始化）
            // 超时不用 new CancellationTokenSource(TimeSpan) / CancelAfter：二者依赖 System.Threading.Timer，
            // 在 WebGL 上永不触发（启动卡住时没有任何兜底），改用 PlayerLoop 驱动的 PlatformTimeout。
            var timeoutCts = new CancellationTokenSource();
            var timeoutDelayCts = new CancellationTokenSource();
            PlatformTimeout.CancelAfterAsync(timeoutCts, timeoutDelayCts.Token,
                TimeSpan.FromSeconds(Mathf.Max(1f, timeoutSeconds))).Forget();
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

                var elapsed = Time.realtimeSinceStartup - t0;
                Log.Debug("[GameEngine] 初始化完成，总耗时: {0:F3}s", elapsed);

                // 隔离订阅者异常：初始化已成功完成，订阅者抛错不应翻转引擎生命周期
                try
                {
                    OnInitProgressSubject?.OnNext(100d);
                }
                catch (Exception ex)
                {
                    Log.Error("[GameEngine] OnInitProgressSubject 订阅者异常（不影响引擎状态）: {0}", ex.Message);
                }
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
                // 先停掉待触发的超时任务，再释放 CTS（否则它会在超时到点后去 Cancel 已释放的对象）
                timeoutDelayCts.Cancel();
                timeoutDelayCts.Dispose();
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

                // 预过滤：记录需要 LateUpdate 的模块（ISystemLateUpdatable）
                if (module is ISystemLateUpdatable lateUpdatable)
                {
                    lateUpdateModules.Add(lateUpdatable);
                }

                // 聚合初始化进度：i 之前的模块计 100%，当前模块取实时 InitProgress，其后计 0。
                // 广播给 OnInitProgressSubject，加载界面可直接订阅显示进度条
                BroadcastInitProgress(moduleList, i);

#if UNITY_EDITOR
                var nowMem = GC.GetTotalMemory(false);
                Log.Debug("  Module [{0}] Init: {1:F3}s, 内存: {2} bytes", module.GetType().Name, endTime - startTime, nowMem - startMem);
#else
                Log.Debug("  Module [{0}] Init: {1:F3}s", module.GetType().Name, endTime - startTime);
#endif
            }
        }

        /// <summary>
        /// 计算并广播模块初始化聚合进度（已完成模块计 100%，当前模块取其实时 InitProgress）。
        /// 订阅者异常隔离（进度通知失败不应中断初始化流程）。
        /// </summary>
        private static void BroadcastInitProgress(IList<ICustomSystem> moduleList, int currentIndex)
        {
            if (OnInitProgressSubject == null || moduleList == null || moduleList.Count <= 0) return;

            double aggregate = 0;
            for (int i = 0; i < moduleList.Count; i++)
            {
                var m = moduleList[i];
                if (m == null) { aggregate += 100; continue; }
                double p = 0;
                if (i < currentIndex) p = 100;
                else if (i == currentIndex)
                {
                    p = m.InitProgress;
                    if (p < 0) p = 0;
                    else if (p > 100) p = 100;
                }
                aggregate += p;
            }

            try
            {
                OnInitProgressSubject.OnNext(aggregate / moduleList.Count);
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] OnInitProgressSubject 订阅者异常（已隔离）: {0}", ex.Message);
            }
        }

    }
}
