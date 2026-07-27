using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util.Manager
{
    /// <summary>
    /// Unity主线程调度器 —— 线程安全地将 Action 从任意线程投递到 Unity 主线程执行。
    /// 消费端先快照队列后释放锁再执行，避免在锁内调用外部代码导致死锁。
    /// </summary>
    public class UnityMainThreadDispatcher : SingletonMgr<UnityMainThreadDispatcher>
    {
        private static readonly Queue<Action> executionQueue = new Queue<Action>();

        private void Update()
        {
            // 先快照队列（持有锁），再释放锁执行，防止外部 Action 回调时递归 Enqueue 导致死锁
            List<Action> snapshot = null;
            lock (executionQueue)
            {
                if (executionQueue.Count > 0)
                {
                    snapshot = new List<Action>(executionQueue.Count);
                    while (executionQueue.Count > 0)
                    {
                        snapshot.Add(executionQueue.Dequeue());
                    }
                }
            }

            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    try
                    {
                        snapshot[i]?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UnityMainThreadDispatcher] 执行 Action 时发生异常: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// 将一个Action添加到队列中
        /// </summary>
        /// <param name="action"></param>
        public void Enqueue(Action action)
        {
            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// 确保UnityMainThreadDispatcher实例存在
        /// </summary>
        public static void EnsureCreated()
        {
            if (Instance == null)
            {
                GameObject obj = new GameObject("MainThreadDispatcher");
                var dispatcher = obj.AddComponent<UnityMainThreadDispatcher>();
                // 使用 Awake 中的赋值路径，避免绕过 SingletonMgr 的 setter 逻辑
                if (Instance == null)
                {
                    Instance = dispatcher;
                }
                else
                {
                    // 并发情况：已有其他线程创建了实例，销毁多余对象
                    Destroy(obj);
                }
            }
        }

        /// <summary>
        /// 将一个Action添加到队列中
        /// </summary>
        /// <param name="action"></param>
        public static void EnqueueAction(Action action)
        {
            if (Instance != null)
            {
                Instance.Enqueue(action);
            }
        }

        /// <summary>
        /// 在主线程中运行一个Action
        /// </summary>
        /// <param name="action"></param>
        public static void RunOnMainThread(Action action)
        {
            EnsureCreated();
            EnqueueAction(action);
        }
    }
}
