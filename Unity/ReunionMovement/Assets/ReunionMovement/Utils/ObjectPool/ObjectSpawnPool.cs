using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 对象池
    /// </summary>
    public class ObjectSpawnPool : MonoBehaviour
    {
        private GameObject spawnTem;
        private int limit = 100;
        private int currentCount = 0;
        private Queue<GameObject> objectQueue = new Queue<GameObject>();
        private Action<GameObject> onSpawn;
        private Action<GameObject> onDespawn;

        /// <summary>
        /// 当前池中可用对象数量
        /// </summary>
        public int Count => objectQueue.Count;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="spawnTem"></param>
        /// <param name="limit"></param>
        /// <param name="onSpawn"></param>
        /// <param name="onDespawn"></param>
        public ObjectSpawnPool(GameObject spawnTem, int limit, Action<GameObject> onSpawn, Action<GameObject> onDespawn)
        {
            this.spawnTem = spawnTem;
            this.limit = limit > 0 ? limit : 100;
            this.onSpawn = onSpawn;
            this.onDespawn = onDespawn;
        }

        /// <summary>
        /// 生成对象
        /// </summary>
        public GameObject Spawn()
        {
            GameObject obj = null;
            if (objectQueue.Count > 0)
            {
                obj = objectQueue.Dequeue();
            }
            else if (currentCount < limit)
            {
                obj = ObjectPoolMgr.CloneGameObject(spawnTem);
                currentCount++;
            }
            else
            {
                Log.Warning("已达到对象池限制。无法生成更多对象。");
                return null;
            }

            obj.SetActive(true);
            onSpawn?.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// 回收对象
        /// </summary>
        public void Despawn(GameObject obj)
        {
            if (obj == null) return;

            // 防止重复回收
            if (objectQueue.Contains(obj))
                return;

            onDespawn?.Invoke(obj);
            obj.SetActive(false);

            if (objectQueue.Count < limit)
            {
                objectQueue.Enqueue(obj);
            }
            else
            {
                ObjectPoolMgr.Kill(obj);
                currentCount--;
            }
        }

        /// <summary>
        /// 清空所有对象
        /// </summary>
        public void Clear()
        {
            while (objectQueue.Count > 0)
            {
                var obj = objectQueue.Dequeue();
                if (obj)
                {
                    ObjectPoolMgr.Kill(obj);
                    currentCount--;
                }
            }
        }

        /// <summary>
        /// 预热对象池（可选）
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && currentCount < limit; i++)
            {
                var obj = ObjectPoolMgr.CloneGameObject(spawnTem);
                obj.SetActive(false);
                objectQueue.Enqueue(obj);
                currentCount++;
            }
        }
    }
}