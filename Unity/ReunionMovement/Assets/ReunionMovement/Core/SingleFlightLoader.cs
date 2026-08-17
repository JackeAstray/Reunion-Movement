using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 通用并发单飞加载门（Single Flight）：同名 key 的并发请求共享同一在途任务并等待同一结果，
    /// 避免重复加载/重复实例化；任务结束或取消后自动摘除（ReferenceEquals 校验防误删新登记条目）。
    /// 用于替代各系统中手写的 "Dictionary&lt;string, UniTaskCompletionSource&lt;T&gt;&gt; + finally 摘除" 模式
    /// （UISystem 窗口加载 / UIToolkitSystem 面板加载 / SoundSystem 音频加载等）。
    /// </summary>
    /// <typeparam name="T">加载结果类型</typeparam>
    public sealed class SingleFlightLoader<T>
    {
        private readonly Dictionary<string, UniTaskCompletionSource<T>> inflight =
            new Dictionary<string, UniTaskCompletionSource<T>>();

        /// <summary>当前在途任务数量（诊断用）</summary>
        public int InflightCount => inflight.Count;

        /// <summary>是否已有同名 key 在途</summary>
        public bool IsInflight(string key) => inflight.ContainsKey(key);

        /// <summary>
        /// 以单飞语义执行 factory：
        /// - 同名 key 已有时等待同一结果（等待方用 SuppressCancellationThrow 吞取消，取消时拿到 default）；
        /// - 否则执行 factory 并把返回值广播给所有等待方；
        /// - factory 抛异常时对等待方 TrySetException 并重抛给主调用方；
        /// - finally 仅摘除自己的条目（期间可能已登记新条目，ReferenceEquals 防误删）。
        /// </summary>
        public async UniTask<T> RunAsync(string key, Func<UniTask<T>> factory)
        {
            if (inflight.TryGetValue(key, out var pending))
            {
                var (_, result) = await pending.Task.SuppressCancellationThrow();
                return result;
            }

            var tcs = new UniTaskCompletionSource<T>();
            inflight[key] = tcs;
            try
            {
                var result = await factory();
                tcs.TrySetResult(result);
                return result;
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                throw;
            }
            finally
            {
                if (inflight.TryGetValue(key, out var cur) && ReferenceEquals(cur, tcs))
                {
                    inflight.Remove(key);
                }
            }
        }

        /// <summary>
        /// 取消全部在途任务（系统 Clear 时调用）：等待方以“已取消”恢复
        /// （SuppressCancellationThrow 返回 default），主调用方后续按已清理状态自行返回失败。
        /// </summary>
        public void CancelAll()
        {
            foreach (var kvp in inflight)
            {
                kvp.Value.TrySetCanceled();
            }
            inflight.Clear();
        }
    }
}
