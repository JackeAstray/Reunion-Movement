using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 单例管理器（线程安全）。
    ///
    /// 与 Lazy&lt;T&gt; 模式不同，此 MonoBehaviour 单例适用于需要挂载到场景 GameObject 的组件。
    /// 纯 C# 系统模块推荐使用 Lazy&lt;T&gt; 单例（更轻量，无需 GameObject）。
    /// </summary>
    public class SingletonMgr<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        private static readonly object instanceLock = new object();

        /// <summary>单例实例（线程安全访问）</summary>
        public static T Instance
        {
            get
            {
                // 快速路径：无锁读取（Unity 主线程调用时无需锁）
                if (instance != null) return instance;

                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = CreateInstance();
                    }
                }
                return instance;
            }
            protected set
            {
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
                        OnInstanceCreated?.Invoke(instance);
                    }
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

        protected virtual void Awake()
        {
            // 使用 setter 设置实例（自动处理重复检测与事件触发）
            Instance = this as T;
        }

        /// <summary>手动销毁单例</summary>
        public static void DestroyInstance()
        {
            lock (instanceLock)
            {
                if (instance != null)
                {
                    OnInstanceDestroyed?.Invoke();
                    if (instance.gameObject != null)
                        Destroy(instance.gameObject);
                    instance = null;
                }
            }
        }

        /// <summary>
        /// 创建单例实例（场景中无现有实例时作为兜底）。
        /// 调用方已持有 instanceLock。
        /// </summary>
        private static T CreateInstance()
        {
            T foundInstance = FindFirstObjectByType<T>();
            if (foundInstance == null)
            {
                var go = new GameObject($"{typeof(T).Name} (Singleton)");
                foundInstance = go.AddComponent<T>();
            }

            OnInstanceCreated?.Invoke(foundInstance);
            return foundInstance;
        }
    }
}
