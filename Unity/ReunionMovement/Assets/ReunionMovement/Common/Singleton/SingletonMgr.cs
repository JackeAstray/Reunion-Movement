using UnityEngine;

namespace ReunionMovement.Common
{
    /// <summary>
    /// 单例管理器
    /// </summary>
    public class SingletonMgr<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = CreateInstance();
                }
                return instance;
            }
            protected set
            {
                if (instance == null)
                {
                    instance = value;
                    isInitialized = true;
                    OnInstanceCreated?.Invoke(instance);
                }
                else if (instance != value)
                {
                    Destroy(value.gameObject);
                }
            }
        }

        /// <summary>
        /// 是否初始化
        /// </summary>
        private static bool isInitialized = false;
        public static bool IsInitialized
        {
            get { return isInitialized; }
        }

        /// <summary>
        /// 单例实例创建事件
        /// </summary>
        public static event System.Action<T> OnInstanceCreated;

        /// <summary>
        /// 单例实例销毁事件
        /// </summary>
        public static event System.Action OnInstanceDestroyed;

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[SingletonMgr] 检测到重复的 {typeof(T).Name} 单例，已销毁: {gameObject.name}");
                Destroy(this.gameObject);
                return;
            }

            // 直接设置静态字段，跳過 setter 中的重复检查
            if (!isInitialized)
            {
                instance = this as T;
                isInitialized = true;
                OnInstanceCreated?.Invoke(instance);
            }
        }

        /// <summary>
        /// 手动销毁单例
        /// </summary>
        public static void DestroyInstance()
        {
            if (instance != null)
            {
                OnInstanceDestroyed?.Invoke();
                Destroy(instance.gameObject);
                instance = null;
                isInitialized = false;
            }
        }

        /// <summary>
        /// 创建单例实例（仅在场景中无现有实例时作为兜底）
        /// </summary>
        private static T CreateInstance()
        {
            // 使用 FindFirstObjectByType（比 FindAnyObjectByType 更适合单例语义）
            T foundInstance = FindFirstObjectByType<T>();
            if (foundInstance == null)
            {
                GameObject singletonObject = new GameObject();
                foundInstance = singletonObject.AddComponent<T>();
                singletonObject.name = typeof(T).ToString() + " (Singleton)";
            }

            // 仅当尚未初始化时才设置标志并触发事件（避免与 Awake→setter 路径重复触发）
            if (!isInitialized)
            {
                isInitialized = true;
                OnInstanceCreated?.Invoke(foundInstance);
            }

            return foundInstance;
        }
    }
}