using UnityEngine;
using UnityEngine.Pool;

namespace ReunionMovement.Example
{
    /// <summary>
    /// 池化子弹组件 —— 挂载在子弹预制体上，负责自动回收。
    ///
    /// 挂载要求：
    ///   - 预制体需挂 Rigidbody（useGravity = true），由物理引擎驱动抛物线运动
    ///   - 初速度由 ObjectPoolExample.FireBullet 在 Get 之后设置
    ///
    /// 自动回收时机：超时 或 落地碰撞。
    /// </summary>
    public class PooledBullet : MonoBehaviour
    {
        [SerializeField, Tooltip("存活时间（秒），超时自动归还池中")]
        private float lifetime = 3f;

        private float elapsed;
        private bool released;

        private void OnEnable()
        {
            elapsed = 0f;
            released = false;
        }

        private void OnDisable()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep(); // 归还时让 Rigidbody 休眠，避免物理引擎无效计算
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime)
                Release();
        }

        private void OnCollisionEnter(Collision collision)
        {
            Release();
        }

        public void Release()
        {
            if (released) return;
            released = true;

            var manager = ObjectPoolExample.Instance;
            if (manager != null)
                manager.RecycleBullet(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
