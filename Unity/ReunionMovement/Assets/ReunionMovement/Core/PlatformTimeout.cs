using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ReunionMovement.Core
{
    /// <summary>
    /// 平台安全的超时取消工具。
    ///
    /// <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/> 与
    /// <c>new CancellationTokenSource(TimeSpan)</c> 的定时机制依赖 <c>System.Threading.Timer</c>，
    /// 而后者依赖线程池 —— WebGL 既无线程池也无 System.Threading.Timer，超时在 WebGL 上永不触发
    /// （Unity 官方文档：CancellationTokenSource 的“超时机制不工作，因为它依赖 Timer”）。
    /// 这里统一改用 PlayerLoop 驱动的 <see cref="UniTask.Delay(TimeSpan, bool, PlayerLoopTiming, CancellationToken)"/>
    /// 触发取消，全平台行为一致，且 ignoreTimeScale 使其不受 Time.timeScale（暂停）影响。
    /// </summary>
    internal static class PlatformTimeout
    {
        /// <summary>
        /// 在 <paramref name="delay"/> 之后取消 <paramref name="target"/>。
        /// 调用方提前结束（启动完成 / 加载完成）时取消 <paramref name="stopToken"/> 停止本任务，
        /// 避免残留一个待触发的取消动作去触碰已 Dispose 的 CTS。
        /// </summary>
        public static async UniTaskVoid CancelAfterAsync(CancellationTokenSource target, CancellationToken stopToken, TimeSpan delay)
        {
            bool stopped = await UniTask.Delay(delay, ignoreTimeScale: true, cancellationToken: stopToken)
                                         .SuppressCancellationThrow();
            if (stopped) return;

            try
            {
                target.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已释放：调用方已结束，忽略
            }
        }
    }
}
