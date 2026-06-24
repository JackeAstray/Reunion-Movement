using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ReunionMovement.Common.Util.Manager
{
    /// <summary>
    /// Unity主线程调度器
    /// </summary>
    public class UnityMainThreadDispatcher : SingletonMgr<UnityMainThreadDispatcher>
    {
        private static readonly Queue<Action> executionQueue = new Queue<Action>();

        private void Update()
        {
            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    executionQueue.Dequeue().Invoke();
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
