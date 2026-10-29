using Astray.Common;
using Astray.Framework.Runtime.Base;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Astray.Framework.Runtime
{
    public sealed partial class GameEngine
    {
        #region 清理
        /// <summary>清除所有模块数据（如切换账号/低内存时调用）</summary>
        public void ClearModuleData()
        {
            if (modules == null) return;
            // 快照遍历：模块 Clear 期间可能修改列表（如移除自身）
            clearSnapshot.Clear();
            clearSnapshot.AddRange(modules);
            for (int index = clearSnapshot.Count - 1; index >= 0; index--)
            {
                if (clearSnapshot[index] is Astray.Framework.Runtime.Resources.ResourcesSystem)
                {
                    var resources = clearSnapshot[index];
                    clearSnapshot.RemoveAt(index);
                    clearSnapshot.Add(resources);
                    break;
                }
            }
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
            lateUpdateModules.Clear();
            modules = null;
            GameEntry = null;
            Current = null;

            // 完成并释放 R3 Subject：
            // 1) OnCompleted 通知所有订阅者流已结束（允许 Dispose 清理订阅）
            // 2) Dispose 释放 Subject 自身资源
            // 3) 置 null 以便 Create() 中 ResetStaticSubjects() 重新初始化
            // 注意：每个 Subject 独立隔离 —— 订阅者 OnCompleted 抛异常不得中断其余 Subject 的释放，
            // 否则静态字段残留“completed 非 null”的 Subject，新引擎事件全部静默失效。
            UpdateSubject          = CompleteAndDisposeSubject(UpdateSubject);
            UpdatePer300msSubject  = CompleteAndDisposeSubject(UpdatePer300msSubject);
            UpdatePer1sSubject     = CompleteAndDisposeSubject(UpdatePer1sSubject);
            OnInitializedSubject   = CompleteAndDisposeSubject(OnInitializedSubject);
            OnInitFailedSubject    = CompleteAndDisposeSubject(OnInitFailedSubject);
            OnInitProgressSubject  = CompleteAndDisposeSubject(OnInitProgressSubject);
            OnAppPauseSubject      = CompleteAndDisposeSubject(OnAppPauseSubject);
            OnAppFocusSubject      = CompleteAndDisposeSubject(OnAppFocusSubject);
        }

        /// <summary>
        /// 安全完成并释放静态 Subject（订阅者异常隔离，无论是否抛出都完成 Dispose 并返回 null）。
        /// 注意：静态 Subject 是属性不能作 ref 参数，采用返回值语义由调用方重新赋值。
        /// </summary>
        private static Subject<T> CompleteAndDisposeSubject<T>(Subject<T> subject)
        {
            if (subject == null) return null;
            try
            {
                subject.OnCompleted();
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] Subject OnCompleted 订阅者异常（已隔离）: {0}", ex.Message);
            }
            try
            {
                subject.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("[GameEngine] Subject Dispose 异常（已隔离）: {0}", ex.Message);
            }
            return null;
        }
        #endregion
    }
}
