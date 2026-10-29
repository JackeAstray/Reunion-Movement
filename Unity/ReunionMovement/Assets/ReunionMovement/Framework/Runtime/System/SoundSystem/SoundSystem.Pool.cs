using ReunionMovement.Common;
using ReunionMovement.Framework.Runtime.Base;
using ReunionMovement.Framework.Runtime.Resources;
using ReunionMovement.Framework.Runtime.Scene;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace ReunionMovement.Framework.Runtime.Sound
{
    public partial class SoundSystem
    {
        /// <summary>
        /// 创建音频根节点
        /// </summary>
        public void CreateAudioRoot()
        {
            // 防止重复创建
            if (musicRoot != null) return;

            musicRoot = new GameObject("MusicRoot");
            musicRoot.transform.position = Vector3.zero;

            sfxRoot = new GameObject("SfxRoot");
            sfxRoot.transform.position = Vector3.zero;

            source = musicRoot.AddComponent<AudioSource>();

            // 仅在真正的 Play Mode 中挂到 DontDestroyOnLoad。
            // 编辑器播放状态切换、预览或脚本触发的编辑器上下文不允许调用它。
            if (Application.isPlaying)
            {
                GameObject.DontDestroyOnLoad(musicRoot);
                GameObject.DontDestroyOnLoad(sfxRoot);
            }
        }

        /// <summary>
        /// 创建对象池
        /// </summary>
        public void CreatePools()
        {
            StartupPool pool = new StartupPool();
            pool.size = 20;
            pool.parent = sfxRoot.transform;
            pool.prefab = Modules.Get<ResourcesSystem>().Load<GameObject>(poolPath);

            if (pool.prefab == null)
            {
                Log.Error("SoundSystem 对象池 prefab 加载失败，路径: {0}", poolPath);
                return;
            }

            startupPools.Add(pool);
            loadedPoolPaths.Add(poolPath);

            // 仅对新添加的 pool 创建对象池（不再冗余重建所有已有池）
            CreatePool(pool.prefab, pool.size, pool.parent);

            // 缓存第一个有效 prefab，避免 PlaySfx 每次遍历 startupPools
            if (cachedSfxPrefab == null)
                cachedSfxPrefab = pool.prefab;
        }

        // 缓存的音效 prefab（避免 PlaySfx 每次遍历 startupPools）
        private GameObject cachedSfxPrefab;
        private readonly List<string> loadedPoolPaths = new List<string>();

        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="size"></param>
        /// <param name="parent"></param>
        public void CreatePool(GameObject prefab, int size, Transform parent)
        {
            if (prefab != null && !pooledObjects.ContainsKey(prefab))
            {
                var newPool = new UnityEngine.Pool.ObjectPool<GameObject>(
                    createFunc: () => GameObject.Instantiate(prefab, parent),
                    actionOnGet: (obj) => obj.SetActive(true),
                    actionOnRelease: (obj) =>
                    {
                        obj.transform.SetParent(sfxRoot.transform);
                        obj.SetActive(false);
                    },
                    actionOnDestroy: (obj) => GameObject.Destroy(obj),
                    collectionCheck: true, // 安全检查，防止对象被重复释放
                    defaultCapacity: size,
                    maxSize: size * 2 // 可根据需求调整
                );

                // 预热对象池
                for (int i = 0; i < size; i++)
                {
                    var obj = newPool.Get();
                    newPool.Release(obj);
                }

                pooledObjects.Add(prefab, newPool);
            }
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Transform parent, Vector3 position)
        {
            return Spawn(prefab, parent, position, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefab, sfxRoot.transform, position, rotation);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Transform parent)
        {
            return Spawn(prefab, parent, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Vector3 position)
        {
            return Spawn(prefab, sfxRoot.transform, position, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab)
        {
            return Spawn(prefab, sfxRoot.transform, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            // 低频清扫：emitter 销毁连带子对象销毁后，sfxObjects/sfxSources 残留 fake-null 键，
            // 长期运行字典缓慢膨胀且计数失真（逐帧遍历字典成本高，故按固定间隔执行）
            if (Time.unscaledTime - lastSfxCleanupTime > SfxCleanupInterval)
            {
                lastSfxCleanupTime = Time.unscaledTime;
                CleanupDestroyedSfxObjects();
            }

            if (!pooledObjects.TryGetValue(prefab, out var pool))
            {
                // 如果没有为该预制件创建池，则动态创建一个
                CreatePool(prefab, 10, parent);
                pool = pooledObjects[prefab];
            }

            GameObject obj = pool.Get();
            obj.transform.SetParent(parent);
            obj.transform.localPosition = position;
            obj.transform.localRotation = rotation;

            sfxObjects[obj] = prefab;
            // 平行缓存 AudioSource：IsClipInUse 驱逐扫描无需每对象 GetComponent
            var src = obj.GetComponent<AudioSource>();
            if (src != null) sfxSources[obj] = src;
            return obj;
        }

        /// <summary>
        /// 回收利用
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        public void Recycle<T>(T obj) where T : Component
        {
            Recycle(obj.gameObject);
        }

        /// <summary>
        /// 回收利用
        /// </summary>
        /// <param name="obj"></param>
        public void Recycle(GameObject obj)
        {
            // fake-null 防护：对象已被销毁（如 emitter 销毁连带子对象销毁）时，不能把假 null 对象
            // 交给 ObjectPool.Release（collectionCheck 会抛异常），直接清扫字典残留
            if (obj == null)
            {
                CleanupDestroyedSfxObjects();
                return;
            }

            if (sfxObjects.TryGetValue(obj, out GameObject prefab))
            {
                if (pooledObjects.TryGetValue(prefab, out var pool))
                {
                    pool.Release(obj);
                    sfxObjects.Remove(obj);
                    sfxSources.Remove(obj);
                }
                else
                {
                    // 如果没有对应的池，直接销毁
                    Log.Warning("没有找到预制件 {0} 对应的对象池，直接销毁对象。", prefab.name);
                    UnityEngine.Object.Destroy(obj);
                    sfxObjects.Remove(obj);
                    sfxSources.Remove(obj);
                }
            }
            else
            {
                // 如果对象不在生成池中，则直接销毁
                Log.Warning("对象 {0} 不在生成池中，直接销毁。", obj.name);
                UnityEngine.Object.Destroy(obj);
            }
        }

        /// <summary>
        /// 清扫 sfxObjects/sfxSources 中已销毁对象（fake-null 键）的残留条目。
        /// 低频调用（Spawn 每 10s 触发一次 / Recycle 遇假 null 对象 / Clear）。
        /// </summary>
        private void CleanupDestroyedSfxObjects()
        {
            if (sfxObjects.Count == 0) return;
            List<GameObject> deadKeys = null;
            foreach (var kvp in sfxObjects)
            {
                if (kvp.Key == null)
                {
                    (deadKeys ?? (deadKeys = new List<GameObject>())).Add(kvp.Key);
                }
            }
            if (deadKeys != null)
            {
                foreach (var key in deadKeys)
                {
                    sfxObjects.Remove(key);
                    sfxSources.Remove(key);
                }
            }
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        public void RecycleAll<T>(T prefab) where T : Component
        {
            RecycleAll(prefab.gameObject);
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        /// <param name="prefab"></param>
        public void RecycleAll(GameObject prefab)
        {
            // 先收集匹配的对象到临时列表，再统一回收（避免迭代时修改集合）
            var toRecycle = GetSpawnedList();
            foreach (var kvp in sfxObjects)
            {
                if (kvp.Value == prefab)
                    toRecycle.Add(kvp.Key);
            }
            foreach (var obj in toRecycle)
            {
                Recycle(obj);
            }
            toRecycle.Clear(); // 复用列表，不清空外层引用
        }

        /// <summary>
        /// 回收所有实例
        /// </summary>
        public void RecycleAll()
        {
            if (sfxObjects.Count == 0) return;
            // 使用可复用列表收集 key，避免每次分配 new List
            var allSpawnedKeys = GetSpawnedList();
            foreach (var kvp in sfxObjects)
            {
                allSpawnedKeys.Add(kvp.Key);
            }
            foreach (var obj in allSpawnedKeys)
            {
                if (sfxObjects.ContainsKey(obj))
                {
                    Recycle(obj);
                }
            }
            allSpawnedKeys.Clear();
        }

        /// <summary>
        /// 对象池计数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountPooled<T>(T prefab) where T : Component
        {
            return CountPooled(prefab.gameObject);
        }

        /// <summary>
        /// 对象池计数
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountPooled(GameObject prefab)
        {
            if (pooledObjects.TryGetValue(prefab, out var pool))
            {
                return pool.CountInactive;
            }
            return 0;
        }


        /// <summary>
        /// 对象池计数
        /// </summary>
        /// <returns></returns>
        public int CountAllPooled()
        {
            int count = 0;
            foreach (var pool in pooledObjects.Values)
            {
                count += pool.CountInactive;
            }
            return count;
        }

        /// <summary>
        /// 对象是否在生成对象池中
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool IsSpawned(GameObject obj)
        {
            return sfxObjects.ContainsKey(obj);
        }

        /// <summary>
        /// 获取生成的数量
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountSpawned<T>(T prefab) where T : Component
        {
            return CountSpawned(prefab.gameObject);
        }

        /// <summary>
        /// 获取生成的数量
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public int CountSpawned(GameObject prefab)
        {
            int count = 0;

            foreach (var instancePrefab in sfxObjects.Values)
            {
                if (prefab == instancePrefab)
                {
                    ++count;
                }
            }
            return count;
        }

        /// <summary>
        /// 从生成对象池中获得同类型对象
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="list"></param>
        /// <param name="appendList"></param>
        /// <returns></returns>
        public List<GameObject> GetSpawned(GameObject prefab, List<GameObject> list, bool appendList)
        {
            if (list == null)
            {
                list = new List<GameObject>();
            }
            if (!appendList)
            {
                list.Clear();
            }

            foreach (var item in sfxObjects)
            {
                if (item.Value == prefab)
                {
                    list.Add(item.Key);
                }
            }
            return list;
        }

        /// <summary>
        /// 从生成对象池中获得同类型对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prefab"></param>
        /// <param name="list"></param>
        /// <param name="appendList"></param>
        /// <returns></returns>
        public List<T> GetSpawned<T>(T prefab, List<T> list, bool appendList) where T : Component
        {
            if (list == null)
            {
                list = new List<T>();
            }
            if (!appendList)
            {
                list.Clear();
            }
            var prefabObj = prefab.gameObject;

            foreach (var item in sfxObjects)
            {
                if (item.Value == prefabObj)
                {
                    list.Add(item.Key.GetComponent<T>());
                }
            }

            return list;
        }

        /// <summary>
        /// 销毁集合
        /// </summary>
        /// <param name="prefab"></param>
        public void DestroyPooled(GameObject prefab)
        {
            if (pooledObjects.TryGetValue(prefab, out var pool))
            {
                pool.Clear(); // 这将销毁池中所有非活动对象
            }
        }

        /// <summary>
        /// 销毁所有实例
        /// </summary>
        /// <param name="prefab"></param>
        public void DestroyAll(GameObject prefab)
        {
            RecycleAll(prefab);
            DestroyPooled(prefab);
        }

    }
}
