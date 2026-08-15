using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ReunionMovement.Common.Util.Pool
{
    /// <summary>
    /// 通用 GameObject 对象池（单预制体）。挂载到场景后配置 prefab/预热/上限即可使用。
    /// 底层为 UnityEngine.Pool.ObjectPool（Unity 官方池，按需扩容、空闲收缩）。
    ///
    /// 用法：
    ///   GameObjectPool pool = GetComponent&lt;GameObjectPool&gt;();
    ///   GameObject obj = pool.Get(parentTransform);
    ///   pool.Release(obj);
    ///
    /// 注意：Release 后不要继续持有/修改对象；预制体组件实现 IPoolable 可做取出/归还钩子。
    /// </summary>
    public class GameObjectPool : MonoBehaviour
    {
        [Header("对象池设置")]
        [Tooltip("要池化的预制体")]
        public GameObject prefab;

        [Tooltip("预热数量（池初始化时预先创建）")]
        public int prewarmCount = 10;

        [Tooltip("最大容量（超出后归还的对象直接销毁；0 = 按 10000 上限）")]
        public int maxSize = 100;

        [Tooltip("收集安全检查（编辑器下检测双归还/池泄漏，正式构建建议关闭）")]
        public bool collectionCheck = true;

        /// <summary>底层 Unity 对象池（Awake 时自动初始化；也可调用 EnsureInitialized 提前初始化）</summary>
        private ObjectPool<GameObject> pool;

        /// <summary>池内对象的挂载根节点（保持场景层级整洁）</summary>
        private Transform poolRoot;

        /// <summary>per-实例 IPoolable 组件表缓存（CreateInstance 时登记）：Get/Release 高频路径不再每次分配数组</summary>
        private readonly Dictionary<GameObject, (int instanceId, IPoolable[] components)> poolablesCache
            = new Dictionary<GameObject, (int instanceId, IPoolable[] components)>();

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>确保池已初始化（幂等；共享池注册表在 AddComponent 后调用以尽早预热）</summary>
        public void EnsureInitialized()
        {
            if (pool != null) return;

            if (poolRoot == null)
            {
                poolRoot = new GameObject($"[Pool]{(prefab != null ? prefab.name : name)}").transform;
                poolRoot.SetParent(transform, false);
            }

            int capacity = maxSize > 0 ? maxSize : 10000;
            pool = new ObjectPool<GameObject>(
                CreateInstance,
                OnTakeFromPool,
                OnReturnToPool,
                OnDestroyInstance,
                collectionCheck,
                prewarmCount,
                capacity);
        }

        /// <summary>从池中取出对象（池空时自动创建）</summary>
        public GameObject Get()
        {
            EnsureInitialized();
            return pool.Get();
        }

        /// <summary>从池中取出对象并挂到指定父节点（本地坐标归零）</summary>
        public GameObject Get(Transform parent)
        {
            var go = Get();
            if (go != null && parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
            }
            return go;
        }

        /// <summary>从池中取出对象并设置世界位置/旋转</summary>
        public GameObject Get(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var go = Get();
            if (go == null) return null;
            go.transform.SetParent(parent, parent != null);
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>归还对象到池（对象将被禁用并挂回池根；池满时销毁）</summary>
        public void Release(GameObject go)
        {
            if (go == null) return;
            EnsureInitialized();
            pool.Release(go);
        }

        /// <summary>预热指定数量（池中不足时补充）</summary>
        public void Prewarm(int count)
        {
            if (count <= 0) return;
            EnsureInitialized();
            var temp = ListPool<GameObject>.Get();
            for (int i = 0; i < count; i++) temp.Add(pool.Get());
            foreach (var go in temp) pool.Release(go);
            ListPool<GameObject>.Release(temp);
        }

        /// <summary>当前池内可复用对象数量</summary>
        public int CountInactive => pool?.CountInactive ?? 0;

        /// <summary>池内对象总数（含已取出未归还）</summary>
        public int CountAll => pool?.CountAll ?? 0;

        private GameObject CreateInstance()
        {
            if (prefab == null)
            {
                Debug.LogError($"GameObjectPool[{name}]: prefab 未赋值，无法创建池化对象");
                // 返回 null（不再创建 EmptyPoolItem 污染池）：Get 返回 null，调用方判空处理
                return null;
            }
            var go = Instantiate(prefab, poolRoot, false);
            go.name = prefab.name;
            // 创建时一次性缓存 IPoolable 组件表（含子对象）：避免每次取出/归还分配数组。
            // 同时记录 instanceID：外部销毁在途实例后，Unity 对象哈希基于 instanceID，
            // ID 复用给新对象时若命中旧条目会对错误对象执行 OnSpawned/OnDespawned，必须比对
            poolablesCache[go] = (go.GetInstanceID(), go.GetComponentsInChildren<IPoolable>(true));
            return go;
        }

        private void OnTakeFromPool(GameObject go)
        {
            // prefab 未赋值时 CreateInstance 返回 null，ObjectPool 仍会回调本方法
            if (go == null) return;
            go.SetActive(true);
            // IPoolable 钩子：通知自身及子对象的池化组件（替代 SendMessage 反射）
            if (poolablesCache.TryGetValue(go, out var entry) && entry.instanceId == go.GetInstanceID())
            {
                foreach (var poolable in entry.components)
                {
                    poolable.OnSpawned();
                }
            }
        }

        private void OnReturnToPool(GameObject go)
        {
            if (go == null) return;
            if (poolablesCache.TryGetValue(go, out var entry) && entry.instanceId == go.GetInstanceID())
            {
                foreach (var poolable in entry.components)
                {
                    poolable.OnDespawned();
                }
            }
            go.SetActive(false);
            go.transform.SetParent(poolRoot, false);
        }

        private void OnDestroyInstance(GameObject go)
        {
            if (go != null)
            {
                poolablesCache.Remove(go);
                Destroy(go);
            }
        }

        private void OnDestroy()
        {
            // Dispose 会对池内所有对象调用 OnDestroyInstance 真正销毁，
            // 避免池对象与池根残留为孤儿场景对象
            pool?.Dispose();
            pool = null;
            poolablesCache.Clear();
        }
    }
}
