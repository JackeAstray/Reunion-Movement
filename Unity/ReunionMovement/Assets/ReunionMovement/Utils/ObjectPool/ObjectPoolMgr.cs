using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 对象池管理器
    /// </summary>
    public class ObjectPoolMgr : SingletonMgr<ObjectPoolMgr>
    {
        public Dictionary<string, ObjectSpawnPool> SpawnPools { get; private set; } = new Dictionary<string, ObjectSpawnPool>();

        [SerializeField] internal int Limit = 100;

        /// <summary>
        /// 注册对象池
        /// </summary>
        public void RegisterSpawnPool(string name, GameObject spawnTem, Action<GameObject> onSpawn = null, Action<GameObject> onDespawn = null, int limit = 0)
        {
            if (string.IsNullOrEmpty(name) || spawnTem == null)
            {
                return;
            }

            if (!SpawnPools.ContainsKey(name))
            {
                int poolLimit = limit > 0 ? limit : Limit;
                SpawnPools.Add(name, new ObjectSpawnPool(spawnTem, poolLimit, onSpawn, onDespawn));
            }
            else
            {
                Log.Error($"注册对象池失败：已存在对象池 {name} ！");
            }
        }

        public bool IsExistSpawnPool(string name) => SpawnPools.ContainsKey(name);

        public void UnRegisterSpawnPool(string name)
        {
            if (SpawnPools.TryGetValue(name, out var pool))
            {
                pool.Clear();
                Destroy(pool);
                SpawnPools.Remove(name);
            }
            else
            {
                Log.Error($"移除对象池失败：不存在对象池 {name} ！");
            }
        }

        public int GetPoolCount(string name)
        {
            if (SpawnPools.TryGetValue(name, out var pool))
            {
                return pool.Count;
            }
            Log.Warning($"获取对象数量失败：不存在对象池 {name} ！");
            return 0;
        }

        public GameObject Spawn(string name)
        {
            if (SpawnPools.TryGetValue(name, out var pool))
            {
                return pool.Spawn();
            }
            Log.Error($"生成对象失败：不存在对象池 {name} ！");
            return null;
        }

        public void Despawn(string name, GameObject target)
        {
            if (SpawnPools.TryGetValue(name, out var pool))
            {
                pool.Despawn(target);
            }
            else
            {
                Log.Error($"回收对象失败：不存在对象池 {name} ！");
            }
        }

        public void Despawns(string name, GameObject[] targets)
        {
            if (targets == null)
            {
                return;
            }
            if (SpawnPools.TryGetValue(name, out var pool))
            {
                foreach (var t in targets)
                {
                    pool.Despawn(t);
                }
            }
            else
            {
                Log.Error($"回收对象失败：不存在对象池 {name} ！");
            }
        }

        public void Despawns(string name, List<GameObject> targets)
        {
            if (targets == null)
            {
                return;
            }
            if (SpawnPools.TryGetValue(name, out var pool))
            {
                foreach (var t in targets)
                {
                    pool.Despawn(t);
                }
                targets.Clear();
            }
            else
            {
                Log.Error($"回收对象失败：不存在对象池 {name} ！");
            }
        }

        public void Clear(string name)
        {
            if (SpawnPools.TryGetValue(name, out var pool))
            {
                pool.Clear();
            }
            else
            {
                Log.Error($"清空对象池失败：不存在对象池 {name} ！");
            }
        }

        public void ClearAll()
        {
            foreach (var pool in SpawnPools.Values)
            {
                pool.Clear();
            }
        }

        #region 静态方法

        public static T Clone<T>(T original) where T : Object
        {
            if (original == null)
            {
                return null;
            }
            return Instantiate(original);
        }

        public static T Clone<T>(T original, Vector3 position, Quaternion rotation) where T : Object
        {
            if (original == null)
            {
                return null;
            }
            return Instantiate(original, position, rotation);
        }

        public static T Clone<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object
        {
            if (original == null)
            {
                return null;
            }
            return Instantiate(original, position, rotation, parent);
        }

        public static T Clone<T>(T original, Transform parent) where T : Object
        {
            if (original == null)
            {
                return null;
            }
            return Instantiate(original, parent);
        }

        public static T Clone<T>(T original, Transform parent, bool worldPositionStays) where T : Object
        {
            if (original == null)
            {
                return null;
            }
            return Instantiate(original, parent, worldPositionStays);
        }

        public static GameObject CloneGameObject(GameObject original, bool isUI = false)
        {
            if (original == null)
            {
                return null;
            }
            GameObject obj = Instantiate(original);
            // 优化：由池管理激活
            obj.SetActive(false);
            obj.transform.SetParent(original.transform.parent);
            if (isUI)
            {
                RectTransform rect = obj.GetComponent<RectTransform>();
                RectTransform originalRect = original.GetComponent<RectTransform>();
                if (rect != null && originalRect != null)
                {
                    rect.anchoredPosition3D = originalRect.anchoredPosition3D;
                    rect.sizeDelta = originalRect.sizeDelta;
                    rect.offsetMin = originalRect.offsetMin;
                    rect.offsetMax = originalRect.offsetMax;
                    rect.anchorMin = originalRect.anchorMin;
                    rect.anchorMax = originalRect.anchorMax;
                    rect.pivot = originalRect.pivot;
                }
            }
            else
            {
                obj.transform.localPosition = original.transform.localPosition;
            }
            obj.transform.localRotation = original.transform.localRotation;
            obj.transform.localScale = original.transform.localScale;
            return obj;
        }

        public static void Kill(Object obj)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        public static void KillImmediate(Object obj)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }

        public static void Kills<T>(List<T> objs) where T : Object
        {
            if (objs == null) return;
            foreach (var obj in objs)
            {
                if (obj != null)
                {
                    GameObject.Destroy(obj);
                }
            }
            objs.Clear();
        }

        public static void Kills<T>(T[] objs) where T : Object
        {
            if (objs == null) return;
            foreach (var obj in objs)
            {
                if (obj != null)
                {
                    GameObject.Destroy(obj);
                }
            }
        }
        #endregion
    }
}