using System.Threading;
using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 单例管理器（线程安全）。
    ///
    /// 与 Lazy&lt;T&gt; 模式不同，此 MonoBehaviour 单例适用于需要挂载到场景 GameObject 的组件。
    /// 纯 C# 系统模块推荐使用 Lazy&lt;T&gt; 单例（更轻量，无需 GameObject）。
    /// 泛型约束 T : SingletonMgr&lt;T&gt;（CRTP）用于在基类中访问子类虚成员（如 IsPersistentAcrossScenes）。
    /// </summary>
    public class SingletonMgr<T> : MonoBehaviour where T : SingletonMgr<T>
    {
        // 注意：不能声明为 volatile T（C# 规范禁止 volatile 使用泛型类型参数），
        // 因此快速路径使用 Volatile.Read 保证 ARM 等弱内存模型下的可见性。
        private static T instance;
        private static readonly object instanceLock = new object();

        /// <summary>单例实例（线程安全访问）</summary>
        public static T Instance
        {
            get
            {
                // 快速路径：无锁读取（Unity 主线程调用时无需锁）
                if (Volatile.Read(ref instance) != null) return instance;

                lock (instanceLock)
                {
                    if (instance != null) return instance;
                }

                // 锁外创建：AddComponent 会同步触发子类 Awake（用户代码），
                // 持锁执行用户代码可能形成锁序环（Awake 内再访问其他单例/等待工作线程）。
                // 并发重复创建由 Awake→setter 的重复检测销毁多余实例。
                var created = CreateInstance();

                lock (instanceLock)
                {
                    // 正常路径 Awake/setter 已写入 instance（并触发创建事件）；此处兜底处理极端未写入场景
                    if (instance == null)
                    {
                        instance = created;
                    }
                }
                return instance;
            }
            protected set
            {
                T created = null;
                lock (instanceLock)
                {
                    if (instance != null && instance != value)
                    {
                        // 已有不同实例：销毁新对象，保留原实例
                        if (value != null && value.gameObject != null)
                            Destroy(value.gameObject);
                        return;
                    }
                    if (instance == null && value != null)
                    {
                        instance = value;
                        created = value;
                    }
                }
                // 锁外触发用户事件：避免持锁回调用户代码形成锁序环（A→B→A 交叉死锁）
                if (created != null)
                {
                    OnInstanceCreated?.Invoke(created);
                }
            }
        }

        /// <summary>单例是否已初始化（不会触发懒加载）</summary>
        public static bool IsInitialized
        {
            get
            {
                lock (instanceLock) { return instance != null; }
            }
        }

        /// <summary>单例实例创建事件</summary>
        public static event System.Action<T> OnInstanceCreated;

        /// <summary>单例实例销毁事件</summary>
        public static event System.Action OnInstanceDestroyed;

        /// <summary>
        /// 动态创建的单例是否需要跨场景存活（DontDestroyOnLoad）。
        /// 默认 false（单例默认随场景存活）：场景卸载时实例随之销毁，
        /// 下次访问 Instance 时会在新场景中按需重新创建。
        /// 需要跨场景保持状态/在途任务的管理器（网络、下载、HTTP、计时器、主线程调度等）
        /// 应重写为 true 以调用 DontDestroyOnLoad。
        /// </summary>
        protected virtual bool IsPersistentAcrossScenes => false;

        protected virtual void Awake()
        {
            // 使用 setter 设置实例（自动处理重复检测与事件触发）
            Instance = this as T;
        }

        /// <summary>
        /// 对象销毁时清空 instance，避免场景卸载后
        /// instance 仍指向已销毁对象（fake null）导致 MissingReferenceException。
        /// 场景卸载自然销毁与手动 DestroyInstance 语义一致：锁外补发 OnInstanceDestroyed，
        /// 依赖该事件做清理的订阅者不再被漏执行。
        /// </summary>
        protected virtual void OnDestroy()
        {
            bool wasInstance = false;
            lock (instanceLock)
            {
                // 仅当当前对象就是单例实例时才清空，避免误清新实例
                if (instance == this as T)
                {
                    instance = null;
                    wasInstance = true;
                }
            }
            if (wasInstance)
            {
                OnInstanceDestroyed?.Invoke();
            }
        }

        /// <summary>
        /// 复位跨 Play 会话静态状态（关闭 Domain Reload 时静态字段与事件订阅会跨会话残留）。
        /// 由 Bootstrap 的 RuntimeInitializeOnLoadMethod(SubsystemRegistration) 调用，
        /// 需对每个具体单例类各调用一次（泛型静态字段按 T 独立）。
        /// </summary>
        public static void ResetStatics()
        {
            lock (instanceLock)
            {
                instance = null;
            }
            OnInstanceCreated = null;
            OnInstanceDestroyed = null;
        }

        /// <summary>手动销毁单例</summary>
        public static void DestroyInstance()
        {
            T toDestroy = null;
            lock (instanceLock)
            {
                if (instance != null)
                {
                    toDestroy = instance;
                    instance = null;
                }
            }
            if (toDestroy != null)
            {
                // 锁外触发用户事件与销毁，避免持锁回调用户代码形成锁序环
                OnInstanceDestroyed?.Invoke();
                if (toDestroy.gameObject != null)
                    Destroy(toDestroy.gameObject);
            }
        }

        /// <summary>
        /// 创建单例实例（场景中无现有实例时作为兜底）。
        /// 调用方已持有 instanceLock。
        /// 注意：Unity API（FindFirstObjectByType/new GameObject）必须在主线程执行，
        /// 若需从工作线程安全访问 Instance，请先在主线程触发一次初始化。
        /// </summary>
        private static T CreateInstance()
        {
            T foundInstance = FindFirstObjectByType<T>();
            if (foundInstance == null)
            {
                var go = new GameObject($"{typeof(T).Name} (Singleton)");
                foundInstance = go.AddComponent<T>();
                // 是否跨场景存活由子类 IsPersistentAcrossScenes 决定。
                // 需在 AddComponent 之后读取（实例虚属性）；AddComponent 会同步触发
                // Awake() → setter → OnInstanceCreated，事件已在 setter 中触发一次，这里不再重复触发。
                if (foundInstance.IsPersistentAcrossScenes)
                {
                    DontDestroyOnLoad(go);
                }
            }
            else
            {
                // 场景中已存在实例：确保 instance 已设置（Awake 可能尚未执行）；
                // 若已设置则 setter 的重复检测会安全跳过，不重复触发事件。
                Instance = foundInstance;
            }

            return foundInstance;
        }
    }
}
