using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ReunionMovement.Common;

namespace ReunionMovement.Example
{
    /// <summary>
    /// UnityEngine.Pool.ObjectPool 使用示例 —— 替代旧版 ObjectPoolMgr。
    ///
    /// 核心 API：
    ///   pool.Get()       — 从池中取出一个对象（池空则自动创建）
    ///   pool.Release()   — 将对象归还池中
    ///   pool.Clear()     — 清空池中所有对象
    ///   pool.Dispose()   — 销毁池及所有对象
    ///
    /// 与旧版 ObjectPoolMgr 对比：
    ///   - 零 GC：内部使用数组 + struct 枚举器，无装箱分配
    ///   - 纯 C#：不依赖 MonoBehaviour，可在任意位置创建
    ///   - 类型安全：泛型 ObjectPool&lt;T&gt;，无需字符串键
    ///   - 内置安全检查：collectionCheck 可检测归还后仍被持有的对象
    /// </summary>
    public class ObjectPoolExample : MonoBehaviour
    {
        /// <summary>供 PooledBullet 查找的静态引用（示例用；多实例时取最近一个）</summary>
        public static ObjectPoolExample Instance { get; private set; }
        public Transform root;

        [Header("对象池配置")]
        [SerializeField, Tooltip("用于创建对象池的预制体")]
        private GameObject bulletPrefab;

        [SerializeField, Tooltip("对象池最大容量")]
        private int maxPoolSize = 50;

        [SerializeField, Tooltip("对象池初始容量（预创建对象数）")]
        private int defaultCapacity = 10;

        [Header("自动发射")]
        [SerializeField, Tooltip("启动后自动发射子弹")]
        private bool autoFire = true;

        [SerializeField, Tooltip("发射间隔（秒）")]
        private float fireInterval = 0.1f;

        [SerializeField, Tooltip("向上初速度")]
        private float launchSpeed = 8f;

        [SerializeField, Tooltip("锥形散射角度（0=垂直向上, 90=全水平）")]
        [Range(0f, 90f)]
        private float spreadAngle = 30f;

        // ============================================================
        //  方式一：单个 ObjectPool<GameObject> —— 最简用法
        // ============================================================
        private ObjectPool<GameObject> bulletPool;

        // ============================================================
        //  方式二：Dictionary<string, IObjectPool<GameObject>> —— 
        //          多池管理（替代旧 ObjectPoolMgr 的字符串键模式）
        // ============================================================
        private readonly Dictionary<string, IObjectPool<GameObject>> managedPools
            = new Dictionary<string, IObjectPool<GameObject>>(8);

        private void Awake()
        {
            Instance = this;

            // ---- 方式一：创建单个对象池 ----
            bulletPool = new ObjectPool<GameObject>(
                createFunc:      CreateBullet,
                actionOnGet:     OnBulletGet,
                actionOnRelease: OnBulletRelease,
                actionOnDestroy: OnBulletDestroy,
                collectionCheck: true,       // 开启安全检查（Release 时检查对象是否已在池中）
                defaultCapacity: defaultCapacity,
                maxSize:         maxPoolSize
            );

            // 可选：预创建对象（消除首次 Get 的 Instantiate 开销）
            // bulletPool.PreWarm(defaultCapacity);

            // ---- 方式二：多池管理 ----
            RegisterManagedPool("Enemy", Resources.Load<GameObject>("Prefabs/Enemy"), 5, 30);
            RegisterManagedPool("Effect", Resources.Load<GameObject>("Prefabs/Effect"), 3, 20);

            Log.Debug("[ObjectPoolExample] 对象池初始化完成");
        }

        private void Start()
        {
            if (autoFire && bulletPrefab != null)
            {
                Log.Debug("[ObjectPoolExample] 自动发射已启动 (间隔={0}s)", fireInterval);
            }
            else if (autoFire && bulletPrefab == null)
            {
                Log.Warning("[ObjectPoolExample] bulletPrefab 为空，无法自动发射");
            }
        }

        private void Update()
        {
            if (!autoFire || bulletPrefab == null) return;

            fireTimer += Time.deltaTime;
            if (fireTimer >= fireInterval)
            {
                fireTimer = 0f;

                // 从发射点向上锥形散射：将 spreadAngle 转为三角函数比例
                // 30° → tan(30°)≈0.58 → 方向为 (0,1,0) + 随机水平偏移*0.58，归一化后得到锥角30°的方向
                float halfAngleRad = spreadAngle * 0.5f * Mathf.Deg2Rad;
                float horizontalScale = Mathf.Tan(halfAngleRad);

                Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 direction = (Vector3.up
                    + new Vector3(randomDir.x * horizontalScale, 0f, randomDir.y * horizontalScale)
                ).normalized;

                FireBullet(transform.position, direction, launchSpeed);
            }
        }

        private float fireTimer;

        #region 方式一：单个对象池的回调

        private GameObject CreateBullet()
        {
            if (bulletPrefab == null)
            {
                Log.Error("[ObjectPoolExample] bulletPrefab 未赋值！");
                return new GameObject("Bullet_Fallback");
            }
            var obj = root != null
                ? Instantiate(bulletPrefab, root)
                : Instantiate(bulletPrefab);
            obj.name = $"Bullet_{obj.GetInstanceID()}";
            return obj;
        }

        private void OnBulletGet(GameObject obj)
        {
            if (root != null) obj.transform.SetParent(root);
            obj.SetActive(true);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void OnBulletRelease(GameObject obj)
        {
            obj.SetActive(false);
            // 可选：清理引用（避免持有已销毁对象的引用）
        }

        private void OnBulletDestroy(GameObject obj)
        {
            Destroy(obj);
        }

        #endregion

        #region 方式二：多池管理（字符串键 → 池映射）

        /// <summary>
        /// 注册一个对象池（字符串键查找，O(1)）
        /// </summary>
        private void RegisterManagedPool(string key, GameObject prefab, int capacity, int maxSize)
        {
            if (prefab == null || managedPools.ContainsKey(key)) return;

            var pool = new ObjectPool<GameObject>(
                createFunc:      () => root != null ? Instantiate(prefab, root) : Instantiate(prefab),
                actionOnGet:     obj =>
                {
                    if (root != null) obj.transform.SetParent(root);
                    obj.SetActive(true);
                    var rb = obj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                },
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: obj => Destroy(obj),
                collectionCheck: true,
                defaultCapacity: capacity,
                maxSize:         maxSize
            );
            managedPools[key] = pool;
            Log.Debug("[ObjectPoolExample] 注册对象池: {0} (capacity={1}, max={2})", key, capacity, maxSize);
        }

        /// <summary>
        /// 从托管池中获取对象
        /// </summary>
        public GameObject GetFromManagedPool(string key)
        {
            if (managedPools.TryGetValue(key, out var pool))
                return pool.Get();

            Log.Warning("[ObjectPoolExample] 对象池不存在: {0}", key);
            return null;
        }

        /// <summary>
        /// 将对象归还到托管池
        /// </summary>
        public void ReleaseToManagedPool(string key, GameObject obj)
        {
            if (obj == null) return;

            if (managedPools.TryGetValue(key, out var pool))
                pool.Release(obj);
            else
                Destroy(obj); // 找不到池则直接销毁
        }

        /// <summary>
        /// 注销托管池并销毁所有对象
        /// </summary>
        public void UnregisterManagedPool(string key)
        {
            if (managedPools.Remove(key, out var pool))
            {
                if (pool is IDisposable disposable)
                    disposable.Dispose(); // Dispose 会调用 actionOnDestroy 销毁所有对象
                else
                    pool.Clear();
            }
        }

        #endregion

        #region 公共 API（配合外部调用）

        /// <summary>
        /// 发射一颗子弹（获取 + 设置位置/方向）
        /// </summary>
        public GameObject FireBullet(Vector3 position, Vector3 direction, float speed)
        {
            var bullet = bulletPool.Get();
            bullet.transform.position = position;

            var rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * speed;
                rb.WakeUp(); // 刚 SetActive 的 Rigidbody 可能处于休眠，需手动唤醒
            }

            return bullet;
        }

        /// <summary>
        /// 回收子弹（通常在子弹命中/超时时调用）
        /// </summary>
        public void RecycleBullet(GameObject bullet)
        {
            if (bullet == null) return;
            bulletPool.Release(bullet);
        }

        /// <summary>
        /// 获取当前池中可用对象数
        /// </summary>
        public int AvailableBullets => bulletPool.CountInactive;

        /// <summary>
        /// 获取当前活跃对象数（已借出）
        /// </summary>
        public int ActiveBullets => bulletPool.CountActive;

        #endregion

        #region 方式三：ObjectPool<T> 用于纯 C# 对象（非 GameObject）

        // 对象池不仅可用于 GameObject，也可用于减少普通 class 的 GC：
        //   private ObjectPool<MyData> dataPool = new ObjectPool<MyData>(
        //       createFunc:      () => new MyData(),
        //       actionOnGet:     data => data.Reset(),
        //       actionOnRelease: data => { /* 清理 */ },
        //       actionOnDestroy: data => { /* 如果是 IDisposable */ }
        //   );
        //
        // 注意：仅当 class 的创建/销毁开销较大时才值得池化；
        // 轻量 struct 直接使用栈分配即可，无需池化。

        #endregion

        #region 生命周期

        private void OnDestroy()
        {
            // 方式一：释放单个池
            bulletPool?.Dispose();

            // 方式二：释放所有托管池
            foreach (var kvp in managedPools)
            {
                if (kvp.Value is IDisposable disposable)
                    disposable.Dispose();
                else
                    kvp.Value.Clear();
            }
            managedPools.Clear();

            Log.Debug("[ObjectPoolExample] 对象池已释放");
        }

        #endregion

        #region 手动测试（右键组件标题 → 选择菜单项）

        [ContextMenu("发射一发子弹")]
        private void TestFireOnce()
        {
            FireBullet(transform.position, transform.forward, 10f);
        }

        [ContextMenu("回收所有子弹")]
        private void TestRecycleAll()
        {
            // 通过查找所有活跃的 PooledBullet 并归还（仅示例，生产环境应避免 FindObjectsOfType）
            var allBullets = FindObjectsByType<PooledBullet>(FindObjectsSortMode.None);
            foreach (var b in allBullets)
                b.Release();
            Log.Debug("[ObjectPoolExample] 已回收 {0} 颗子弹 | 活跃={1} 可用={2}",
                allBullets.Length, bulletPool.CountActive, bulletPool.CountInactive);
        }

        [ContextMenu("切换自动发射")]
        private void TestToggleAutoFire()
        {
            autoFire = !autoFire;
            Log.Debug("[ObjectPoolExample] 自动发射: {0}", autoFire ? "开启" : "关闭");
        }

        #endregion
    }
}
