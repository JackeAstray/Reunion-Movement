using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util.Pool
{
    /// <summary>
    /// 共享对象池注册表 —— 按预制体获取跨场景共享的 GameObjectPool，
    /// 避免每个使用方各自建池造成重复实例与内存浪费。
    ///
    /// 用法：
    ///   GameObjectPool pool = GameObjectPoolRegistry.GetPool(prefab);
    ///   GameObject obj = pool.Get(parent);
    ///   GameObjectPoolRegistry.Release(prefab, obj);
    /// </summary>
    public static class GameObjectPoolRegistry
    {
        // 以资产 GUID 字符串为键（此前以 GameObject 实例为键）：实例销毁后 instanceID 可能被
        // 新对象复用，命中错误池或残留 fake-null 条目；GUID 对同一资产恒定且唯一。
        private static readonly Dictionary<string, GameObjectPool> pools = new Dictionary<string, GameObjectPool>();
        private static Transform root;

        /// <summary>获取预制体的稳定键：编辑器下用资产 GUID；运行时退化为 name+instanceID</summary>
        private static string GetPrefabKey(GameObject prefab)
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long _))
            {
                return guid;
            }
#endif
            return prefab.name + "#" + prefab.GetInstanceID();
        }

        /// <summary>获取（或创建）指定预制体的共享池</summary>
        public static GameObjectPool GetPool(GameObject prefab, int prewarm = 10, int maxSize = 100)
        {
            if (prefab == null)
            {
                Debug.LogError("GameObjectPoolRegistry.GetPool: prefab 不能为 null");
                return null;
            }
            string key = GetPrefabKey(prefab);
            if (pools.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            EnsureRoot();
            var go = new GameObject($"[SharedPool]{prefab.name}");
            go.transform.SetParent(root, false);
            var pool = go.AddComponent<GameObjectPool>();
            pool.prefab = prefab;
            pool.prewarmCount = prewarm;
            pool.maxSize = maxSize;
            pool.EnsureInitialized(); // AddComponent 已触发 Awake，此处幂等；确保参数在预热前生效
            pools[key] = pool;
            return pool;
        }

        /// <summary>从共享池取出对象</summary>
        public static GameObject Get(GameObject prefab, Transform parent = null)
        {
            var pool = GetPool(prefab);
            return pool != null ? pool.Get(parent) : null;
        }

        /// <summary>归还对象到共享池</summary>
        public static void Release(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null) return;
            if (pools.TryGetValue(GetPrefabKey(prefab), out var pool) && pool != null)
            {
                pool.Release(instance);
                return;
            }
            // 池不存在（异常路径）：直接销毁，避免对象泄漏
            Object.Destroy(instance);
        }

        /// <summary>清除所有共享池（销毁池根与全部池内对象；场景切换/测试清理用）</summary>
        public static void ClearAll()
        {
            foreach (var pool in pools.Values)
            {
                if (pool != null) Object.Destroy(pool.gameObject);
            }
            pools.Clear();
        }

        private static void EnsureRoot()
        {
            if (root != null) return;
            var go = new GameObject("[SharedObjectPoolRoot]");
            Object.DontDestroyOnLoad(go);
            root = go.transform;
        }

        /// <summary>复位跨 Play 会话静态状态（关闭 Domain Reload 时注册表会残留上一会话的已销毁池引用）</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            pools.Clear();
            root = null;
        }
    }
}
