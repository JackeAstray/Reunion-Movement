using ReunionMovement.Common.Util;
using UnityEngine;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 对象池物体示例脚本
    /// </summary>
    public class ObjectPoolObjExample : MonoBehaviour
    {
        public float speed = 3f;

        private void OnEnable()
        {
            Invoke("OnDespawn", speed);
        }

        public void OnDespawn()
        {
            ObjectPoolMgr.Instance.Despawn("EnemyPool", gameObject);
        }
    }
}