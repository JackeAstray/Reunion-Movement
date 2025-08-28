using ReunionMovement.Common.Util;
using System.Collections.Generic;
using UnityEngine;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 对象池使用示例
    /// </summary>
    public class ObjectPoolExample : MonoBehaviour
    {
        public Transform root;
        public GameObject prefab;
        public int count = 0;
        public int limit = 50;
        public float time = 0;
        public float timeMax = 4f;

        private void Start()
        {
            ObjectPoolMgr.Instance.RegisterSpawnPool("EnemyPool", prefab, OnSpawn, OnDespawn, limit);
            count = 0;
        }

        private void Update()
        {
            time += Time.deltaTime;

            if (time >= timeMax)
            {
                time = 0;

                GameObject go = ObjectPoolMgr.Instance.Spawn("EnemyPool");
                if (go != null)
                {
                    go.transform.position = new Vector3(UnityEngine.Random.Range(-5, 5), UnityEngine.Random.Range(-5, 5), 0);
                    go.transform.SetParent(root);
                    go.name = "Enemy" + count;
                    count++;
                }
            }
        }

        public void OnSpawn(GameObject go)
        {
            go.SetActive(true);
        }

        public void OnDespawn(GameObject go)
        {
            go.SetActive(false);
        }
    }
}