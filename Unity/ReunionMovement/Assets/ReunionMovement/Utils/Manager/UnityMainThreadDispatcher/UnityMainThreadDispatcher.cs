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
        /// <summary>跨线程投递的队列/回调需跨场景存活，避免切场景时丢失在途回调</summary>
        protected override bool IsPersistentAcrossScenes => true;

        private static readonly Queue<Action> executionQueue = new Queue<Action>();

        /// <summary>主线程 ID（由主线程初始化钩子记录，用于检测跨线程创建）</summary>
        private static volatile int mainThreadId = -1;

        /// <summary>当前线程是否为主线程</summary>
        private static bool IsMainThread => mainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// 主线程初始化钩子：确保调度器在游戏启动时即由主线程创建，
        /// 避免后台线程首次调用 RunOnMainThread 时跨线程创建 Unity 对象（非法）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateOnMainThread()
        {
            mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            EnsureCreated();
        }

        /// <summary>复用的快照缓冲（执行后 Clear），避免每帧 new List 分配</summary>
        private readonly List<Action> snapshotBuffer = new List<Action>(16);

        private void Update()
        {
            // 先快照队列（持有锁），再释放锁执行，防止外部 Action 回调时递归 Enqueue 导致死锁
            lock (executionQueue)
            {
                if (executionQueue.Count > 0)
                {
                    snapshotBuffer.Clear();
                    while (executionQueue.Count > 0)
                    {
                        snapshotBuffer.Add(executionQueue.Dequeue());
                    }
                }
            }

            for (int i = 0; i < snapshotBuffer.Count; i++)
            {
                try
                {
                    snapshotBuffer[i]?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityMainThreadDispatcher] 执行 Action 时发生异常: {ex}");
                }
            }
            snapshotBuffer.Clear();
        }

        /// <summary>
        /// 将一个Action添加到队列中
        /// </summary>
        /// <param name="action"></param>
        public void Enqueue(Action action)
        {
            if (action == null) return;
            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// 确保UnityMainThreadDispatcher实例存在。必须在主线程调用：
        /// 跨线程创建 Unity GameObject 在运行时是非法的。
        /// </summary>
        public static void EnsureCreated()
        {
            if (IsInitialized) return;

            // 后台线程调用时不创建对象（非法），仅告警；
            // 调度器由主线程初始化钩子 AutoCreateOnMainThread 自动创建，已入队的回调不会丢失。
            if (mainThreadId >= 0 && !IsMainThread)
            {
                Debug.LogWarning("[UnityMainThreadDispatcher] EnsureCreated 在后台线程被调用，已忽略。" +
                    "调度器将在主线程初始化时自动创建，已入队的回调不会丢失。");
                return;
            }

            var go = new GameObject("MainThreadDispatcher");
            // AddComponent 会同步触发 Awake → 基类 setter 注册实例（含重复检测与事件）
            var dispatcher = go.AddComponent<UnityMainThreadDispatcher>();
            // 与基类 CreateInstance 路径一致：持久化单例需 DontDestroyOnLoad
            DontDestroyOnLoad(go);
            // 兜底：正常路径 Awake 已设置实例，这里防御性校验
            if (Instance == null)
            {
                Instance = dispatcher;
            }
        }

        /// <summary>
        /// 将一个Action添加到队列中（静态便捷方法，不丢失回调）
        /// </summary>
        /// <param name="action"></param>
        public static void EnqueueAction(Action action)
        {
            if (action == null) return;
            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// 在任意线程将一个 Action 加入主线程执行队列。
        /// 直接入静态队列，即使调度器尚未创建也绝不丢失回调；
        /// 若当前线程为主线程且调度器未创建，则顺带补建。
        /// </summary>
        /// <param name="action"></param>
        public static void RunOnMainThread(Action action)
        {
            if (action == null) return;
            // 直接入队（队列为 static + 加锁，跨线程安全），
            // 避免原 EnqueueAction 在 Instance == null 时静默丢弃回调的问题。
            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }

            // 仅在主线程补建调度器；后台线程绝不在调用栈中创建 Unity 对象
            if (!IsInitialized && IsMainThread)
            {
                EnsureCreated();
            }
        }
    }
}
