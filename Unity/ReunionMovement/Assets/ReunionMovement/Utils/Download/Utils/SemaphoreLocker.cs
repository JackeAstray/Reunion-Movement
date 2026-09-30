using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ReunionMovement.Common.Util.Download
{
    /// <summary>
    /// 信号锁
    /// </summary>
    public class SemaphoreLocker
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 异步执行带锁的操作
        /// </summary>
        /// <param name="worker">需要加锁执行的异步方法</param>
        public async UniTask LockAsync(Func<UniTask> worker)
        {
            await semaphore.WaitAsync();
            try
            {
                await worker();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 异步执行带锁的操作并返回结果
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="worker">需要加锁执行的异步方法</param>
        /// <returns>异步操作结果</returns>
        public async UniTask<T> LockAsync<T>(Func<UniTask<T>> worker)
        {
            await semaphore.WaitAsync();
            try
            {
                return await worker();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>同步 Lock 的等待上限：超时即抛异常，避免主线程永久挂死</summary>
        private const int SyncLockTimeoutMs = 5000;

        /// <summary>
        /// 同步执行带锁的操作。
        /// 警告：本方法阻塞调用线程。若信号量正被 <see cref="LockAsync"/> 持有，而该异步操作的
        /// 续体又需要回到当前线程（UniTask 默认如此），就会形成死锁 ——
        /// 请优先使用 LockAsync。这里改为带超时等待，把"永久挂死"变成可诊断的异常。
        /// </summary>
        /// <param name="worker">需要加锁执行的方法</param>
        public void Lock(Action worker)
        {
            if (!semaphore.Wait(SyncLockTimeoutMs))
            {
                throw new InvalidOperationException(
                    $"SemaphoreLocker.Lock 等待 {SyncLockTimeoutMs}ms 超时：锁仍被 LockAsync 持有，" +
                    "继续阻塞会死锁。请改用 await LockAsync。");
            }
            try
            {
                worker();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 同步执行带锁的操作并返回结果。
        /// 警告：同 <see cref="Lock(Action)"/>，阻塞等待有死锁风险，优先使用 LockAsync。
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="worker">需要加锁执行的方法</param>
        /// <returns>操作结果</returns>
        public T Lock<T>(Func<T> worker)
        {
            if (!semaphore.Wait(SyncLockTimeoutMs))
            {
                throw new InvalidOperationException(
                    $"SemaphoreLocker.Lock<{typeof(T).Name}> 等待 {SyncLockTimeoutMs}ms 超时：锁仍被 LockAsync 持有，" +
                    "继续阻塞会死锁。请改用 await LockAsync。");
            }
            try
            {
                return worker();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}