using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 广告牌：一个组件，用开关组合控制三种朝向行为
    /// </summary>
    //[ExecuteAlways]
    public class Billboard : MonoBehaviour
    {
        [Header("朝向行为")]
        [Tooltip("跟随相机完整旋转（含俯仰/翻滚），对应原 Mode1")]
        public bool followCamera = false;

        [Tooltip("Z 轴朝向相机（水平朝向或直接朝向的基础）")]
        public bool faceCamera = true;

        [Tooltip("仅水平朝向相机（俯仰恒为 0）；关闭则直接朝向相机含俯仰")]
        public bool horizontalOnly = true;

        [Header("水平朝向设置")]
        [Tooltip("开启后偏航跟随相机视线（方向基准与 followCamera 相同），俯仰/翻滚恒为 0；关闭则朝向相机位置")]
        public bool yawFollowCamera = false;

        [Tooltip("yawFollowCamera 切换瞬间，将该子物体的本地 Y 旋转清零（避免残留偏航）")]
        public Transform yawResetChild;

        public Transform targetTF;

        /// <summary>偏航跟随开关上一次状态（用于检测切换瞬间）</summary>
        private bool lastYawFollowCamera = false;

        Quaternion originalRotation = Quaternion.identity;
        private float lastErrorLogTime = -999f;
        private CameraMainRetry cameraRetry = new CameraMainRetry(-999f);

        void Start()
        {
            if (targetTF == null)
            {
                var cam = Camera.main;
                if (cam != null)
                    targetTF = cam.transform;
            }
            originalRotation = transform.rotation;
        }

        void Update()
        {
            // 如果目标引用丢失，限流重试获取主相机（Camera.main 内部是 FindGameObjectsWithTag，极慢）
            if (targetTF == null)
            {
                var cam = cameraRetry.TryGetCamera();
                if (cam != null)
                    targetTF = cam.transform;
            }

            if (targetTF == null)
            {
                // 每 5 秒最多记录一次，避免每帧刷屏
                if (Time.time - lastErrorLogTime > 5f)
                {
                    lastErrorLogTime = Time.time;
                    Log.Error("Billboard 目标不存在，请查找原因！");
                }
                return;
            }

            // 模式合成：followCamera 优先；其次 faceCamera + horizontalOnly 组合
            if (followCamera)
            {
                // 原 Mode1：与相机保持相同方向和角度
                transform.rotation = targetTF.rotation * originalRotation;
                return;
            }

            if (!faceCamera)
            {
                // 不朝向相机：保持当前旋转
                return;
            }

            if (horizontalOnly)
            {
                // 水平朝向：仅绕世界 Y 轴水平朝向相机（俯仰/翻滚恒为 0，角度一直为 0）
                // 偏航开关切换瞬间重置子物体本地 Y 旋转（残留偏航清理）
                HandleYawToggle();

                if (yawFollowCamera)
                {
                    // 偏航跟随相机视线（方向基准与 Mode1 相同），但俯仰恒为 0。
                    // 不能用欧拉角归零：相机带俯仰/翻滚时欧拉分解会使 Y 偏航异常甚至反向。
                    Vector3 forward = targetTF.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.0001f)
                    {
                        // 相机垂直向上/向下看：水平方向趋零，保持当前旋转避免零向量报错
                        return;
                    }
                    transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * originalRotation;
                }
                else
                {
                    // 朝向相机位置的水平分量；相机与物体水平重合时保持当前旋转，避免 LookRotation 零向量报错
                    Vector3 dir = targetTF.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
                return;
            }

            // 原 Mode3：Z 轴直接朝向相机（含俯仰，无翻滚）
            {
                Vector3 toTarget = targetTF.position - transform.position;
                if (toTarget.sqrMagnitude < 0.0001f)
                {
                    // 零向量防护：相机与物体重合时避免 LookAt 产生 NaN 旋转
                    return;
                }
                transform.LookAt(targetTF.position);
            }
        }

        /// <summary>
        /// yawFollowCamera 开关状态变化瞬间，将 yawResetChild 的本地 Y 旋转清零（避免残留偏航）。
        /// 顺带同步 lastYawFollowCamera，确保重置仅在切换那一帧执行一次。
        /// </summary>
        private void HandleYawToggle()
        {
            if (yawResetChild != null && lastYawFollowCamera != yawFollowCamera)
            {
                var euler = yawResetChild.localEulerAngles;
                euler.y = 0f;
                yawResetChild.localRotation = Quaternion.Euler(euler);
            }
            lastYawFollowCamera = yawFollowCamera;
        }
    }
}