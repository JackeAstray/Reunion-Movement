using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 任务程序
    /// </summary>
    public static class TaskUtil
    {
        /// <summary>
        /// 开始一个不返回值的任务。
        /// </summary>
        /// <param name="action">要执行的操作。</param>
        /// <param name="callback">任务成功完成时的回调。</param>
        /// <param name="onError">任务发生异常时的回调。</param>
        /// <param name="timeout">任务超时时间。</param>
        /// <param name="cancellationToken">用于取消任务的 CancellationToken。</param>
        public static async void StartTask(Action action, Action callback = null, Action<Exception> onError = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await ExecuteTask(() => { action?.Invoke(); return true; }, timeout, cancellationToken);
                callback?.Invoke();
            }
            catch (Exception ex)
            {
                HandleException(ex, onError);
            }
        }

        /// <summary>
        /// 开始一个返回指定类型值的任务。
        /// </summary>
        /// <typeparam name="T">任务返回值的类型。</typeparam>
        /// <param name="func">要执行的函数。</param>
        /// <param name="callback">任务成功完成时的回调，接收任务结果。</param>
        /// <param name="onError">任务发生异常时的回调。</param>
        /// <param name="timeout">任务超时时间。</param>
        /// <param name="cancellationToken">用于取消任务的 CancellationToken。</param>
        public static async void StartTask<T>(Func<T> func, Action<T> callback = null, Action<Exception> onError = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            try
            {
                T result = await ExecuteTask(func, timeout, cancellationToken);
                callback?.Invoke(result);
            }
            catch (Exception ex)
            {
                HandleException(ex, onError);
            }
        }

        /// <summary>
        /// 在前一个任务成功完成后继续执行新的无返回值的任务。
        /// </summary>
        /// <param name="previousTask">前一个任务。</param>
        /// <param name="continuation">要执行的接续操作。</param>
        /// <param name="onError">任务发生异常时的回调。</param>
        /// <param name="cancellationToken">用于取消任务的 CancellationToken。</param>
        public static void ContinueWith(this Task previousTask, Action continuation, Action<Exception> onError = null, CancellationToken cancellationToken = default)
        {
            previousTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    HandleException(t.Exception.InnerException, onError);
                }
                else if (!t.IsCanceled)
                {
                    continuation?.Invoke();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 在前一个任务成功完成后继续执行新的带返回值的任务。
        /// </summary>
        /// <typeparam name="TResult">前一个任务返回值的类型。</typeparam>
        /// <typeparam name="TNewResult">新任务返回值的类型。</typeparam>
        /// <param name="previousTask">前一个任务。</param>
        /// <param name="continuation">要执行的接续函数，接收前一个任务的结果。</param>
        /// <param name="onError">任务发生异常时的回调。</param>
        /// <param name="cancellationToken">用于取消任务的 CancellationToken。</param>
        /// <returns>表示新任务的 Task。</returns>
        public static Task<TNewResult> ContinueWith<TResult, TNewResult>(this Task<TResult> previousTask, Func<TResult, TNewResult> continuation, Action<Exception> onError = null, CancellationToken cancellationToken = default)
        {
            return previousTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    HandleException(t.Exception.InnerException, onError);
                    return default;
                }
                if (t.IsCanceled)
                {
                    return default;
                }
                return continuation(t.Result);
            }, cancellationToken);
        }

        /// <summary>
        /// 开始一个带重试机制的任务。
        /// </summary>
        /// <typeparam name="T">任务返回值的类型。</typeparam>
        /// <param name="func">要执行的函数。</param>
        /// <param name="maxRetries">最大重试次数。</param>
        /// <param name="retryDelay">每次重试之间的延迟。</param>
        /// <param name="onError">任务发生异常时的回调。</param>
        /// <param name="cancellationToken">用于取消任务的 CancellationToken。</param>
        /// <returns>表示异步操作的 Task，包含任务结果。</returns>
        public static async Task<T> StartTaskWithRetry<T>(Func<Task<T>> func, int maxRetries = 3, TimeSpan? retryDelay = null, Action<Exception> onError = null, CancellationToken cancellationToken = default)
        {
            int attempts = 0;
            while (attempts < maxRetries)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await func();
                }
                catch (Exception ex)
                {
                    attempts++;
                    if (attempts >= maxRetries)
                    {
                        HandleException(ex, onError);
                        throw;
                    }
                    if (retryDelay.HasValue)
                    {
                        await Task.Delay(retryDelay.Value, cancellationToken);
                    }
                }
            }
            // 此处理论上不会到达，因为循环要么成功返回，要么在最后一次尝试失败后抛出异常。
            throw new InvalidOperationException("任务重试逻辑异常。");
        }

        /// <summary>
        /// 执行一个带有超时和取消支持的任务。
        /// </summary>
        /// <typeparam name="T">任务返回值的类型。</typeparam>
        /// <param name="func">要执行的函数。</param>
        /// <param name="timeout">任务超时时间。</param>
        /// <param name="cancellationToken">用于取消任务的 CancellationToken。</param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        private static async Task<T> ExecuteTask<T>(Func<T> func, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!timeout.HasValue)
            {
                return await Task.Run(func, cancellationToken);
            }

            using (var timeoutCts = new CancellationTokenSource(timeout.Value))
            using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            {
                var token = combinedCts.Token;
                Task<T> task = Task.Run(func, token);

                try
                {
                    return await task;
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException("任务超时");
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// 处理异常，根据是否提供了自定义错误处理回调来决定使用哪种方式处理。
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="onError"></param>
        private static void HandleException(Exception ex, Action<Exception> onError)
        {
            if (onError != null)
            {
                onError(ex);
            }
            else
            {
                HandleTaskException(ex);
            }
        }

        /// <summary>
        /// 处理任务异常的默认方法。
        /// </summary>
        /// <param name="ex"></param>
        private static void HandleTaskException(Exception ex)
        {
            switch (ex)
            {
                case OperationCanceledException:
                    Log.Debug("任务被取消");
                    break;
                case TimeoutException timeoutEx:
                    Log.Warning($"任务超时: {timeoutEx.Message}");
                    break;
                default:
                    Log.Error($"任务异常: {ex?.Message}");
                    break;
            }
        }
    }
}